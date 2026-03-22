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
            if (!await context.Categories.AnyAsync())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Tops", Subcategories = new List<Subcategory> 
                        { 
                            new Subcategory { Name = "T-Shirts" }, 
                            new Subcategory { Name = "Shirts" }, 
                            new Subcategory { Name = "Sweaters" },
                            new Subcategory { Name = "Jackets" }
                        } 
                    },
                    new Category { Name = "Bottoms", Subcategories = new List<Subcategory> 
                        { 
                            new Subcategory { Name = "Jeans" }, 
                            new Subcategory { Name = "Trousers" }, 
                            new Subcategory { Name = "Shorts" },
                            new Subcategory { Name = "Skirts" }
                        } 
                    },
                    new Category { Name = "Footwear", Subcategories = new List<Subcategory> 
                        { 
                            new Subcategory { Name = "Sneakers" }, 
                            new Subcategory { Name = "Boots" }, 
                            new Subcategory { Name = "Sandals" }
                        } 
                    },
                    new Category { Name = "Accessories", Subcategories = new List<Subcategory> 
                        { 
                            new Subcategory { Name = "Hats" }, 
                            new Subcategory { Name = "Belts" }, 
                            new Subcategory { Name = "Watches" }
                        } 
                    }
                };

                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }

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
    }
}
