public interface IItemCatalog
{
    bool TryGet(string id, out ItemDefinition item);
    bool TryGetByHash(string hash, out ItemDefinition item);
    bool TryGetTool(string id, out ToolDefinition tool);
    IReadOnlyCollection<ItemDefinition> All { get; }
    IReadOnlyCollection<ToolDefinition> AllTools { get; }
}

public sealed record ItemDefinition(string Id, int MaxStack, int BuyPrice, int SellPrice)
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "General";
    public bool IsTool { get; init; } = false;
    public string LookupHash { get; init; } = "";
    public int Edibility { get; init; }
    public bool IsDrink { get; init; }
    /// <summary>Data/ObjectInformation index for named objects ("parsnip" is 24), or -1.</summary>
    public int ObjectIndex { get; init; } = -1;
    internal bool? ShippingAllowed { get; init; }
    public bool CanShip => (ShippingAllowed ?? SellPrice > 0) && !IsTool &&
        Category is ("Resource" or "Crop" or "Seeds" or "Object" or "Fish" or "Egg" or "Milk" or "AnimalProduct" or
            "Vegetable" or "Fruit" or "Artisan" or "Mineral");
}

public sealed record ToolDefinition(
    string Id,
    string Name,
    string ActionKind,
    int PowerTier,
    int EnergyCost
);
