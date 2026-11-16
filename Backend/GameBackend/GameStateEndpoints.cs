using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

public static partial class GameStateEndpoints
{
    private const int MaxBagSize = 36;
    private const int ChestSize = 36;
    private const int ChestWoodCost = 50;
    private const long MoneyLimit = 999_999_999;
    private static readonly Lazy<GuildStockFile> GuildStock = new(() =>
        JsonSerializer.Deserialize<GuildStockFile>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "AdventureGuildStock.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Adventurer's Guild stock is missing."));

    private sealed record GuildStockFile(GuildStockEntry[] Offers, Dictionary<string, int>? Resale);
    private sealed record GuildStockEntry(string ItemId, int Price, int Floor, int CombatLevel, int SellPrice);
    private static readonly Lazy<Dictionary<int, MineTreasureReward>> MineTreasure = new(() =>
        (JsonSerializer.Deserialize<MineTreasureFile>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "MineTreasureRewards.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Mine treasure rewards are missing.")).Rewards.ToDictionary(reward => reward.Floor));

    private sealed record MineTreasureFile(MineTreasureReward[] Rewards);
    private sealed record MineTreasureReward(int Floor, int X, int Y, string ItemId, int Amount, bool Special);

    public static async Task EnsureStateTable(GameDb db)
    {
        // Existing account databases were originally created with EnsureCreated.
        // Add this aggregate table without dropping or recreating their accounts.
        var pg = db.Database.IsNpgsql();
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "PlayerStates" ("UserId" uuid NOT NULL PRIMARY KEY, "Money" bigint NOT NULL, "BagSize" integer NOT NULL DEFAULT 12, "InventoryJson" text NOT NULL, "ChestsJson" text NOT NULL, "RecentRequestsJson" text NOT NULL, "Version" integer NOT NULL)"""
            : """CREATE TABLE IF NOT EXISTS "PlayerStates" ("UserId" TEXT NOT NULL PRIMARY KEY, "Money" INTEGER NOT NULL, "BagSize" INTEGER NOT NULL DEFAULT 12, "InventoryJson" TEXT NOT NULL, "ChestsJson" TEXT NOT NULL, "RecentRequestsJson" TEXT NOT NULL, "Version" INTEGER NOT NULL)""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "ProcessedGameRequests" ("UserId" uuid NOT NULL, "RequestId" uuid NOT NULL, "CreatedUtc" timestamp with time zone NOT NULL, PRIMARY KEY ("UserId", "RequestId"))"""
            : """CREATE TABLE IF NOT EXISTS "ProcessedGameRequests" ("UserId" TEXT NOT NULL, "RequestId" TEXT NOT NULL, "CreatedUtc" TEXT NOT NULL, PRIMARY KEY ("UserId", "RequestId"))""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "FishingAttempts" ("Id" uuid NOT NULL PRIMARY KEY, "UserId" uuid NOT NULL, "FishItemId" text NOT NULL, "Size" integer NOT NULL, "StartedUtc" timestamp with time zone NOT NULL, "ClaimedUtc" timestamp with time zone)"""
            : """CREATE TABLE IF NOT EXISTS "FishingAttempts" ("Id" TEXT NOT NULL PRIMARY KEY, "UserId" TEXT NOT NULL, "FishItemId" TEXT NOT NULL, "Size" INTEGER NOT NULL, "StartedUtc" TEXT NOT NULL, "ClaimedUtc" TEXT)""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_FishingAttempts_UserId_StartedUtc" ON "FishingAttempts" ("UserId", "StartedUtc")""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "ResourceClaims" ("UserId" uuid NOT NULL, "Day" text NOT NULL, "Location" text NOT NULL, "TileX" integer NOT NULL, "TileY" integer NOT NULL, "ItemId" text NOT NULL, "Amount" integer NOT NULL, PRIMARY KEY ("UserId", "Day", "Location", "TileX", "TileY", "ItemId"))"""
            : """CREATE TABLE IF NOT EXISTS "ResourceClaims" ("UserId" TEXT NOT NULL, "Day" TEXT NOT NULL, "Location" TEXT NOT NULL, "TileX" INTEGER NOT NULL, "TileY" INTEGER NOT NULL, "ItemId" TEXT NOT NULL, "Amount" INTEGER NOT NULL, PRIMARY KEY ("UserId", "Day", "Location", "TileX", "TileY", "ItemId"))""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "FarmCropStates" ("UserId" uuid NOT NULL, "TileX" integer NOT NULL, "TileY" integer NOT NULL, "SeedItemId" text NOT NULL, "HarvestItemId" text NOT NULL, "ReadyUtc" timestamp with time zone NOT NULL, "RegrowDays" integer NOT NULL, PRIMARY KEY ("UserId", "TileX", "TileY"))"""
            : """CREATE TABLE IF NOT EXISTS "FarmCropStates" ("UserId" TEXT NOT NULL, "TileX" INTEGER NOT NULL, "TileY" INTEGER NOT NULL, "SeedItemId" TEXT NOT NULL, "HarvestItemId" TEXT NOT NULL, "ReadyUtc" TEXT NOT NULL, "RegrowDays" INTEGER NOT NULL, PRIMARY KEY ("UserId", "TileX", "TileY"))""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "NpcFriendships" ("UserId" uuid NOT NULL, "NpcName" text NOT NULL, "Points" integer NOT NULL, "LastGiftDay" integer NOT NULL, "GiftWeek" integer NOT NULL, "GiftsThisWeek" integer NOT NULL, PRIMARY KEY ("UserId", "NpcName"))"""
            : """CREATE TABLE IF NOT EXISTS "NpcFriendships" ("UserId" TEXT NOT NULL, "NpcName" TEXT NOT NULL, "Points" INTEGER NOT NULL, "LastGiftDay" INTEGER NOT NULL, "GiftWeek" INTEGER NOT NULL, "GiftsThisWeek" INTEGER NOT NULL, PRIMARY KEY ("UserId", "NpcName"))""");
        if (pg)
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "BagSize" integer NOT NULL DEFAULT 12""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "ShippingJson" text NOT NULL DEFAULT '[]'""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "GameDay" integer NOT NULL DEFAULT 1""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "DeepestMineFloor" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "SessionJson" text NOT NULL DEFAULT ''""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "SkillsJson" text NOT NULL DEFAULT '[0,0,0,0,0]'""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "ProgressJson" text NOT NULL DEFAULT '{{}}'""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "TotalEarnings" bigint NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PlayerStates" ADD COLUMN IF NOT EXISTS "RentDebt" bigint NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "ReadyGameDay" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "HasHarvested" boolean NOT NULL DEFAULT false""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "DaysToGrow" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "GrowthDays" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "RegrowDaysLeft" integer NOT NULL DEFAULT 0""");
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "FarmCropStates" ADD COLUMN IF NOT EXISTS "IsWatered" boolean NOT NULL DEFAULT false""");
        }
        else
        {
            await db.Database.OpenConnectionAsync();
            try
            {
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = """PRAGMA table_info("PlayerStates")""";
                using var reader = await command.ExecuteReaderAsync();
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));
                await reader.DisposeAsync();
                if (!columns.Contains("BagSize"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "BagSize" INTEGER NOT NULL DEFAULT 12""");
                if (!columns.Contains("ShippingJson"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "ShippingJson" TEXT NOT NULL DEFAULT '[]'""");
                if (!columns.Contains("GameDay"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "GameDay" INTEGER NOT NULL DEFAULT 1""");
                if (!columns.Contains("DeepestMineFloor"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "DeepestMineFloor" INTEGER NOT NULL DEFAULT 0""");
                if (!columns.Contains("SessionJson"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "SessionJson" TEXT NOT NULL DEFAULT ''""");
                if (!columns.Contains("SkillsJson"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "SkillsJson" TEXT NOT NULL DEFAULT '[0,0,0,0,0]'""");
                if (!columns.Contains("ProgressJson"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "ProgressJson" TEXT NOT NULL DEFAULT '{{}}'""");
                if (!columns.Contains("TotalEarnings"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "TotalEarnings" INTEGER NOT NULL DEFAULT 0""");
                if (!columns.Contains("RentDebt"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "PlayerStates" ADD COLUMN "RentDebt" INTEGER NOT NULL DEFAULT 0""");
                command.CommandText = """PRAGMA table_info("FarmCropStates")""";
                using var cropReader = await command.ExecuteReaderAsync();
                bool hasReadyGameDay = false;
                bool hasHarvested = false;
                var cropColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await cropReader.ReadAsync())
                {
                    cropColumns.Add(cropReader.GetString(1));
                    hasReadyGameDay |= string.Equals(cropReader.GetString(1), "ReadyGameDay",
                        StringComparison.OrdinalIgnoreCase);
                    hasHarvested |= string.Equals(cropReader.GetString(1), "HasHarvested",
                        StringComparison.OrdinalIgnoreCase);
                }
                await cropReader.DisposeAsync();
                if (!hasReadyGameDay)
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "ReadyGameDay" INTEGER NOT NULL DEFAULT 0""");
                if (!hasHarvested)
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "HasHarvested" INTEGER NOT NULL DEFAULT 0""");
                if (!cropColumns.Contains("DaysToGrow"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "DaysToGrow" INTEGER NOT NULL DEFAULT 0""");
                if (!cropColumns.Contains("GrowthDays"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "GrowthDays" INTEGER NOT NULL DEFAULT 0""");
                if (!cropColumns.Contains("RegrowDaysLeft"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "RegrowDaysLeft" INTEGER NOT NULL DEFAULT 0""");
                if (!cropColumns.Contains("IsWatered"))
                    await db.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "FarmCropStates" ADD COLUMN "IsWatered" INTEGER NOT NULL DEFAULT 0""");
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        // Older saves inferred growth from wall-clock UTC, even while the player
        // stayed in the farmhouse. That can make day-one seeds instantly ripe.
        // ReadyGameDay == 0 identifies crops that have not yet been reconciled
        // with the owner's personal, watered game-day progression.
        var legacyCrops = await db.FarmCropStates
            .Where(c => c.DaysToGrow == 0 || c.ReadyGameDay == 0).ToListAsync();
        var playerDays = await db.PlayerStates.AsNoTracking()
            .Select(s => new { s.UserId, s.GameDay })
            .ToDictionaryAsync(s => s.UserId, s => Math.Max(1, s.GameDay));
        foreach (var crop in legacyCrops)
        {
            int daysToGrow = crop.SeedItemId switch
            {
                "seeds_parsnip" => 4,
                "seeds_potato" => 6,
                "seeds_green_bean" => 10,
                "seeds_cauliflower" => 12,
                _ => 0
            };
            if (daysToGrow == 0) continue;
            bool firstMigration = crop.DaysToGrow == 0;
            crop.DaysToGrow = daysToGrow;
            int gameDay = playerDays.GetValueOrDefault(crop.UserId, 1);
            if (crop.HasHarvested && crop.RegrowDays > 0)
                crop.RegrowDaysLeft = crop.RegrowDays;
            else
                crop.GrowthDays = firstMigration ? 0 :
                    Math.Clamp(crop.GrowthDays, 0, Math.Min(daysToGrow, gameDay - 1));
            crop.ReadyGameDay = gameDay + (crop.HasHarvested && crop.RegrowDays > 0
                ? crop.RegrowDaysLeft : daysToGrow - crop.GrowthDays);
        }
        if (legacyCrops.Count > 0) await db.SaveChangesAsync();
    }

    public static void MapGameState(this WebApplication app)
    {
        app.MapGet("/api/game/state", async (HttpRequest request, GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var state = await Load(db, userId.Value);
            var doc = Decode(state);
            await HydrateLegacyProgress(db, doc);
            return Results.Ok(Snapshot(doc, state.Version, catalog) with
            {
                Session = PlayerSession.ForDay(state.SessionJson, doc.GameDay)
            });
        });

        // Saved whenever the player leaves the game (quit, back to title) and periodically, so
        // the next login resumes in the same place instead of waking up in bed at 6:00.
        app.MapPost("/api/game/session", async (HttpRequest request, PlayerSession input, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var state = await Load(db, userId.Value);
            string? error = input.Validate(state.GameDay);
            if (error != null) return Results.BadRequest(new ApiError(error));
            string json = JsonSerializer.Serialize(input);
            await db.PlayerStates.Where(s => s.UserId == userId.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.SessionJson, json));
            return Results.Ok();
        });

        app.MapGet("/api/game/farm/crops", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var state = await Load(db, userId.Value);
            bool raining = DailyEvents.FarmWeather.IsRain(WeatherForDay(userId.Value, state.GameDay));
            var crops = await db.FarmCropStates.AsNoTracking()
                .Where(c => c.UserId == userId.Value)
                .Select(c => new FarmCropDto(c.TileX, c.TileY, c.SeedItemId,
                    c.HarvestItemId, c.ReadyUtc, c.RegrowDays, c.ReadyGameDay,
                    c.HasHarvested, c.DaysToGrow, c.GrowthDays,
                    c.RegrowDaysLeft, c.IsWatered || raining)).ToArrayAsync();
            var soil = PlayerProgress.FromJson(state.ProgressJson).FarmSoil.Values
                .Select(tile => new FarmSoilDto(tile.TileX, tile.TileY,
                    raining || tile.WateredDay == state.GameDay, tile.Fertilizer)).ToArray();
            return Results.Ok(new FarmCropListResponse(crops, soil));
        });
        MapFarmLand(app);
        MapRanch(app);

        app.MapGet("/api/game/npc/friendships", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var friends = await db.NpcFriendships.AsNoTracking()
                .Where(f => f.UserId == userId.Value)
                .Select(f => new NpcFriendshipDto(f.NpcName, f.Points, f.LastGiftDay,
                    f.GiftsThisWeek)).ToArrayAsync();
            return Results.Ok(new NpcFriendshipListResponse(friends));
        });

        app.MapPost("/api/game/npc/gift", async (HttpRequest request, NpcGiftRequest input,
            GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(input.NpcName) || !NpcGiftTastes.IsKnownNpc(input.NpcName))
                return Results.BadRequest(new ApiError("NPC is not available for gifts."));
            var friendship = await db.NpcFriendships.SingleOrDefaultAsync(f =>
                f.UserId == userId.Value && f.NpcName == input.NpcName);
            return await Change(request, db, input.RequestId, doc =>
            {
                if (input.Slot < 0 || input.Slot >= doc.BagSize ||
                    string.IsNullOrWhiteSpace(input.ItemId)) return "Gift slot is invalid.";
                var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
                if (stack == null || stack.ItemId != input.ItemId || stack.Amount < 1 ||
                    !catalog.TryGet(stack.ItemId, out var item) || item.IsTool ||
                    item.Category is "Seeds" or "BigCraftable" or "Furniture")
                    return "This item cannot be given as a gift.";
                int week = (doc.GameDay - 1) / 7;
                if (friendship != null && (friendship.LastGiftDay == doc.GameDay ||
                    (friendship.GiftWeek == week && friendship.GiftsThisWeek >= 2)))
                    return "You have already given the allowed gifts to this NPC.";
                stack.Amount--;
                if (stack.Amount == 0) doc.Inventory.Remove(stack);
                if (friendship == null)
                {
                    friendship = new NpcFriendship { UserId = userId.Value,
                        NpcName = input.NpcName };
                    db.NpcFriendships.Add(friendship);
                }
                friendship.Points = Math.Clamp(friendship.Points +
                    NpcGiftTastes.FriendshipPoints(input.NpcName, item), 0, 2500);
                doc.Progress.FriendshipPoints[input.NpcName!] = friendship.Points;
                friendship.LastGiftDay = doc.GameDay;
                friendship.GiftsThisWeek = friendship.GiftWeek == week
                    ? friendship.GiftsThisWeek + 1 : 1;
                friendship.GiftWeek = week;
                return null;
            });
        });

        app.MapPost("/api/game/chests", async (HttpRequest request, CreateChestRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                var name = input.Name?.Trim() ?? "";
                if (name.Length is < 1 or > 24 || doc.Chests.Count >= 20)
                    return "Chest name or limit is invalid.";
                if (!ValidChestTile(input.Location, input.TileX, input.TileY))
                    return "Place the chest on a farmhouse floor tile.";
                if (doc.Chests.Any(c => c.Location == input.Location &&
                                        c.TileX == input.TileX && c.TileY == input.TileY))
                    return "A chest already occupies that tile.";
                // A chest crafted at the crafting page is used first; otherwise 50 wood.
                if (TakeItems(doc.Inventory, "big_craftable_130", 1))
                {
                    doc.Chests.Add(new ChestEntry
                    {
                        Id = Guid.NewGuid(), Name = name, Location = input.Location!,
                        TileX = input.TileX, TileY = input.TileY
                    });
                    return null;
                }
                int wood = doc.Inventory.Where(s => s.ItemId == "wood").Sum(s => s.Amount);
                if (wood < ChestWoodCost) return "Crafting a chest requires 50 wood.";
                int remaining = ChestWoodCost;
                foreach (var stack in doc.Inventory.Where(s => s.ItemId == "wood").ToArray())
                {
                    int take = Math.Min(remaining, stack.Amount);
                    stack.Amount -= take;
                    remaining -= take;
                    if (stack.Amount == 0) doc.Inventory.Remove(stack);
                    if (remaining == 0) break;
                }
                doc.Chests.Add(new ChestEntry
                {
                    Id = Guid.NewGuid(), Name = name, Location = input.Location!,
                    TileX = input.TileX, TileY = input.TileY
                });
                return null;
            }));

        // Chests created by the older storage screen have no world position.
        // Their owner can place each one once, preserving its contents.
        app.MapPost("/api/game/chests/{id:guid}/place",
            async (HttpRequest request, Guid id, PlaceChestRequest input, GameDb db) =>
                await Change(request, db, input.RequestId, doc =>
                {
                    var chest = doc.Chests.Find(c => c.Id == id);
                    if (chest == null) return "Chest not found.";
                    if (!string.IsNullOrEmpty(chest.Location)) return "Chest is already placed.";
                    if (!ValidChestTile(input.Location, input.TileX, input.TileY))
                        return "Place the chest on a farmhouse floor tile.";
                    if (doc.Chests.Any(c => c.Location == input.Location &&
                                            c.TileX == input.TileX && c.TileY == input.TileY))
                        return "A chest already occupies that tile.";
                    chest.Location = input.Location!;
                    chest.TileX = input.TileX;
                    chest.TileY = input.TileY;
                    return null;
                }));

        app.MapPost("/api/game/transfer", async (HttpRequest request, TransferRequest input, GameDb db,
            IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc => Transfer(doc, input, catalog)));

        app.MapPost("/api/game/inventory/swap", async (HttpRequest request, SwapSlotsRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.FromSlot < 0 || input.FromSlot >= doc.BagSize ||
                    input.ToSlot < 0 || input.ToSlot >= doc.BagSize || input.FromSlot == input.ToSlot)
                    return "Inventory slots are invalid.";
                var from = doc.Inventory.Find(s => s.Slot == input.FromSlot);
                var to = doc.Inventory.Find(s => s.Slot == input.ToSlot);
                if (from == null) return "Source item is no longer available.";
                if (!catalog.TryGet(from.ItemId, out var definition)) return "Unknown source item.";
                if (to != null && to.ItemId == from.ItemId)
                {
                    int amount = Math.Min(from.Amount, Math.Max(0, definition.MaxStack - to.Amount));
                    if (amount == 0) return "Target stack is full.";
                    to.Amount += amount;
                    from.Amount -= amount;
                    if (from.Amount == 0) doc.Inventory.Remove(from);
                }
                else
                {
                    from.Slot = input.ToSlot;
                    if (to != null) to.Slot = input.FromSlot;
                }
                return null;
            }));

        app.MapPost("/api/game/inventory/organize", async (HttpRequest request, BackpackRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc => OrganizeInventory(doc, catalog)));

        app.MapPost("/api/game/inventory/discard", async (HttpRequest request, DiscardItemRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Slot < 0 || input.Slot >= doc.BagSize || string.IsNullOrEmpty(input.ItemId))
                    return "Item slot is invalid.";
                var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
                if (stack == null || stack.ItemId != input.ItemId ||
                    !catalog.TryGet(stack.ItemId, out var item) || stack.Amount <= 0 || item.IsTool)
                    return "Item is no longer available.";
                doc.Inventory.Remove(stack);
                // Upgraded trash cans give back 15% of the sell price per level.
                long refund = (long)(item.SellPrice * 0.15 * doc.Progress.TrashCanLevel * stack.Amount);
                if (refund > 0) doc.Money = Math.Min(MoneyLimit, doc.Money + refund);
                return null;
            }));

        app.MapPost("/api/game/craft", async (HttpRequest request, CraftItemRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc => Craft(doc, input.RecipeName, catalog)));

        MapNaturalResources(app);
        MapPlacedObjects(app);
        MapChestActions(app);
        MapBundles(app);
        MapMineProgress(app);
        MapProfessions(app);
        MapJojaRoute(app);
        MapForage(app);

        app.MapPost("/api/game/resources/collect", async (HttpRequest request,
            CollectResourceRequest input, GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return await Change(request, db, input.RequestId, doc =>
            {
                bool inMine = int.TryParse(input.Location, out int floor) && floor > 0;
                int tileLimit = input.ItemId switch
                {
                    "wood" or "stone" => 24,
                    "fiber" or "object_92" or "hardwood" or "object_709" => 12,
                    "mixed_seeds" or "coal" or "copper_ore" or "iron_ore" or "gold_ore" => 8,
                    "acorn" or "maple_seed" or "pine_cone" => 3,
                    "object_60" or "object_62" or "object_64" or "object_66" or "object_68" or
                    "object_70" or "object_72" or "object_74" or "object_535" or "object_536" or
                    "object_537" or "object_749" when inMine => 1,
                    "object_386" or "object_848" or "object_881" or "object_909" or "object_330" or
                    "object_719" when inMine => 8,
                    "object_335" or "object_769" when inMine => 8,
                    _ => 0
                };
                if (tileLimit == 0 || input.Amount < 1 || input.Amount > tileLimit ||
                    string.IsNullOrWhiteSpace(input.Location) || input.Location.Length > 64 ||
                    !input.Location.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or ' ') ||
                    input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512 ||
                    !catalog.TryGet(input.ItemId!, out var item))
                    return "Resource claim is invalid.";
                if (!WorldSessionEndpoints.IsPlayerNear(userId.Value, input.Location!,
                    input.TileX, input.TileY, 6f)) return "Move closer to the resource.";
                string day = doc.GameDay.ToString(System.Globalization.CultureInfo.InvariantCulture);
                // Mine floors are rebuilt on every visit, so their tiles are counted per visit.
                string claimLocation = inMine && input.Visit != 0
                    ? input.Location + "#" + input.Visit.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : input.Location!;
                int collectedToday = db.ResourceClaims.Where(c => c.UserId == userId.Value && c.Day == day)
                    .Sum(c => (int?)c.Amount) ?? 0;
                if (collectedToday + input.Amount > 1500) return "Daily resource limit reached.";
                var claim = db.ResourceClaims.SingleOrDefault(c => c.UserId == userId.Value &&
                    c.Day == day && c.Location == claimLocation && c.TileX == input.TileX &&
                    c.TileY == input.TileY && c.ItemId == input.ItemId);
                if ((claim?.Amount ?? 0) + input.Amount > tileLimit)
                    return "This resource has already been collected.";
                if (!CanAdd(doc.Inventory, item.Id, input.Amount, item.MaxStack, doc.BagSize))
                    return "Inventory is full.";
                Add(doc.Inventory, item.Id, input.Amount, item.MaxStack, doc.BagSize);
                if (inMine && input.ItemId == "copper_ore" && doc.Progress.Milestones.FirstCopperDay == 0)
                    doc.Progress.Milestones.FirstCopperDay = doc.GameDay;
                if (claim == null)
                {
                    int skill = inMine || input.ItemId is "stone" or "coal" or "copper_ore" or
                        "iron_ore" or "gold_ore" ? 3 : 2;
                    GainExperience(doc, skill, skill == 3 ? 5 : 7);
                }
                if (claim == null) db.ResourceClaims.Add(new ResourceClaim { UserId = userId.Value,
                    Day = day, Location = claimLocation, TileX = input.TileX, TileY = input.TileY,
                    ItemId = item.Id, Amount = input.Amount });
                else claim.Amount += input.Amount;
                return null;
            });
        });

        app.MapPost("/api/game/farm/plant", async (HttpRequest request, PlantFarmCropRequest input,
            GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return await Change(request, db, input.RequestId, doc =>
            {
                if (input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512 ||
                    input.SeedSlot < 0 || input.SeedSlot >= doc.BagSize)
                    return "Farm tile or seed slot is invalid.";
                if (!WorldSessionEndpoints.IsPlayerNear(userId.Value, "Farm",
                    input.TileX, input.TileY, 2.5f)) return "Move closer to the farm tile.";
                if (!doc.Progress.FarmSoil.TryGetValue(FarmTileKey(input.TileX, input.TileY), out var soil))
                    return "Hãy cuốc ô đất trước khi gieo hạt.";
                var landError = FarmExpansionError(db, doc, addingTile: false);
                if (landError != null) return landError;
                var stack = doc.Inventory.Find(s => s.Slot == input.SeedSlot);
                if (stack == null || stack.Amount < 1 || stack.ItemId != input.SeedItemId ||
                    !catalog.TryGet(stack.ItemId, out var seed))
                    return "Seed is no longer available.";
                var eventCrop = EventCalendar.Value.FindCrop(stack.ItemId);
                if (seed.Category != "Seeds" && eventCrop == null) return "Seed is no longer available.";
                if (eventCrop != null && eventCrop.season != new DailyEvents.FarmDate(doc.GameDay).Season)
                    return "This crop does not grow in the current season.";
                if (db.FarmCropStates.Any(c => c.UserId == userId.Value &&
                    c.TileX == input.TileX && c.TileY == input.TileY))
                    return "This tile is already planted.";
                string cropSeedId = stack.ItemId;
                if (cropSeedId == "mixed_seeds")
                {
                    // Spring low-grade crops are 472..475, with 473 remapped to 472.
                    if ((doc.GameDay - 1) / 28 % 4 != 0)
                        return "Mixed Seeds currently have crop definitions for spring only.";
                    cropSeedId = Random.Shared.Next(4) switch
                    {
                        2 => "seeds_cauliflower",
                        3 => "seeds_potato",
                        _ => "seeds_parsnip"
                    };
                }
                var crop = eventCrop != null
                    ? (Harvest: eventCrop.harvest, Days: eventCrop.GrowthDays, Regrow: eventCrop.regrow)
                    : cropSeedId switch
                {
                    "seeds_parsnip" => (Harvest: "parsnip", Days: 4, Regrow: 0),
                    "seeds_potato" => (Harvest: "potato", Days: 6, Regrow: 0),
                    "seeds_green_bean" => (Harvest: "green_bean", Days: 10, Regrow: 3),
                    "seeds_cauliflower" => (Harvest: "cauliflower", Days: 12, Regrow: 0),
                    _ => (Harvest: "", Days: 0, Regrow: 0)
                };
                if (crop.Days == 0 || !catalog.TryGet(crop.Harvest, out _))
                    return "This seed has no server crop definition.";
                stack.Amount--;
                if (stack.Amount == 0) doc.Inventory.Remove(stack);
                db.FarmCropStates.Add(new FarmCropState { UserId = userId.Value,
                    TileX = input.TileX, TileY = input.TileY, SeedItemId = seed.Id,
                    HarvestItemId = crop.Harvest, RegrowDays = crop.Regrow,
                    ReadyUtc = DateTime.UtcNow.AddSeconds(crop.Days * 840),
                    ReadyGameDay = doc.GameDay + SpeedUp(crop.Days, soil.Fertilizer),
                    DaysToGrow = SpeedUp(crop.Days, soil.Fertilizer), IsWatered = soil.WateredDay == doc.GameDay ||
                        DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay)) });
                return null;
            });
        });

        app.MapPost("/api/game/farm/water", async (HttpRequest request,
            WaterFarmCropRequest input, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return await Change(request, db, input.RequestId, doc =>
            {
                if (input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512 ||
                    !WorldSessionEndpoints.IsPlayerNear(userId.Value, "Farm",
                        input.TileX, input.TileY, 2.5f)) return "Farm tile is not nearby.";
                if (DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay)))
                    return "Mưa đã tưới đất hôm nay.";
                var crop = db.FarmCropStates.SingleOrDefault(c => c.UserId == userId.Value &&
                    c.TileX == input.TileX && c.TileY == input.TileY);
                if (crop != null)
                {
                    if (crop.IsWatered) return "Ô đất đã được tưới hôm nay.";
                    crop.IsWatered = true;
                }
                else
                {
                    int key = FarmTileKey(input.TileX, input.TileY);
                    if (!doc.Progress.FarmSoil.TryGetValue(key, out var soil))
                        return "Hãy cuốc ô đất trước khi tưới.";
                    if (soil.WateredDay == doc.GameDay) return "Ô đất đã được tưới hôm nay.";
                    doc.Progress.FarmSoil[key] = soil with { WateredDay = doc.GameDay };
                }
                return null;
            });
        });

        app.MapPost("/api/game/farm/harvest", async (HttpRequest request,
            HarvestFarmCropRequest input, GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return await Change(request, db, input.RequestId, doc =>
            {
                if (input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512)
                    return "Farm tile is invalid.";
                if (!WorldSessionEndpoints.IsPlayerNear(userId.Value, "Farm",
                    input.TileX, input.TileY, 2.5f)) return "Move closer to the farm tile.";
                var crop = db.FarmCropStates.SingleOrDefault(c => c.UserId == userId.Value &&
                    c.TileX == input.TileX && c.TileY == input.TileY);
                if (crop == null ||
                    (crop.DaysToGrow > 0
                        ? (crop.HasHarvested ? crop.RegrowDaysLeft > 0 : crop.GrowthDays < crop.DaysToGrow)
                        : crop.ReadyGameDay > 0 ? doc.GameDay < crop.ReadyGameDay : crop.ReadyUtc > DateTime.UtcNow) ||
                    !catalog.TryGet(crop.HarvestItemId, out var item))
                    return "Crop is not ready to harvest.";
                var soilTile = doc.Progress.FarmSoil.GetValueOrDefault(FarmTileKey(input.TileX, input.TileY));
                string harvested = WithQuality(item.Id, CropQuality(doc, soilTile?.Fertilizer,
                    new Random(HashCode.Combine(doc.UserId, doc.GameDay, input.TileX, input.TileY))));
                if (!catalog.TryGet(harvested, out var harvestedItem)) harvestedItem = item;
                if (!CanAdd(doc.Inventory, harvestedItem.Id, 1, harvestedItem.MaxStack, doc.BagSize))
                    return "Inventory is full.";
                Add(doc.Inventory, harvestedItem.Id, 1, harvestedItem.MaxStack, doc.BagSize);
                GainExperience(doc, 0, (int)Math.Round(16d * Math.Log(1d + item.SellPrice * 0.018d)));
                if (crop.RegrowDays > 0)
                {
                    crop.ReadyUtc = DateTime.UtcNow.AddSeconds(crop.RegrowDays * 840);
                    crop.ReadyGameDay = doc.GameDay + crop.RegrowDays;
                    crop.HasHarvested = true;
                    crop.RegrowDaysLeft = crop.RegrowDays;
                    crop.IsWatered = false;
                }
                else
                {
                    doc.Progress.FarmSoil[FarmTileKey(input.TileX, input.TileY)] =
                        new(input.TileX, input.TileY, crop.IsWatered ? doc.GameDay : 0, soilTile?.Fertilizer);
                    db.FarmCropStates.Remove(crop);
                }
                return null;
            });
        });

        app.MapGet("/api/game/shop/{shop}", async (string shop, HttpRequest request, GameDb db,
            IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            if (shop is not ("Clint" or "Robin" or "Marlon" or "Marnie")) return Results.NotFound();
            var doc = Decode(await Load(db, userId.Value));
            var offers = MerchantStock(shop, doc, catalog)
                .Select(offer => new ShopOffer(offer.Key, offer.Value)).ToArray();
            return Results.Ok(new ShopOfferList(offers));
        });

        app.MapPost("/api/game/mine/treasure", async (HttpRequest request, MineTreasureRequest input,
            GameDb db, IItemCatalog catalog) => await Change(request, db, input.RequestId, doc =>
            {
                if (!MineTreasure.Value.TryGetValue(input.Floor, out var reward))
                    return "There is no reward on this floor.";
                if (doc.Progress.ClaimedMineChests.Contains(input.Floor))
                    return "You already opened this chest.";
                if (!WorldSessionEndpoints.IsPlayerNearPosition(doc.UserId, input.Floor.ToString(),
                    reward.X + 0.5f, -reward.Y + 0.5f, 2.25f))
                    return "Stand next to the chest and try again.";
                // The Stardrop is consumed immediately and the Skull Key is a wallet unlock.
                if (!reward.Special)
                {
                    if (!catalog.TryGet(reward.ItemId, out var item))
                        return "This reward is unavailable.";
                    if (!CanAdd(doc.Inventory, item.Id, reward.Amount, item.MaxStack, doc.BagSize))
                        return "Make room in your backpack.";
                    Add(doc.Inventory, item.Id, reward.Amount, item.MaxStack, doc.BagSize);
                }
                doc.Progress.ClaimedMineChests.Add(input.Floor);
                return null;
            }));

        app.MapPost("/api/game/blacksmith/geode", async (HttpRequest request, SellRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc => CrackGeode(doc, input, catalog)));

        app.MapPost("/api/game/blacksmith/upgrade", async (HttpRequest request, SellRequest input,
            GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc => OrderToolUpgrade(doc, input, catalog)));
        app.MapPost("/api/game/blacksmith/collect-upgrade", async (HttpRequest request, BackpackRequest input,
            GameDb db, IItemCatalog catalog) => await Change(request, db, input.RequestId, doc =>
            {
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Blacksmith", 3, 14, 2.5f))
                    return "Stand at Clint's counter.";
                var order = doc.Progress.ToolUpgrade;
                if (order == null || doc.GameDay < order.ReadyDay) return "Your tool is not ready.";
                int trashTier = Array.IndexOf(TrashCanIds, order.ItemId);
                if (trashTier >= 0)
                {
                    doc.Progress.TrashCanLevel = Math.Max(doc.Progress.TrashCanLevel, trashTier + 1);
                    doc.Progress.ToolUpgrade = null;
                    return null;
                }
                if (!catalog.TryGet(order.ItemId, out var item) ||
                    !CanAdd(doc.Inventory, order.ItemId, 1, item.MaxStack, doc.BagSize))
                    return "Make room in the backpack.";
                Add(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize);
                doc.Progress.ToolUpgrade = null;
                return null;
            }));

        app.MapPost("/api/game/robin/upgrade-house", async (HttpRequest request, BackpackRequest input,
            GameDb db) => await Change(request, db, input.RequestId, doc =>
            {
                if (doc.Progress.HouseUpgradeDay > 0) return "House upgrade already ordered.";
                if (RanchConstructionPending(doc)) return "Robin đang thi công chuồng trại.";
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "ScienceHouse", 8, 19, 2.5f))
                    return "Stand at Robin's counter.";
                const int price = 10_000;
                const int wood = 450;
                if (doc.Money < price) return "You need 10000g.";
                if (!TakeItems(doc.Inventory, "wood", wood)) return "You need 450 Wood.";
                doc.Money -= price;
                doc.Progress.HouseUpgradeDay = doc.GameDay + 3;
                return null;
            }));

        app.MapPost("/api/game/buy", async (HttpRequest request, BuyRequest input, GameDb db,
            IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Shop is "Marlon" or "Robin" or "Marnie" && !AtMerchantCounter(doc.UserId, input.Shop))
                    return $"Hãy đứng ở quầy của {input.Shop}.";
                if (input.Shop == "Robin" && input.ItemId?.StartsWith("recipe_", StringComparison.Ordinal) == true)
                    return BuyRobinRecipe(doc, input, catalog);
                if (input.Shop == "JojaMart" && doc.Progress.Milestones.Seen.Contains("community_opening"))
                    return "JojaMart đã đóng cửa.";
                if (input.Amount is < 1 or > 999 || !catalog.TryGet(input.ItemId ?? "", out var item))
                    return "Item or amount is invalid.";
                int unitPrice = input.Shop is "Clint" or "Robin" or "Marlon" or "Marnie"
                    ? MerchantStock(input.Shop, doc, catalog).GetValueOrDefault(item.Id)
                    : ShopUnitPrice(input.Shop, input.ItemId!, item.BuyPrice);
                if (unitPrice <= 0) return "Item is not sold at this shop.";
                long cost = (long)unitPrice * input.Amount;
                if (doc.Money < cost) return "Not enough money.";
                if (!CanAdd(doc.Inventory, input.ItemId!, input.Amount, item.MaxStack, doc.BagSize))
                    return "Inventory is full.";
                Add(doc.Inventory, input.ItemId!, input.Amount, item.MaxStack, doc.BagSize);
                doc.Money -= cost;
                return null;
            }));

        // The current bag tier selects the 2,000g or 10,000g upgrade.
        app.MapPost("/api/game/backpack", async (HttpRequest request, BackpackRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (doc.BagSize >= MaxBagSize) return "Backpack is already fully upgraded.";
                long price = doc.BagSize == 12 ? 2_000 : 10_000;
                if (doc.Money < price) return "Not enough money.";
                doc.Money -= price;
                doc.BagSize += 12;
                return null;
            }));

        app.MapPost("/api/game/sell", async (HttpRequest request, SellRequest input, GameDb db,
            IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
                if (stack == null || input.Amount < 1 || input.Amount > stack.Amount ||
                    string.IsNullOrEmpty(input.ItemId) || stack.ItemId != input.ItemId ||
                    !catalog.TryGet(stack.ItemId, out var item))
                    return "Item or amount is invalid.";
                bool guild = input.Shop == "Marlon";
                if (guild && !AtMerchantCounter(doc.UserId, "Marlon")) return "Hãy đứng ở quầy của Marlon.";
                if (!guild && IsGuildGear(item.Id)) return "Hãy bán trang bị này cho Marlon ở Hội Mạo Hiểm.";
                int unitPrice = guild ? GuildResalePrice(item) : SalePrice(doc, item, catalog);
                if (unitPrice <= 0) return guild ? "Marlon không thu mua món này." : "Item cannot be sold.";
                long proceeds = (long)unitPrice * input.Amount;
                if (doc.Money > MoneyLimit - proceeds) return "Money limit reached.";
                stack.Amount -= input.Amount;
                if (stack.Amount == 0) doc.Inventory.Remove(stack);
                doc.Money += proceeds;
                doc.TotalEarnings = Math.Min(long.MaxValue - proceeds, doc.TotalEarnings) + proceeds;
                return null;
            }));

        // Shippable objects are stored until the next game day.
        app.MapPost("/api/game/shipping/deposit", async (HttpRequest request,
            SellRequest input, GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
                if (stack == null || input.Amount < 1 || input.Amount > stack.Amount ||
                    stack.ItemId != input.ItemId || !catalog.TryGet(stack.ItemId, out var item) ||
                    !item.CanShip)
                    return "Item cannot be shipped.";
                doc.Shipping.Add(new ShippingEntry(stack.ItemId, input.Amount, SalePrice(doc, item, catalog)));
                stack.Amount -= input.Amount;
                if (stack.Amount == 0) doc.Inventory.Remove(stack);
                return null;
            }));

        app.MapPost("/api/game/shipping/retrieve", async (HttpRequest request,
            BackpackRequest input, GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (doc.Shipping.Count == 0) return "Shipping bin is empty.";
                var last = doc.Shipping[^1];
                if (!catalog.TryGet(last.ItemId, out var item) ||
                    !CanAdd(doc.Inventory, last.ItemId, last.Amount, item.MaxStack, doc.BagSize))
                    return "Not enough room in the backpack.";
                Add(doc.Inventory, last.ItemId, last.Amount, item.MaxStack, doc.BagSize);
                doc.Shipping.RemoveAt(doc.Shipping.Count - 1);
                return null;
            }));

        MapDailyEventEndpoints(app);
        MapMilestones(app);

        app.MapPost("/api/game/day/advance", async (HttpRequest request,
            AdvanceDayRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Day != doc.GameDay + 1) return "Game day is out of sync.";
                // Farmer.performPassoutWarp: passing out away from home costs a tenth of the gold
                // carried, at most 1000g, taken before the night's shipping is paid.
                string? passedOut = input.PassedOutAt?.Trim();
                if (!string.IsNullOrEmpty(passedOut) && passedOut.Length <= 64 &&
                    passedOut is not ("FarmHouse" or "FarmHouse1" or "Cellar"))
                {
                    long fee = Math.Min(1000, doc.Money / 10);
                    doc.Money -= fee;
                    doc.Progress.LastPassOutCharge = new PassOutCharge(input.Day, fee, passedOut);
                }
                long proceeds = 0;
                foreach (var entry in doc.Shipping)
                {
                    if (entry.Amount < 1 || entry.UnitPrice < 0) return "Shipping data is invalid.";
                    long value = (long)entry.Amount * entry.UnitPrice;
                    if (value > MoneyLimit - proceeds) return "Money limit reached.";
                    proceeds += value;
                    GameProgression.RecordShipment(doc.Progress, entry.ItemId, entry.Amount);
                }
                if (doc.Money > MoneyLimit - proceeds) return "Money limit reached.";
                doc.Money += proceeds;
                doc.TotalEarnings = Math.Min(long.MaxValue - proceeds, doc.TotalEarnings) + proceeds;
                doc.Shipping.Clear();
                long billed = 0;
                if ((input.Day - 1) % TvEconomyEndpoints.BaseFarmRentDays == 0)
                {
                    billed = TvEconomyEndpoints.BaseFarmRentPrice;
                    doc.RentDebt = Math.Min(long.MaxValue - billed, doc.RentDebt) + billed;
                }
                // Rent is taken overnight like the gold lost when passing out: no bill to accept.
                // Whatever the farmer cannot cover stays owed and is taken on a later morning.
                if (doc.RentDebt > 0)
                {
                    long paid = Math.Min(doc.Money, doc.RentDebt);
                    doc.Money -= paid;
                    doc.RentDebt -= paid;
                    if (paid > 0 || billed > 0)
                        doc.Progress.LastRentCharge = new RentCharge(input.Day, billed, paid, doc.RentDebt);
                }
                bool rainToday = DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay));
                bool rainTomorrow = DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, input.Day));
                UpdateRanchAnimals(doc, doc.GameDay, rainToday);
                int endedDay = doc.GameDay;
                doc.GameDay = input.Day;
                var sprinkled = SprinklerTiles(doc);
                var retainRandom = new Random(HashCode.Combine(doc.UserId, input.Day));
                bool Retains(FarmSoilTile? soil) => soil?.Fertilizer != null &&
                    Artisan.Value.Fertilizers.Retain.TryGetValue(soil.Fertilizer, out double chance) &&
                    retainRandom.NextDouble() < chance;
                foreach (var pair in doc.Progress.FarmSoil.ToArray())
                {
                    var soil = pair.Value;
                    bool wet = sprinkled.Contains((soil.TileX, soil.TileY + 1)) ||
                               (soil.WateredDay == endedDay || rainToday) && Retains(soil);
                    if (wet) doc.Progress.FarmSoil[pair.Key] = soil with { WateredDay = input.Day };
                }
                foreach (var crop in db.FarmCropStates.Where(c => c.UserId == doc.UserId))
                {
                    bool wateredToday = crop.IsWatered || rainToday;
                    if (crop.DaysToGrow > 0 && (crop.IsWatered || rainToday))
                    {
                        if (crop.HasHarvested && crop.RegrowDaysLeft > 0)
                            crop.RegrowDaysLeft--;
                        else if (!crop.HasHarvested && crop.GrowthDays < crop.DaysToGrow)
                            crop.GrowthDays++;
                    }
                    var cropSoil = doc.Progress.FarmSoil.GetValueOrDefault(FarmTileKey(crop.TileX, crop.TileY));
                    crop.IsWatered = rainTomorrow || sprinkled.Contains((crop.TileX, crop.TileY + 1)) ||
                                     wateredToday && Retains(cropSoil);
                }
                return null;
            }));

        app.MapPost("/api/game/consume", async (HttpRequest request, ConsumeFoodRequest input, GameDb db,
            IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Slot < 0 || input.Slot >= doc.BagSize || string.IsNullOrWhiteSpace(input.ItemId))
                    return "Food slot or item is invalid.";
                var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
                if (stack == null || stack.Amount <= 0 || stack.ItemId != input.ItemId ||
                    !catalog.TryGet(stack.ItemId, out var item) || item.Edibility <= 0)
                    return "Food is no longer available.";
                stack.Amount--;
                if (stack.Amount == 0) doc.Inventory.Remove(stack);
                return null;
            }));

        app.MapPost("/api/game/fish/start", async (HttpRequest request, GameDb db,
            IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var player = await Load(db, userId.Value);
            if (!Decode(player).Inventory.Any(s => s.ItemId == "fishing_rod" && s.Amount > 0))
                return Results.BadRequest(new ApiError("A fishing rod is required."));
            var now = DateTime.UtcNow;
            var latest = await db.FishingAttempts.AsNoTracking()
                .Where(a => a.UserId == userId).OrderByDescending(a => a.StartedUtc).FirstOrDefaultAsync();
            if (latest != null && latest.StartedUtc > now.AddSeconds(-8))
                return Results.BadRequest(new ApiError("Please wait before starting another fishing attempt."));
            var fish = catalog.All.Where(item => item.Category == "Fish" &&
                item.Id.StartsWith("fish_", StringComparison.Ordinal) &&
                item.Id is not ("fish_159" or "fish_160" or "fish_163" or
                    "fish_682" or "fish_775")).ToArray();
            if (fish.Length == 0) return Results.Problem("Fish catalog is unavailable.");
            var selected = fish[RandomNumberGenerator.GetInt32(fish.Length)];
            var attempt = new FishingAttempt { Id = Guid.NewGuid(), UserId = userId.Value,
                FishItemId = selected.Id, Size = RandomNumberGenerator.GetInt32(5, 51), StartedUtc = now };
            db.FishingAttempts.Add(attempt);
            await db.SaveChangesAsync();
            return Results.Ok(new FishingAttemptResponse(attempt.Id, attempt.FishItemId, attempt.Size));
        });

        app.MapPost("/api/game/fish/catch", async (HttpRequest request, CatchFishRequest input, GameDb db,
            IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return await Change(request, db, input.RequestId, doc =>
            {
                var attempt = db.FishingAttempts.SingleOrDefault(a =>
                    a.Id == input.AttemptId && a.UserId == userId.Value);
                if (attempt == null || attempt.ClaimedUtc != null ||
                    attempt.StartedUtc > DateTime.UtcNow.AddSeconds(-3) ||
                    attempt.StartedUtc < DateTime.UtcNow.AddMinutes(-10) ||
                    db.FishingAttempts.Where(a => a.UserId == userId.Value)
                        .Max(a => (DateTime?)a.StartedUtc) != attempt.StartedUtc ||
                    !catalog.TryGet(attempt.FishItemId, out var fish) || fish.Category != "Fish")
                    return "Fish catch is invalid or has expired.";
                if (!CanAdd(doc.Inventory, fish.Id, 1, fish.MaxStack, doc.BagSize))
                    return "Inventory is full.";
                Add(doc.Inventory, fish.Id, 1, fish.MaxStack, doc.BagSize);
                GainExperience(doc, 1, FishExperience.ForNormalCatch(fish.Id));
                GameProgression.RecordFish(doc.Progress, fish.Id);
                attempt.ClaimedUtc = DateTime.UtcNow;
                return null;
            });
        });
    }

    private static async Task<IResult> Change(HttpRequest request, GameDb db, Guid requestId,
                                               Func<StateDocument, string?> operation, bool includeFarmTileLimit = false)
    {
        var userId = await AuthTokens.ResolveUserId(request, db);
        if (userId == null) return Results.Unauthorized();
        if (requestId == Guid.Empty) return Results.BadRequest(new ApiError("requestId is required."));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var state = await Load(db, userId.Value);
        var doc = Decode(state);
        await HydrateLegacyProgress(db, doc);
        var catalog = request.HttpContext.RequestServices.GetRequiredService<IItemCatalog>();
        GameSnapshot CurrentSnapshot() => Snapshot(doc, state.Version, catalog,
            includeFarmTileLimit ? FarmTileLimit(db, doc.UserId, DateTime.UtcNow) : null);
        if (doc.RecentRequests.Contains(requestId) ||
            await db.ProcessedGameRequests.AnyAsync(r => r.UserId == userId && r.RequestId == requestId))
            return Results.Ok(CurrentSnapshot());
        var error = operation(doc);
        if (error != null) return Results.BadRequest(new ApiError(error));
        GameProgression.Evaluate(doc.Progress, doc.TotalEarnings);
        doc.RecentRequests.Add(requestId);
        if (doc.RecentRequests.Count > 100) doc.RecentRequests.RemoveAt(0);
        if (!await Persist(db, state, doc)) return Results.Conflict(new ApiError("State changed; reload and retry."));
        db.ProcessedGameRequests.Add(new ProcessedGameRequest { UserId = userId.Value, RequestId = requestId });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new ApiError("Request was already processed.")); }
        await transaction.CommitAsync();
        return Results.Ok(CurrentSnapshot());
    }

    /// <summary>
    /// Writes a decoded state back when its version is unchanged (optimistic concurrency) and
    /// advances <paramref name="state"/> to match. False when another request saved first.
    /// </summary>
    private static async Task<bool> Persist(GameDb db, PlayerState state, StateDocument doc)
    {
        var inventory = JsonSerializer.Serialize(doc.Inventory);
        var chests = JsonSerializer.Serialize(doc.Chests);
        var recent = JsonSerializer.Serialize(doc.RecentRequests);
        var shipping = JsonSerializer.Serialize(doc.Shipping);
        var skills = JsonSerializer.Serialize(doc.SkillExperience);
        var progress = JsonSerializer.Serialize(doc.Progress);
        var version = state.Version;
        int changed = await db.PlayerStates.Where(s => s.UserId == doc.UserId && s.Version == version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Money, doc.Money)
                .SetProperty(s => s.TotalEarnings, doc.TotalEarnings)
                .SetProperty(s => s.RentDebt, doc.RentDebt)
                .SetProperty(s => s.BagSize, doc.BagSize)
                .SetProperty(s => s.InventoryJson, inventory)
                .SetProperty(s => s.ChestsJson, chests)
                .SetProperty(s => s.RecentRequestsJson, recent)
                .SetProperty(s => s.ShippingJson, shipping)
                .SetProperty(s => s.SkillsJson, skills)
                .SetProperty(s => s.ProgressJson, progress)
                .SetProperty(s => s.GameDay, doc.GameDay)
                .SetProperty(s => s.DeepestMineFloor, doc.DeepestMineFloor)
                .SetProperty(s => s.Version, version + 1));
        if (changed != 1) return false;
        state.Money = doc.Money;
        state.TotalEarnings = doc.TotalEarnings;
        state.RentDebt = doc.RentDebt;
        state.BagSize = doc.BagSize;
        state.InventoryJson = inventory;
        state.ChestsJson = chests;
        state.ShippingJson = shipping;
        state.SkillsJson = skills;
        state.ProgressJson = progress;
        state.GameDay = doc.GameDay;
        state.DeepestMineFloor = doc.DeepestMineFloor;
        state.Version = version + 1;
        return true;
    }

    private static string? Transfer(StateDocument doc, TransferRequest request, IItemCatalog catalog)
    {
        if (!TryChestId(request.SourceChestId, out var sourceChestId) ||
            !TryChestId(request.TargetChestId, out var targetChestId)) return "Invalid chest ID.";
        if (request.Amount < 1 || request.SourceSlot < 0 || request.SourceSlot >= ChestSize ||
            request.TargetSlot < 0 || request.TargetSlot >= ChestSize ||
            (sourceChestId == null && request.SourceSlot >= doc.BagSize) ||
            (targetChestId == null && request.TargetSlot >= doc.BagSize) ||
            (sourceChestId == targetChestId && request.SourceSlot == request.TargetSlot))
            return "Invalid transfer.";
        var source = FindContainer(doc, sourceChestId);
        var target = FindContainer(doc, targetChestId);
        if (source == null || target == null) return "Chest not found.";
        var from = source.Find(s => s.Slot == request.SourceSlot);
        if (from == null || from.Amount < request.Amount || !catalog.TryGet(from.ItemId, out var definition))
            return "Item is not available.";
        var to = target.Find(s => s.Slot == request.TargetSlot);
        if (to != null && (to.ItemId != from.ItemId || to.Amount + request.Amount > definition.MaxStack))
            return "Target slot cannot hold that stack.";
        if (to == null && request.Amount > definition.MaxStack) return "Stack limit exceeded.";
        if (to == null) target.Add(new StackEntry { Slot = request.TargetSlot, ItemId = from.ItemId, Amount = request.Amount });
        else to.Amount += request.Amount;
        from.Amount -= request.Amount;
        if (from.Amount == 0) source.Remove(from);
        return null;
    }

    private static string? OrganizeInventory(StateDocument doc, IItemCatalog catalog)
    {
        var ordered = doc.Inventory.Select(stack => (Stack: stack,
                Item: catalog.TryGet(stack.ItemId, out var item) ? item : null))
            .OrderBy(entry => entry.Item?.IsTool == true ? 0 : 1)
            .ThenBy(entry => entry.Item?.IsTool == true ? "" : entry.Item?.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Item?.IsTool == true ? "" : entry.Item?.Name ?? entry.Stack.ItemId,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Stack.Slot);

        var organized = new List<StackEntry>();
        foreach (var (stack, item) in ordered)
        {
            // Keep old save entries even if their catalog definition is gone.
            if (item == null || item.IsTool)
            {
                if (organized.Count >= doc.BagSize) return "Inventory is full.";
                organized.Add(new StackEntry { Slot = organized.Count, ItemId = stack.ItemId, Amount = stack.Amount });
                continue;
            }
            if (item.MaxStack < 1 || !CanAdd(organized, stack.ItemId, stack.Amount, item.MaxStack, doc.BagSize))
                return "Inventory is full.";
            Add(organized, stack.ItemId, stack.Amount, item.MaxStack, doc.BagSize);
        }
        doc.Inventory = organized;
        return null;
    }

    private static string? Craft(StateDocument doc, string? recipeName, IItemCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return "Recipe is required.";
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "CraftingRecipes.json");
        if (!File.Exists(path)) return "Crafting recipes are unavailable.";
        var recipes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        if (recipes == null || !recipes.TryGetValue(recipeName, out var recipe))
            return "Unknown recipe.";
        var fields = recipe.Split('/');
        if (fields.Length < 5) return "Recipe definition is invalid.";
        bool learnedFromSkill = SkillRecipeRequirement.TryRead(fields[4], out int skill, out int level) &&
            GameProgression.LevelFromExperience(doc.SkillExperience[skill]) >= level;
        bool robinRecipe = RobinRecipes.Any(recipe => recipe.Name == recipeName);
        if (robinRecipe && !KnowsRobinRecipe(doc.Progress, recipeName)) return "Hãy mua bản vẽ ở cửa hàng Robin.";
        if (!robinRecipe && fields[4] != "default" && !learnedFromSkill &&
            (recipeName != "Furnace" || !doc.Progress.Milestones.Seen.Contains("furnace_blueprint")))
            return "Recipe is not unlocked.";
        var ingredientTokens = fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var outputTokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ingredientTokens.Length == 0 || ingredientTokens.Length % 2 != 0 || outputTokens.Length == 0)
            return "Recipe definition is invalid.";
        string outputId = fields[3] == "true" ? "big_craftable_" + outputTokens[0] : RecipeItemId(outputTokens[0]);
        int outputAmount = outputTokens.Length > 1 && int.TryParse(outputTokens[1], out int parsedAmount)
            ? parsedAmount : 1;
        if (outputAmount is < 1 or > 999 || !catalog.TryGet(outputId, out var output))
            return "Recipe output is unavailable.";
        var ingredients = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < ingredientTokens.Length; i += 2)
        {
            string token = ingredientTokens[i];
            if (!int.TryParse(ingredientTokens[i + 1], out int count) || count is < 1 or > 999 ||
                !RecipeIngredients.IsCategory(token) && !catalog.TryGet(RecipeItemId(token), out _))
                return "Recipe ingredient is invalid.";
            ingredients[token] = ingredients.GetValueOrDefault(token) + count;
        }
        var proposed = doc.Inventory.Select(s => new StackEntry
            { Slot = s.Slot, ItemId = s.ItemId, Amount = s.Amount }).ToList();
        if (!RecipeIngredients.TryConsume(proposed, ingredients, RecipeItemId))
            return "Not enough ingredients.";
        if (!CanAdd(proposed, outputId, outputAmount, output.MaxStack, doc.BagSize))
            return "Inventory is full.";
        Add(proposed, outputId, outputAmount, output.MaxStack, doc.BagSize);
                doc.Inventory = proposed;
        if (!string.Equals(recipeName, "Wedding Ring", StringComparison.Ordinal))
            doc.Progress.CraftedRecipes.Add(recipeName);
        return null;
    }

    private static string RecipeItemId(string id) => id switch
    {
        "388" => "wood", "390" => "stone", "771" => "fiber",
        "382" => "coal", "378" => "copper_ore", "380" => "iron_ore",
        "384" => "gold_ore", "24" => "parsnip", "188" => "green_bean",
        "190" => "cauliflower", "192" => "potato",
        _ when id.StartsWith("(BC)", StringComparison.Ordinal) => "big_craftable_" + id[4..],
        _ when id.StartsWith("(O)", StringComparison.Ordinal) => RecipeItemId(id[3..]),
        _ => "object_" + id
    };

    private static bool ValidChestTile(string? location, int x, int y) =>
        location == "FarmHouse" && x is >= 1 and <= 11 && y is >= 2 and <= 10;

    private static List<StackEntry>? FindContainer(StateDocument doc, Guid? chestId) => chestId == null
        ? doc.Inventory : doc.Chests.Find(c => c.Id == chestId)?.Slots;

    private static bool TryChestId(string? value, out Guid? id)
    {
        id = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!Guid.TryParse(value, out var parsed)) return false;
        id = parsed;
        return true;
    }

    private static bool CanAdd(List<StackEntry> stacks, string itemId, int amount, int max, int size)
    {
        long room = stacks.Where(s => s.ItemId == itemId).Sum(s => Math.Max(0, max - s.Amount));
        room += (long)(size - stacks.Count) * max;
        return room >= amount;
    }

    private static int ShopUnitPrice(string? shop, string itemId, int basePrice)
    {
        if (shop == "Pierre")
        {
            bool generalGood = itemId is "grass_starter" or "sugar" or "wheat_flour" or "rice" or
                "oil" or "vinegar" or "basic_fertilizer" or "basic_retaining_soil" or "speed_gro" or
                "cherry_sapling" or "apricot_sapling" or "orange_sapling" or "peach_sapling" or
                "pomegranate_sapling" or "apple_sapling" or "bouquet";
            return itemId.StartsWith("seeds_", StringComparison.Ordinal) || generalGood ? basePrice : 0;
        }
        if (shop == "JojaMart")
            return itemId.StartsWith("seeds_", StringComparison.Ordinal) ||
                itemId is "grass_starter" or "sugar" or "wheat_flour"
                ? basePrice * 5 / 4 : 0;
        return (shop, itemId) switch
        {
            ("Willy", "fishing_rod") => 500,
            ("Gus", "coffee") => 300,
            ("Gus", "spicy_eel") => 350,
            _ => 0
        };
    }

    private static Dictionary<string, int> MerchantStock(string shop, StateDocument doc, IItemCatalog catalog)
    {
        int day = doc.GameDay;
        bool laterYear = day > 112;
        if (shop == "Clint")
            return new() { ["copper_ore"] = laterYear ? 150 : 75, ["iron_ore"] = laterYear ? 250 : 150,
                ["coal"] = laterYear ? 250 : 150, ["gold_ore"] = laterYear ? 750 : 400 };
        if (shop == "Marnie")
        {
            var stock = new Dictionary<string, int> { ["object_178"] = 50 };
            if (!doc.Inventory.Any(stack => stack.ItemId == "tool_MilkPail")) stock["tool_MilkPail"] = 1000;
            if (!doc.Inventory.Any(stack => stack.ItemId == "tool_Shears")) stock["tool_Shears"] = 1000;
            return stock;
        }
        if (shop == "Marlon")
        {
            int combatLevel = GameProgression.LevelFromExperience(doc.SkillExperience[4]);
            return GuildStock.Value.Offers
                .Where(offer => doc.DeepestMineFloor >= offer.Floor && combatLevel >= offer.CombatLevel &&
                    catalog.TryGet(offer.ItemId, out _))
                .ToDictionary(offer => offer.ItemId, offer => offer.Price);
        }

        return CarpenterStock(doc, catalog);
    }

    private static string? CrackGeode(StateDocument doc, SellRequest input, IItemCatalog catalog)
    {
        if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Blacksmith", 3, 14, 2.5f))
            return "Stand at Clint's counter.";
        var geode = doc.Inventory.Find(stack => stack.Slot == input.Slot &&
            stack.ItemId == input.ItemId && stack.Amount > 0);
        if (geode == null || geode.ItemId is not ("object_535" or "object_536" or "object_537" or "object_749"))
            return "Select a geode.";
        const int fee = 25;
        if (doc.Money < fee) return "Not enough money.";

        var random = Random.Shared;
        // One branch for resources and one for minerals. The item data has IDs but no
        // geode treasure fields.
        (string itemId, int amount) = random.Next(4) switch
        {
            0 => ("stone", random.Next(3) * 2 + 1),
            1 => (geode.ItemId switch
            {
                "object_535" => random.Next(2) == 0 ? "copper_ore" : "coal",
                "object_536" => random.Next(2) == 0 ? "iron_ore" : "coal",
                _ => random.Next(2) == 0 ? "gold_ore" : "iron_ore"
            }, random.Next(3) * 2 + 1),
            _ => ("object_" + (geode.ItemId switch
            {
                "object_535" => random.Next(538, 551),
                "object_536" => random.Next(551, 565),
                "object_537" => random.Next(565, 579),
                _ => random.Next(538, 579)
            }), 1)
        };
        if (!catalog.TryGet(itemId, out var item)) return "Geode reward is unavailable.";
        if (--geode.Amount == 0) doc.Inventory.Remove(geode);
        if (!CanAdd(doc.Inventory, itemId, amount, item.MaxStack, doc.BagSize)) return "Inventory is full.";
        Add(doc.Inventory, itemId, amount, item.MaxStack, doc.BagSize);
        doc.Money -= fee;
        doc.GeodeReward = new(itemId, amount);
        return null;
    }

    private static readonly string[] TrashCanIds =
        { "tool_CopperTrashCan", "tool_SteelTrashCan", "tool_GoldTrashCan", "tool_IridiumTrashCan" };

    private static string? OrderToolUpgrade(StateDocument doc, SellRequest input, IItemCatalog catalog)
    {
        if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Blacksmith", 3, 14, 2.5f))
            return "Stand at Clint's counter.";
        if (doc.Progress.ToolUpgrade != null) return "Clint already has one of your tools.";
        if (input.ItemId == "trash_can")
        {
            int level = doc.Progress.TrashCanLevel;
            if (level >= 4) return "Thùng rác đã ở cấp cao nhất.";
            int cost = new[] { 1000, 2500, 5000, 12500 }[level];
            if (doc.Money < cost) return $"Bạn cần {cost}g.";
            if (!TakeItems(doc.Inventory, "object_" + (334 + level), 5)) return "Bạn cần đủ 5 thỏi cùng loại.";
            doc.Money -= cost;
            doc.Progress.ToolUpgrade = new(TrashCanIds[level], doc.GameDay + 2);
            return null;
        }
        var tool = doc.Inventory.Find(stack => stack.Slot == input.Slot && stack.ItemId == input.ItemId);
        if (tool == null) return "Select a tool.";
        string[] tiers = { "", "Copper", "Steel", "Gold", "Iridium" };
        string[] toolTypes = { "Axe", "Pickaxe", "Hoe", "WateringCan" };
        string[] basicIds = { "axe", "pickaxe", "hoe", "watering_can" };
        for (int i = 0; i < toolTypes.Length; i++)
        for (int tier = 0; tier < tiers.Length - 1; tier++)
        {
            string current = tier == 0 ? basicIds[i] : "tool_" + tiers[tier] + toolTypes[i];
            if (current != tool.ItemId) continue;
            string upgraded = "tool_" + tiers[tier + 1] + toolTypes[i];
            if (!catalog.TryGet(upgraded, out _)) return "Upgrade is unavailable.";
            int price = new[] { 2000, 5000, 10000, 25000 }[tier];
            if (doc.Money < price) return $"You need {price}g.";
            string barId = "object_" + (334 + tier);
            if (!TakeItems(doc.Inventory, barId, 5)) return $"You need 5 {tiers[tier + 1]} tier bars.";
            doc.Inventory.Remove(tool);
            doc.Money -= price;
            doc.Progress.ToolUpgrade = new(upgraded, doc.GameDay + 2);
            return null;
        }
        return "Select an axe, pickaxe, hoe or watering can below Iridium tier.";
    }

    private static bool TakeItems(List<StackEntry> inventory, string id, int amount)
    {
        var stacks = inventory.Where(stack => stack.ItemId == id).ToArray();
        if (stacks.Sum(stack => stack.Amount) < amount) return false;
        foreach (var stack in stacks)
        {
            int taken = Math.Min(amount, stack.Amount);
            stack.Amount -= taken;
            amount -= taken;
            if (stack.Amount == 0) inventory.Remove(stack);
            if (amount == 0) break;
        }
        return true;
    }

    private static void Add(List<StackEntry> stacks, string itemId, int amount, int max, int size)
    {
        foreach (var stack in stacks.Where(s => s.ItemId == itemId && s.Amount < max))
        {
            int put = Math.Min(amount, max - stack.Amount);
            stack.Amount += put;
            amount -= put;
            if (amount == 0) return;
        }
        for (int slot = 0; slot < size && amount > 0; slot++)
        {
            if (stacks.Any(s => s.Slot == slot)) continue;
            int put = Math.Min(amount, max);
            stacks.Add(new StackEntry { Slot = slot, ItemId = itemId, Amount = put });
            amount -= put;
        }
    }

    internal static async Task<PlayerState> Load(GameDb db, Guid userId)
    {
        var state = await db.PlayerStates.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId);
        if (state != null) return state;
        bool hasOpening = await db.Farmers.AnyAsync(farmer => farmer.UserId == userId && farmer.IntroMode != 0);
        state = new PlayerState
        {
            UserId = userId,
            InventoryJson = hasOpening ? NewFarmerInventory : JsonSerializer.Serialize(StarterInventory())
        };
        db.PlayerStates.Add(state);
        try { await db.SaveChangesAsync(); return state; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.PlayerStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
        }
    }

    internal static async Task InitializeNewFarmerInventory(GameDb db, Guid userId)
    {
        await Load(db, userId);
        // The state endpoint can have created a preview inventory before the account had a farmer.
        await db.PlayerStates.Where(state => state.UserId == userId &&
                (state.InventoryJson != NewFarmerInventory || state.BagSize != 12))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(state => state.InventoryJson, NewFarmerInventory)
                .SetProperty(state => state.BagSize, 12)
                .SetProperty(state => state.Version, state => state.Version + 1));
    }

    private static readonly string NewFarmerInventory = JsonSerializer.Serialize(StarterTools());

    private static List<StackEntry> StarterTools() => new()
    {
        new() { Slot = 0, ItemId = "axe", Amount = 1 },
        new() { Slot = 1, ItemId = "hoe", Amount = 1 },
        new() { Slot = 2, ItemId = "watering_can", Amount = 1 },
        new() { Slot = 3, ItemId = "pickaxe", Amount = 1 },
        new() { Slot = 4, ItemId = "scythe", Amount = 1 }
    };

    private static List<StackEntry> StarterInventory() =>
    [
        ..StarterTools(),
        new() { Slot = 6, ItemId = "seeds_parsnip", Amount = 20 },
        new() { Slot = 7, ItemId = "seeds_potato", Amount = 20 },
        new() { Slot = 8, ItemId = "seeds_green_bean", Amount = 20 },
        new() { Slot = 9, ItemId = "mixed_seeds", Amount = 12 },
        new() { Slot = 10, ItemId = "seeds_cauliflower", Amount = 20 },
        new() { Slot = 11, ItemId = "wood", Amount = 100 }
    ];

    private static StateDocument Decode(PlayerState state)
    {
        var inventory = JsonSerializer.Deserialize<List<StackEntry>>(state.InventoryJson) ?? new();
        // The old schema allowed all 36 slots. Keep any occupied legacy slots
        // visible when migrating an existing player's save.
        int occupiedTier = inventory.Any(s => s.Slot >= 24) ? 36 :
            inventory.Any(s => s.Slot >= 12) ? 24 : 12;
        return new StateDocument
        {
            UserId = state.UserId,
            Money = state.Money,
            TotalEarnings = state.TotalEarnings,
            RentDebt = state.RentDebt,
            BagSize = Math.Clamp(Math.Max(state.BagSize, occupiedTier), 12, MaxBagSize),
            Inventory = inventory,
            Chests = JsonSerializer.Deserialize<List<ChestEntry>>(state.ChestsJson) ?? new(),
            Shipping = JsonSerializer.Deserialize<List<ShippingEntry>>(state.ShippingJson) ?? new(),
            SkillExperience = NormalizeSkills(JsonSerializer.Deserialize<int[]>(state.SkillsJson)),
            Progress = SyncAdminWeather(state.UserId, PlayerProgress.FromJson(state.ProgressJson)),
            GameDay = Math.Max(1, state.GameDay),
            DeepestMineFloor = state.DeepestMineFloor,
            RecentRequests = JsonSerializer.Deserialize<List<Guid>>(state.RecentRequestsJson) ?? new()
        };
    }

    private static GameSnapshot Snapshot(StateDocument doc, int version, IItemCatalog catalog, int? farmTileLimit = null)
    {
        GameProgression.Evaluate(doc.Progress, doc.TotalEarnings);
        return new(doc.Money, doc.BagSize, version, doc.Inventory, doc.Chests,
            doc.Shipping, doc.GameDay, doc.SkillExperience, doc.TotalEarnings, doc.RentDebt,
            doc.SkillExperience.Select(GameProgression.LevelFromExperience).ToArray(),
            doc.Progress.Achievements.OrderBy(id => id).ToArray(),
            CollectionCounts(doc.Progress.BasicShipped), CollectionCounts(doc.Progress.FishCaught),
            doc.Progress.HouseUpgradeDay, doc.Progress.ToolUpgrade,
            doc.Progress.ClaimedMineChests.OrderBy(floor => floor).ToArray(),
            DailyEventSnapshot(doc), BuildMilestoneStatus(doc), doc.GeodeReward,
            doc.Progress.NaturalResources.Values.ToArray(), doc.DeepestMineFloor,
            new FarmWeatherForecast(WeatherForDay(doc.UserId, doc.GameDay),
                WeatherForDay(doc.UserId, doc.GameDay == int.MaxValue ? doc.GameDay : doc.GameDay + 1)))
        {
            FarmTileLimit = farmTileLimit,
            LearnedShopRecipes = RobinRecipes.Where(recipe => KnowsRobinRecipe(doc.Progress, recipe.Name)).Select(recipe => recipe.Name).ToArray(),
            RanchHomes = RanchHomeStatuses(doc),
            RanchAnimals = doc.Progress.RanchAnimals.ToArray(),
            RanchProducts = doc.Progress.RanchProducts.ToArray(),
            PiecesOfHay = doc.Progress.PiecesOfHay,
            PlacedObjects = doc.Progress.PlacedObjects.ToArray(),
            TrashCanLevel = doc.Progress.TrashCanLevel,
            Professions = doc.Progress.Professions.OrderBy(id => id).ToArray(),
            ForagePicked = doc.Progress.ForagePicked.Where(key => key.StartsWith(doc.GameDay + "/", StringComparison.Ordinal)).ToArray(),
            RentCharge = doc.Progress.LastRentCharge?.Day == doc.GameDay ? doc.Progress.LastRentCharge : null,
            PassOutCharge = doc.Progress.LastPassOutCharge?.Day == doc.GameDay ? doc.Progress.LastPassOutCharge : null,
            Bundles = BundleStatusFor(doc),
            HayCapacity = HayCapacity(doc),
            GuildResale = doc.Inventory.Select(stack => stack.ItemId).Distinct(StringComparer.Ordinal)
                .Where(IsGuildGear).Select(id => new ShopOffer(id, catalog.TryGet(id, out var item) ? GuildResalePrice(item) : 0))
                .Where(offer => offer.Price > 0).ToArray(),
            ShippableItems = doc.Inventory.Select(stack => stack.ItemId).Distinct(StringComparer.Ordinal)
                .Where(id => catalog.TryGet(id, out var item) && item.CanShip).ToArray()
        };
    }

    private static int WeatherForDay(Guid userId, int day) =>
        AdminWeather.TryGetValue((userId, day), out int weather) ? weather :
        DailyEvents.FarmWeather.ForDay(day, userId, EventCalendar.Value);

    private static CollectionCount[] CollectionCounts(Dictionary<string, int> counts) =>
        counts.Select(pair => new CollectionCount(pair.Key, pair.Value)).ToArray();

    private static int[] NormalizeSkills(int[]? values)
    {
        var result = new int[5];
        if (values != null)
            for (int i = 0; i < Math.Min(result.Length, values.Length); i++)
                result[i] = Math.Max(0, values[i]);
        return result;
    }

    private static async Task HydrateLegacyProgress(GameDb db, StateDocument doc)
    {
        await HydrateCopperHistory(db, doc);
        if (doc.Progress.LegacyHydrated) return;
        var friendships = await db.NpcFriendships.AsNoTracking()
            .Where(f => f.UserId == doc.UserId)
            .Select(f => new { f.NpcName, f.Points }).ToListAsync();
        foreach (var friend in friendships)
            doc.Progress.FriendshipPoints[friend.NpcName] = Math.Max(
                doc.Progress.FriendshipPoints.GetValueOrDefault(friend.NpcName), friend.Points);
        var fish = await db.FishingAttempts.AsNoTracking()
            .Where(f => f.UserId == doc.UserId && f.ClaimedUtc != null)
            .GroupBy(f => f.FishItemId)
            .Select(group => new { ItemId = group.Key, Count = group.Count() }).ToListAsync();
        foreach (var entry in fish)
        {
            string id = entry.ItemId.StartsWith("fish_", StringComparison.Ordinal)
                ? entry.ItemId[5..] : entry.ItemId;
            doc.Progress.FishCaught[id] = Math.Max(doc.Progress.FishCaught.GetValueOrDefault(id), entry.Count);
        }
        doc.Progress.LegacyHydrated = true;
    }

    private static void GainExperience(StateDocument doc, int skill, int amount)
    {
        GameProgression.AddExperience(doc.SkillExperience, skill, amount);
    }

    private sealed class StateDocument
    {
        public Guid UserId;
        public long Money;
        public long TotalEarnings;
        public long RentDebt;
        public int BagSize = 12;
        public List<StackEntry> Inventory = new();
        public List<ChestEntry> Chests = new();
        public List<ShippingEntry> Shipping = new();
        public int GameDay = 1;
        public int DeepestMineFloor;
        public int[] SkillExperience = new int[5];
        public PlayerProgress Progress = new();
        public GeodeReward? GeodeReward;
        public List<Guid> RecentRequests = new();
    }
}

