using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WardrobeApi.Models;
using WardrobeApi.Data;

namespace WardrobeApi.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Categories and Subcategories
            await EnsureWardrobeTaxonomyAsync(context);

            // 2. Seed Default Users
            string password = "DefaultPassword123!";
            var defaultUsers = new List<string> { "user1@example.com", "user2@example.com", "user3@example.com" };

            foreach (var email in defaultUsers)
            {
                if (await userManager.FindByEmailAsync(email) == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };

                    await userManager.CreateAsync(user, password);
                }
            }
        }

        public static async Task EnsureWardrobeTaxonomyAsync(ApplicationDbContext context)
        {
            var taxonomy = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tops"] = new[] { "T-shirt", "Blouse", "Sweater", "Hoodie" },
                ["Bottoms"] = new[] { "Jeans", "Trousers", "Skirt", "Shorts" },
                ["Dress"] = new[] { "Mini", "Maxi", "Cocktail", "Evening" },
                ["Outerwear"] = new[] { "Jacket", "Coat", "Blazer", "Vest" },
                ["Shoes"] = new[] { "Sneakers", "Boots", "Heels", "Sandals" },
                ["Accessories"] = new[] { "Scarf", "Belt", "Jewelry", "Hat" }
            };

            var categories = await context.Categories
                .Include(category => category.Subcategories)
                .ToListAsync();

            foreach (var categoryDefinition in taxonomy)
            {
                var category = categories.FirstOrDefault(item =>
                    string.Equals(item.Name, categoryDefinition.Key, StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    category = new Category { Name = categoryDefinition.Key };
                    context.Categories.Add(category);
                    categories.Add(category);
                }

                foreach (var subcategoryName in categoryDefinition.Value)
                {
                    var hasSubcategory = category.Subcategories.Any(item =>
                        string.Equals(item.Name, subcategoryName, StringComparison.OrdinalIgnoreCase));

                    if (!hasSubcategory)
                    {
                        category.Subcategories.Add(new Subcategory { Name = subcategoryName });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
