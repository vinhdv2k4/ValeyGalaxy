using System;
using UnityEngine;

/// <summary>
/// Mutable item slot holding data and stack count.
/// </summary>
[Serializable]
public sealed class ItemSlot : IReadOnlyItemSlot
{
    [SerializeField] private ItemData data;
    [SerializeField] private int amount;

    public ItemData Data => data;
    public int Amount => amount;
    public bool IsEmpty => data == null || amount <= 0;

    public ItemSlot()
    {
        data = null;
        amount = 0;
    }

    public ItemSlot(ItemData itemData, int count)
    {
        Set(itemData, count);
    }

    public void Set(ItemData itemData, int count)
    {
        data = itemData;
        amount = itemData != null ? Mathf.Max(0, count) : 0;
        if (amount == 0)
        {
            data = null;
        }
    }

    public void Clear()
    {
        data = null;
        amount = 0;
    }

    public int AddAmount(int toAdd)
    {
        if (data == null) return toAdd;
        int space = data.maxStackSize - amount;
        int added = Mathf.Min(space, toAdd);
        amount += added;
        return toAdd - added;
    }

    public bool RemoveAmount(int toRemove)
    {
        if (amount < toRemove) return false;
        amount -= toRemove;
        if (amount <= 0)
        {
            Clear();
        }
        return true;
    }
}
