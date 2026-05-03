using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;
using System.Linq;
using WardrobeApi.Data;
using WardrobeApi.DTOs;
using WardrobeApi.Models;

namespace WardrobeApi.Controllers
{
    [Route("api/account")]
    [ApiController]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("profile")]
        public async Task<ActionResult<AccountProfileDto>> GetProfile()
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null) return Unauthorized();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            return Ok(ToAccountProfileDto(user, profile));
        }

        [HttpPut("profile")]
        public async Task<ActionResult<AccountProfileDto>> UpdateProfile([FromBody] AccountProfileDto request)
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null) return Unauthorized();

            var profile = await GetOrCreateProfileAsync(user.Id);

            var normalizedEmail = (request.Email ?? string.Empty).Trim();
            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(user.UserName, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = normalizedEmail;
                user.UserName = normalizedEmail;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return BadRequest(updateResult.Errors);
                }
            }

            profile.DisplayName = request.DisplayName ?? string.Empty;
            profile.Bio = request.Bio ?? string.Empty;
            profile.Phone = request.Phone ?? string.Empty;
            profile.City = request.City ?? string.Empty;
            profile.Birthday = request.Birthday ?? string.Empty;
            profile.FavoriteStyle = request.FavoriteStyle ?? string.Empty;
            profile.FavoriteBrands = request.FavoriteBrands ?? string.Empty;
            profile.PreferredColors = request.PreferredColors ?? string.Empty;
            profile.InstagramHandle = request.InstagramHandle ?? string.Empty;
            profile.StylingNotes = request.StylingNotes ?? string.Empty;

            await _context.SaveChangesAsync();

            return Ok(ToAccountProfileDto(user, profile));
        }

        [HttpGet("profile-photo")]
        public async Task<ActionResult<ProfilePhotoDto>> GetProfilePhoto()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var photoDataUrl = await _context.UserProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.ProfilePhotoDataUrl)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(photoDataUrl))
            {
                return NoContent();
            }

            return Ok(new ProfilePhotoDto
            {
                PhotoDataUrl = photoDataUrl
            });
        }

        [HttpPut("profile-photo")]
        public async Task<ActionResult<ProfilePhotoDto>> UpdateProfilePhoto([FromBody] ProfilePhotoDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var profile = await GetOrCreateProfileAsync(userId);
            profile.ProfilePhotoDataUrl = string.IsNullOrWhiteSpace(request.PhotoDataUrl)
                ? null
                : request.PhotoDataUrl;

            await _context.SaveChangesAsync();

            return Ok(new ProfilePhotoDto
            {
                PhotoDataUrl = profile.ProfilePhotoDataUrl
            });
        }

        [HttpDelete("profile-photo")]
        public async Task<IActionResult> DeleteProfilePhoto()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProfilePhotoDataUrl))
            {
                return NoContent();
            }

            profile.ProfilePhotoDataUrl = null;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return BadRequest(new { message = "Current password is required." });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "New password is required." });
            }

            var currentPasswordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!currentPasswordValid)
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            var newPasswordSameAsCurrent = request.CurrentPassword == request.NewPassword
                || await _userManager.CheckPasswordAsync(user, request.NewPassword);

            if (newPasswordSameAsCurrent)
            {
                return BadRequest(new { message = "New password must be different from the current password." });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = string.Join(" ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { message = "Password updated successfully." });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null) return Unauthorized();

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var ownedOutfitIds = await _context.Outfits
                    .Where(o => o.UserId == user.Id)
                    .Select(o => o.Id)
                    .ToListAsync();

                var ownedClothingItemIds = await _context.ClothingItems
                    .Where(i => i.UserId == user.Id)
                    .Select(i => i.Id)
                    .ToListAsync();

                var outfitLinks = await _context.OutfitClothingItems
                    .Where(oci => ownedOutfitIds.Contains(oci.OutfitId) || ownedClothingItemIds.Contains(oci.ClothingItemId))
                    .ToListAsync();

                if (outfitLinks.Count > 0)
                {
                    _context.OutfitClothingItems.RemoveRange(outfitLinks);
                }

                var outfits = await _context.Outfits
                    .Where(o => o.UserId == user.Id)
                    .ToListAsync();

                if (outfits.Count > 0)
                {
                    _context.Outfits.RemoveRange(outfits);
                }

                var clothingItems = await _context.ClothingItems
                    .Where(i => i.UserId == user.Id)
                    .ToListAsync();

                if (clothingItems.Count > 0)
                {
                    _context.ClothingItems.RemoveRange(clothingItems);
                }

                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile != null)
                {
                    _context.UserProfiles.Remove(profile);
                }

                await _context.SaveChangesAsync();

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new
                    {
                        message = string.Join(" ", deleteResult.Errors.Select(e => e.Description))
                    });
                }

                await transaction.CommitAsync();

                return Ok(new { message = "Account deleted successfully." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ApplicationUser?> GetAuthenticatedUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return null;

            return await _userManager.FindByIdAsync(userId);
        }

        private async Task<UserProfile> GetOrCreateProfileAsync(string userId)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null) return profile;

            profile = new UserProfile
            {
                UserId = userId
            };

            _context.UserProfiles.Add(profile);
            return profile;
        }

        private static AccountProfileDto ToAccountProfileDto(ApplicationUser user, UserProfile? profile)
        {
            return new AccountProfileDto
            {
                DisplayName = profile?.DisplayName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Bio = profile?.Bio ?? string.Empty,
                Phone = profile?.Phone ?? string.Empty,
                City = profile?.City ?? string.Empty,
                Birthday = profile?.Birthday ?? string.Empty,
                FavoriteStyle = profile?.FavoriteStyle ?? string.Empty,
                FavoriteBrands = profile?.FavoriteBrands ?? string.Empty,
                PreferredColors = profile?.PreferredColors ?? string.Empty,
                InstagramHandle = profile?.InstagramHandle ?? string.Empty,
                StylingNotes = profile?.StylingNotes ?? string.Empty
            };
        }
    }
}
