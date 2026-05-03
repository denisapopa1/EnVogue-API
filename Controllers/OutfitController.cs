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
            if (userId == null) return Unauthorized();

            var outfits = await ProjectOutfits(_context.Outfits
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedDate))
                .ToListAsync();

            return Ok(outfits);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOutfit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var dto = await ProjectOutfits(_context.Outfits
                .Where(o => o.Id == id && o.UserId == userId))
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOutfit([FromBody] CreateOutfitDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var itemValidation = await ValidateClothingItemIds(model.ClothingItemIds, userId);
            if (itemValidation.ErrorResult != null) return itemValidation.ErrorResult;

            var outfit = new Outfit
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? "Outfit nou" : model.Name.Trim(),
                Occasion = model.Occasion,
                UserId = userId
            };

            foreach (var itemId in itemValidation.ValidItemIds)
            {
                outfit.OutfitClothingItems.Add(new OutfitClothingItem
                {
                    ClothingItemId = itemId
                });
            }

            _context.Outfits.Add(outfit);
            await _context.SaveChangesAsync();

            var dto = await GetOutfitDto(outfit.Id, userId);
            return CreatedAtAction(nameof(GetOutfit), new { id = outfit.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOutfit(int id, [FromBody] UpdateOutfitDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var outfit = await _context.Outfits
                .Include(o => o.OutfitClothingItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (outfit == null) return NotFound();

            var itemValidation = await ValidateClothingItemIds(model.ClothingItemIds, userId);
            if (itemValidation.ErrorResult != null) return itemValidation.ErrorResult;

            outfit.Name = string.IsNullOrWhiteSpace(model.Name) ? outfit.Name : model.Name.Trim();
            outfit.Occasion = model.Occasion;
            if (model.IsFavorite.HasValue)
            {
                outfit.IsFavorite = model.IsFavorite.Value;
            }

            var existingItemIds = outfit.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList();
            var itemsToRemove = outfit.OutfitClothingItems
                .Where(oci => !itemValidation.ValidItemIds.Contains(oci.ClothingItemId))
                .ToList();

            _context.OutfitClothingItems.RemoveRange(itemsToRemove);

            foreach (var itemId in itemValidation.ValidItemIds.Except(existingItemIds))
            {
                outfit.OutfitClothingItems.Add(new OutfitClothingItem
                {
                    OutfitId = id,
                    ClothingItemId = itemId
                });
            }

            await _context.SaveChangesAsync();

            return Ok(await GetOutfitDto(id, userId));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOutfit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

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
            if (userId == null) return Unauthorized();

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
            return Ok(await GetOutfitDto(id, userId));
        }

        [HttpDelete("{outfitId}/items/{itemId}")]
        public async Task<IActionResult> RemoveItemFromOutfit(int outfitId, int itemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var outfitItem = await _context.OutfitClothingItems
                .Include(oci => oci.Outfit)
                .FirstOrDefaultAsync(oci => oci.OutfitId == outfitId && oci.ClothingItemId == itemId && oci.Outfit.UserId == userId);

            if (outfitItem == null) return NotFound();

            _context.OutfitClothingItems.Remove(outfitItem);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<OutfitDto?> GetOutfitDto(int id, string userId)
        {
            return await ProjectOutfits(_context.Outfits
                .Where(o => o.Id == id && o.UserId == userId))
                .FirstOrDefaultAsync();
        }

        private async Task<(List<int> ValidItemIds, IActionResult? ErrorResult)> ValidateClothingItemIds(List<int>? clothingItemIds, string userId)
        {
            var requestedItemIds = clothingItemIds?.Distinct().ToList() ?? new List<int>();

            if (!requestedItemIds.Any())
            {
                return (requestedItemIds, BadRequest("Selecteaza cel putin o piesa pentru outfit."));
            }

            var validItemIds = await _context.ClothingItems
                .Where(i => i.UserId == userId && requestedItemIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();

            var invalidItemIds = requestedItemIds.Except(validItemIds).ToList();
            if (invalidItemIds.Any())
            {
                return (validItemIds, BadRequest(new
                {
                    message = "Unele piese nu exista sau nu apartin utilizatorului curent.",
                    invalidItemIds
                }));
            }

            return (validItemIds, null);
        }

        private static IQueryable<OutfitDto> ProjectOutfits(IQueryable<Outfit> outfits)
        {
            return outfits.Select(o => new OutfitDto
            {
                Id = o.Id,
                Name = o.Name,
                CreatedDate = o.CreatedDate,
                IsFavorite = o.IsFavorite,
                Occasion = o.Occasion,
                ClothingItemIds = o.OutfitClothingItems.Select(oci => oci.ClothingItemId).ToList(),
                ClothingItems = o.OutfitClothingItems
                    .Select(oci => new ClothingItemDto
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
                    })
                    .ToList()
            });
        }
    }
}
