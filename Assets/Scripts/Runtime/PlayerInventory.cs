using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerItemKind { Hoe, Seeds, Material, WateringCan, FishingRod, Sword, Shears, MilkPail, Pan, Axe, Pickaxe }

[Serializable]
public sealed class InventorySlot : IReadOnlyItemSlot
{
    public string displayName;
    public PlayerItemKind kind;
    public ItemData itemData;
    public Sprite heldSprite;
    public Sprite heldSpriteDown;
    public Sprite heldSpriteRight;
    public Sprite heldSpriteUp;
    public Sprite[] heldAnimationSprites;
    public Sprite inventoryIcon;
    public bool hasUseAnimation;
    public FarmerAnimationAction useAnimation;
    [Min(0)] public int amount;

    public ItemData Data => itemData;
    public FarmerAnimationAction ResolvedUseAnimation => ToolCatalog.Resolve(this)?.UseAnimation ?? useAnimation;
    public int Amount => amount;
    public bool IsEmpty => (itemData == null && string.IsNullOrEmpty(displayName)) || amount <= 0;

    public Sprite GetHeldSprite(int direction)
    {
        if (itemData != null)
        {
            return itemData.GetHeldSprite(direction);
        }

        return direction switch
        {
            0 => heldSpriteDown != null ? heldSpriteDown : heldSprite,
            1 or 3 => heldSpriteRight != null ? heldSpriteRight : heldSprite,
            2 => heldSpriteUp != null ? heldSpriteUp : heldSprite,
            _ => heldSprite
        };
    }

    public Sprite GetHeldAnimationSprite(int indexOffset)
    {
        if (itemData != null)
        {
            return itemData.GetHeldAnimationSprite(indexOffset);
        }

        return heldAnimationSprites != null && indexOffset >= 0 && indexOffset < heldAnimationSprites.Length
            && heldAnimationSprites[indexOffset] != null ? heldAnimationSprites[indexOffset] : heldSprite;
    }

    public void SyncFromData()
    {
        if (itemData != null)
        {
            displayName = itemData.displayName;
            kind = itemData.kind;
            heldSprite = itemData.heldSprite;
            heldSpriteDown = itemData.heldSpriteDown;
            heldSpriteRight = itemData.heldSpriteRight;
            heldSpriteUp = itemData.heldSpriteUp;
            heldAnimationSprites = itemData.heldAnimationSprites;
            inventoryIcon = itemData.GetIcon();
            hasUseAnimation = itemData.hasUseAnimation;
            useAnimation = itemData.useAnimation;
        }
    }
}

public sealed class PlayerInventory : MonoBehaviour, IReadOnlyInventory, IInventoryController
{
    public const int MaxCapacity = 36;
    public const int ToolbarCount = 12;

    [SerializeField] private int unlockedSlots = 12;
    [SerializeField] private List<InventorySlot> slots = new();
    [SerializeField] private int selectedIndex;
    [SerializeField] private bool isOpen;

    public int TotalCapacity => MaxCapacity;
    public int UnlockedSlots => Mathf.Clamp(unlockedSlots, ToolbarCount, MaxCapacity);
    public void SetUnlockedSlots(int count)
    {
        int next = Mathf.Clamp(count <= 0 ? ToolbarCount : count, ToolbarCount, MaxCapacity);
        if (next == unlockedSlots) return;
        unlockedSlots = next;
        EnsureCapacity();
        for (int i = 0; i < MaxCapacity; i++) SlotChanged?.Invoke(i, slots[i]);
    }
    public int SelectedIndex => selectedIndex;
    public bool IsOpen => isOpen;
    public IReadOnlyItemSlot SelectedSlot => (selectedIndex >= 0 && selectedIndex < slots.Count) ? slots[selectedIndex] : null;
    public InventorySlot SelectedItem => slots.Count == 0 ? null : slots[Mathf.Clamp(selectedIndex, 0, slots.Count - 1)];
    internal ItemData LeftLightRing { get; private set; }
    internal ItemData RightLightRing { get; private set; }

    public event Action<int, IReadOnlyItemSlot> SlotChanged;
    public event Action<int> SelectionChanged;
    public event Action<bool> OpenChanged;
    public event Action<bool> VisibilityChanged; // Backward compatibility

