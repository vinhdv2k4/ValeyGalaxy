using Microsoft.EntityFrameworkCore;

public static partial class GameStateEndpoints
{
    internal const int BaseFarmTiles = 15;
    internal const int TilesPerParcel = 15;
    private const string RentDue = "Hãy thanh toán tiền thuê trại trong hộp thư để tiếp tục cuốc và gieo đất.";

    private static bool ValidFarmTile(int x, int y) => x is >= 0 and <= 512 && y is >= 0 and <= 512;
    private static int FarmTileKey(int x, int y) => y * 513 + x;

    /// <summary>Speed-Gro shortens growth by 10/25/33%, rounded up as in the original.</summary>
    private static int SpeedUp(int days, string? fertilizer)
    {
        if (fertilizer == null || !Artisan.Value.Fertilizers.Speed.TryGetValue(fertilizer, out double speed)) return days;
        return Math.Max(1, days - (int)Math.Ceiling(days * speed));
    }

    /// <summary>Harvest quality from Farming level and fertilizer, using the original chances.</summary>
    private static int CropQuality(StateDocument doc, string? fertilizer, Random random)
    {
        int farming = GameProgression.LevelFromExperience(doc.SkillExperience[0]);
        int fert = fertilizer != null && Artisan.Value.Fertilizers.Quality.TryGetValue(fertilizer, out int level) ? level : 0;
        double gold = 0.2 * (farming / 10.0) + 0.2 * fert * ((farming + 2.0) / 12.0) + 0.01;
        double silver = Math.Min(0.75, gold * 2.0);
        if (fert >= 3 && random.NextDouble() < gold / 2.0) return 4;
        if (random.NextDouble() < gold) return 2;
        if (random.NextDouble() < silver || fert >= 3) return 1;
        return 0;
    }

    private static int FarmTilesUsed(GameDb db, Guid owner, PlayerProgress progress)
    {
        var keys = progress.FarmSoil.Keys.ToHashSet();
        foreach (var crop in db.FarmCropStates.AsNoTracking().Where(c => c.UserId == owner)
            .Select(c => new { c.TileX, c.TileY }))
            keys.Add(FarmTileKey(crop.TileX, crop.TileY));
        return keys.Count;
    }

    internal static int FarmTileLimit(GameDb db, Guid owner, DateTime now) =>
        BaseFarmTiles + TilesPerParcel * db.TvLandHoldings
            .Where(h => h.OwnerId == owner && (h.IsPermanent || h.ExpiresAtUtc > now))
            .Select(h => h.ParcelId).Distinct().Count();

    internal static int FarmTilesUsed(GameDb db, PlayerState state) =>
        FarmTilesUsed(db, state.UserId, PlayerProgress.FromJson(state.ProgressJson));

    private static string? FarmExpansionError(GameDb db, StateDocument doc, bool addingTile)
    {
        if (doc.RentDebt > 0) return RentDue;
        int limit = FarmTileLimit(db, doc.UserId, DateTime.UtcNow);
        int used = FarmTilesUsed(db, doc.UserId, doc.Progress);
        return used > limit || addingTile && used == limit
            ? $"Đã dùng {used}/{limit} ô. Dọn ô trống bằng cuốc chim hoặc thuê thêm đất trên TV."
            : null;
    }

    private static void MapFarmLand(WebApplication app)
    {
        // Fertilizer, retaining soil and speed-gro go on tilled soil; speed-gro also on a fresh seed.
        app.MapPost("/api/game/farm/fertilize", async (HttpRequest request, FertilizeRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                string item = input.ItemId ?? "";
                var table = Artisan.Value.Fertilizers;
                bool speed = table.Speed.ContainsKey(item);
                if (!table.Quality.ContainsKey(item) && !table.Retain.ContainsKey(item) && !speed)
                    return "Đây không phải phân bón.";
                if (!ValidFarmTile(input.TileX, input.TileY) ||
                    !WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", input.TileX, input.TileY, 2.5f))
                    return "Hãy đến gần ô đất.";
                int key = FarmTileKey(input.TileX, input.TileY);
                if (!doc.Progress.FarmSoil.TryGetValue(key, out var soil)) return "Hãy cuốc đất trước khi bón phân.";
                if (soil.Fertilizer != null) return "Ô đất này đã được bón.";
                var crop = db.FarmCropStates.SingleOrDefault(c => c.UserId == doc.UserId &&
                    c.TileX == input.TileX && c.TileY == input.TileY);
                if (crop != null && (!speed || crop.GrowthDays > 0 || crop.HasHarvested))
                    return "Phải bón trước khi gieo hạt.";
                if (!TakeItems(doc.Inventory, item, 1)) return "Bạn không còn phân bón này.";
                doc.Progress.FarmSoil[key] = soil with { Fertilizer = item };
                if (crop != null && crop.DaysToGrow > 0)
                {
                    crop.DaysToGrow = SpeedUp(crop.DaysToGrow, item);
                    crop.ReadyGameDay = doc.GameDay + crop.DaysToGrow;
                }
                return null;
            }));

        app.MapPost("/api/game/farm/soil", async (HttpRequest request, FarmSoilRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (!ValidFarmTile(input.TileX, input.TileY) ||
                    !WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", input.TileX, input.TileY, 2.5f))
                    return "Hãy đến gần ô đất trên nông trại.";

                int key = FarmTileKey(input.TileX, input.TileY);
                bool hasCrop = db.FarmCropStates.Any(c => c.UserId == doc.UserId &&
                    c.TileX == input.TileX && c.TileY == input.TileY);
                if (input.Clear)
                {
                    if (hasCrop) return "Hãy thu hoạch cây trước khi dọn ô đất.";
                    doc.Progress.FarmSoil.Remove(key);
                    return null;
                }

                if (hasCrop || doc.Progress.FarmSoil.ContainsKey(key)) return null;
                if (InBuiltRanchPlot(doc, input.TileX, input.TileY)) return "Ô này thuộc khu chuồng trại.";
                var error = FarmExpansionError(db, doc, addingTile: true);
                if (error != null) return error;
                doc.Progress.FarmSoil.Add(key, new(input.TileX, input.TileY));
                return null;
            }));

        // Charged Hoe: Copper 3 tiles in a line, Steel 5, Gold 3x3, Iridium 6x3.
        app.MapPost("/api/game/farm/soil-area", async (HttpRequest request, FarmAreaRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                string? invalid = ValidateArea(doc, input, ToolTier(doc, "hoe", "Hoe"));
                if (invalid != null) return invalid;
                int tilled = 0;
                string? limit = null;
                foreach (var tile in input.Tiles!)
                {
                    int key = FarmTileKey(tile.TileX, tile.TileY);
                    if (doc.Progress.FarmSoil.ContainsKey(key) || InBuiltRanchPlot(doc, tile.TileX, tile.TileY) ||
                        db.FarmCropStates.Any(c => c.UserId == doc.UserId && c.TileX == tile.TileX && c.TileY == tile.TileY))
                        continue;
                    limit = FarmExpansionError(db, doc, addingTile: true);
                    if (limit != null) break;
                    doc.Progress.FarmSoil.Add(key, new(tile.TileX, tile.TileY));
                    tilled++;
                }
                return tilled == 0 && limit != null ? limit : null;
            }));

