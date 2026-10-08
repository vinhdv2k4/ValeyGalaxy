using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SRP & DIP: Handles the on-screen 12-slot HUD toolbar.
/// Implements Stardew Valley mechanics:
/// - 12 horizontal slots with key bindings 1..9, 0, -, =
/// - Mouse wheel cycle selection
/// - Screen repositioning / flip if player walks to the bottom of the screen.
/// </summary>
public class ToolbarUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private RectTransform toolbarPanel;

    [Header("Sprite Assets")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Sprite lockedSlotSprite;

    [Header("Smart Screen Flip Settings (Stardew Toolbar.cs:126-158)")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float bottomFlipThreshold = 0.22f; // Viewport Y threshold
    [SerializeField] private Vector2 bottomAnchorPosition = new Vector2(0.5f, 0.06f);
    [SerializeField] private Vector2 topAnchorPosition = new Vector2(0.5f, 0.94f);

    private readonly List<InventorySlotUI> slotViews = new();
    private IReadOnlyInventory readOnlyInventory;
    private IInventoryController inventoryController;

    private void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (playerTransform == null && playerInventory != null)
        {
            playerTransform = playerInventory.transform;
        }

        if (playerInventory != null)
        {
            BindInventory(playerInventory, playerInventory);
        }
    }

    public void BindInventory(IReadOnlyInventory readOnly, IInventoryController controller)
    {
        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged -= OnSlotChanged;
            readOnlyInventory.SelectionChanged -= OnSelectionChanged;
        }

        readOnlyInventory = readOnly;
        inventoryController = controller;

        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged += OnSlotChanged;
            readOnlyInventory.SelectionChanged += OnSelectionChanged;
            CreateToolbarSlots();
            RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (readOnlyInventory != null)
        {
            readOnlyInventory.SlotChanged -= OnSlotChanged;
            readOnlyInventory.SelectionChanged -= OnSelectionChanged;
        }
    }

    private void CreateToolbarSlots()
    {
        // Clear old children if any
        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }
        slotViews.Clear();

        for (int i = 0; i < InventoryModel.ToolbarSlotCount; i++)
        {
            var slotUI = Instantiate(slotPrefab, slotsContainer);
            slotUI.Initialize(i, normalSlotSprite, selectedSlotSprite, lockedSlotSprite);
            slotUI.Clicked += OnSlotClicked;
            slotViews.Add(slotUI);
        }
    }

    private void Update()
    {
        HandleKeyboardAndScrollInput();
        HandleSmartScreenFlip();
    }

    private void HandleKeyboardAndScrollInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && inventoryController != null)
        {
            // Number keys 1..9
            for (int i = 0; i < 9; i++)
            {
                var key = (Key)((int)Key.Digit1 + i);
                if (keyboard[key].wasPressedThisFrame)
                {
                    inventoryController.SelectSlot(i);
                    return;
                }
            }

            // Key '0' -> slot 9
            if (keyboard[Key.Digit0].wasPressedThisFrame)
            {
                inventoryController.SelectSlot(9);
                return;
            }

            // Key '-' -> slot 10
            if (keyboard[Key.Minus].wasPressedThisFrame)
            {
                inventoryController.SelectSlot(10);
                return;
            }

            // Key '=' -> slot 11
            if (keyboard[Key.Equals].wasPressedThisFrame)
            {
                inventoryController.SelectSlot(11);
                return;
            }
        }

        // Mouse scroll wheel
        var mouse = Mouse.current;
        if (mouse != null && inventoryController != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0.1f)
            {
                CycleSelection(-1);
            }
            else if (scrollY < -0.1f)
            {
                CycleSelection(1);
            }
        }
    }

    private void CycleSelection(int direction)
    {
        if (readOnlyInventory == null || inventoryController == null) return;
        int next = (readOnlyInventory.SelectedIndex + direction) % InventoryModel.ToolbarSlotCount;
        if (next < 0) next += InventoryModel.ToolbarSlotCount;
        inventoryController.SelectSlot(next);
    }

    private void HandleSmartScreenFlip()
    {
        if (playerTransform == null || targetCamera == null || toolbarPanel == null) return;

        Vector3 viewportPos = targetCamera.WorldToViewportPoint(playerTransform.position);
        bool shouldFlipToTop = viewportPos.y < bottomFlipThreshold;

        Vector2 targetAnchor = shouldFlipToTop ? topAnchorPosition : bottomAnchorPosition;
        toolbarPanel.anchorMin = targetAnchor;
        toolbarPanel.anchorMax = targetAnchor;
        toolbarPanel.anchoredPosition = Vector2.zero;
    }

    private void OnSlotClicked(int index)
    {
        inventoryController?.SelectSlot(index);
    }

    private void OnSlotChanged(int index, IReadOnlyItemSlot slot)
    {
        if (index < slotViews.Count)
        {
            bool isSelected = readOnlyInventory != null && readOnlyInventory.SelectedIndex == index;
            bool isLocked = readOnlyInventory != null && index >= readOnlyInventory.UnlockedSlots;
            slotViews[index].Render(slot, isSelected, isLocked);
        }
    }

    private void OnSelectionChanged(int newSelectedIndex)
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (readOnlyInventory == null) return;

        for (int i = 0; i < slotViews.Count; i++)
        {
            var slot = readOnlyInventory.GetSlot(i);
            bool isSelected = readOnlyInventory.SelectedIndex == i;
            bool isLocked = i >= readOnlyInventory.UnlockedSlots;
            slotViews[i].Render(slot, isSelected, isLocked);
        }
    }
}
