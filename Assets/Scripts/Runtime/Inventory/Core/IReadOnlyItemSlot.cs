using UnityEngine;

/// <summary>
/// ISP: Read-only contract for an item slot.
/// UI only requires observation of what is currently stored.
/// </summary>
public interface IReadOnlyItemSlot
{
    ItemData Data { get; }
    int Amount { get; }
    bool IsEmpty { get; }
}
