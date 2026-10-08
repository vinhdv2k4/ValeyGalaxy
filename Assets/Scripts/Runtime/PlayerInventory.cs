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
            inventoryIcon = itemData.icon;
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
    public int SelectedIndex => selectedIndex;
    public bool IsOpen => isOpen;
    public IReadOnlyItemSlot SelectedSlot => (selectedIndex >= 0 && selectedIndex < slots.Count) ? slots[selectedIndex] : null;
    public InventorySlot SelectedItem => slots.Count == 0 ? null : slots[Mathf.Clamp(selectedIndex, 0, slots.Count - 1)];

    public event Action<int, IReadOnlyItemSlot> SlotChanged;
    public event Action<int> SelectionChanged;
    public event Action<bool> OpenChanged;
    public event Action<bool> VisibilityChanged; // Backward compatibility

    private void Awake()
    {
        EnsureCapacity();
        SyncAllSlots();
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
        if (item == null || amount <= 0 || item.maxStackSize <= 0) return false;
        EnsureCapacity();

        // World drops retry collection when the bag is full. Reserve enough
        // capacity before mutating slots so a failed attempt cannot duplicate
        // part of a stack on the next retry.
        int available = 0;
        for (int i = 0; i < UnlockedSlots; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) available += item.maxStackSize;
            else if (slot.Data != null && slot.Data.CanStackWith(item))
                available += Mathf.Max(0, item.maxStackSize - slot.amount);
        }
        if (available < amount) return false;

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
