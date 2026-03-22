using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WardrobeApi.Data;
using WardrobeApi.Models;

namespace WardrobeApi.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.Subcategories)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    Subcategories = c.Subcategories.Select(s => new { s.Id, s.Name })
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("{id}/subcategories")]
        public async Task<IActionResult> GetSubcategories(int id)
        {
            var subcategories = await _context.Subcategories
                .Where(s => s.CategoryId == id)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();

            if (!subcategories.Any()) return NotFound("No subcategories found for this category");

            return Ok(subcategories);
        }
    }
}
