using System.Text.Json;

/// <summary>
/// An object placed in the world: machines, producers, sprinklers, torches, fences, gates and
/// floors. Machines and producers hold their output until it is collected.
/// </summary>
public sealed record PlacedObject(Guid Id, string ItemId, string Location, int X, int Y,
    string? Output = null, long ReadyAt = 0, int OutputCount = 1, string? Variant = null, bool Open = false);

public sealed record PlaceObjectRequest(Guid RequestId, string? ItemId, string? Location, int X, int Y,
    string? Variant = null, int Minute = 600);
public sealed record ObjectActionRequest(Guid RequestId, string? ObjectId, string? ItemId, int Minute,
    string? Location = null, int X = 0, int Y = 0);

public static partial class GameStateEndpoints
{
    internal sealed record MachineRule(string Input, int Count, string Output, int Minutes, string? Preserve, int Quality,
        int OutputMin = 1, int OutputMax = 1);
    internal sealed record MachineFuel(string Id, int Count);
    internal sealed record MachineDefinition(string Id, MachineRule[] Rules, MachineFuel? Fuel);
    internal sealed record PreserveDefinition(string Name, double Multiplier, int Add);
    internal sealed record TreeProduct(string Output, int Minutes);
    internal sealed record ProducerDefinition(string Id, bool OnTree, Dictionary<string, TreeProduct>? Trees,
        string? Output, int Minutes, bool FarmOnly, bool NoWinter, int OutputMin = 1, int OutputMax = 1);
    internal sealed record SprinklerDefinition(string Id, string Range);
    internal sealed record FenceDefinition(string Id, string Texture, bool Gate);
    internal sealed record FloorDefinition(string Id, int Floor);
    internal sealed record FertilizerTable(Dictionary<string, int> Quality, Dictionary<string, double> Retain,
        Dictionary<string, double> Speed);
    internal sealed record ArtisanCatalog(MachineDefinition[] Machines, Dictionary<string, PreserveDefinition> Preserves,
        string[] Fruits, string[] Vegetables, ProducerDefinition[] Producers, SprinklerDefinition[] Sprinklers,
        FenceDefinition[] Fences, FloorDefinition[] Floors, FertilizerTable Fertilizers, Dictionary<string, int> Bombs,
        JsonElement[] Lights);

