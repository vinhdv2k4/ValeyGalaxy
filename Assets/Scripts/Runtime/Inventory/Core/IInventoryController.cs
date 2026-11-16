/// <summary>
/// ISP: Controller interface for mutating inventory contents and selection.
/// </summary>
public interface IInventoryController
{
    void SelectSlot(int index);
    bool SwapSlots(int fromIndex, int toIndex);
    bool ConsumeSelected(int amount = 1);
    bool AddItem(ItemData item, int amount = 1);
    bool ConsumeItem(string itemId, int amount = 1);
    void ClearSlot(int index);
    void SetOpen(bool open);
    void ToggleOpen();
}
