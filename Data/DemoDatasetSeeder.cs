using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WardrobeApi.Models;

namespace WardrobeApi.Data
{
    public class DemoDatasetSeeder
    {
        private static readonly string[] DemoBrands =
        {
            "Zara",
            "Mango",
            "H&M",
            "Nike",
            "Adidas",
            "Stradivarius",
            "Bershka",
            "Pull&Bear",
            "Reserved",
            "Massimo Dutti"
        };

        private static readonly Dictionary<string, string[]> DemoColorsByCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tops"] = new[] { "White", "Beige", "Gray", "Pink", "Green", "Blue", "Navy" },
            ["Bottoms"] = new[] { "Blue", "Beige", "Gray", "Brown", "White", "Navy" },
            ["Dress"] = new[] { "Red", "Pink", "Beige", "White", "Purple", "Green" },
            ["Outerwear"] = new[] { "Beige", "Brown", "Gray", "Navy", "White" },
            ["Shoes"] = new[] { "White", "Brown", "Beige", "Silver", "Gold" },
            ["Accessories"] = new[] { "Gold", "Silver", "White", "Pink", "Brown", "Navy" }
        };

        private static readonly string[] DemoColors =
        {
            "White",
            "Gray",
            "Silver",
            "Beige",
            "Brown",
            "Navy",
            "Blue",
            "Red",
            "Green",
            "Pink",
            "Purple",
            "Gold"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public DemoDatasetSeeder(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<DemoDatasetSeedResult> SeedForUserAsync(string userId)
        {
            await DbSeeder.EnsureWardrobeTaxonomyAsync(_context);

            var manifest = await LoadManifestAsync();
            if (manifest == null)
            {
                return new DemoDatasetSeedResult
                {
                    Message = "Demo dataset manifest was not found in backend wwwroot."
                };
            }

            if (!manifest.Items.Any())
            {
                return new DemoDatasetSeedResult
                {
                    Message = "Demo dataset manifest is empty or invalid."
                };
            }

            var existingItems = await _context.ClothingItems
                .Where(item => item.UserId == userId)
                .ToListAsync();

            var existingItemsByName = existingItems
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var categories = await _context.Categories.Include(category => category.Subcategories).ToListAsync();
            var categoryNamesById = categories.ToDictionary(category => category.Id, category => category.Name);
            var uploadRelativePath = _configuration["ImageStorage:LocalPath"] ?? "uploads/images";
            var uploadPath = Path.Combine(_environment.WebRootPath, uploadRelativePath);
            Directory.CreateDirectory(uploadPath);

            var result = new DemoDatasetSeedResult
            {
                Total = manifest.Items.Count
            };

            var randomizedItems = manifest.Items
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            foreach (var datasetItem in randomizedItems)
            {
                if (existingItemsByName.TryGetValue(datasetItem.Name, out var existingItem))
                {
                    if (ApplyDemoMetadata(existingItem, datasetItem))
                    {
                        result.Updated++;
                    }

                    result.Skipped++;
                    continue;
                }

                var category = categories.FirstOrDefault(item =>
                    string.Equals(item.Name, datasetItem.Category, StringComparison.OrdinalIgnoreCase));

                var subcategory = category?.Subcategories.FirstOrDefault(item =>
                    string.Equals(item.Name, datasetItem.Subcategory, StringComparison.OrdinalIgnoreCase));

                var sourceImagePath = Path.Combine(
                    _environment.WebRootPath,
                    datasetItem.RelativeImagePath.Replace('/', Path.DirectorySeparatorChar));

                if (category == null || subcategory == null || !File.Exists(sourceImagePath))
                {
                    result.Failed++;
                    continue;
                }

                var uploadedFileName = $"{Guid.NewGuid()}{Path.GetExtension(sourceImagePath)}";
                var destinationImagePath = Path.Combine(uploadPath, uploadedFileName);
                File.Copy(sourceImagePath, destinationImagePath, overwrite: true);

                var clothingItem = new ClothingItem
                {
                    Name = datasetItem.Name,
                    Brand = ResolveBrand(datasetItem),
                    CategoryId = category.Id,
                    SubcategoryId = subcategory.Id,
                    Season = datasetItem.Season,
                    Style = datasetItem.Style,
                    Color = ResolveColor(datasetItem),
                    ImagePath = uploadedFileName,
                    UserId = userId,
                    AddedDate = DateTime.UtcNow.AddSeconds(-result.Imported)
                };

                _context.ClothingItems.Add(clothingItem);
                existingItemsByName[datasetItem.Name] = clothingItem;
                result.Imported++;
            }

            result.Updated += ApplyMissingMetadataToExistingWardrobe(existingItems, categoryNamesById);

            await _context.SaveChangesAsync();
            result.Message = $"Demo dataset seed complete: {result.Imported} imported, {result.Updated} updated, {result.Skipped} skipped, {result.Failed} failed.";
            return result;
        }

        private static int ApplyMissingMetadataToExistingWardrobe(
            IEnumerable<ClothingItem> existingItems,
            IReadOnlyDictionary<int, string> categoryNamesById)
        {
            var updated = 0;

            foreach (var existingItem in existingItems)
            {
                var changed = false;

                if (string.IsNullOrWhiteSpace(existingItem.Color) ||
                    string.Equals(existingItem.Color, "Black", StringComparison.OrdinalIgnoreCase))
                {
                    existingItem.Color = ResolveColor(existingItem, categoryNamesById);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingItem.Brand))
                {
                    existingItem.Brand = ResolveBrand(existingItem);
                    changed = true;
                }

                if (changed)
                {
                    updated++;
                }
            }

            return updated;
        }

        private static bool ApplyDemoMetadata(ClothingItem existingItem, DemoDatasetItem datasetItem)
        {
            var changed = false;
            var resolvedColor = ResolveColor(datasetItem);
            var resolvedBrand = ResolveBrand(datasetItem);

            if (string.IsNullOrWhiteSpace(existingItem.Color) ||
                string.Equals(existingItem.Color, "Black", StringComparison.OrdinalIgnoreCase))
            {
                existingItem.Color = resolvedColor;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existingItem.Brand))
            {
                existingItem.Brand = resolvedBrand;
                changed = true;
            }

            return changed;
        }

