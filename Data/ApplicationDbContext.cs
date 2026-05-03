using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WardrobeApi.Models;

namespace WardrobeApi.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ClothingItem> ClothingItems { get; set; }
        public DbSet<Outfit> Outfits { get; set; }
        public DbSet<OutfitClothingItem> OutfitClothingItems { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Subcategory> Subcategories { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<OutfitClothingItem>()
                .HasKey(oci => new { oci.OutfitId, oci.ClothingItemId });

            builder.Entity<OutfitClothingItem>()
                .HasOne(oci => oci.Outfit)
                .WithMany(o => o.OutfitClothingItems)
                .HasForeignKey(oci => oci.OutfitId);

            builder.Entity<OutfitClothingItem>()
                .HasOne(oci => oci.ClothingItem)
                .WithMany()
                .HasForeignKey(oci => oci.ClothingItemId);

            builder.Entity<UserProfile>()
                .HasOne(up => up.User)
                .WithOne()
                .HasForeignKey<UserProfile>(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
