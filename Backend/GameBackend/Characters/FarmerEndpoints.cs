/// <summary>
/// SRP: Maps the create-once PUT /api/farmer endpoint.
/// The first saved farmer belongs to the account for its lifetime.
/// </summary>
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;

public static class FarmerEndpoints
{
    public static async Task EnsureFarmerColumns(GameDb db)
    {
        string[] columns = { "Accessory", "PetBreed", "HairColorRgb", "EyeColorRgb", "PantsColorRgb", "IntroMode" };
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "Accessory" integer NOT NULL DEFAULT -1""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "PetBreed" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "HairColorRgb" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "EyeColorRgb" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "PantsColorRgb" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Farmers" ADD COLUMN IF NOT EXISTS "IntroMode" integer NOT NULL DEFAULT 0""");
            return;
        }
        await db.Database.OpenConnectionAsync();
        try
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(\"Farmers\")";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) existing.Add(reader.GetString(1));
            }
            foreach (string column in columns)
                if (!existing.Contains(column))
                    await db.Database.ExecuteSqlRawAsync(column switch
                    {
                        "Accessory" => """ALTER TABLE "Farmers" ADD COLUMN "Accessory" INTEGER NOT NULL DEFAULT -1""",
                        "PetBreed" => """ALTER TABLE "Farmers" ADD COLUMN "PetBreed" INTEGER NOT NULL DEFAULT 0""",
                        "HairColorRgb" => """ALTER TABLE "Farmers" ADD COLUMN "HairColorRgb" INTEGER NOT NULL DEFAULT 0""",
                        "EyeColorRgb" => """ALTER TABLE "Farmers" ADD COLUMN "EyeColorRgb" INTEGER NOT NULL DEFAULT 0""",
                        "IntroMode" => """ALTER TABLE "Farmers" ADD COLUMN "IntroMode" INTEGER NOT NULL DEFAULT 0""",
                        _ => """ALTER TABLE "Farmers" ADD COLUMN "PantsColorRgb" INTEGER NOT NULL DEFAULT 0"""
                    });
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public static void MapFarmer(this WebApplication app)
    {
        app.MapPut("/api/farmer", async (HttpRequest request, FarmerDto input, GameDb db) =>
        {
            var user = await AuthTokens.ResolveUser(request, db);
            if (user == null) return Results.Unauthorized();
            if (user.Farmer != null)
                return Results.Conflict(new ApiError("Farmer already exists for this account."));

            if (!ValidName(input.Name, 24) || !ValidName(input.FarmName, 24) ||
                !ValidName(input.FavoriteThing, 32) ||
                !InRange(input.Gender, 2) || !(InRange(input.Hair, 56) || input.Hair is >= 100 and <= 117) || !InRange(input.Skin, 24) ||
                !InRange(input.Shirt, 304) || !InRange(input.Pants, 20) ||
                !InRange(input.Pet, 2) || !InRange(input.PetBreed, 3) ||
                input.Accessory < -1 || input.Accessory > 18 ||
                !InRange(input.HairColor, 6) || !InRange(input.EyeColor, 6) ||
                !ValidRgb(input.HairColorRgb) || !ValidRgb(input.EyeColorRgb) || !ValidRgb(input.PantsColorRgb) ||
                !InRange(input.IntroMode, 3))
                return Results.BadRequest(new ApiError("Invalid farmer profile."));

            var farmer = new FarmerProfile { UserId = user.Id };
            farmer.CopyFrom(input);
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Farmers.Add(farmer);
            try
            {
                await db.SaveChangesAsync();
                if (farmer.IntroMode != 0)
                    await GameStateEndpoints.InitializeNewFarmerInventory(db, user.Id);
                await transaction.CommitAsync();
            }
            catch (DbUpdateException error) when (
                error.InnerException is SqliteException { SqliteErrorCode: 19 } ||
                error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two sessions may submit the first profile at the same time.
                return Results.Conflict(new ApiError("Farmer already exists for this account."));
            }
            return Results.Ok(farmer.ToDto());
        });
    }

    private static bool ValidName(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;

    private static bool InRange(int value, int count) =>
        value >= 0 && value < count;
    private static bool ValidRgb(int value) => value == 0 || (value >= 0x1000000 && value <= 0x1ffffff);
}
