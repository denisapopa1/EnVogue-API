using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WardrobeApi.Data;
using WardrobeApi.DTOs;
using WardrobeApi.Models;

namespace WardrobeApi.Controllers
{
    [Route("api/outfits")]
    [ApiController]
    [Authorize]
    public class OutfitController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OutfitController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetOutfits()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfits = await _context.Outfits
                .Where(o => o.UserId == userId)
                .Include(o => o.OutfitClothingItems)
                .ThenInclude(oci => oci.ClothingItem)
                .Select(o => new OutfitDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    CreatedDate = o.CreatedDate,
                    IsFavorite = o.IsFavorite,
                    Occasion = o.Occasion,
                    ClothingItemIds = o.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList()
                })
                .ToListAsync();

            return Ok(outfits);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOutfit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfit = await _context.Outfits
                .Include(o => o.OutfitClothingItems)
                .ThenInclude(oci => oci.ClothingItem)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (outfit == null) return NotFound();

            var dto = new OutfitDto
            {
                Id = outfit.Id,
                Name = outfit.Name,
                CreatedDate = outfit.CreatedDate,
                IsFavorite = outfit.IsFavorite,
                Occasion = outfit.Occasion,
                ClothingItemIds = outfit.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList()
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOutfit([FromBody] CreateOutfitDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var outfit = new Outfit
            {
                Name = model.Name,
                Occasion = model.Occasion,
                UserId = userId
            };

            foreach (var itemId in model.ClothingItemIds)
            {
                var item = await _context.ClothingItems.FindAsync(itemId);
                if (item != null && item.UserId == userId)
                {
                    outfit.OutfitClothingItems.Add(new OutfitClothingItem
                    {
                        ClothingItemId = itemId
                    });
                }
            }

            _context.Outfits.Add(outfit);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOutfit), new { id = outfit.Id }, new OutfitDto
            {
                Id = outfit.Id,
                Name = outfit.Name,
                CreatedDate = outfit.CreatedDate,
                IsFavorite = outfit.IsFavorite,
                Occasion = outfit.Occasion,
                ClothingItemIds = outfit.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList()
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOutfit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfit = await _context.Outfits.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (outfit == null) return NotFound();

            _context.Outfits.Remove(outfit);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/items")]
        public async Task<IActionResult> AddItemToOutfit(int id, [FromBody] int clothingItemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfit = await _context.Outfits
                .Include(o => o.OutfitClothingItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (outfit == null) return NotFound("Outfit not found");

            var item = await _context.ClothingItems.FirstOrDefaultAsync(i => i.Id == clothingItemId && i.UserId == userId);
            if (item == null) return NotFound("Clothing item not found");

            if (outfit.OutfitClothingItems.Any(oci => oci.ClothingItemId == clothingItemId))
                return BadRequest("Item already in outfit");

            outfit.OutfitClothingItems.Add(new OutfitClothingItem
            {
                OutfitId = id,
                ClothingItemId = clothingItemId
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{outfitId}/items/{itemId}")]
        public async Task<IActionResult> RemoveItemFromOutfit(int outfitId, int itemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var outfitItem = await _context.OutfitClothingItems
                .Include(oci => oci.Outfit)
                .FirstOrDefaultAsync(oci => oci.OutfitId == outfitId && oci.ClothingItemId == itemId && oci.Outfit.UserId == userId);

            if (outfitItem == null) return NotFound();

            _context.OutfitClothingItems.Remove(outfitItem);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
