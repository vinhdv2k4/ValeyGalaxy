using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Immutable server-authoritative item and tool catalog loaded and indexed in-memory at startup.
/// Uses FrozenDictionary hash maps for sub-microsecond O(1) lookups by ID and Hash.
/// </summary>
public sealed class JsonItemCatalog : IItemCatalog
{
    private static readonly HashSet<string> KnownToolIds = new(StringComparer.Ordinal)
    {
        "axe", "hoe", "watering_can", "pickaxe", "scythe", "fishing_rod"
    };

    private static readonly Dictionary<string, (string Name, string Action, int Tier, int Energy)> ToolDetails = new(StringComparer.Ordinal)
    {
        { "axe", ("Rìu cơ bản", "Chop", 1, 2) },
        { "hoe", ("Cuốc đất", "Till", 1, 2) },
        { "watering_can", ("Bình tưới nước", "Water", 1, 2) },
        { "pickaxe", ("Cuốc chim", "Mine", 1, 2) },
        { "scythe", ("Lưỡi liềm", "Harvest", 1, 0) },
        { "fishing_rod", ("Cần câu gỗ", "Fish", 1, 8) }
    };

    // The Unity export supplies the IDs. Native shipping prices are separate
    // because the exported Object records contain empty definitions.
    private static readonly (string File, string Prefix, string Category, int MaxStack)[] ContentSources =
    [
        ("Objects.json", "object", "Object", 999),
        ("BigCraftables.json", "big_craftable", "BigCraftable", 1),
        ("Weapons.json", "weapon", "Weapon", 1),
        ("Boots.json", "boot", "Boot", 1),
        ("hats.json", "hat", "Hat", 1),
        ("Furniture.json", "furniture", "Furniture", 1),
        ("Tools.json", "tool", "Tool", 1),
        ("Shirts.json", "shirt", "Shirt", 1),
        ("Pants.json", "pants", "Pants", 1),
        ("Trinkets.json", "trinket", "Trinket", 1),
        ("Fish.json", "fish", "Fish", 999),
        ("Crops.json", "crop", "CropDefinition", 1),
        ("FruitTrees.json", "fruit_tree", "FruitTree", 1),
        ("Fences.json", "fence", "Fence", 1),
        ("FloorsAndPaths.json", "floor_path", "FloorPath", 1)
    ];

    private readonly FrozenDictionary<string, ItemDefinition> _byId;
    private readonly FrozenDictionary<string, ItemDefinition> _byHash;
    private readonly FrozenDictionary<string, ToolDefinition> _toolsById;

    public IReadOnlyCollection<ItemDefinition> All { get; }
    public IReadOnlyCollection<ToolDefinition> AllTools { get; }

