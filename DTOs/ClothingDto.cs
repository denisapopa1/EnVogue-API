using System;

namespace WardrobeApi.DTOs
{
    public class ClothingItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? SubcategoryId { get; set; }
        public string? SubcategoryName { get; set; }
        public string? Season { get; set; }
        public string? Style { get; set; }
        public string? Color { get; set; }
        public string? Brand { get; set; }
        public string? ImagePath { get; set; }
        public string? Tags { get; set; }
        public bool IsFavorite { get; set; }
    }

    public class CreateClothingItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public int? SubcategoryId { get; set; }
        public string? Season { get; set; }
        public string? Style { get; set; }
        public string? Color { get; set; }
        public string? Brand { get; set; }
        public string? ImagePath { get; set; }
        public string? Tags { get; set; }
    }
}
