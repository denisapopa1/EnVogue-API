using Microsoft.EntityFrameworkCore;

namespace WardrobeApi.Data
{
    public static class DbSchemaUpdater
    {
        public static async Task EnsureUserProfilesTableAsync(ApplicationDbContext context)
        {
            const string sql = """
                CREATE TABLE IF NOT EXISTS "UserProfiles" (
                    "UserId" TEXT NOT NULL CONSTRAINT "PK_UserProfiles" PRIMARY KEY,
                    "DisplayName" TEXT NOT NULL DEFAULT '',
                    "Bio" TEXT NOT NULL DEFAULT '',
                    "Phone" TEXT NOT NULL DEFAULT '',
                    "City" TEXT NOT NULL DEFAULT '',
                    "Birthday" TEXT NOT NULL DEFAULT '',
                    "FavoriteStyle" TEXT NOT NULL DEFAULT '',
                    "FavoriteBrands" TEXT NOT NULL DEFAULT '',
                    "PreferredColors" TEXT NOT NULL DEFAULT '',
                    "InstagramHandle" TEXT NOT NULL DEFAULT '',
                    "StylingNotes" TEXT NOT NULL DEFAULT '',
                    "ProfilePhotoDataUrl" TEXT NULL,
                    CONSTRAINT "FK_UserProfiles_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id")
                        ON DELETE CASCADE
                );
                """;

            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
