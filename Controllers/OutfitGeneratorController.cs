using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Claims;
using WardrobeApi.Data;
using WardrobeApi.DTOs;
using WardrobeApi.Models;

namespace WardrobeApi.Controllers
{
    [Route("api/outfit-generator")]
    [ApiController]
    [Authorize]
    public class OutfitGeneratorController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, int> SuggestionCounters = new();

        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public OutfitGeneratorController(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpPost("wardrobe")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> UploadWardrobe(IFormFile file)
        {
            var email = GetUserEmail();
            if (email == null) return Unauthorized();

            if (file == null || file.Length == 0)
            {
                return BadRequest("Upload a wardrobe .zip file.");
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only .zip wardrobe archives are accepted.");
            }

            using var content = new MultipartFormDataContent();
            await using var fileStream = file.OpenReadStream();
            using var streamContent = new StreamContent(fileStream);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/zip" : file.ContentType;
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", file.FileName);

            var response = await _httpClientFactory.CreateClient().PostAsync(BuildAiStylistUri($"wardrobe/{Uri.EscapeDataString(email)}?replace=true"), content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, body);
            }

            return Content(body, "application/json");
        }

        [HttpGet("generate")]
        public async Task<IActionResult> GenerateOutfit(
            [FromQuery] string? style,
            [FromQuery] string? season,
            [FromQuery] string? weather,
            [FromQuery] int? variant = null)
        {
            var email = GetUserEmail();
            if (email == null) return Unauthorized();

            var normalizedStyle = NormalizeStyle(style);
            var normalizedSeason = NormalizeSeason(season);
            var normalizedWeather = NormalizeWeather(weather);
            var modelUserId = BuildModelUserId(email, normalizedStyle);

            var endpoint =
                $"outfit/{Uri.EscapeDataString(modelUserId)}" +
                $"?style={Uri.EscapeDataString(normalizedStyle)}" +
                $"&season={Uri.EscapeDataString(normalizedSeason)}" +
                $"&weather={Uri.EscapeDataString(normalizedWeather)}" +
                "&count=12";

            AiStylistResponseDto? aiResponse;
            try
            {
                aiResponse = await GetAiStylistResponseAsync(endpoint, modelUserId, normalizedStyle);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    $"AI stylist service is unavailable. Start the pre-trained model API first. Details: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var suggestions = aiResponse?.Suggestions
                .Where(s => s.Items.Any())
                .ToList() ?? new List<AiStylistSuggestionDto>();
            var suggestionIndex = suggestions.Any()
                ? ResolveSuggestionIndex(email, normalizedStyle, normalizedSeason, normalizedWeather, variant, suggestions.Count)
                : 0;
            var suggestion = suggestions.Any()
                ? suggestions[suggestionIndex]
                : null;
            if (suggestion == null || !suggestion.Items.Any())
            {
                return NotFound("The AI stylist could not generate an outfit for this wardrobe.");
            }

            return Ok(MapSuggestionToOutfit(suggestion, normalizedStyle, normalizedSeason, normalizedWeather));
        }

        private static int ResolveSuggestionIndex(
            string email,
            string style,
            string season,
            string weather,
            int? requestedVariant,
            int suggestionCount)
        {
            if (requestedVariant.HasValue)
            {
                return Math.Abs(requestedVariant.Value) % suggestionCount;
            }

            var key = $"{email}|{style}|{season}|{weather}";
            var next = SuggestionCounters.AddOrUpdate(key, 1, (_, current) => current + 1);
            return (next - 1) % suggestionCount;
        }

        private async Task<AiStylistResponseDto?> GetAiStylistResponseAsync(
            string endpoint,
            string modelUserId,
            string normalizedStyle)
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(BuildAiStylistUri(endpoint));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync();
                if (body.Contains("No wardrobe", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("Wardrobe is empty", StringComparison.OrdinalIgnoreCase))
                {
                    await SyncWardrobeFromDatabaseAsync(modelUserId, normalizedStyle);
                    response = await client.GetAsync(BuildAiStylistUri(endpoint));
                }
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiStylistResponseDto>();
        }

        private async Task SyncWardrobeFromDatabaseAsync(string modelUserId, string normalizedStyle)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                throw new InvalidOperationException("Cannot sync AI wardrobe without an authenticated user.");
            }

            var allItems = await _context.ClothingItems
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .Where(i => i.UserId == userId && !string.IsNullOrWhiteSpace(i.ImagePath))
                .ToListAsync();
            var items = allItems
                .Where(i => NormalizeStyle(i.Style) == normalizedStyle)
                .ToList();

