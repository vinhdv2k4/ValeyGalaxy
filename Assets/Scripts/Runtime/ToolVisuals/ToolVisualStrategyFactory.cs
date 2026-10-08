/// <summary>
/// Resolves visuals through the selected tool profile. Items outside the Tool
/// hierarchy keep their own presentation strategy.
/// </summary>
public static class ToolVisualStrategyFactory
{
    private static readonly IToolVisualStrategy SeedStrategy = new SeedThrowVisualStrategy();
    private static readonly IToolVisualStrategy DefaultStrategy = new DefaultHoldVisualStrategy();

    public static IToolVisualStrategy GetStrategy(InventorySlot slot)
    {
        if (slot == null) return DefaultStrategy;
        return ToolCatalog.Resolve(slot)?.VisualStrategy ?? GetStrategy(slot.kind);
    }

    public static IToolVisualStrategy GetStrategy(ItemData item)
    {
        if (item == null) return DefaultStrategy;
        return ToolCatalog.Resolve(item)?.VisualStrategy ?? GetStrategy(item.kind);
    }

    public static IToolVisualStrategy GetStrategy(PlayerItemKind kind)
    {
        return kind == PlayerItemKind.Seeds ? SeedStrategy
            : ToolCatalog.Resolve(kind)?.VisualStrategy ?? DefaultStrategy;
    }
}
