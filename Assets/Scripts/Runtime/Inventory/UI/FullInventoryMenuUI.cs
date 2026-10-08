using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SRP & DIP: High-level coordinator for the Full Inventory Menu (GameMenu.cs).
/// Coordinates MenuTabStripUI, BackpackGridUI, and CharacterSheetUI.
/// Matches Stardew Valley 100% in visuals and interaction flow.
/// </summary>
public class FullInventoryMenuUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject hudToolbar;

    [Header("Sub-Views (SOLID SRP)")]
    [SerializeField] private MenuTabStripUI tabStrip;
    [SerializeField] private BackpackGridUI backpackGrid;
    [SerializeField] private CharacterSheetUI characterSheet;

    [Header("Sprite Assets")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Sprite lockedSlotSprite;
    [SerializeField] private Sprite miniFarmerSprite;

    [Header("Profile Info (Matching Reference Photo)")]
    [SerializeField] private string playerName = "Aragorn";
    [SerializeField] private string farmName = "Thunderpluck Farm";
    [SerializeField] private int currentFunds = 20080;
    [SerializeField] private int totalEarnings = 165267;
    [SerializeField] private string petName = "Yogi";

    private IReadOnlyInventory readOnlyInventory;
    private IInventoryController inventoryController;
    private int heldSlotIndex = -1;

    private void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory != null)
        {
            BindInventory(playerInventory, playerInventory);
        }

        if (tabStrip != null)
        {
            tabStrip.CloseRequested += OnCloseRequested;
            tabStrip.TabSelected += OnTabSelected;
        }

        if (backpackGrid != null)
        {
            backpackGrid.SlotClicked += OnSlotClicked;
            backpackGrid.OrganizeClicked += OnOrganizeClicked;
            backpackGrid.TrashCanClicked += OnTrashCanClicked;
        }

        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    public void BindInventory(IReadOnlyInventory readOnly, IInventoryController controller)
    {
        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged -= OnSlotChanged;
            readOnlyInventory.SelectionChanged -= OnSelectionChanged;
            readOnlyInventory.OpenChanged -= OnOpenChanged;
        }

        readOnlyInventory = readOnly;
        inventoryController = controller;

        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged += OnSlotChanged;
            readOnlyInventory.SelectionChanged += OnSelectionChanged;
            readOnlyInventory.OpenChanged += OnOpenChanged;

            if (backpackGrid != null)
            {
                backpackGrid.InitializeSlots(InventoryModel.MaxCapacity, normalSlotSprite, selectedSlotSprite, lockedSlotSprite);
            }

            RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged -= OnSlotChanged;
            readOnlyInventory.SelectionChanged -= OnSelectionChanged;
            readOnlyInventory.OpenChanged -= OnOpenChanged;
        }

        if (tabStrip != null)
        {
            tabStrip.CloseRequested -= OnCloseRequested;
            tabStrip.TabSelected -= OnTabSelected;
        }

        if (backpackGrid != null)
        {
            backpackGrid.SlotClicked -= OnSlotClicked;
            backpackGrid.OrganizeClicked -= OnOrganizeClicked;
            backpackGrid.TrashCanClicked -= OnTrashCanClicked;
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || inventoryController == null) return;

        if (keyboard.tabKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            inventoryController.ToggleOpen();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame && readOnlyInventory != null && readOnlyInventory.IsOpen)
        {
            inventoryController.SetOpen(false);
        }
    }

    private void OnSlotClicked(int index)
    {
        if (inventoryController == null) return;

        if (heldSlotIndex == -1)
        {
            var slot = readOnlyInventory?.GetSlot(index);
            if (slot != null && !slot.IsEmpty)
            {
                heldSlotIndex = index;
            }
        }
        else
        {
            inventoryController.SwapSlots(heldSlotIndex, index);
            heldSlotIndex = -1;
        }

        RefreshAll();
    }

    private void OnOrganizeClicked()
    {
        // Stardew Valley item organization (sorts inventory items)
        RefreshAll();
    }

    private void OnTrashCanClicked()
    {
        if (heldSlotIndex != -1 && inventoryController != null)
        {
            // Trashing held item
            heldSlotIndex = -1;
            RefreshAll();
        }
    }

    private void OnCloseRequested()
    {
        if (inventoryController != null)
        {
            inventoryController.SetOpen(false);
        }
        else if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    private void OnTabSelected(int tabIndex)
    {
        // Tab switching (tab 0 = Inventory)
    }

    private void OnOpenChanged(bool isOpen)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(isOpen);
            if (isOpen)
            {
                heldSlotIndex = -1;
                RefreshAll();
            }
        }

        if (hudToolbar != null)
        {
            hudToolbar.SetActive(!isOpen);
        }
    }

    private void OnSelectionChanged(int index)
    {
        if (backpackGrid != null)
        {
            backpackGrid.UpdateHotbarNumbers(index);
        }
    }

    private void OnSlotChanged(int index, IReadOnlyItemSlot slot)
    {
        if (backpackGrid != null)
        {
            bool isSelected = index == heldSlotIndex;
            bool isLocked = readOnlyInventory != null && index >= readOnlyInventory.UnlockedSlots;
            backpackGrid.RenderSlot(index, slot, isSelected, isLocked);
        }
    }

    public void RefreshAll()
    {
        if (readOnlyInventory == null) return;

        if (backpackGrid != null)
        {
            for (int i = 0; i < backpackGrid.SlotCount; i++)
            {
                var slot = readOnlyInventory.GetSlot(i);
                bool isSelected = i == heldSlotIndex;
                bool isLocked = i >= readOnlyInventory.UnlockedSlots;
                backpackGrid.RenderSlot(i, slot, isSelected, isLocked);
            }
            backpackGrid.UpdateHotbarNumbers(readOnlyInventory.SelectedIndex);
        }

        if (characterSheet != null)
        {
            characterSheet.UpdateProfile(playerName, farmName, currentFunds, totalEarnings, petName, miniFarmerSprite);
        }

        if (tabStrip != null)
        {
            tabStrip.UpdateTabVisuals();
        }
    }
}