public sealed class StackEntry
{
    public int Slot { get; set; }
    public string ItemId { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class ChestEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public List<StackEntry> Slots { get; set; } = new();
}

public record ShippingEntry(string ItemId, int Amount, int UnitPrice);
public record GeodeReward(string ItemId, int Amount);
/// <summary>Rent settled automatically on the morning of <paramref name="Day"/>.</summary>
public record RentCharge(int Day, long Billed, long Paid, long StillOwed);
public record FarmWeatherForecast(int Today, int Tomorrow);
public record GameSnapshot(long Money, int BagSize, int Version, List<StackEntry> Inventory,
    List<ChestEntry> Chests, List<ShippingEntry> Shipping, int GameDay, int[] SkillExperience,
    long TotalEarnings, long RentDebt, int[] SkillLevels, int[] Achievements,
    CollectionCount[] ItemsShipped, CollectionCount[] FishCaught, int HouseUpgradeDay, ToolUpgradeOrder? ToolUpgrade,
    int[] ClaimedMineChests, DailyEventStatus DailyEvents, MilestoneStatus Milestones, GeodeReward? GeodeReward = null,
    NaturalResourceState[]? NaturalResources = null, int DeepestMineFloor = 0, FarmWeatherForecast? Weather = null)
{
    public string[] ShippableItems { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PlayerSession? Session { get; init; }
    public RanchHomeStatus[] RanchHomes { get; init; } = [];
    public RanchAnimal[] RanchAnimals { get; init; } = [];
    public RanchProduct[] RanchProducts { get; init; } = [];
    public int PiecesOfHay { get; init; }
    public PlacedObject[] PlacedObjects { get; init; } = [];
    public int TrashCanLevel { get; init; }
    public int[] Professions { get; init; } = [];
    public string[] ForagePicked { get; init; } = [];
    public RentCharge? RentCharge { get; init; }
    public PassOutCharge? PassOutCharge { get; init; }
    public CommunityBundleStatus? Bundles { get; init; }
    public int HayCapacity { get; init; }
    public ShopOffer[] GuildResale { get; init; } = [];
    public string[] LearnedShopRecipes { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? FarmTileLimit { get; init; }
}
public record MineTreasureRequest(Guid RequestId, int Floor);
public record CollectionCount(string ItemId, int Amount);
public record CreateChestRequest(Guid RequestId, string? Name, string? Location, int TileX, int TileY);
public record PlaceChestRequest(Guid RequestId, string? Location, int TileX, int TileY);
public record TransferRequest(Guid RequestId, string? SourceChestId, int SourceSlot, string? TargetChestId,
    int TargetSlot, int Amount);
public record SwapSlotsRequest(Guid RequestId, int FromSlot, int ToSlot);
public record DiscardItemRequest(Guid RequestId, int Slot, string? ItemId);
public record CraftItemRequest(Guid RequestId, string? RecipeName);
public record CollectResourceRequest(Guid RequestId, string? Location, int TileX, int TileY,
    string? ItemId, int Amount, int Visit = 0);
public record PlantFarmCropRequest(Guid RequestId, int TileX, int TileY, int SeedSlot, string? SeedItemId);
public record HarvestFarmCropRequest(Guid RequestId, int TileX, int TileY);
public record FarmCropDto(int TileX, int TileY, string SeedItemId, string HarvestItemId,
    DateTime ReadyUtc, int RegrowDays, int ReadyGameDay, bool HasHarvested,
    int DaysToGrow, int GrowthDays, int RegrowDaysLeft, bool IsWatered);
public record FarmCropListResponse(FarmCropDto[] Crops, FarmSoilDto[] Soil);
public record WaterFarmCropRequest(Guid RequestId, int TileX, int TileY);
public record NpcGiftRequest(Guid RequestId, string? NpcName, int Slot, string? ItemId);
public record NpcFriendshipDto(string NpcName, int Points, int LastGiftDay, int GiftsThisWeek);
public record NpcFriendshipListResponse(NpcFriendshipDto[] Friends);
public record BuyRequest(Guid RequestId, string? ItemId, int Amount, string? Shop = null);
public record ShopOffer(string ItemId, int Price);
public record ShopOfferList(ShopOffer[] Offers);
public record BackpackRequest(Guid RequestId);
public record SellRequest(Guid RequestId, int Slot, string? ItemId, int Amount, string? Shop = null);
/// <param name="PassedOutAt">Where the farmer passed out at 2 AM, or null when they went to bed.</param>
public record AdvanceDayRequest(Guid RequestId, int Day, string? PassedOutAt = null);

public sealed record PlayerSession(int Day, string Location, float X, float Y, float FacingX, float FacingY,
    int Time, int Stamina, int Health)
{
    internal string? Validate(int gameDay)
    {
        if (Day != gameDay) return "Game day is out of sync.";
        if (string.IsNullOrWhiteSpace(Location) || Location.Length > 64 ||
            !Location.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
            return "Invalid location.";
        if (!float.IsFinite(X) || !float.IsFinite(Y) || MathF.Abs(X) > 10_000 || MathF.Abs(Y) > 10_000 ||
            !float.IsFinite(FacingX) || !float.IsFinite(FacingY))
            return "Invalid position.";
        if (Time is < 600 or >= 2600 || Time % 100 >= 60) return "Invalid time of day.";
        if (Stamina is < 0 or > 1000 || Health is < 0 or > 1000) return "Invalid energy.";
        return null;
    }

    // A session only resumes on the day it was saved; after sleeping the day starts in bed.
    internal static PlayerSession? ForDay(string? json, int gameDay)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var session = JsonSerializer.Deserialize<PlayerSession>(json);
            return session != null && session.Validate(gameDay) == null ? session : null;
        }
        catch (JsonException) { return null; }
    }
}
public record ConsumeFoodRequest(Guid RequestId, int Slot, string? ItemId);
public record FishingAttemptResponse(Guid AttemptId, string FishItemId, int Size);
public record CatchFishRequest(Guid RequestId, Guid AttemptId);
