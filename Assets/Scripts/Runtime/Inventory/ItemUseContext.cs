using UnityEngine;

public readonly struct ItemUseContext
{
    public readonly Transform User;
    public readonly Vector2 Facing;
    public readonly InventorySlot Item;
    public readonly PlayerInventory Inventory;
    public readonly PlayerFarmingInteractor Farming;

    public ItemUseContext(Transform user, Vector2 facing, InventorySlot item,
        PlayerInventory inventory, PlayerFarmingInteractor farming)
    {
        User = user;
        Facing = facing;
        Item = item;
        Inventory = inventory;
        Farming = farming;
    }
}
