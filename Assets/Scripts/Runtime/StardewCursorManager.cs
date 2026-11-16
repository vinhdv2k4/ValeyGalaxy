using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cursor identifiers (mouseCursor indices):
/// 0 = Default wooden arrow pointer
/// 1 = Hourglass wait cursor
/// 2 = White glove hand pointer / action (doors, chests, buttons, interactables)
/// 3 = Gift box pointer (holding a gift item / giving to NPC)
/// 4 = Speech bubble pointer (chatting with NPC)
/// 5 = Magnifying glass (inspect)
/// 6 = Green plus harvest pointer (ready-to-harvest crops)
/// 7 = Heart tile in Cursors.png (animal petting)
/// 44 = Gamepad glove reticle
/// </summary>
public enum StardewCursorType
{
    Default = 0,
    Wait = 1,
    Hand = 2,
    Gift = 3,
    Speech = 4,
    Inspect = 5,
    Harvest = 6,
    Heart = 7,
    Gamepad = 44
}

/// <summary>
/// Manages context-sensitive mouse cursor visuals.
/// Compatible with Unity New Input System (UnityEngine.InputSystem).
/// Automatically runs before scene load and provides dynamic context-sensitive cursor updates.
/// </summary>
public sealed class StardewCursorManager : MonoBehaviour
{
    private static StardewCursorManager instance;

    [Header("Cursor Settings")]
    [Tooltip("If true, uses 64x64 textures (4x pixel zoom for 1080p). If false, uses 32x32 crisp native hardware cursor.")]
    [SerializeField] private bool useLargeCursor = true;
    // Hardware cursor: the OS moves it at the mouse's own rate, so it stays smooth even when the
    // game renders below 60 fps. A software cursor is only redrawn once per rendered frame (and a
    // frame late), which made it stutter. The 64 px textures are the Cursors tiles at 4x.
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private readonly Dictionary<StardewCursorType, Texture2D> textures32 = new();
    private readonly Dictionary<StardewCursorType, Texture2D> textures64 = new();
    private readonly Dictionary<StardewCursorType, Texture2D> dimTextures32 = new();
    private readonly Dictionary<StardewCursorType, Texture2D> dimTextures64 = new();

    private StardewCursorType currentCursor = StardewCursorType.Default;
    private bool currentDim;
    private StardewCursorType? overrideCursor;
    private bool isWaiting;
    private PlayerInventory playerInventory;
    private CharacterMotor playerMotor;
    private PlayerFarmingInteractor farmingInteractor;
    private Sprite placementIcon;
    private bool showPlacementIcon;
    private bool validPlacement;

    // Cache raycast results to avoid GC allocation each frame
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private ContactFilter2D contactFilter;
    private readonly List<RaycastResult> uiRaycastResults = new();

    public static StardewCursorManager Instance => instance;

    /// <summary>
    /// Active cursor index property (getter and setter).
    /// </summary>
    public static int mouseCursor
    {
        get => (int)CurrentCursor;
        set => SetCursor(value);
    }

    public static StardewCursorType CurrentCursor => instance != null ? instance.currentCursor : StardewCursorType.Default;

