using System;
using System.ComponentModel.DataAnnotations;

namespace WardrobeApi.Models
{
    public class ClothingItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? SubcategoryId { get; set; }
        public Subcategory? Subcategory { get; set; }

        public string? Season { get; set; }

        public string? Style { get; set; }

        public string? Color { get; set; }

        public string? Brand { get; set; }

        public string? ImagePath { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        public string? Tags { get; set; }

        public bool IsFavorite { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}