    private void Awake()
    {
        if (StartupSession.Api == null)
            unlockedSlots = Mathf.Clamp(PlayerPrefs.GetInt("BackpackSlots.Local", unlockedSlots),
                ToolbarCount, MaxCapacity);
        EnsureCapacity();
        SyncAllSlots();
        SlotChanged += ValidateLightRings;
    }

    internal bool SetLightRing(bool right, int backpackSlot)
    {
        if (IsLocked || backpackSlot >= UnlockedSlots) return false;
        ItemData item = null;
        if (backpackSlot >= 0)
        {
            var slot = GetSlot(backpackSlot);
            if (slot == null || slot.IsEmpty || slot.Data == null) return false;
            item = slot.Data;
            int id = WorldLightingController.LightRingId(item.itemId);
            if (id == 0) return false;
            ItemData other = right ? LeftLightRing : RightLightRing;
            if (id == WorldLightingController.LightRingId(other?.itemId) && CountLightRings(id) < 2)
                return false;
        }

        if (right) RightLightRing = item;
        else LeftLightRing = item;
        return true;
    }

    private void ValidateLightRings(int _, IReadOnlyItemSlot __)
    {
        int left = WorldLightingController.LightRingId(LeftLightRing?.itemId);
        int right = WorldLightingController.LightRingId(RightLightRing?.itemId);
        int leftCount = left == 0 ? 0 : CountLightRings(left);
        if (leftCount == 0) LeftLightRing = null;
        if (right != 0 && (right == left ? leftCount < 2 : CountLightRings(right) == 0))
            RightLightRing = null;
    }

