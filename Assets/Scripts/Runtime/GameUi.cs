/// <summary>
/// The one place that knows which menus take over the screen. Gameplay asks here instead of
/// listing every menu itself, so a new menu is added in one spot and nothing forgets it.
/// </summary>
internal static class GameUi
{
    /// <summary>
    /// A modal menu or dialog is open: the farmer stands still and world clicks are ignored.
    /// The farmer's own bag and farmhouse chests are checked separately by their owners.
    /// </summary>
    internal static bool IsModalOpen =>
        NpcConversation.IsOpen || NpcShop.IsOpen || MineElevatorUI.IsOpen ||
        InteractableTV.IsMenuOpen || FarmShippingBin.IsMenuOpen ||
        CalendarBillboard.IsOpen || JojaMorrisDesk.IsOpen;

    /// <summary><see cref="IsModalOpen"/> or the full inventory screen.</summary>
    internal static bool IsMenuOrInventoryOpen => IsModalOpen || FullInventoryMenuUI.IsOpen;

    /// <summary>Closes the menus a night or a knockout interrupts, in the order they stack.</summary>
    internal static void CloseMenus()
    {
        FarmShippingBin.CloseMenu();
        InteractableTV.CloseMenu();
        NpcShop.CloseMenu();
        if (MineElevatorUI.IsOpen) MineElevatorUI.Close();
        if (NpcConversation.IsOpen) NpcConversation.Close();
    }
}
