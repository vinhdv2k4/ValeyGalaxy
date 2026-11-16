using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SRP & DIP: High-level coordinator for the Full Inventory Menu (GameMenu.cs).
/// Coordinates MenuTabStripUI, BackpackGridUI, and CharacterSheetUI.
/// Routes the GameMenu tab order to the available runtime pages.
/// </summary>
public class FullInventoryMenuUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerInventory playerInventory;
    private OnlineGameState playerOnline;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject hudToolbar;

    [Header("Sub-Views (SOLID SRP)")]
    [SerializeField] private MenuTabStripUI tabStrip;
    [SerializeField] private BackpackGridUI backpackGrid;
    [SerializeField] private CharacterSheetUI characterSheet;
    [SerializeField] private Texture2D worldMap;
    [SerializeField] private Texture2D secondaryCursors;
    [SerializeField] private TextAsset fishDefinitions;
    [SerializeField] private TextAsset cookingRecipes;
    [SerializeField] private Texture2D craftablesAtlas;
    [SerializeField] private Texture2D objectsAtlas;
    [SerializeField] private TextAsset craftingRecipes;

    [Header("Sprite Assets")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Sprite lockedSlotSprite;
    [SerializeField] private Sprite miniFarmerSprite;

    [Header("Profile Info (Matching Reference Photo)")]
    [SerializeField] private string playerName = "";
    [SerializeField] private string farmName = "";
    [SerializeField] private int currentFunds;
    public int CurrentFunds => currentFunds;
    public Texture2D CraftablesAtlas => craftablesAtlas;
    public Texture2D ObjectsAtlas => objectsAtlas;
    internal TextAsset CraftingRecipes => craftingRecipes;
    internal TextAsset CookingRecipes => cookingRecipes;
    public long TotalEarnings => totalEarnings;
    public string PlayerName => playerName;
    public string FarmName => farmName;
    internal Vector2Int? PlayerMapTile => playerInventory == null ? null :
        NpcPathfinder.WorldToTile(playerInventory.transform.position);
    public string PetName => petName;
    internal bool HasHeldItem => heldSlotIndex >= 0;
    internal ItemData EquippedLightRing(bool right) => playerInventory == null ? null :
        right ? playerInventory.RightLightRing : playerInventory.LeftLightRing;

    internal void ClickLightRingSlot(bool right)
    {
        if (playerInventory == null || inventoryChangePending || !playerInventory.SetLightRing(right, heldSlotIndex))
            return;
        heldSlotIndex = -1;
        RefreshAll();
    }
    public static FullInventoryMenuUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        currentFunds = 0;
    }

    public void SetServerFunds(long amount)
    {
        currentFunds = amount < 0 ? 0 : amount > int.MaxValue ? int.MaxValue : (int)amount;
        RefreshAll();
    }
    public void SetServerTotalEarnings(long amount)
    {
        totalEarnings = amount < 0 ? 0 : amount;
        RefreshAll();
    }
    [SerializeField] private long totalEarnings;
    [SerializeField] private string petName = "";

    private IReadOnlyInventory readOnlyInventory;
    private IInventoryController inventoryController;
    private int heldSlotIndex = -1;
    private bool inventoryChangePending;
    private GameMenuPagesUI pages;
    private GameObject inventoryFrame;

    private void Start()
    {
        CreatedFarmer farmer = StartupSession.Farmer;
        if (farmer != null)
        {
            playerName = farmer.name ?? "";
            farmName = farmer.farmName ?? "";
        }
        IsOpen = menuRoot != null && menuRoot.activeSelf;
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory != null)
        {
            BindInventory(playerInventory, playerInventory);
            characterSheet?.BindPortrait(playerInventory.GetComponent<PlayerAppearance>());
        }

        if (tabStrip != null)
        {
            tabStrip.CloseRequested += OnCloseRequested;
            tabStrip.TabSelected += OnTabSelected;
        }

        if (menuRoot != null)
        {
            pages = menuRoot.GetComponent<GameMenuPagesUI>();
            if (pages == null) pages = menuRoot.AddComponent<GameMenuPagesUI>();
            pages.Initialize(menuRoot.GetComponent<RectTransform>(), worldMap,
                craftablesAtlas, objectsAtlas, craftingRecipes,
                readOnlyInventory, OnCloseRequested);
            pages.BindMenuControls(tabStrip, secondaryCursors, fishDefinitions, cookingRecipes);
            characterSheet?.UseBitmapView();
            backpackGrid?.UseBitmapNumbers();
            SetupInventoryFrame();
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

    private void SetupInventoryFrame()
    {
        Transform frame = menuRoot.transform.Find("MenuFrame");
        Texture2D atlas = StardewUiAtlas.MenuTiles;
        if (frame == null || atlas == null || !frame.TryGetComponent(out UnityEngine.UI.Image bakedFrame)) return;
        inventoryFrame = frame.gameObject;
        bakedFrame.enabled = false;

        // Relative to the visible top of the box.
        const float width = 864f;
        const float height = 616f;
        AddTile(9, 28, 28, width - 64, height - 64);
        AddTile(0, 0, 0, 64, 64);
        AddTile(2, 64, 0, width - 128, 64);
        AddTile(3, width - 64, 0, 64, 64);
        AddTile(8, 0, 64, 64, height - 128);
        AddTile(11, width - 64, 64, 64, height - 128);
        AddTile(12, 0, height - 64, 64, 64);
        AddTile(14, 64, height - 64, width - 128, 64);
        AddTile(15, width - 64, height - 64, 64, 64);
        AddTile(4, 0, 264, 64, 64);
        AddTile(6, 64, 264, width - 128, 64);
        AddTile(7, width - 64, 264, 64, 64);

        void AddTile(int tile, float x, float y, float w, float h)
        {
            var view = new GameObject($"Menu tile {tile}", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            var rect = (RectTransform)view.transform;
            rect.SetParent(frame, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            var image = view.GetComponent<UnityEngine.UI.RawImage>();
            image.texture = atlas;
            image.uvRect = StardewUiAtlas.ToUvMenuTiles(StardewUiAtlas.MenuTileRect(tile));
            image.raycastTarget = false;
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

            pages?.BindInventory(readOnlyInventory);
            RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsOpen = false;
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
        if (IsOpen && backpackGrid != null)
        {
            // Looked up once per bag instead of every frame the menu is open.
            if (playerOnline == null && playerInventory != null) playerOnline = playerInventory.GetComponent<OnlineGameState>();
            backpackGrid.UpdateTrashCan(playerOnline != null && playerOnline.Snapshot != null ? playerOnline.Snapshot.trashCanLevel : 0,
                heldSlotIndex != -1);
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
            readOnlyInventory != null && readOnlyInventory.IsOpen)
        {
            if (heldSlotIndex != -1)
            {
                heldSlotIndex = -1;
                RefreshAll();
                return;
            }
            inventoryController?.SetOpen(false);
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && heldSlotIndex != -1)
        {
            heldSlotIndex = -1;
            RefreshAll();
        }
    }

    private void OnGUI()
    {
        if (!IsOpen || heldSlotIndex < 0 || readOnlyInventory == null) return;
        var slot = readOnlyInventory.GetSlot(heldSlotIndex);
        if (slot == null || slot.IsEmpty)
        {
            heldSlotIndex = -1;
            return;
        }

        Sprite icon = (slot is InventorySlot inv && inv.inventoryIcon != null)
            ? inv.inventoryIcon : slot.Data?.GetIcon();
        if (icon == null || icon.texture == null) return;

        Vector2 mousePos = Event.current.mousePosition;
        Rect iconRect = new Rect(mousePos.x + 12f, mousePos.y + 12f, 48f, 48f);

        GUI.depth = -1000;
        Rect texRect = icon.rect;
        Texture2D tex = icon.texture;
        Rect uv = new Rect(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);
        GUI.DrawTextureWithTexCoords(iconRect, tex, uv);

        StardewUiAtlas.DrawStackCount(iconRect, slot.Amount, slot.Data?.itemId);
    }

    private void OnSlotClicked(int index)
    {
        if (inventoryController == null || inventoryChangePending) return;
        if (readOnlyInventory != null && index >= readOnlyInventory.UnlockedSlots) return;

        if (heldSlotIndex == -1)
        {
            var slot = readOnlyInventory?.GetSlot(index);
            if (slot != null && !slot.IsEmpty)
            {
                heldSlotIndex = index;
            }
        }
        else if (heldSlotIndex == index)
        {
            heldSlotIndex = -1;
        }
        else
        {
            var online = playerInventory == null ? null : playerInventory.GetComponent<OnlineGameState>();
            if (online == null) { NpcConversation.ShowNotice("Đăng nhập để thay đổi túi đồ."); return; }
            inventoryChangePending = true;
            online.SwapInventory(heldSlotIndex, index, error =>
            {
                inventoryChangePending = false;
                if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
                RefreshAll();
            });
            heldSlotIndex = -1;
        }

        RefreshAll();
    }

    private void OnOrganizeClicked()
    {
        if (inventoryChangePending) return;
        var online = playerInventory == null ? null : playerInventory.GetComponent<OnlineGameState>();
        if (online == null)
        {
            NpcConversation.ShowNotice("Đăng nhập để sắp xếp túi đồ.");
            return;
        }
        heldSlotIndex = -1;
        inventoryChangePending = true;
        online.OrganizeInventory(error =>
        {
            inventoryChangePending = false;
            if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
            RefreshAll();
        });
    }

    private void OnTrashCanClicked()
    {
        if (heldSlotIndex != -1 && inventoryController != null && !inventoryChangePending)
        {
            var online = playerInventory == null ? null : playerInventory.GetComponent<OnlineGameState>();
            var slot = readOnlyInventory?.GetSlot(heldSlotIndex);
            if (online == null || slot == null || slot.IsEmpty || slot.Data == null)
            {
                NpcConversation.ShowNotice("Không thể bỏ vật phẩm này.");
                return;
            }
            inventoryChangePending = true;
            GameAudio.Play("trashcan");
            online.DiscardInventory(heldSlotIndex, slot.Data.itemId, error =>
            {
                inventoryChangePending = false;
                if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
                RefreshAll();
            });
            heldSlotIndex = -1;
            RefreshAll();
        }
    }

    private void OnCloseRequested()
    {
        IsOpen = false;
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
        heldSlotIndex = -1;
        if (inventoryFrame != null) inventoryFrame.SetActive(tabIndex == 0);
        if (backpackGrid != null)
        {
            backpackGrid.gameObject.SetActive(tabIndex == 0);
            backpackGrid.SetButtonsActive(tabIndex == 0);
        }
        pages?.SelectPage(tabIndex);
    }

    private void OnOpenChanged(bool isOpen)
    {
        GameAudio.Play(isOpen ? "bigSelect" : "bigDeSelect");
        if (!isOpen) InputController.SuppressWorldUseAfterModal();
        IsOpen = isOpen;
        if (menuRoot != null)
        {
            menuRoot.SetActive(isOpen);
            if (isOpen)
            {
                heldSlotIndex = -1;
                if (tabStrip != null) tabStrip.SelectTab(0);
                if (backpackGrid != null) backpackGrid.SetButtonsActive(true, false);
                RefreshAll();
            }
        }

        RefreshHudToolbar();
    }

    internal void RefreshHudToolbar() =>
        hudToolbar?.SetActive(!IsOpen && !StartupSession.IsOpening);

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