            if (!items.Any())
            {
                throw new InvalidOperationException(
                    $"Nu exista haine cu stilul {ToDisplayStyle(normalizedStyle)} in garderoba. Actualizeaza stilul pieselor sau alege alt stil.");
            }

            var copiedItems = 0;
            await using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in items)
                {
                    var fileName = Path.GetFileName(item.ImagePath);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    var sourcePath = Path.Combine(_environment.WebRootPath, "uploads", "images", fileName);
                    if (!System.IO.File.Exists(sourcePath))
                    {
                        continue;
                    }

                    var category = SafeZipPart(item.Category?.Name, "Other");
                    var subcategory = SafeZipPart(item.Subcategory?.Name, "Items");
                    var entryName = $"{category}/{subcategory}/{item.Id}_{fileName}";
                    archive.CreateEntryFromFile(sourcePath, entryName);
                    copiedItems++;
                }
            }

            if (copiedItems == 0)
            {
                throw new InvalidOperationException(
                    $"Hainele pentru stilul {ToDisplayStyle(normalizedStyle)} nu au imagini disponibile pentru modelul AI.");
            }

            zipStream.Position = 0;

            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(zipStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(streamContent, "file", "wardrobe.zip");

            var response = await _httpClientFactory.CreateClient()
                .PostAsync(BuildAiStylistUri($"wardrobe/{Uri.EscapeDataString(modelUserId)}?replace=true"), content);

            response.EnsureSuccessStatusCode();
        }

        private static string BuildModelUserId(string email, string normalizedStyle)
            => $"{email}__{normalizedStyle}";

        private static string SafeZipPart(string? value, string fallback)
        {
            var cleaned = new string((value ?? fallback)
                .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))
                .ToArray())
                .Trim();

            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveGeneratedOutfit([FromBody] SaveAiOutfitDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (model.Items == null || !model.Items.Any())
            {
                return BadRequest("The generated outfit has no items to save.");
            }

            var outfit = new Outfit
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? "AI Styled Look" : model.Name.Trim(),
                Occasion = string.IsNullOrWhiteSpace(model.Occasion) ? "AI Stylist" : model.Occasion.Trim(),
                UserId = userId
            };

            foreach (var generatedItem in model.Items)
            {
                var item = new ClothingItem
                {
                    Name = string.IsNullOrWhiteSpace(generatedItem.Name) ? "AI wardrobe item" : generatedItem.Name.Trim(),
                    ImagePath = await PersistGeneratedImageAsync(generatedItem.ImagePath),
                    Tags = "AI Generated",
                    UserId = userId
                };

                await AssignCategoryAsync(item, generatedItem.CategoryName, generatedItem.SubcategoryName);
                _context.ClothingItems.Add(item);
                outfit.OutfitClothingItems.Add(new OutfitClothingItem { ClothingItem = item });
            }

            _context.Outfits.Add(outfit);
            await _context.SaveChangesAsync();

            var dto = await ProjectOutfit(outfit.Id, userId);
            return Created($"/api/outfits/{outfit.Id}", dto);
        }

        private async Task<string?> PersistGeneratedImageAsync(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            if (!imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !Path.IsPathFullyQualified(imagePath))
            {
                return Path.GetFileName(imagePath);
            }

            var extension = Path.GetExtension(new Uri(imagePath, UriKind.RelativeOrAbsolute).LocalPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "images");
            Directory.CreateDirectory(uploadPath);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var destinationPath = Path.Combine(uploadPath, fileName);

            if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _httpClientFactory.CreateClient().GetByteArrayAsync(imagePath);
                await System.IO.File.WriteAllBytesAsync(destinationPath, bytes);
                return fileName;
            }

            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Copy(imagePath, destinationPath, overwrite: true);
                return fileName;
            }

            return Path.GetFileName(imagePath);
        }

        private string? GetUserEmail()
        {
            return User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name;
        }

        private Uri BuildAiStylistUri(string relativePath)
        {
            var baseUrl = _configuration["AiStylist:BaseUrl"] ?? "http://localhost:8000/";
            return new Uri(new Uri(baseUrl.EndsWith("/") ? baseUrl : $"{baseUrl}/"), relativePath);
        }

        private OutfitDto MapSuggestionToOutfit(
            AiStylistSuggestionDto suggestion,
            string style,
            string season,
            string weather)
        {
            var clothingItems = suggestion.Items.Select((item, index) => new ClothingItemDto
            {
                Id = 0,
                Name = string.IsNullOrWhiteSpace(item.Label) ? item.Role : item.Label,
                CategoryName = ToCategoryName(item.Role),
                SubcategoryName = ToSubcategoryName(item.Label),
                Style = style,
                Season = season,
                ImagePath = ToPublicAiImageUrl(item.Image)
            }).ToList();

            return new OutfitDto
            {
                Id = 0,
                Name = string.IsNullOrWhiteSpace(suggestion.Name) ? "AI Styled Look" : suggestion.Name,
                Occasion = $"{style} / {season} / {weather}",
                CreatedDate = DateTime.UtcNow,
                ClothingItems = clothingItems,
                ClothingItemIds = new List<int>()
            };
        }

        private string ToPublicAiImageUrl(string imagePath)
        {
            var normalized = imagePath.Replace('\\', '/');
            const string marker = "/BaseImages/";
            var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                var relative = normalized[(markerIndex + marker.Length)..];
                return BuildAiStylistUri($"images/{relative}").ToString();
            }

            return imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? imagePath
                : BuildAiStylistUri(imagePath.TrimStart('/')).ToString();
        }

        private static string ToCategoryName(string role)
        {
            return role.ToLowerInvariant() switch
            {
                "top" => "Tops",
                "bottom" => "Bottoms",
                "dress" => "Dress",
                "shoes" => "Shoes",
                "outerwear" => "Outerwear",
                "accessory" => "Accessories",
                _ => role
            };
        }

        private static string? ToSubcategoryName(string label)
        {
            var parts = label.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 1 ? parts[1] : null;
        }

        private async Task AssignCategoryAsync(ClothingItem item, string? categoryName, string? subcategoryName)
        {
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                item.CategoryId = await _context.Categories
                    .Where(c => c.Name.ToLower() == categoryName.ToLower())
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();
            }

            if (!string.IsNullOrWhiteSpace(subcategoryName))
            {
                item.SubcategoryId = await _context.Subcategories
                    .Where(s => s.Name.ToLower() == subcategoryName.ToLower())
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync();
            }
        }

        private async Task<OutfitDto?> ProjectOutfit(int outfitId, string userId)
        {
            return await _context.Outfits
                .Where(o => o.Id == outfitId && o.UserId == userId)
                .Select(o => new OutfitDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    CreatedDate = o.CreatedDate,
                    IsFavorite = o.IsFavorite,
                    Occasion = o.Occasion,
                    ClothingItemIds = o.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList(),
                    ClothingItems = o.OutfitClothingItems.Select(oci => new ClothingItemDto
                    {
                        Id = oci.ClothingItem.Id,
                        Name = oci.ClothingItem.Name,
                        CategoryId = oci.ClothingItem.CategoryId,
                        CategoryName = oci.ClothingItem.Category != null ? oci.ClothingItem.Category.Name : null,
                        SubcategoryId = oci.ClothingItem.SubcategoryId,
                        SubcategoryName = oci.ClothingItem.Subcategory != null ? oci.ClothingItem.Subcategory.Name : null,
                        Season = oci.ClothingItem.Season,
                        Style = oci.ClothingItem.Style,
                        Color = oci.ClothingItem.Color,
                        Brand = oci.ClothingItem.Brand,
                        ImagePath = oci.ClothingItem.ImagePath,
                        Tags = oci.ClothingItem.Tags,
                        IsFavorite = oci.ClothingItem.IsFavorite
                    }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        private static string NormalizeStyle(string? style)
        {
            var value = style?.Trim().ToLowerInvariant() ?? string.Empty;
            return value switch
            {
                "formal wear" or "formal" => "formal",
                "elegant style" or "elegant" => "elegant",
                "sporty look" or "sporty" => "sporty",
                _ => "casual"
            };
        }

        private static string ToDisplayStyle(string normalizedStyle)
        {
            return normalizedStyle switch
            {
                "formal" => "Formal Wear",
                "elegant" => "Elegant Style",
                "sporty" => "Sporty Look",
                _ => "Casual Style"
            };
        }

        private static string NormalizeSeason(string? season)
        {
            var value = season?.Trim().ToLowerInvariant() ?? string.Empty;
            return value is "winter" or "spring" or "summer" or "autumn" ? value : "spring";
        }

        private static string NormalizeWeather(string? weather)
        {
            var value = weather?.Trim().ToLowerInvariant() ?? string.Empty;
            return value is "sunny" or "cloudy" or "rainy" or "cold" ? value : "sunny";
        }
    }
}
