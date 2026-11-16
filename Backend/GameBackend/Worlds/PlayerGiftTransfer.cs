/// <summary>
/// Farmer-to-farmer gifts (FarmerTeam.SendProposal with ProposalType.Gift): the sender keeps
/// the item while the offer waits, and loses one only when the receiver accepts. Both bags
/// change in one transaction, so a gift can never be duplicated or lost.
/// </summary>
public static partial class GameStateEndpoints
{
    /// <summary>Object.canBeGivenAsGift: tools, big craftables and furniture stay with their owner.</summary>
    internal static bool CanGiftToFarmer(ItemDefinition item) =>
        !item.IsTool && item.Category is not ("BigCraftable" or "Furniture");

    /// <summary>Checks the sender still holds a giftable item in that slot; null when it does.</summary>
    internal static async Task<string?> FarmerGiftOfferError(GameDb db, IItemCatalog catalog,
        Guid senderId, int slot, string? itemId)
    {
        var sender = Decode(await Load(db, senderId));
        return SenderGiftError(sender, slot, itemId, catalog, out _);
    }

    /// <summary>Moves one item from the sender's slot into the receiver's bag.</summary>
    internal static async Task<(string? Error, GameSnapshot? ReceiverSnapshot)> GiveItemToFarmer(
        GameDb db, IItemCatalog catalog, Guid senderId, Guid receiverId, int slot, string itemId)
    {
        if (senderId == receiverId) return ("Không thể tự tặng quà cho mình.", null);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var senderState = await Load(db, senderId);
        var receiverState = await Load(db, receiverId);
        var sender = Decode(senderState);
        var receiver = Decode(receiverState);
        await HydrateLegacyProgress(db, sender);
        await HydrateLegacyProgress(db, receiver);

        var error = SenderGiftError(sender, slot, itemId, catalog, out var item);
        if (error != null) return (error, null);
        // FarmerTeam.handleIncomingProposal: GiftPlayerItem_NoInventorySpace.
        if (!CanAdd(receiver.Inventory, itemId, 1, item!.MaxStack, receiver.BagSize))
            return ("Túi đồ của người nhận đã đầy.", null);

        var stack = sender.Inventory.Find(s => s.Slot == slot)!;
        stack.Amount--;
        if (stack.Amount == 0) sender.Inventory.Remove(stack);
        Add(receiver.Inventory, itemId, 1, item.MaxStack, receiver.BagSize);

        if (!await Persist(db, senderState, sender) || !await Persist(db, receiverState, receiver))
            return ("Túi đồ vừa thay đổi, hãy thử lại.", null);
        await transaction.CommitAsync();
        return (null, Snapshot(receiver, receiverState.Version, catalog));
    }

    private static string? SenderGiftError(StateDocument sender, int slot, string? itemId,
        IItemCatalog catalog, out ItemDefinition? item)
    {
        item = null;
        if (slot < 0 || slot >= sender.BagSize || string.IsNullOrWhiteSpace(itemId))
            return "Ô quà tặng không hợp lệ.";
        var stack = sender.Inventory.Find(s => s.Slot == slot);
        if (stack == null || stack.ItemId != itemId || stack.Amount < 1)
            return "Món quà không còn trong túi đồ.";
        if (!catalog.TryGet(itemId, out var definition) || !CanGiftToFarmer(definition))
            return "Không thể tặng món đồ này.";
        item = definition;
        return null;
    }
}
