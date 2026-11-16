using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SRP: Pure data and domain logic for player inventory.
/// No UI dependencies. Fully testable and decoupled.
/// </summary>
[Serializable]
public class InventoryModel : IReadOnlyInventory, IInventoryController
{
    public const int MaxCapacity = 36;
    public const int ToolbarSlotCount = 12;

    [SerializeField] private int unlockedSlots = 12;
    [SerializeField] private int selectedIndex = 0;
    [SerializeField] private List<ItemSlot> slots = new();
    private bool isOpen = false;

    public int TotalCapacity => MaxCapacity;
    public int UnlockedSlots => Mathf.Clamp(unlockedSlots, ToolbarSlotCount, MaxCapacity);
    public int SelectedIndex => selectedIndex;
    public bool IsOpen => isOpen;

    public IReadOnlyItemSlot SelectedSlot => (selectedIndex >= 0 && selectedIndex < slots.Count) ? slots[selectedIndex] : null;

    public event Action<int, IReadOnlyItemSlot> SlotChanged;
    public event Action<int> SelectionChanged;
    public event Action<bool> OpenChanged;

    public InventoryModel()
    {
        EnsureCapacity();
    }

    public void EnsureCapacity()
    {
        while (slots.Count < MaxCapacity)
        {
            slots.Add(new ItemSlot());
        }
    }

    public IReadOnlyItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public ItemSlot GetMutableSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public void SelectSlot(int index)
    {
        index = Mathf.Clamp(index, 0, ToolbarSlotCount - 1);
        if (selectedIndex != index)
        {
            selectedIndex = index;
            SelectionChanged?.Invoke(selectedIndex);
        }
    }

    public void CycleSelection(int direction)
    {
        int next = (selectedIndex + direction) % ToolbarSlotCount;
        if (next < 0) next += ToolbarSlotCount;
        SelectSlot(next);
    }

    public bool SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= UnlockedSlots || toIndex < 0 || toIndex >= UnlockedSlots || fromIndex == toIndex)
        {
            return false;
        }

        var slotA = slots[fromIndex];
        var slotB = slots[toIndex];

        // If both have same stackable item, try stacking
        if (!slotA.IsEmpty && !slotB.IsEmpty && slotB.Data.CanStackWith(slotA.Data))
        {
            int remaining = slotB.AddAmount(slotA.Amount);
            if (remaining == 0)
            {
                slotA.Clear();
            }
            else
            {
                slotA.Set(slotA.Data, remaining);
            }
        }
        else
        {
            // Simple swap
            var tempData = slotA.Data;
            var tempCount = slotA.Amount;
            slotA.Set(slotB.Data, slotB.Amount);
            slotB.Set(tempData, tempCount);
        }

        SlotChanged?.Invoke(fromIndex, slotA);
        SlotChanged?.Invoke(toIndex, slotB);

        if (fromIndex == selectedIndex || toIndex == selectedIndex)
        {
            SelectionChanged?.Invoke(selectedIndex);
        }

        return true;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        EnsureCapacity();

        // 1. Stack into existing slots first
        if (item.maxStackSize > 1)
        {
            for (int i = 0; i < UnlockedSlots; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Data.CanStackWith(item) && slots[i].Amount < item.maxStackSize)
                {
                    amount = slots[i].AddAmount(amount);
                    SlotChanged?.Invoke(i, slots[i]);
                    if (amount <= 0) return true;
                }
            }
        }

        // 2. Put into first empty slot
        for (int i = 0; i < UnlockedSlots; i++)
        {
            if (slots[i].IsEmpty)
            {
                int toPut = Mathf.Min(amount, item.maxStackSize);
                slots[i].Set(item, toPut);
                amount -= toPut;
                SlotChanged?.Invoke(i, slots[i]);
                if (amount <= 0) return true;
            }
        }

        return amount <= 0;
    }

    public bool ConsumeSelected(int amount = 1)
    {
        if (selectedIndex < 0 || selectedIndex >= slots.Count) return false;
        var slot = slots[selectedIndex];
        if (slot.IsEmpty || slot.Amount < amount) return false;

        bool removed = slot.RemoveAmount(amount);
        if (removed)
        {
            SlotChanged?.Invoke(selectedIndex, slot);
            SelectionChanged?.Invoke(selectedIndex);
        }
        return removed;
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
                int toTake = Mathf.Min(remaining, s.Amount);
                s.RemoveAmount(toTake);
                remaining -= toTake;
                SlotChanged?.Invoke(i, s);
                if (remaining <= 0) break;
            }
        }
        return remaining <= 0;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= UnlockedSlots) return;
        slots[index].Clear();
        SlotChanged?.Invoke(index, slots[index]);
    }

    public void UnlockBackpack(int newCount)
    {
        unlockedSlots = Mathf.Clamp(newCount, ToolbarSlotCount, MaxCapacity);
        for (int i = 0; i < MaxCapacity; i++)
        {
            SlotChanged?.Invoke(i, slots[i]);
        }
    }

    public void SetOpen(bool open)
    {
        if (isOpen != open)
        {
            isOpen = open;
            OpenChanged?.Invoke(isOpen);
        }
    }

    public void ToggleOpen() => SetOpen(!isOpen);
}
