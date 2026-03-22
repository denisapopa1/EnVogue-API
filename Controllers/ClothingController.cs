using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WardrobeApi.Data;
using WardrobeApi.DTOs;
using WardrobeApi.Models;

namespace WardrobeApi.Controllers
{
    [Route("api/clothing")]
    [ApiController]
    [Authorize]
    public class ClothingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClothingController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(
            [FromQuery] int? categoryId,
            [FromQuery] int? subcategoryId,
            [FromQuery] string? season,
            [FromQuery] string? style,
            [FromQuery] string? color,
            [FromQuery] string? search,
            [FromQuery] string? sort)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.ClothingItems
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .Where(i => i.UserId == userId);

            // Filtering
            if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId);
            if (subcategoryId.HasValue) query = query.Where(i => i.SubcategoryId == subcategoryId);
            if (!string.IsNullOrEmpty(season)) query = query.Where(i => i.Season == season);
            if (!string.IsNullOrEmpty(style)) query = query.Where(i => i.Style == style);
            if (!string.IsNullOrEmpty(color)) query = query.Where(i => i.Color == color);
            if (!string.IsNullOrEmpty(search)) query = query.Where(i => i.Name.Contains(search) || (i.Tags != null && i.Tags.Contains(search)));

            // Sorting
            query = sort switch
            {
                "newest" => query.OrderByDescending(i => i.AddedDate),
                "oldest" => query.OrderBy(i => i.AddedDate),
                "name" => query.OrderBy(i => i.Name),
                _ => query.OrderByDescending(i => i.AddedDate)
            };

            var items = await query.Select(i => new ClothingItemDto
            {
                Id = i.Id,
                Name = i.Name,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null,
                SubcategoryId = i.SubcategoryId,
                SubcategoryName = i.Subcategory != null ? i.Subcategory.Name : null,
                Season = i.Season,
                Style = i.Style,
                Color = i.Color,
                Brand = i.Brand,
                ImagePath = i.ImagePath,
                Tags = i.Tags,
                IsFavorite = i.IsFavorite
            }).ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetItem(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = await _context.ClothingItems
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (item == null) return NotFound();

            var dto = new ClothingItemDto
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

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] CreateClothingItemDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var item = new ClothingItem
            {
                Name = model.Name,
                CategoryId = model.CategoryId,
                SubcategoryId = model.SubcategoryId,
                Season = model.Season,
                Style = model.Style,
                Color = model.Color,
                Brand = model.Brand,
                ImagePath = model.ImagePath,
                Tags = model.Tags,
                UserId = userId
            };

            _context.ClothingItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] CreateClothingItemDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = await _context.ClothingItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (item == null) return NotFound();

            item.Name = model.Name;
            item.CategoryId = model.CategoryId;
            item.SubcategoryId = model.SubcategoryId;
            item.Season = model.Season;
            item.Style = model.Style;
            item.Color = model.Color;
            item.Brand = model.Brand;
            item.ImagePath = model.ImagePath;
            item.Tags = model.Tags;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = await _context.ClothingItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (item == null) return NotFound();

            _context.ClothingItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
