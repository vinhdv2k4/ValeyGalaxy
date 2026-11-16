using System.Globalization;

public static partial class GameStateEndpoints
{
    private static void MapMineProgress(WebApplication app)
    {
        app.MapPost("/api/game/mine/arrive", async (HttpRequest request,
            MineArrivalRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Floor is < 1 or > 120 || doc.GameDay < EventCalendar.Value.Find("mine_open")!.day)
                    return "Mỏ chưa mở hoặc tầng không hợp lệ.";
                if (input.Floor <= doc.DeepestMineFloor) return null;
                if (input.Floor != doc.DeepestMineFloor + 1)
                    return "Hãy xuống mỏ lần lượt bằng thang.";
                string floor = input.Floor.ToString(CultureInfo.InvariantCulture);
                string source = input.Floor == 1 ? "Mine" :
                    (input.Floor - 1).ToString(CultureInfo.InvariantCulture);
                if (!WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, floor) &&
                    !WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, source))
                    return "Vị trí người chơi chưa đồng bộ.";
                doc.DeepestMineFloor = input.Floor;
                return null;
            }));

        // When knocked out in the mines, the farmer wakes at the entrance having
        // lost some gold (a fifth to a quarter, at most 5000g, rounded down to 100g) and, with a 25%
        // chance each, items from the backpack. Tools, the scythe, the Galaxy Sword and rings are kept.
        app.MapPost("/api/game/mine/knockout", async (HttpRequest request,
            MineKnockoutRequest input, GameDb db, IItemCatalog catalog) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (input.Floor is < 1 or > 120 && input.Floor != 77377) return "Tầng mỏ không hợp lệ.";
                if (!WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, input.Floor.ToString(CultureInfo.InvariantCulture)))
                    return "Vị trí người chơi chưa đồng bộ.";
                var random = new Random(HashCode.Combine(doc.UserId, doc.GameDay, input.RequestId));
                long low = doc.Money / 20, high = doc.Money / 4;
                long lost = Math.Min(high > low ? low + (long)(random.NextDouble() * (high - low)) : low, 5000);
                lost -= lost % 100;
                doc.Money = Math.Max(0, doc.Money - lost);
                doc.Inventory.RemoveAll(stack => CanLoseInMine(stack, catalog) && random.NextDouble() < 0.25);
                doc.Progress.LastMineKnockout = new MineKnockout(doc.GameDay, lost);
                return null;
            }));
    }

    private static bool CanLoseInMine(StackEntry stack, IItemCatalog catalog)
    {
        if (string.IsNullOrEmpty(stack.ItemId) || stack.Amount <= 0) return false;
        if (!catalog.TryGet(stack.ItemId, out var item)) return false;
        if (item.IsTool || item.Category == "Tool") return false;
        // Scythe and Galaxy Sword are never lost; rings are kept too.
        if (stack.ItemId is "weapon_47" or "weapon_4") return false;
        return !RingObjects.Contains(stack.ItemId);
    }

    // Object ids of the rings in Data/ObjectInformation (type "Ring").
    private static readonly HashSet<string> RingObjects = new(
        new[] { 516, 517, 518, 519, 520, 521, 522, 523, 524, 525, 526, 527, 528, 529, 530, 531, 532, 533, 534,
                801, 810, 811, 839, 859, 860, 861, 862, 863, 867, 868, 887, 888 }
            .Select(id => "object_" + id), StringComparer.Ordinal);
}

public sealed record MineArrivalRequest(Guid RequestId, int Floor);
public sealed record MineKnockoutRequest(Guid RequestId, int Floor);
/// <summary>Gold taken when knocked out in the mines on <paramref name="Day"/>.</summary>
public sealed record MineKnockout(int Day, long GoldLost);
/// <summary>Gold taken when passing out away from home at 2 AM, charged on the morning of <paramref name="Day"/>.</summary>
public sealed record PassOutCharge(int Day, long GoldLost, string Location);