    public static bool UseLargeCursor
    {
        get => instance != null && instance.useLargeCursor;
        set
        {
            if (instance != null) instance.SetLargeCursor(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("StardewCursorManager");
        instance = go.AddComponent<StardewCursorManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        contactFilter = new ContactFilter2D();
        contactFilter.useTriggers = true;

        LoadAllTextures();
        ApplyCursor(StardewCursorType.Default, force: true);
    }

    private void Start()
    {
        ApplyCursor(StardewCursorType.Default, force: true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyCursor(currentCursor, force: true, dim: currentDim);
        }
    }

    private void Update()
    {
        UpdateCursorContext();
    }

    /// <summary>
    /// Sets cursor by numeric cursor ID.
    /// </summary>
    public static void SetCursor(int cursorId)
    {
        StardewCursorType type = cursorId switch
        {
            0 => StardewCursorType.Default,
            1 => StardewCursorType.Wait,
            2 => StardewCursorType.Hand,
            3 => StardewCursorType.Gift,
            4 => StardewCursorType.Speech,
            5 => StardewCursorType.Inspect,
            6 => StardewCursorType.Harvest,
            7 => StardewCursorType.Heart,
            44 => StardewCursorType.Gamepad,
            _ => StardewCursorType.Default
        };
        SetCursor(type);
    }

    /// <summary>
    /// Sets active cursor to a specific cursor type.
    /// </summary>
    public static void SetCursor(StardewCursorType type)
    {
        if (instance == null) return;
        instance.ApplyCursor(type);
    }

    /// <summary>
    /// Resets cursor back to Default wooden arrow.
    /// </summary>
    public static void ResetCursor()
    {
        SetCursor(StardewCursorType.Default);
    }

    /// <summary>
    /// Sets a temporary override cursor (e.g. while performing an action or dragging).
    /// Pass null to remove override.
    /// </summary>
    public static void SetOverride(StardewCursorType? overrideType)
    {
        if (instance == null) return;
        instance.overrideCursor = overrideType;
        instance.ApplyCursor(overrideType ?? StardewCursorType.Default);
    }

    /// <summary>
    /// Sets the hourglass wait cursor while loading or busy.
    /// </summary>
    public static void SetWait(bool wait)
    {
        if (instance == null) return;
        instance.isWaiting = wait;
        if (wait)
            instance.ApplyCursor(StardewCursorType.Wait);
        else
            instance.ApplyCursor(instance.overrideCursor ?? StardewCursorType.Default);
    }

    public void SetLargeCursor(bool large)
    {
        if (useLargeCursor == large) return;
        useLargeCursor = large;
        ApplyCursor(currentCursor, force: true, dim: currentDim);
    }

    public void SetCursorMode(CursorMode mode)
    {
        if (cursorMode == mode) return;
        cursorMode = mode;
        ApplyCursor(currentCursor, force: true, dim: currentDim);
    }

    private void LoadAllTextures()
    {
        LoadCursorPair(StardewCursorType.Default, "Cursor_0_Default");
        LoadCursorPair(StardewCursorType.Wait, "Cursor_1_Wait");
        LoadCursorPair(StardewCursorType.Hand, "Cursor_2_Hand");
        LoadCursorPair(StardewCursorType.Gift, "Cursor_3_Gift");
        LoadCursorPair(StardewCursorType.Speech, "Cursor_4_Speech");
        LoadCursorPair(StardewCursorType.Inspect, "Cursor_5_Inspect");
        LoadCursorPair(StardewCursorType.Harvest, "Cursor_6_Harvest");
        LoadCursorPair(StardewCursorType.Heart, "Cursor_7_Heart");
        LoadCursorPair(StardewCursorType.Gamepad, "Cursor_44_Gamepad");
    }

    private void LoadCursorPair(StardewCursorType type, string resourceName)
    {
        Texture2D t32 = Resources.Load<Texture2D>($"Cursors/{resourceName}");
        if (t32 != null) textures32[type] = t32;

        Texture2D t64 = Resources.Load<Texture2D>($"Cursors/{resourceName}_64");
        if (t64 != null) textures64[type] = t64;
    }

    private void ApplyCursor(StardewCursorType type, bool force = false, bool dim = false)
    {
        if (!force && currentCursor == type && currentDim == dim) return;

        currentCursor = type;
        currentDim = dim;

        var dict = useLargeCursor ? textures64 : textures32;
        if (!dict.TryGetValue(type, out Texture2D tex) || tex == null)
        {
            // Fallback to the other resolution if available
            var fallbackDict = useLargeCursor ? textures32 : textures64;
            fallbackDict.TryGetValue(type, out tex);
        }

        if (tex != null)
        {
            if (dim) tex = GetDimTexture(type, tex);
            // Draws each 16x16 cursor tile at the mouse position's top-left.
            Cursor.SetCursor(tex, Vector2.zero, cursorMode);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
        }
    }

    private Texture2D GetDimTexture(StardewCursorType type, Texture2D source)
    {
        var cache = useLargeCursor ? dimTextures64 : dimTextures32;
        if (cache.TryGetValue(type, out Texture2D cached)) return cached;
        var pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].a = (byte)(pixels[i].a / 2);
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        cache[type] = texture;
        return texture;
    }

    private void OnDestroy()
    {
        foreach (Texture2D texture in dimTextures32.Values) if (texture != null) Destroy(texture);
        foreach (Texture2D texture in dimTextures64.Values) if (texture != null) Destroy(texture);
        if (instance != this) return;
        Cursor.SetCursor(null, Vector2.zero, cursorMode);
        instance = null;
    }

    /// <summary>
    /// Safely gets the current mouse screen position using the Unity New Input System.
    /// </summary>
    private Vector2 GetMouseScreenPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        return Vector2.zero;
    }