        // A charged Watering Can covers the same tiles; untilled tiles just get wet.
        app.MapPost("/api/game/farm/water-area", async (HttpRequest request, FarmAreaRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                string? invalid = ValidateArea(doc, input, ToolTier(doc, "watering_can", "WateringCan"));
                if (invalid != null) return invalid;
                if (DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay)))
                    return "Mưa đã tưới đất hôm nay.";
                foreach (var tile in input.Tiles!)
                {
                    var crop = db.FarmCropStates.SingleOrDefault(c => c.UserId == doc.UserId &&
                        c.TileX == tile.TileX && c.TileY == tile.TileY);
                    if (crop != null) { crop.IsWatered = true; continue; }
                    int key = FarmTileKey(tile.TileX, tile.TileY);
                    if (doc.Progress.FarmSoil.TryGetValue(key, out var soil) && soil.WateredDay != doc.GameDay)
                        doc.Progress.FarmSoil[key] = soil with { WateredDay = doc.GameDay };
                }
                return null;
            }));

        app.MapPost("/api/game/farm/pay-rent", async (HttpRequest request, FarmRentPaymentRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", 68, 16, 2.5f) &&
                    !WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", 85, 20, 2.5f))
                    return "Hãy đến hộp thư để thanh toán tiền thuê trại.";
                if (doc.RentDebt <= 0) return "Không có hóa đơn tiền thuê chưa thanh toán.";
                if (doc.Money <= 0) return "Không đủ vàng để thanh toán.";
                long paid = Math.Min(doc.Money, doc.RentDebt);
                doc.Money -= paid;
                doc.RentDebt -= paid;
                return null;
            }));
    }
}

public static partial class GameStateEndpoints
{
    private static readonly string[] ToolTiers = { "", "Copper", "Steel", "Gold", "Iridium" };

    /// <summary>Best upgrade level of a tool the player is carrying, or -1 without one.</summary>
    private static int ToolTier(StateDocument doc, string basicId, string toolType)
    {
        int best = -1;
        foreach (var stack in doc.Inventory)
        {
            if (stack.ItemId == basicId) best = Math.Max(best, 0);
            for (int tier = 1; tier < ToolTiers.Length; tier++)
                if (stack.ItemId == "tool_" + ToolTiers[tier] + toolType) best = Math.Max(best, tier);
        }
        return best;
    }

    // Tile count and farthest forward offset for each charge level.
    private static readonly int[] AreaTileCount = { 1, 3, 5, 9, 18 };
    private static readonly int[] AreaForwardReach = { 0, 2, 4, 2, 5 };

    private static string? ValidateArea(StateDocument doc, FarmAreaRequest input, int toolTier)
    {
        if (toolTier < 0) return "Bạn không mang dụng cụ phù hợp.";
        if (input.Power < 0 || input.Power > toolTier) return "Dụng cụ chưa được nâng cấp đủ để gồng lực này.";
        if (input.Tiles == null || input.Tiles.Length == 0 || input.Tiles.Length > AreaTileCount[input.Power])
            return "Vùng tác động không hợp lệ.";
        if (input.Tiles.Select(t => (t.TileX, t.TileY)).Distinct().Count() != input.Tiles.Length)
            return "Vùng tác động không hợp lệ.";
        float reach = 2.5f + AreaForwardReach[input.Power];
        foreach (var tile in input.Tiles)
            if (!ValidFarmTile(tile.TileX, tile.TileY) ||
                !WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", tile.TileX, tile.TileY, reach))
                return "Hãy đến gần ô đất trên nông trại.";
        return null;
    }
}

public sealed record FarmTileRef(int TileX, int TileY);
public sealed record FarmAreaRequest(Guid RequestId, int Power, FarmTileRef[]? Tiles);
public sealed record FarmSoilTile(int TileX, int TileY, int WateredDay = 0, string? Fertilizer = null);
public sealed record FarmSoilDto(int TileX, int TileY, bool IsWatered, string? Fertilizer = null);
public sealed record FertilizeRequest(Guid RequestId, int TileX, int TileY, string? ItemId);
public sealed record FarmSoilRequest(Guid RequestId, int TileX, int TileY, bool Clear);
public sealed record FarmRentPaymentRequest(Guid RequestId);
