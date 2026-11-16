public sealed record ChestQuickMoveRequest(Guid RequestId, string? ChestId, bool FromChest, int Slot, int Amount);
public sealed record ChestRequest(Guid RequestId, string? ChestId);

/// <summary>
/// Chest menu actions: clicking a stack sends it to the other side (stacking onto matching items
/// first), "fill stacks" tops up items the chest already holds, and "organize" sorts the chest.
/// </summary>
public static partial class GameStateEndpoints
{
    private static void MapChestActions(WebApplication app)
    {
        app.MapPost("/api/game/chests/quick-move", async (HttpRequest request, ChestQuickMoveRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => QuickMove(doc, input, catalog)));
        app.MapPost("/api/game/chests/fill-stacks", async (HttpRequest request, ChestRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => FillChestStacks(doc, input, catalog)));
        app.MapPost("/api/game/chests/organize", async (HttpRequest request, ChestRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => OrganizeChest(doc, input, catalog)));
    }

    private static ChestEntry? OpenChest(StateDocument doc, string? chestId) =>
        Guid.TryParse(chestId, out Guid id) ? doc.Chests.Find(chest => chest.Id == id) : null;

    /// <summary>Adds up to <paramref name="amount"/> onto matching stacks, then into the lowest empty slots.</summary>
    private static int PutInto(List<StackEntry> target, int size, string itemId, int amount, int maxStack)
    {
        int moved = 0;
        foreach (var stack in target.Where(s => s.ItemId == itemId && s.Amount < maxStack).OrderBy(s => s.Slot))
        {
            int put = Math.Min(amount - moved, maxStack - stack.Amount);
            stack.Amount += put;
            moved += put;
            if (moved == amount) return moved;
        }
        for (int slot = 0; slot < size && moved < amount; slot++)
        {
            if (target.Any(s => s.Slot == slot)) continue;
            int put = Math.Min(amount - moved, maxStack);
            target.Add(new StackEntry { Slot = slot, ItemId = itemId, Amount = put });
            moved += put;
        }
        return moved;
    }

    private static string? QuickMove(StateDocument doc, ChestQuickMoveRequest input, IItemCatalog catalog)
    {
        var chest = OpenChest(doc, input.ChestId);
        if (chest == null) return "Không tìm thấy rương.";
        var source = input.FromChest ? chest.Slots : doc.Inventory;
        var target = input.FromChest ? doc.Inventory : chest.Slots;
        int targetSize = input.FromChest ? doc.BagSize : ChestSize;
        var from = source.Find(s => s.Slot == input.Slot);
        if (from == null || !catalog.TryGet(from.ItemId, out var item)) return "Ô này đang trống.";
        int amount = input.Amount <= 0 ? from.Amount : Math.Min(input.Amount, from.Amount);
        int moved = PutInto(target, targetSize, from.ItemId, amount, Math.Max(1, item.MaxStack));
        if (moved == 0) return input.FromChest ? "Túi đồ đã đầy." : "Rương đã đầy.";
        from.Amount -= moved;
        if (from.Amount == 0) source.Remove(from);
        return null;
    }

    private static string? FillChestStacks(StateDocument doc, ChestRequest input, IItemCatalog catalog)
    {
        var chest = OpenChest(doc, input.ChestId);
        if (chest == null) return "Không tìm thấy rương.";
        var wanted = chest.Slots.Select(s => s.ItemId).ToHashSet(StringComparer.Ordinal);
        foreach (var stack in doc.Inventory.Where(s => wanted.Contains(s.ItemId)).OrderBy(s => s.Slot).ToList())
        {
            if (!catalog.TryGet(stack.ItemId, out var item) || item.IsTool) continue;
            int moved = PutInto(chest.Slots, ChestSize, stack.ItemId, stack.Amount, Math.Max(1, item.MaxStack));
            stack.Amount -= moved;
            if (stack.Amount == 0) doc.Inventory.Remove(stack);
        }
        return null;
    }

    private static string? OrganizeChest(StateDocument doc, ChestRequest input, IItemCatalog catalog)
    {
        var chest = OpenChest(doc, input.ChestId);
        if (chest == null) return "Không tìm thấy rương.";
        var ordered = chest.Slots
            .Select(stack => (Stack: stack, Item: catalog.TryGet(stack.ItemId, out var item) ? item : null))
            .OrderBy(entry => entry.Item?.IsTool == true ? 0 : 1)
            .ThenBy(entry => entry.Item?.Category ?? "", StringComparer.Ordinal)
            .ThenBy(entry => entry.Item?.Name ?? entry.Stack.ItemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Stack.Slot).ToList();
        var organized = new List<StackEntry>();
        foreach (var (stack, item) in ordered)
        {
            int max = Math.Max(1, item?.MaxStack ?? 999);
            int moved = PutInto(organized, ChestSize, stack.ItemId, stack.Amount, max);
            if (moved < stack.Amount) return "Rương đã đầy.";
        }
        chest.Slots.Clear();
        chest.Slots.AddRange(organized);
        return null;
    }
}