    private int CountLightRings(int id)
    {
        int count = 0;
        for (int i = 0; i < UnlockedSlots && i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot != null && !slot.IsEmpty && WorldLightingController.LightRingId(slot.Data?.itemId) == id)
                count += slot.Amount;
        }
        return count;
    }

    public void EnsureCapacity()
    {
        while (slots.Count < MaxCapacity)
        {
            slots.Add(new InventorySlot());
        }
    }

    private void SyncAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i]?.SyncFromData();
        }
    }

    public IReadOnlyItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public bool IsLocked { get; set; }

    public void ReplaceFromServer(OnlineStack[] serverSlots, Func<string, ItemData> resolveItem)
    {
        EnsureCapacity();
        for (int i = 0; i < MaxCapacity; i++)
        {
            var slot = slots[i];
            slot.itemData = null;
            slot.displayName = string.Empty;
            slot.inventoryIcon = null;
            slot.amount = 0;
        }
        if (serverSlots != null)
        {
            foreach (var entry in serverSlots)
            {
                if (entry == null || entry.slot < 0 || entry.slot >= MaxCapacity || entry.amount <= 0) continue;
                var item = resolveItem(entry.itemId);
                if (item == null)
                {
                    Debug.LogWarning("Unknown server item: " + entry.itemId);
                    continue;
                }
                var slot = slots[entry.slot];
                slot.itemData = item;
                slot.amount = entry.amount;
                slot.SyncFromData();
            }
        }
        for (int i = 0; i < MaxCapacity; i++) SlotChanged?.Invoke(i, slots[i]);
        SelectionChanged?.Invoke(selectedIndex);
    }

    public void SelectSlot(int index)
    {
        if (IsLocked) return;
        index = Mathf.Clamp(index, 0, ToolbarCount - 1);
        if (selectedIndex != index)
        {
            selectedIndex = index;
            SelectionChanged?.Invoke(selectedIndex);
        }
    }

    public bool SwapSlots(int fromIndex, int toIndex)
    {
        if (IsLocked) return false;
        if (fromIndex < 0 || fromIndex >= UnlockedSlots || toIndex < 0 || toIndex >= UnlockedSlots || fromIndex == toIndex)
        {
            return false;
        }

        var a = slots[fromIndex];
        var b = slots[toIndex];

        // Check stacking
        if (!a.IsEmpty && !b.IsEmpty && a.Data != null && b.Data != null && b.Data.CanStackWith(a.Data))
        {
            int space = b.Data.maxStackSize - b.amount;
            int add = Mathf.Min(space, a.amount);
            b.amount += add;
            a.amount -= add;
            if (a.amount <= 0)
            {
                a.itemData = null;
                a.displayName = "";
                a.inventoryIcon = null;
            }
        }
        else
        {
            slots[fromIndex] = b;
            slots[toIndex] = a;
        }

        SlotChanged?.Invoke(fromIndex, slots[fromIndex]);
        SlotChanged?.Invoke(toIndex, slots[toIndex]);

        if (fromIndex == selectedIndex || toIndex == selectedIndex)
        {
            SelectionChanged?.Invoke(selectedIndex);
        }

        return true;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (!CanAccept(item, amount)) return false;
        EnsureCapacity();

        // 1. Stack into existing slots
        if (item.maxStackSize > 1)
        {
            for (int i = 0; i < UnlockedSlots; i++)
            {
                var s = slots[i];
                if (!s.IsEmpty && s.Data != null && s.Data.CanStackWith(item) && s.amount < item.maxStackSize)
                {
                    int space = item.maxStackSize - s.amount;
                    int add = Mathf.Min(space, amount);
                    s.amount += add;
                    amount -= add;
                    SlotChanged?.Invoke(i, s);
                    if (amount <= 0) return true;
                }
            }
        }

        // 2. Add to first empty slot
        for (int i = 0; i < UnlockedSlots; i++)
        {
            var s = slots[i];
            if (s.IsEmpty)
            {
                int toPut = Mathf.Min(amount, item.maxStackSize);
                s.itemData = item;
                s.amount = toPut;
                s.SyncFromData();
                amount -= toPut;
                SlotChanged?.Invoke(i, s);
                if (amount <= 0) return true;
            }
        }

        return amount <= 0;
    }

    // Debris checks inventory acceptance before it starts moving toward the farmer.
    public bool CanAccept(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0 || item.maxStackSize <= 0) return false;
        EnsureCapacity();
        int available = 0;
        for (int i = 0; i < UnlockedSlots; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty) available += item.maxStackSize;
            else if (slot.Data != null && slot.Data.CanStackWith(item))
                available += Mathf.Max(0, item.maxStackSize - slot.amount);
            if (available >= amount) return true;
        }
        return false;
    }

    public bool ConsumeSelected(int amount = 1)
    {
        var item = SelectedItem;
        if (item == null || item.IsEmpty || item.amount < amount) return false;
        item.amount -= amount;
        if (item.amount <= 0)
        {
            item.itemData = null;
            item.displayName = "";
            item.inventoryIcon = null;
        }
        SlotChanged?.Invoke(selectedIndex, item);
        SelectionChanged?.Invoke(selectedIndex);
        return true;
    }

    public bool ConsumeSelected(PlayerItemKind expectedKind, int amount = 1)
    {
        var item = SelectedItem;
        if (item == null || item.kind != expectedKind || item.amount < amount) return false;
        return ConsumeSelected(amount);
    }

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        EnsureCapacity();
        string id = itemId.ToLowerInvariant();
        int remaining = amount;
        for (int i = 0; i < UnlockedSlots; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty && s.Data != null && s.Data.itemId.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                int toTake = Mathf.Min(remaining, s.amount);
                s.amount -= toTake;
                remaining -= toTake;
                if (s.amount <= 0)
                {
                    s.itemData = null;
                    s.displayName = "";
                    s.inventoryIcon = null;
                    s.amount = 0;
                }
                SlotChanged?.Invoke(i, s);
                if (remaining <= 0) break;
            }
        }
        return remaining <= 0;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= UnlockedSlots) return;
        var s = slots[index];
        if (s == null) return;
        s.itemData = null;
        s.displayName = "";
        s.inventoryIcon = null;
        s.amount = 0;
        SlotChanged?.Invoke(index, s);
        if (index == selectedIndex)
            SelectionChanged?.Invoke(selectedIndex);
    }

    public void SetOpen(bool value)
    {
        if (isOpen == value) return;
        isOpen = value;
        OpenChanged?.Invoke(isOpen);
        VisibilityChanged?.Invoke(isOpen);
    }

    public void ToggleOpen() => SetOpen(!isOpen);
    public void Toggle() => ToggleOpen();
}
