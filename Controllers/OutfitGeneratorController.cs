using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly ApplicationDbContext _context;

        public OutfitGeneratorController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("generate")]
        public async Task<IActionResult> GenerateOutfit([FromQuery] string? color, [FromQuery] string? occasion)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var selection = await SelectGeneratedOutfitItems(userId, color);
            if (selection.ErrorMessage != null) return NotFound(selection.ErrorMessage);

            var name = GetGeneratedOutfitName(occasion);
            return Ok(BuildGeneratedOutfitResponse(name, occasion, selection.Items));
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateAndSaveOutfit([FromQuery] string? color, [FromQuery] string? occasion)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var selection = await SelectGeneratedOutfitItems(userId, color);
            if (selection.ErrorMessage != null) return NotFound(selection.ErrorMessage);

            var outfit = new Outfit
            {
                Name = GetGeneratedOutfitName(occasion),
                Occasion = occasion,
                UserId = userId
            };

            foreach (var item in selection.Items)
            {
                outfit.OutfitClothingItems.Add(new OutfitClothingItem
                {
                    ClothingItemId = item.Id
                });
            }

            _context.Outfits.Add(outfit);
            await _context.SaveChangesAsync();

            var dto = new OutfitDto
            {
                Id = outfit.Id,
                Name = outfit.Name,
                CreatedDate = outfit.CreatedDate,
                IsFavorite = outfit.IsFavorite,
                Occasion = outfit.Occasion,
                ClothingItemIds = selection.Items.Select(i => i.Id).ToList(),
                ClothingItems = selection.Items.Select(ToClothingItemDto).ToList()
            };

            return Created($"/api/outfits/{outfit.Id}", dto);
        }

        private async Task<(string? ErrorMessage, List<ClothingItem> Items)> SelectGeneratedOutfitItems(string userId, string? color)
        {
            var items = await _context.ClothingItems
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .Where(i => i.UserId == userId)
                .ToListAsync();

            if (!items.Any()) return ("No clothing items found to generate an outfit", new List<ClothingItem>());

            // Simple logic for demonstration
            // 1. Filter by color if provided
            var filteredItems = items;
            if (!string.IsNullOrEmpty(color))
                filteredItems = filteredItems.Where(i => string.Equals(i.Color, color, StringComparison.OrdinalIgnoreCase)).ToList();

            // 2. Try to pick a Top and a Bottom using Category Names from the seeded data
            var tops = filteredItems.Where(i => string.Equals(i.Category?.Name, "Tops", StringComparison.OrdinalIgnoreCase)).ToList();
            var bottoms = filteredItems.Where(i => string.Equals(i.Category?.Name, "Bottoms", StringComparison.OrdinalIgnoreCase)).ToList();

            if (!tops.Any() || !bottoms.Any())
            {
                // Fallback to any items if categories don't match
                tops = items.Take(1).ToList();
                bottoms = items.Skip(1).Take(1).ToList();
            }

            var random = new Random();
            var top = tops.Any() ? tops[random.Next(tops.Count)] : null;
            var bottom = bottoms.Any() ? bottoms[random.Next(bottoms.Count)] : null;

            if (top == null || bottom == null) return ("Could not generate a complete outfit", new List<ClothingItem>());

            return (null, new List<ClothingItem> { top, bottom });
        }

        private static string GetGeneratedOutfitName(string? occasion)
        {
            return $"Generated {occasion ?? "Daily"} Outfit";
        }

        private static object BuildGeneratedOutfitResponse(string name, string? occasion, List<ClothingItem> items)
        {
            return new
            {
                name,
                occasion,
                clothingItemIds = items.Select(i => i.Id).ToList(),
                items = items.Select(ToClothingItemDto).ToList()
            };
        }

        private static ClothingItemDto ToClothingItemDto(ClothingItem item)
        {
            return new ClothingItemDto
            {
                Id = item.Id,
                Name = item.Name,
                CategoryId = item.CategoryId,
                CategoryName = item.Category?.Name,
                SubcategoryId = item.SubcategoryId,
                SubcategoryName = item.Subcategory?.Name,
                Season = item.Season,
                Style = item.Style,
                Color = item.Color,
                Brand = item.Brand,
                ImagePath = item.ImagePath,
                Tags = item.Tags,
                IsFavorite = item.IsFavorite
            };
        }
    }
}
