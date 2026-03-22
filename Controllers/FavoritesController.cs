using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WardrobeApi.Data;

namespace WardrobeApi.Controllers
{
    [Route("api/favorites")]
    [ApiController]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var items = await _context.ClothingItems
                .Where(i => i.UserId == userId && i.IsFavorite)
                .ToListAsync();

            var outfits = await _context.Outfits
                .Where(o => o.UserId == userId && o.IsFavorite)
                .ToListAsync();

            return Ok(new { items, outfits });
        }

        [HttpPost("items/{id}")]
        public async Task<IActionResult> ToggleItemFavorite(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = await _context.ClothingItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (item == null) return NotFound();

            item.IsFavorite = !item.IsFavorite;
            await _context.SaveChangesAsync();
            return Ok(new { isFavorite = item.IsFavorite });
        }

        [HttpPost("outfits/{id}")]
        public async Task<IActionResult> ToggleOutfitFavorite(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfit = await _context.Outfits.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (outfit == null) return NotFound();

            outfit.IsFavorite = !outfit.IsFavorite;
            await _context.SaveChangesAsync();
            return Ok(new { isFavorite = outfit.IsFavorite });
        }
    }
}
