using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WardrobeApi.Models
{
    public class Outfit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public bool IsFavorite { get; set; }

        public string? Occasion { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ICollection<OutfitClothingItem> OutfitClothingItems { get; set; } = new List<OutfitClothingItem>();
    }

    public class OutfitClothingItem
    {
        public int OutfitId { get; set; }
        public Outfit Outfit { get; set; } = null!;

        public int ClothingItemId { get; set; }
        public ClothingItem ClothingItem { get; set; } = null!;
    }
}
