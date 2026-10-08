using System.Collections.Generic;

public static class ItemUseCatalog
{
    private static readonly Dictionary<PlayerItemKind, IItemUseBehavior> OtherItems = new()
    {
        { PlayerItemKind.Seeds, new SeedUseBehavior() }
    };

    public static IItemUseBehavior Resolve(InventorySlot slot)
    {
        if (slot == null) return null;
        if (ToolCatalog.Resolve(slot) is IItemUseBehavior tool) return tool;
        return OtherItems.TryGetValue(slot.kind, out var behavior) ? behavior : null;
    }

    public static void Register(PlayerItemKind kind, IItemUseBehavior behavior)
    {
        if (behavior != null) OtherItems[kind] = behavior;
    }
}
