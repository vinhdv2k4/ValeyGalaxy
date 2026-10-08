using System;

/// <summary>
/// ISP: Read-only contract for inventory state and events.
/// Decouples visual presentation (HUD toolbar, pause menu, inspection) from inventory mutation logic.
/// </summary>
public interface IReadOnlyInventory
{
    int TotalCapacity { get; }
    int UnlockedSlots { get; }
    int SelectedIndex { get; }
    bool IsOpen { get; }
    IReadOnlyItemSlot SelectedSlot { get; }

    IReadOnlyItemSlot GetSlot(int index);

    event Action<int, IReadOnlyItemSlot> SlotChanged;
    event Action<int> SelectionChanged;
    event Action<bool> OpenChanged;
}