    /// <summary>
    /// Evaluates what is beneath the mouse pointer and selects the matching cursor.
    /// </summary>
    private void UpdateCursorContext()
    {
        showPlacementIcon = false;
        // Map loads behind a black screen show the hourglass, except while a dialogue
        // (cutscene text on black, the opening's name entry) is waiting for the player.
        if (isWaiting || GameLoadingScreen.IsShowing ||
            LocationManager.Instance?.IsLoadingScreen == true && !NpcConversation.IsOpen)
        {
            ApplyCursor(StardewCursorType.Wait);
            return;
        }

        if (overrideCursor.HasValue)
        {
            ApplyCursor(overrideCursor.Value);
            return;
        }

        Vector2 mouseScreen = GetMouseScreenPosition();
        if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerMotor == null) playerMotor = FindFirstObjectByType<CharacterMotor>();
        if (farmingInteractor == null) farmingInteractor = FindFirstObjectByType<PlayerFarmingInteractor>();
        Camera cam = Camera.main;
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        if (cam == null || location == null || !location.gameObject.activeInHierarchy ||
            BlocksWorldInput(mouseScreen))
        {
            ApplyCursor(StardewCursorType.Default);
            return;
        }

        if (mouseScreen.x < 0 || mouseScreen.y < 0 ||
            mouseScreen.x >= Screen.width || mouseScreen.y >= Screen.height)
            return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        InventorySlot selected = playerInventory?.SelectedItem;
        bool holdingObject = selected != null && !selected.IsEmpty && !PlayerAppearance.IsTool(selected);

        if (IsPointerOverDoor(mouseWorld))
        {
            ApplyCursor(StardewCursorType.Hand, dim: !IsNearby(mouseWorld));
            return;
        }

        foreach (var (_, noteX, noteY) in CommunityCenterRooms.NoteTiles())
            // Tile (x, y) spans world y from -y to 1 - y (NpcPathfinder.TileToWorld).
            if (Mathf.FloorToInt(mouseWorld.x) == noteX && Mathf.FloorToInt(1f - mouseWorld.y) == noteY)
            {
                ApplyCursor(StardewCursorType.Hand, dim: !IsNearby(mouseWorld));
                return;
            }

        var mailbox = location.Mailbox;
        if (mailbox != null && mailbox.ContainsPointer(mouseWorld))
        {
            ApplyCursor(StardewCursorType.Hand,
                dim: playerMotor == null || !mailbox.IsInReach(playerMotor.StandingPosition));
            return;
        }

        // Utility.checkForCharacterInteractionAtTile only selects gift while over a villager.
        foreach (NpcActor npc in FindObjectsByType<NpcActor>(FindObjectsSortMode.None))
        {
            if (npc == null || !npc.IsVisible || !npc.ContainsPointer(mouseWorld)) continue;
            ApplyCursor(holdingObject && CanGift(selected) ? StardewCursorType.Gift : StardewCursorType.Speech,
                dim: !IsNearby(npc.transform.position));
            return;
        }

        if (IsShopActionAt(location.LocationName, mouseWorld))
        {
            ApplyCursor(StardewCursorType.Hand, dim: !IsNearby(mouseWorld));
            return;
        }

        int hitCount = Physics2D.OverlapPoint(mouseWorld, contactFilter, overlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = overlapResults[i];
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            var crop = col.GetComponentInParent<CropPlot>();
            if (crop != null && crop.IsReadyToHarvest)
            {
                ApplyCursor(StardewCursorType.Harvest, dim: !IsNearby(crop.transform.position));
                return;
            }

            var tv = col.GetComponentInParent<InteractableTV>();
            if (tv != null && location.LocationName == "FarmHouse")
            {
                ApplyCursor(StardewCursorType.Hand, dim: !IsNearby(tv.transform.position));
                return;
            }

            var shippingBin = col.GetComponentInParent<FarmShippingBin>();
            if (shippingBin != null && location.LocationName == "Farm")
            {
                ApplyCursor(StardewCursorType.Hand,
                    dim: playerMotor == null || !shippingBin.IsInReach(playerMotor.StandingPosition));
                return;
            }

            var chest = col.GetComponentInParent<PlacedPlayerChest>();
            if (chest != null && location.LocationName == "FarmHouse")
            {
                ApplyCursor(StardewCursorType.Hand, dim: !IsNearby(chest.transform.position));
                return;
            }
        }