    public JsonItemCatalog(IHostEnvironment environment, ISecureIdHasher hasher)
    {
        var path = Path.Combine(environment.ContentRootPath, "item-catalog.json");
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("Item catalog is empty.");
        var items = file.Items ?? throw new InvalidDataException("Item catalog has no items.");
        var shippingPath = Path.Combine(environment.ContentRootPath, "object-shipping.json");
        var shippingPrices = JsonSerializer.Deserialize<ObjectShippingFile>(File.ReadAllText(shippingPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Prices ??
            throw new InvalidDataException("Object shipping catalog has no prices.");
        if (shippingPrices.Count == 0 || shippingPrices.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0))
            throw new InvalidDataException("Object shipping catalog contains an invalid ID or price.");
        int? ShippingPrice(string id) => shippingPrices.TryGetValue(id, out int price) ? price : null;

        var fishPath = Path.Combine(environment.ContentRootPath, "fish-catalog.json");
        var fishFile = JsonSerializer.Deserialize<FishCatalogFile>(File.ReadAllText(fishPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var fishIds = fishFile?.Ids ?? fishFile?.SellPrices?.Keys.ToArray() ?? [];
        if (fishIds.Any(id => id <= 0)) throw new InvalidDataException("Fish catalog contains an invalid ID.");

        var byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        var byHash = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        var tools = new Dictionary<string, ToolDefinition>(StringComparer.Ordinal);
        var enrichedList = new List<ItemDefinition>(items.Length);

        foreach (var rawItem in items)
        {
            if (rawItem == null || string.IsNullOrWhiteSpace(rawItem.Id) || rawItem.Id != rawItem.Id.Trim() ||
                rawItem.MaxStack is < 1 or > 999 || rawItem.BuyPrice < 0 || rawItem.SellPrice < 0 ||
                rawItem.Edibility is < 0 or > 1000 ||
                byId.ContainsKey(rawItem.Id))
            {
                throw new InvalidDataException("Item catalog contains an invalid or duplicate entry.");
            }

            var isTool = KnownToolIds.Contains(rawItem.Id);
            var category = isTool ? "Tool"
                : rawItem.Id.StartsWith("seeds_") || rawItem.Id == "mixed_seeds" ? "Seeds"
                : rawItem.Id is "parsnip" or "potato" or "green_bean" or "cauliflower" ? "Crop"
                : "Resource";

            var lookupHash = hasher.ComputeCatalogHash(rawItem.Id);
            string? objectId = file.ObjectIds?.GetValueOrDefault(rawItem.Id);
            int? objectPrice = objectId == null ? null : ShippingPrice(objectId);

            var item = new ItemDefinition(rawItem.Id, rawItem.MaxStack, rawItem.BuyPrice,
                objectId == null ? rawItem.SellPrice : objectPrice.GetValueOrDefault())
            {
                Name = string.IsNullOrWhiteSpace(rawItem.Name) ? ReadableName(rawItem.Id) : rawItem.Name,
                Category = category,
                IsTool = isTool,
                LookupHash = lookupHash,
                Edibility = rawItem.Edibility,
                IsDrink = rawItem.IsDrink,
                ObjectIndex = int.TryParse(objectId, out int objectIndex) ? objectIndex : -1,
                ShippingAllowed = objectId == null ? null : objectPrice.HasValue
            };

            byId[item.Id] = item;
            byHash[lookupHash] = item;
            enrichedList.Add(item);

            if (isTool && ToolDetails.TryGetValue(item.Id, out var details))
            {
                tools[item.Id] = new ToolDefinition(item.Id, details.Name, details.Action, details.Tier, details.Energy);
            }
        }

        var gameNames = LoadGameNames(environment);
        foreach (var source in ContentSources)
        {
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "ContentData", source.File);
            if (!File.Exists(sourcePath))
                sourcePath = Path.Combine(environment.ContentRootPath, "ContentData", source.File);
            if (!File.Exists(sourcePath)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(sourcePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Content item file {source.File} is invalid.");
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                string id = source.Prefix + "_" + entry.Name;
                if (byId.ContainsKey(id)) continue;
                string[] fields = entry.Value.ValueKind == JsonValueKind.String
                    ? entry.Value.GetString()?.Split('/') ?? [] : [];
                // The exported records are empty, so names come from the game's display names.
                string name = gameNames.GetValueOrDefault(id) ?? fields.FirstOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(name)) name = source.Category + " #" + entry.Name;
                string lookupHash = hasher.ComputeCatalogHash(id);
                int buyPrice = source.Category == "Furniture" && fields.Length > 5 &&
                    int.TryParse(fields[5], out int furniturePrice) ? Math.Max(0, furniturePrice) : 0;
                int maxStack = id is "object_529" or "object_530" or "object_531" or
                    "object_532" or "object_533" or "object_534" ? 1 : source.MaxStack;
                int? sellPrice = source.Prefix is "fish" or "object" ? ShippingPrice(entry.Name) : null;
                var item = new ItemDefinition(id, maxStack, buyPrice, sellPrice.GetValueOrDefault())
                {
                    Name = name, Category = source.Category,
                    IsTool = source.Category == "Tool", LookupHash = lookupHash,
                    Edibility = source.Category == "Fish" ? 5 : 0,
                    ShippingAllowed = sellPrice.HasValue
                };
                byId[id] = item;
                byHash[lookupHash] = item;
                enrichedList.Add(item);
            }
        }

        foreach (int fishId in fishIds)
        {
            string id = "fish_" + fishId;
            if (byId.ContainsKey(id)) continue;
            string lookupHash = hasher.ComputeCatalogHash(id);
            int? sellPrice = ShippingPrice(fishId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var fishItem = new ItemDefinition(id, 999, 0, sellPrice.GetValueOrDefault())
            {
                Name = "Fish #" + fishId, Category = "Fish", LookupHash = lookupHash, Edibility = 5,
                ShippingAllowed = sellPrice.HasValue
            };
            byId[id] = fishItem;
            byHash[lookupHash] = fishItem;
            enrichedList.Add(fishItem);
        }

        ApplyRanchItems(environment, hasher, byId, byHash, enrichedList);
        ApplyArtisanNames(environment, byId, byHash, enrichedList);

        if (byId.Count == 0) throw new InvalidDataException("Item catalog has no items.");

        _byId = byId.ToFrozenDictionary(StringComparer.Ordinal);
        _byHash = byHash.ToFrozenDictionary(StringComparer.Ordinal);
        _toolsById = tools.ToFrozenDictionary(StringComparer.Ordinal);

        All = enrichedList.AsReadOnly();
        AllTools = tools.Values.ToArray();
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ItemDefinition> _qualityItems = new(StringComparer.Ordinal);

    /// <summary>
    /// Items may carry a quality suffix ("object_176@2"): same item, star shown in the bag and a
    /// higher sell price (silver x1.25, gold x1.5, iridium x2).
    /// </summary>
    public bool TryGet(string id, out ItemDefinition item)
    {
        if (_byId.TryGetValue(id ?? "", out item!)) return true;
        int hash = id?.IndexOf('#') ?? -1;
        if (hash > 0) return TryGetPreserved(id!, hash, out item);
        int split = id?.LastIndexOf('@') ?? -1;
        if (split <= 0 || !int.TryParse(id!.AsSpan(split + 1), out int quality) || quality is not (1 or 2 or 4) ||
            !_byId.TryGetValue(id[..split], out var baseItem))
            return false;
        item = _qualityItems.GetOrAdd(id, key => baseItem with
        {
            Id = key,
            SellPrice = (int)(baseItem.SellPrice * (quality switch { 1 => 1.25, 2 => 1.5, _ => 2.0 }))
        });
        return true;
    }

    /// <summary>Preserved goods name their source: "object_348#object_258" is Blueberry Wine.</summary>
    private bool TryGetPreserved(string id, int hash, out ItemDefinition item)
    {
        item = null!;
        string output = id[..hash], source = id[(hash + 1)..];
        if (!PreserveOutputs.TryGetValue(output, out string? kind) || !_preserves.TryGetValue(kind, out var rule) ||
            !_byId.TryGetValue(output, out var baseItem) || !_byId.TryGetValue(source, out var sourceItem))
            return false;
        item = _qualityItems.GetOrAdd(id, key => baseItem with
        {
            Id = key,
            Name = string.Format(rule.Name, sourceItem.Name),
            SellPrice = (int)(sourceItem.SellPrice * rule.Multiplier) + rule.Add
        });
        return true;
    }

    public bool TryGetByHash(string hash, out ItemDefinition item) =>
        _byHash.TryGetValue(hash ?? "", out item!);

    public bool TryGetTool(string id, out ToolDefinition tool) =>
        _toolsById.TryGetValue(id ?? "", out tool!);

    private sealed record RanchItem(string Id, string Name, string Category, int SellPrice, int BuyPrice,
        int Edibility, bool Qualities);
    private sealed record RanchItemFile(RanchItem[]? Items);

    private static readonly (int Quality, string Label, double Multiplier)[] Qualities =
        [(1, "bạc", 1.25), (2, "vàng", 1.5), (4, "iridium", 2.0)];

    /// <summary>
    /// Animal products, hay and ranch tools get real names, categories and prices, and every
    /// animal product gets its silver, gold and iridium variants ("object_176@2").
    /// </summary>
    private static void ApplyRanchItems(IHostEnvironment environment, ISecureIdHasher hasher,
        Dictionary<string, ItemDefinition> byId, Dictionary<string, ItemDefinition> byHash, List<ItemDefinition> list)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "RanchShop.json");
        if (!File.Exists(path)) path = Path.Combine(environment.ContentRootPath, "ContentData", "RanchShop.json");
        if (!File.Exists(path)) return;
        var file = JsonSerializer.Deserialize<RanchItemFile>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var entry in file?.Items ?? [])
        {
            if (!byId.TryGetValue(entry.Id, out var current)) continue;
            var updated = current with
            {
                Name = entry.Name, Category = entry.Category, SellPrice = entry.SellPrice,
                BuyPrice = entry.BuyPrice, Edibility = entry.Edibility,
                ShippingAllowed = entry.SellPrice > 0
            };
            Replace(current, updated);
            if (!entry.Qualities) continue;
            foreach (var (quality, label, multiplier) in Qualities)
            {
                string id = entry.Id + "@" + quality;
                if (byId.ContainsKey(id)) continue;
                var variant = updated with
                {
                    Id = id, Name = $"{entry.Name} ({label})", SellPrice = (int)(entry.SellPrice * multiplier),
                    LookupHash = hasher.ComputeCatalogHash(id)
                };
                byId[id] = variant;
                byHash[variant.LookupHash] = variant;
                list.Add(variant);
            }
        }

        void Replace(ItemDefinition current, ItemDefinition updated)
        {
            byId[updated.Id] = updated;
            byHash[updated.LookupHash] = updated;
            int index = list.IndexOf(current);
            if (index >= 0) list[index] = updated;
        }
    }

    private sealed record NamedItem(string Id, string Name, string Category);
    private sealed record PreserveRule(string Name, double Multiplier, int Add);
    private sealed record ArtisanFile(NamedItem[]? Items, Dictionary<string, PreserveRule>? Preserves);
    private static readonly Dictionary<string, string> PreserveOutputs = new(StringComparer.Ordinal)
        { ["object_348"] = "wine", ["object_350"] = "juice", ["object_344"] = "jelly", ["object_342"] = "pickles" };
    private Dictionary<string, PreserveRule> _preserves = new(StringComparer.Ordinal);

    private static string ArtisanPath(IHostEnvironment environment)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "Artisan.json");
        return File.Exists(path) ? path : Path.Combine(environment.ContentRootPath, "ContentData", "Artisan.json");
    }

    /// <summary>Readable names for fruits, vegetables, artisan goods, machines and every crafted output.</summary>
    private void ApplyArtisanNames(IHostEnvironment environment, Dictionary<string, ItemDefinition> byId,
        Dictionary<string, ItemDefinition> byHash, List<ItemDefinition> list)
    {
        string path = ArtisanPath(environment);
        if (!File.Exists(path)) return;
        var file = JsonSerializer.Deserialize<ArtisanFile>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _preserves = new Dictionary<string, PreserveRule>(file?.Preserves ?? new(), StringComparer.Ordinal);
        foreach (var entry in file?.Items ?? [])
        {
            if (!byId.TryGetValue(entry.Id, out var current)) continue;
            bool generic = current.Category is "Object" or "BigCraftable" or "Resource" or "General";
            var updated = current with
            {
                Name = entry.Name,
                Category = generic && entry.Category is not ("Machine" or "BigCraftable" or "Crafted") ? entry.Category : current.Category
            };
            byId[updated.Id] = updated;
            byHash[updated.LookupHash] = updated;
            int index = list.IndexOf(current);
            if (index >= 0) list[index] = updated;
        }
    }

    /// <summary>English display names keyed by item id ("object_16" → "Wild Horseradish").</summary>
    private static Dictionary<string, string> LoadGameNames(IHostEnvironment environment)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "ItemNames.json");
        if (!File.Exists(path)) path = Path.Combine(environment.ContentRootPath, "ContentData", "ItemNames.json");
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static string ReadableName(string id) =>
        string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private sealed record CatalogFile(ItemDefinition[]? Items, Dictionary<string, string>? ObjectIds);
    private sealed record ObjectShippingFile(Dictionary<string, int>? Prices);
    private sealed record FishCatalogFile(int[]? Ids, Dictionary<int, int>? SellPrices);
}
