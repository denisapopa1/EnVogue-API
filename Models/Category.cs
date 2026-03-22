using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WardrobeApi.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
    }

    public class Subcategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}
