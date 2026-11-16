using Microsoft.EntityFrameworkCore;

public static class DatabaseSeeder
{
    public static async Task EnsureTablesAndSeedAsync(GameDb db, IItemCatalog catalog)
    {
        var pg = db.Database.IsNpgsql();

        // 1. Ensure CatalogItems table and O(1) Hash Index exist
        await db.Database.ExecuteSqlRawAsync(pg
            ? """
              CREATE TABLE IF NOT EXISTS "CatalogItems" (
                  "Id" text NOT NULL PRIMARY KEY,
                  "Name" text NOT NULL,
                  "Category" text NOT NULL,
                  "IsTool" boolean NOT NULL,
                  "MaxStack" integer NOT NULL,
                  "BuyPrice" integer NOT NULL,
                  "SellPrice" integer NOT NULL,
                  "Edibility" integer NOT NULL DEFAULT 0,
                  "IsDrink" boolean NOT NULL DEFAULT false,
                  "ToolAction" text,
                  "PowerTier" integer NOT NULL,
                  "EnergyCost" integer NOT NULL,
                  "HarvestItemId" text,
                  "GrowthDays" integer NOT NULL DEFAULT 0,
                  "RegrowDays" integer NOT NULL DEFAULT 0,
                  "LookupHash" text NOT NULL,
                  "CreatedUtc" timestamp with time zone NOT NULL
              )
              """
            : """
              CREATE TABLE IF NOT EXISTS "CatalogItems" (
                  "Id" TEXT NOT NULL PRIMARY KEY,
                  "Name" TEXT NOT NULL,
                  "Category" TEXT NOT NULL,
                  "IsTool" INTEGER NOT NULL,
                  "MaxStack" INTEGER NOT NULL,
                  "BuyPrice" INTEGER NOT NULL,
                  "SellPrice" INTEGER NOT NULL,
                  "Edibility" INTEGER NOT NULL DEFAULT 0,
                  "IsDrink" INTEGER NOT NULL DEFAULT 0,
                  "ToolAction" TEXT,
                  "PowerTier" INTEGER NOT NULL,
                  "EnergyCost" INTEGER NOT NULL,
                  "HarvestItemId" TEXT,
                  "GrowthDays" INTEGER NOT NULL DEFAULT 0,
                  "RegrowDays" INTEGER NOT NULL DEFAULT 0,
                  "LookupHash" TEXT NOT NULL,
                  "CreatedUtc" TEXT NOT NULL
              )
              """);

        if (pg)
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "CatalogItems" ADD COLUMN IF NOT EXISTS "Edibility" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "CatalogItems" ADD COLUMN IF NOT EXISTS "IsDrink" boolean NOT NULL DEFAULT false""");
        }
        else
        {
            await db.Database.OpenConnectionAsync();
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = """PRAGMA table_info("CatalogItems")""";
                var columns = new HashSet<string>(StringComparer.Ordinal);
                await using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
                if (!columns.Contains("Edibility"))
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "CatalogItems" ADD COLUMN "Edibility" INTEGER NOT NULL DEFAULT 0""");
                if (!columns.Contains("IsDrink"))
                    await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "CatalogItems" ADD COLUMN "IsDrink" INTEGER NOT NULL DEFAULT 0""");
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }

        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE INDEX IF NOT EXISTS "IX_CatalogItems_LookupHash" ON "CatalogItems" USING HASH ("LookupHash")"""
            : """CREATE INDEX IF NOT EXISTS "IX_CatalogItems_LookupHash" ON "CatalogItems" ("LookupHash")""");

        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_CatalogItems_Category" ON "CatalogItems" ("Category")""");

        // 2. Ensure LandTypes table exists
        await db.Database.ExecuteSqlRawAsync(pg
            ? """
              CREATE TABLE IF NOT EXISTS "LandTypes" (
                  "Id" text NOT NULL PRIMARY KEY,
                  "Name" text NOT NULL,
                  "Description" text NOT NULL,
                  "GrowthSpeedMultiplier" double precision NOT NULL,
                  "WaterRetentionChance" double precision NOT NULL,
                  "AllowsAllSeasons" boolean NOT NULL,
                  "BonusHarvestChance" double precision NOT NULL,
                  "CreatedUtc" timestamp with time zone NOT NULL
              )
              """
            : """
              CREATE TABLE IF NOT EXISTS "LandTypes" (
                  "Id" TEXT NOT NULL PRIMARY KEY,
                  "Name" TEXT NOT NULL,
                  "Description" TEXT NOT NULL,
                  "GrowthSpeedMultiplier" REAL NOT NULL,
                  "WaterRetentionChance" REAL NOT NULL,
                  "AllowsAllSeasons" INTEGER NOT NULL,
                  "BonusHarvestChance" REAL NOT NULL,
                  "CreatedUtc" TEXT NOT NULL
              )
              """);

        // 3. Seed LandTypes if empty
        if (!await db.LandTypes.AnyAsync())
        {
            var landTypes = new LandTypeEntity[]
            {
                new()
                {
                    Id = "normal",
                    Name = "Đất thường",
                    Description = "Đất canh tác cơ bản, cần tưới nước mỗi ngày.",
                    GrowthSpeedMultiplier = 1.0,
                    WaterRetentionChance = 0.0,
                    AllowsAllSeasons = false,
                    BonusHarvestChance = 0.0
                },
                new()
                {
                    Id = "fertile",
                    Name = "Đất màu mỡ",
                    Description = "Giàu dưỡng chất, rút ngắn thời gian sinh trưởng của cây 25%.",
                    GrowthSpeedMultiplier = 1.25,
                    WaterRetentionChance = 0.15,
                    AllowsAllSeasons = false,
                    BonusHarvestChance = 0.10
                },
                new()
                {
                    Id = "retaining",
                    Name = "Đất giữ ẩm",
                    Description = "Đất mùn có khả năng giữ ẩm cao, 70% không cần tưới lại vào ngày hôm sau.",
                    GrowthSpeedMultiplier = 1.0,
                    WaterRetentionChance = 0.70,
                    AllowsAllSeasons = false,
                    BonusHarvestChance = 0.05
                },
                new()
                {
                    Id = "greenhouse",
                    Name = "Đất nhà kính",
                    Description = "Nhiệt độ tối ưu, cho phép gieo trồng tất cả các loại hạt bất kể mùa nào.",
                    GrowthSpeedMultiplier = 1.15,
                    WaterRetentionChance = 0.30,
                    AllowsAllSeasons = true,
                    BonusHarvestChance = 0.15
                },
                new()
                {
                    Id = "mineral",
                    Name = "Đất khoáng sản",
                    Description = "Vùng đất trù phú chứa vi khoáng, có cơ hội nhân đôi sản lượng thu hoạch.",
                    GrowthSpeedMultiplier = 1.0,
                    WaterRetentionChance = 0.10,
                    AllowsAllSeasons = false,
                    BonusHarvestChance = 0.25
                }
            };

            db.LandTypes.AddRange(landTypes);
            await db.SaveChangesAsync();
        }

        // Keep older databases in sync as content gains new IDs. Existing
        // player inventory references stay valid because rows are never removed.
        var existing = await db.CatalogItems.ToDictionaryAsync(item => item.Id, StringComparer.Ordinal);
        var tools = catalog.AllTools.ToDictionary(tool => tool.Id, StringComparer.Ordinal);
        foreach (var item in catalog.All)
        {
            if (!existing.TryGetValue(item.Id, out var entity))
            {
                entity = new CatalogItemEntity { Id = item.Id, CreatedUtc = DateTime.UtcNow };
                db.CatalogItems.Add(entity);
            }
            entity.Name = item.Name;
            entity.Category = item.Category;
            entity.IsTool = item.IsTool;
            entity.MaxStack = item.MaxStack;
            entity.BuyPrice = item.BuyPrice;
            entity.SellPrice = item.SellPrice;
            entity.Edibility = item.Edibility;
            entity.IsDrink = item.IsDrink;
            entity.LookupHash = item.LookupHash;
            if (tools.TryGetValue(item.Id, out var tool))
            {
                entity.ToolAction = tool.ActionKind;
                entity.PowerTier = tool.PowerTier;
                entity.EnergyCost = tool.EnergyCost;
            }
            (entity.HarvestItemId, entity.GrowthDays, entity.RegrowDays) = item.Id switch
            {
                "seeds_parsnip" => ("parsnip", 4, 0),
                "seeds_potato" => ("potato", 6, 0),
                "seeds_green_bean" => ("green_bean", 10, 3),
                "seeds_cauliflower" => ("cauliflower", 12, 0),
                "mixed_seeds" => ("mixed", 0, 0),
                _ => (null, 0, 0)
            };
        }
        await db.SaveChangesAsync();
    }
}
