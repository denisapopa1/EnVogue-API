using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WardrobeApi.Data;
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
            var items = await _context.ClothingItems
                .Include(i => i.Category)
                .Where(i => i.UserId == userId)
                .ToListAsync();

            if (!items.Any()) return NotFound("No clothing items found to generate an outfit");

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

            if (top == null || bottom == null) return NotFound("Could not generate a complete outfit");

            return Ok(new
            {
                name = $"Generated {occasion ?? "Daily"} Outfit",
                occasion = occasion,
                items = new[] 
                { 
                    new { top.Id, top.Name, Category = top.Category?.Name },
                    new { bottom.Id, bottom.Name, Category = bottom.Category?.Name }
                }
            });
        }
    }
}