        // ActiveObject.drawInMenu keeps the default cursor and adds the item icon beside it.
        ApplyCursor(StardewCursorType.Default);
        if (holdingObject && playerMotor != null && playerMotor.IsMovementEnabled)
        {
            placementIcon = selected.itemData != null ? selected.itemData.GetIcon() : selected.inventoryIcon;
            showPlacementIcon = placementIcon != null;
            validPlacement = CanPlaceSelectedItem(selected, mouseWorld);
        }
    }

    public static bool BlocksWorldInput(Vector2 mouseScreen) => instance != null &&
        (instance.IsModalOpen() || instance.IsPointerOverInteractiveUi(mouseScreen));

    private bool IsModalOpen()
    {
        if (GameUi.IsMenuOrInventoryOpen || playerInventory?.IsOpen == true)
            return true;
        // Asked every frame by InputController: look the farmer's components up once per farmer,
        // and again until StartupSession has added OnlineGameState after the scene loaded.
        if (playerMotor != blockerFarmer || blockerOnline == null)
        {
            blockerFarmer = playerMotor;
            blockerOnline = playerMotor != null ? playerMotor.GetComponent<OnlineGameState>() : null;
            blockerFishing = playerMotor != null ? playerMotor.GetComponent<FishingController>() : null;
        }
        if (blockerOnline != null && blockerOnline.IsChestInteractionOpen) return true;
        return blockerFishing != null && blockerFishing.State != FishingController.FishingState.Inactive;
    }

    private CharacterMotor blockerFarmer;
    private OnlineGameState blockerOnline;
    private FishingController blockerFishing;

    private bool IsPointerOverInteractiveUi(Vector2 mouseScreen)
    {
        if (EventSystem.current == null) return false;
        var eventData = new PointerEventData(EventSystem.current) { position = mouseScreen };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);
        foreach (RaycastResult hit in uiRaycastResults)
        {
            if (hit.gameObject == null) continue;
            if (hit.gameObject.GetComponentInParent<Selectable>() != null ||
                hit.gameObject.GetComponentInParent<InventorySlotUI>() != null)
                return true;
        }
        return false;
    }

    private static bool CanGift(InventorySlot selected)
    {
        if (selected == null || selected.IsEmpty || PlayerAppearance.IsTool(selected)) return false;
        // Big craftables and furniture are excluded by Object.canBeGivenAsGift.
        string id = selected.itemData?.itemId ?? string.Empty;
        return !id.StartsWith("chest", StringComparison.OrdinalIgnoreCase) &&
               !id.StartsWith("furniture", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsShopActionAt(string location, Vector2 point)
    {
        if (string.Equals(location, "SeedShop", StringComparison.OrdinalIgnoreCase) &&
            point.y >= -18.7f && point.y <= -14.8f)
            return point.x >= 3f && point.x <= 6f ||
                playerInventory != null && playerInventory.UnlockedSlots < 36 &&
                point.x >= 6.3f && point.x <= 8.3f;
        if (string.Equals(location, "JojaMart", StringComparison.OrdinalIgnoreCase))
            return Mathf.FloorToInt(point.x) == 10 &&
                (Mathf.FloorToInt(-point.y) == 24 || Mathf.FloorToInt(-point.y) == 25);
        return false;
    }

    private bool IsNearby(Vector2 world)
    {
        if (playerMotor == null) return false;
        Vector2 player = playerMotor.StandingPosition;
        return Mathf.Abs(Mathf.FloorToInt(player.x) - Mathf.FloorToInt(world.x)) <= 1 &&
               Mathf.Abs(Mathf.FloorToInt(-player.y) - Mathf.FloorToInt(-world.y)) <= 1;
    }

    private bool CanPlaceSelectedItem(InventorySlot selected, Vector2 mouseWorld)
    {
        if (selected.kind != PlayerItemKind.Seeds || farmingInteractor == null ||
            !farmingInteractor.IsFarmingAllowed() || playerMotor == null ||
            Vector2.Distance(playerMotor.StandingPosition, mouseWorld) > 1.8f) return false;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(mouseWorld))
        {
            CropPlot plot = hit.GetComponentInParent<CropPlot>();
            if (plot != null && plot.IsTilled && !plot.IsPlanted) return true;
        }
        return false;
    }

    private void OnGUI()
    {
        if (!showPlacementIcon || placementIcon == null || Event.current.type != EventType.Repaint ||
            Mouse.current == null) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        Texture2D texture = placementIcon.texture;
        Rect source = placementIcon.textureRect;
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, validPlacement ? 1f : 0.5f);
        GUI.DrawTextureWithTexCoords(new Rect(mouse.x + 16f, Screen.height - mouse.y + 16f, 64f, 64f),
            texture, uv);
        GUI.color = oldColor;
    }

    /// <summary>
    /// Only map Action doors use the hand cursor; passive walk warps do not.
    /// </summary>
    private bool IsPointerOverDoor(Vector2 mouseWorld)
    {
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        if (location == null) return false;
        foreach (LocationWarp warp in location.Warps)
            if (warp != null && warp.fromMapAction && !warp.npcOnly &&
                warp.ContainsDoorPointer(mouseWorld)) return true;
        return false;
    }
}