    internal static readonly Lazy<ArtisanCatalog> Artisan = new(() =>
        JsonSerializer.Deserialize<ArtisanCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "ContentData", "Artisan.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Artisan data is missing."));

    private const int MaxPlacedObjects = 600;
    private const string StaircaseId = "big_craftable_71";
    private static readonly HashSet<string> LightObjects = new(StringComparer.Ordinal) { "object_93" };

    private static long GameMinute(StateDocument doc, int minute) => (long)doc.GameDay * 1440 + minute;

    private static bool ValidMinute(int minute) => minute is >= 360 and <= 1600;

    private static bool PlaceableLocation(StateDocument doc, string location) =>
        location is "Farm" or "FarmHouse" ||
        doc.Progress.RanchHomes.Any(home => home.Kind != "Silo" && RanchRoomName(home, doc.GameDay) == location);

    private static bool IsFloor(string itemId) => Artisan.Value.Floors.Any(floor => floor.Id == itemId);

    /// <summary>Items that can be put down: numeric big craftables and a few objects.</summary>
    private static bool IsPlaceable(string itemId)
    {
        if (itemId is "big_craftable_130" or StaircaseId) return false;
        if (itemId.StartsWith("big_craftable_", StringComparison.Ordinal))
            return int.TryParse(itemId.AsSpan("big_craftable_".Length), out _);
        var data = Artisan.Value;
        return IsFloor(itemId) || LightObjects.Contains(itemId) ||
               data.Sprinklers.Any(item => item.Id == itemId) || data.Fences.Any(item => item.Id == itemId);
    }

    private static void MapPlacedObjects(WebApplication app)
    {
        app.MapPost("/api/game/objects/place", async (HttpRequest request, PlaceObjectRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => PlaceObject(doc, input, db, catalog)));
        app.MapPost("/api/game/objects/pickup", async (HttpRequest request, ObjectActionRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => PickUpObject(doc, input, catalog)));
        app.MapPost("/api/game/objects/load", async (HttpRequest request, ObjectActionRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => LoadMachine(doc, input)));
        app.MapPost("/api/game/objects/collect", async (HttpRequest request, ObjectActionRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => CollectMachine(doc, input, catalog)));
        app.MapPost("/api/game/objects/toggle", async (HttpRequest request, ObjectActionRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => ToggleGate(doc, input)));
        app.MapPost("/api/game/objects/bomb", async (HttpRequest request, ObjectActionRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => ThrowBomb(doc, input)));
        app.MapPost("/api/game/objects/staircase", async (HttpRequest request, ObjectActionRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => UseStaircase(doc, input)));
    }

    private static PlacedObject? FindPlaced(StateDocument doc, string? id) =>
        Guid.TryParse(id, out Guid guid) ? doc.Progress.PlacedObjects.FirstOrDefault(item => item.Id == guid) : null;

    // Presence is checked against the tile centre (farm API rows sit one above the map row).
    private static bool NearObject(StateDocument doc, string location, int x, int y) =>
        WorldSessionEndpoints.IsPlayerNear(doc.UserId, location, x, y - 1, 3f);

    private static ProducerDefinition? ProducerOf(string itemId) =>
        Artisan.Value.Producers.FirstOrDefault(item => item.Id == itemId);

    /// <summary>Starts (or restarts) a producer's next batch.</summary>
    private static PlacedObject StartProducing(StateDocument doc, PlacedObject placed, ProducerDefinition producer,
        int minute, Random random)
    {
        string? output = producer.Output;
        int minutes = producer.Minutes;
        if (producer.OnTree && producer.Trees != null && placed.Variant != null &&
            producer.Trees.TryGetValue(placed.Variant, out var product))
            (output, minutes) = (product.Output, product.Minutes);
        int count = random.Next(Math.Max(1, producer.OutputMin), Math.Max(producer.OutputMin, producer.OutputMax) + 1);
        long readyAt = GameMinute(doc, minute) + minutes;
        // Bees rest through winter: a batch that would finish in winter finishes in early spring instead.
        if (producer.NoWinter && Season((int)(readyAt / 1440)) == 3)
        {
            int day = (int)(readyAt / 1440);
            int springStart = (day - 1) / 28 * 28 + 28 + 1;
            readyAt = (long)springStart * 1440 + 360 + minutes;
        }
        return placed with { Output = output, ReadyAt = readyAt, OutputCount = count };
    }

    private static string? PlaceObject(StateDocument doc, PlaceObjectRequest input, GameDb db, IItemCatalog catalog)
    {
        string itemId = input.ItemId ?? "";
        string location = input.Location ?? "";
        if (!IsPlaceable(itemId) || !catalog.TryGet(itemId, out _)) return "Vật phẩm này không đặt xuống được.";
        if (!PlaceableLocation(doc, location)) return "Không thể đặt đồ ở đây.";
        if (input.X is < 0 or > 200 || input.Y is < 0 or > 200) return "Ô không hợp lệ.";
        if (!NearObject(doc, location, input.X, input.Y)) return "Hãy lại gần chỗ muốn đặt.";
        if (doc.Progress.PlacedObjects.Count >= MaxPlacedObjects) return "Bạn đã đặt quá nhiều đồ.";
        int x = input.X, y = input.Y;
        bool floor = IsFloor(itemId);
        var producer = ProducerOf(itemId);
        var here = doc.Progress.PlacedObjects.Where(item => item.Location == location && item.X == x && item.Y == y).ToList();
        // Objects may stand on a floor; a floor cannot go under an object.
        if (floor ? here.Count > 0 : here.Any(item => !IsFloor(item.ItemId)))
            return "Ô này đã có đồ vật.";
        if (doc.Chests.Any(chest => chest.Location == location && chest.TileX == x && chest.TileY + 1 == y))
            return "Ô này đã có đồ vật.";
        if (producer?.FarmOnly == true && location != "Farm") return "Chỉ đặt được ngoài nông trại.";
        if (producer?.OnTree == true)
        {
            if (location != "Farm" || input.Variant is not ("oak" or "maple" or "pine"))
                return "Hãy đặt máy chiết nhựa lên một cây trưởng thành.";
            if (doc.Progress.NaturalResources.TryGetValue($"Farm:{x}:{y - 1}", out var tree) &&
                (tree.Kind != "tree" || tree.Stump || doc.GameDay < tree.RegrowDay))
                return "Cây này đã bị chặt.";
        }
        else if (location == "Farm")
        {
            if (InBuiltRanchPlot(doc, x, y - 1) ||
                FarmLandmarks.Any(mark => Covers(mark.X, mark.Y, mark.W, mark.H, x, y)) ||
                doc.Progress.FarmSoil.Values.Any(tile => tile.TileX == x && tile.TileY + 1 == y) ||
                db.FarmCropStates.Any(crop => crop.UserId == doc.UserId && crop.TileX == x && crop.TileY + 1 == y))
                return "Không thể đặt đồ ở đây.";
        }
        if (!TakeItems(doc.Inventory, itemId, 1)) return "Bạn không còn vật phẩm này.";
        var placed = new PlacedObject(Guid.NewGuid(), itemId, location, x, y,
            Variant: producer?.OnTree == true ? input.Variant : null);
        if (producer != null)
            placed = StartProducing(doc, placed, producer, ValidMinute(input.Minute) ? input.Minute : 600,
                new Random(HashCode.Combine(placed.Id, doc.GameDay)));
        doc.Progress.PlacedObjects.Add(placed);
        return null;
    }

    private static string? PickUpObject(StateDocument doc, ObjectActionRequest input, IItemCatalog catalog)
    {
        var placed = FindPlaced(doc, input.ObjectId);
        if (placed == null) return "Không tìm thấy đồ vật.";
        if (!NearObject(doc, placed.Location, placed.X, placed.Y)) return "Hãy lại gần đồ vật.";
        bool producer = ProducerOf(placed.ItemId) != null;
        if (placed.Output != null && !producer) return "Hãy lấy thành phẩm ra trước.";
        if (!catalog.TryGet(placed.ItemId, out var item) || !CanAdd(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize))
            return "Túi đồ đã đầy.";
        Add(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize);
        doc.Progress.PlacedObjects.Remove(placed);
        return null;
    }

    private static bool InputMatches(string ruleInput, string itemId)
    {
        string baseId = RecipeIngredients.BaseId(itemId);
        return ruleInput switch
        {
            "#fruit" => Artisan.Value.Fruits.Contains(baseId),
            "#vegetable" => Artisan.Value.Vegetables.Contains(baseId),
            _ => baseId == ruleInput
        };
    }

    private static string? LoadMachine(StateDocument doc, ObjectActionRequest input)
    {
        var placed = FindPlaced(doc, input.ObjectId);
        if (placed == null) return "Không tìm thấy máy.";
        if (!NearObject(doc, placed.Location, placed.X, placed.Y)) return "Hãy lại gần máy.";
        if (!ValidMinute(input.Minute)) return "Thời gian không hợp lệ.";
        var machine = Artisan.Value.Machines.FirstOrDefault(item => item.Id == placed.ItemId);
        if (machine == null) return "Đồ vật này không nhận nguyên liệu.";
        if (placed.Output != null) return "Máy đang bận.";
        string held = input.ItemId ?? "";
        var rule = machine.Rules.FirstOrDefault(item => InputMatches(item.Input, held));
        if (rule == null) return "Máy không nhận vật phẩm này.";
        string heldBase = RecipeIngredients.BaseId(held);
        if (doc.Inventory.Where(stack => RecipeIngredients.BaseId(stack.ItemId) == heldBase).Sum(stack => stack.Amount) < rule.Count)
            return $"Cần {rule.Count} vật phẩm này.";
        if (machine.Fuel != null && doc.Inventory.Where(stack => stack.ItemId == machine.Fuel.Id).Sum(stack => stack.Amount) < machine.Fuel.Count)
            return "Cần thêm than.";
        var needed = new Dictionary<string, int> { [heldBase] = rule.Count };
        if (machine.Fuel != null) needed[machine.Fuel.Id] = needed.GetValueOrDefault(machine.Fuel.Id) + machine.Fuel.Count;
        if (!RecipeIngredients.TryConsume(doc.Inventory, needed, id => id)) return "Không đủ nguyên liệu.";
        var random = new Random(HashCode.Combine(placed.Id, doc.GameDay, input.Minute));
        string[] choices = rule.Output.Split('|');
        string output = choices[random.Next(choices.Length)];
        output = rule.Preserve != null ? $"{output}#{heldBase}" : WithQuality(output, rule.Quality);
        int count = random.Next(Math.Max(1, rule.OutputMin), Math.Max(rule.OutputMin, rule.OutputMax) + 1);
        int index = doc.Progress.PlacedObjects.IndexOf(placed);
        doc.Progress.PlacedObjects[index] = placed with
            { Output = output, ReadyAt = GameMinute(doc, input.Minute) + rule.Minutes, OutputCount = count };
        return null;
    }

    private static string? CollectMachine(StateDocument doc, ObjectActionRequest input, IItemCatalog catalog)
    {
        var placed = FindPlaced(doc, input.ObjectId);
        if (placed == null) return "Không tìm thấy máy.";
        if (!NearObject(doc, placed.Location, placed.X, placed.Y)) return "Hãy lại gần máy.";
        if (!ValidMinute(input.Minute)) return "Thời gian không hợp lệ.";
        if (placed.Output == null) return "Máy đang trống.";
        if (GameMinute(doc, input.Minute) < placed.ReadyAt) return "Chưa xong.";
        int count = Math.Max(1, placed.OutputCount);
        if (!catalog.TryGet(placed.Output, out var item) || !CanAdd(doc.Inventory, item.Id, count, item.MaxStack, doc.BagSize))
            return "Túi đồ đã đầy.";
        Add(doc.Inventory, item.Id, count, item.MaxStack, doc.BagSize);
        int index = doc.Progress.PlacedObjects.IndexOf(placed);
        var producer = ProducerOf(placed.ItemId);
        doc.Progress.PlacedObjects[index] = producer != null
            ? StartProducing(doc, placed, producer, input.Minute, new Random(HashCode.Combine(placed.Id, doc.GameDay, input.Minute)))
            : placed with { Output = null, ReadyAt = 0, OutputCount = 1 };
        return null;
    }

    private static string? ToggleGate(StateDocument doc, ObjectActionRequest input)
    {
        var placed = FindPlaced(doc, input.ObjectId);
        if (placed == null || !Artisan.Value.Fences.Any(fence => fence.Id == placed.ItemId && fence.Gate))
            return "Không tìm thấy cổng.";
        if (!NearObject(doc, placed.Location, placed.X, placed.Y)) return "Hãy lại gần cổng.";
        int index = doc.Progress.PlacedObjects.IndexOf(placed);
        doc.Progress.PlacedObjects[index] = placed with { Open = !placed.Open };
        return null;
    }

    private static string? ThrowBomb(StateDocument doc, ObjectActionRequest input)
    {
        string bomb = input.ItemId ?? "";
        if (!Artisan.Value.Bombs.ContainsKey(bomb)) return "Đây không phải bom.";
        if (string.IsNullOrWhiteSpace(input.Location) || input.Location.Length > 64) return "Vị trí không hợp lệ.";
        if (!NearObject(doc, input.Location, input.X, input.Y)) return "Hãy lại gần chỗ đặt bom.";
        if (!TakeItems(doc.Inventory, bomb, 1)) return "Bạn không còn bom.";
        return null;
    }

    private static string? UseStaircase(StateDocument doc, ObjectActionRequest input)
    {
        if (!int.TryParse(input.Location, out int floor) || floor is < 1 or >= 120)
            return "Chỉ dùng cầu thang trong hầm mỏ.";
        if (!WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, input.Location!)) return "Hãy vào hầm mỏ.";
        if (!TakeItems(doc.Inventory, StaircaseId, 1)) return "Bạn không còn cầu thang.";
        return null;
    }

    // ── Sprinklers ───────────────────────────────────────────────────────────────────────────

    /// <summary>Map tiles watered each morning by the farm's sprinklers.</summary>
    private static HashSet<(int X, int Y)> SprinklerTiles(StateDocument doc)
    {
        var tiles = new HashSet<(int X, int Y)>();
        foreach (var placed in doc.Progress.PlacedObjects)
        {
            if (placed.Location != "Farm") continue;
            var sprinkler = Artisan.Value.Sprinklers.FirstOrDefault(item => item.Id == placed.ItemId);
            if (sprinkler == null) continue;
            int reach = sprinkler.Range switch { "square1" => 1, "square2" => 2, _ => 1 };
            for (int dx = -reach; dx <= reach; dx++)
            for (int dy = -reach; dy <= reach; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (sprinkler.Range == "plus" && dx != 0 && dy != 0) continue;
                tiles.Add((placed.X + dx, placed.Y + dy));
            }
        }
        return tiles;
    }
}
