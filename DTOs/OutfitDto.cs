using System;
using System.Collections.Generic;

namespace WardrobeApi.DTOs
{
    public class OutfitDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsFavorite { get; set; }
        public string? Occasion { get; set; }
        public List<int> ClothingItemIds { get; set; } = new List<int>();
        public List<ClothingItemDto> ClothingItems { get; set; } = new List<ClothingItemDto>();
    }

    public class CreateOutfitDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Occasion { get; set; }
        public List<int> ClothingItemIds { get; set; } = new List<int>();
    }

    public class UpdateOutfitDto : CreateOutfitDto
    {
        public bool? IsFavorite { get; set; }
    }
}
