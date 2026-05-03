using System.ComponentModel.DataAnnotations;

namespace WardrobeApi.Models
{
    public class UserProfile
    {
        [Key]
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Birthday { get; set; } = string.Empty;
        public string FavoriteStyle { get; set; } = string.Empty;
        public string FavoriteBrands { get; set; } = string.Empty;
        public string PreferredColors { get; set; } = string.Empty;
        public string InstagramHandle { get; set; } = string.Empty;
        public string StylingNotes { get; set; } = string.Empty;
        public string? ProfilePhotoDataUrl { get; set; }

        public ApplicationUser User { get; set; } = null!;
    }
}