        private static string ResolveBrand(DemoDatasetItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Brand))
            {
                return item.Brand.Trim();
            }

            var stableKey = $"{item.Category}|{item.Subcategory}|{item.Name}";
            var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(stableKey)) % DemoBrands.Length;
            return DemoBrands[index];
        }

        private static string ResolveBrand(ClothingItem item)
        {
            var stableKey = $"{item.CategoryId}|{item.SubcategoryId}|{item.Name}";
            var index = (StringComparer.OrdinalIgnoreCase.GetHashCode(stableKey) & 0x7fffffff) % DemoBrands.Length;
            return DemoBrands[index];
        }

        private static string ResolveColor(DemoDatasetItem item)
        {
            var manifestColor = item.Color?.Trim();

            // The prepared demo manifest used Black as a placeholder, so redistribute it
            // to make the imported wardrobe look like a real user added items over time.
            if (!string.IsNullOrWhiteSpace(manifestColor) &&
                !string.Equals(manifestColor, "Black", StringComparison.OrdinalIgnoreCase))
            {
                return manifestColor;
            }

            var colorPalette = DemoColorsByCategory.TryGetValue(item.Category, out var categoryColors)
                ? categoryColors
                : DemoColors;

            var stableKey = $"{item.Category}|{item.Subcategory}|{item.Name}|color";
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(stableKey) & 0x7fffffff;
            return colorPalette[hash % colorPalette.Length];
        }

        private static string ResolveColor(
            ClothingItem item,
            IReadOnlyDictionary<int, string> categoryNamesById)
        {
            var categoryName = item.CategoryId.HasValue && categoryNamesById.TryGetValue(item.CategoryId.Value, out var name)
                ? name
                : item.Category?.Name ?? "";

            var colorPalette = DemoColorsByCategory.TryGetValue(categoryName, out var categoryColors)
                ? categoryColors
                : DemoColors;

            var stableKey = $"{categoryName}|{item.SubcategoryId}|{item.Name}|existing-color";
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(stableKey) & 0x7fffffff;
            return colorPalette[hash % colorPalette.Length];
        }

        public async Task<DemoDatasetManifest?> LoadManifestAsync()
        {
            var manifestPath = Path.Combine(
                _environment.WebRootPath,
                "datasets",
                "my__DataSet",
                "manifest.json");

            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<DemoDatasetManifest>(manifestJson, JsonOptions);
        }
    }
}
