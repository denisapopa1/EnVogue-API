using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardrobeApi.Data;

namespace WardrobeApi.Controllers
{
    [Route("api/demo")]
    [ApiController]
    [Authorize]
    public class DemoController : ControllerBase
    {
        private readonly DemoDatasetSeeder _demoDatasetSeeder;

        public DemoController(DemoDatasetSeeder demoDatasetSeeder)
        {
            _demoDatasetSeeder = demoDatasetSeeder;
        }

        [AllowAnonymous]
        [HttpGet("dataset")]
        public async Task<IActionResult> GetDataset()
        {
            var manifest = await _demoDatasetSeeder.LoadManifestAsync();
            if (manifest == null)
            {
                return NotFound();
            }

            return Ok(manifest);
        }

        [HttpPost("seed-wardrobe")]
        public async Task<IActionResult> SeedWardrobe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _demoDatasetSeeder.SeedForUserAsync(userId);
            return Ok(result);
        }
    }
}
