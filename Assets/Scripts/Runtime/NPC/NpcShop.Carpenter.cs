using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>Robin's construction menu: blueprint viewer plus a farm view for placing, upgrading and moving buildings.</summary>
public sealed partial class NpcShop
{
    private enum FarmTask { None, Build, Upgrade, Move, Demolish, Paint, PlaceAnimal }

    private const float CarpenterWidth = 1032f, CarpenterHeight = 680f;
    private const float MenuX = 108f, MenuY = 24f;
    private const float ViewerWidth = 448f, ViewerHeight = 512f, DescriptionWidth = 416f;
    private const float MenuWidth = ViewerWidth + DescriptionWidth + 16f * 2 + 64f;
    private const float ButtonRight = MenuX + MenuWidth - 40f - 16f;
    private const int FarmWidth = 80, FarmHeight = 65;

    private static readonly Rect OkSource = new(366, 373, 16, 16);
    private static readonly Rect CancelSource = new(192, 256, 64, 64);
    private static readonly Rect BackSource = new(352, 495, 12, 11);
    private static readonly Rect ForwardSource = new(365, 495, 12, 11);
    private static readonly Rect MoveSource = new(257, 284, 16, 16);
    private static readonly Rect UpgradeArrowSource = new(402, 328, 9, 13);
    private static readonly Rect DemolishSource = new(348, 372, 17, 17);
    private static readonly Rect PaintSource = new(80, 208, 16, 16);
    private static readonly Rect PreviousRegionSource = new(0, 256, 64, 64);
    private static readonly Rect NextRegionSource = new(0, 192, 64, 64);
    private static readonly Rect DefaultColorSource = new(80, 144, 16, 16);
    private static readonly Rect SavedColorSource = new(96, 144, 16, 16);
    private static readonly Rect CopyColorSource = new(274, 284, 16, 16);
    private static readonly Rect SliderHandleSource = new(72, 256, 16, 20);
    private static readonly Rect ConfirmSource = new(128, 256, 64, 64);
    private static readonly Dictionary<string, string> PaintRegionNames = new(StringComparer.Ordinal)
        { ["Building"] = "Tường", ["Roof"] = "Mái", ["Trim"] = "Viền" };
    private static readonly List<Vector3> SavedPaintColors = new();
    private static readonly Color TextColor = new(86f / 255f, 22f / 255f, 12f / 255f);

    // Map-tile rectangles kept clear of animal buildings (same list the server checks).
    private static readonly RectInt[] FarmLandmarks = { new(59, 11, 9, 7), new(71, 14, 2, 1), new(25, 10, 7, 7) };

    private static readonly Dictionary<string, string> BlueprintText = new(StringComparer.Ordinal)
    {
        ["Coop"] = "Chuồng cho gà. Có thể nâng cấp để nuôi thêm vật nuôi.",
        ["Big Coop"] = "Nuôi được 8 con vật ở chuồng gà. Mở khóa vịt.",
        ["Deluxe Coop"] = "Nuôi được 12 con vật ở chuồng gà. Mở khóa thỏ. Có máy cho ăn tự động.",
        ["Barn"] = "Chuồng cho bò. Có thể nâng cấp để nuôi thêm vật nuôi.",
        ["Big Barn"] = "Nuôi được 8 con vật ở chuồng lớn. Mở khóa dê.",
        ["Deluxe Barn"] = "Nuôi được 12 con vật ở chuồng lớn. Mở khóa cừu và heo. Có máy cho ăn tự động.",
        ["Silo"] = "Cho phép cắt cỏ bằng liềm để dự trữ cỏ khô. Mỗi Silo chứa được 240 cỏ khô."
    };

    private readonly List<RanchPlanView> blueprints = new();
    private int blueprintIndex;
    private FarmTask farmTask;
    private Action restoreFarmView;
    private Transform farmViewTarget;
    private GameLocation farmView;
    private OnlineRanchHome buildingToMove;
    private readonly HashSet<Vector2Int> occupiedFarmTiles = new();
    private Transform placementOverlay;
    private readonly List<SpriteRenderer> placementTiles = new();
    private SpriteRenderer placementGhost;
    private Sprite goodTileSprite, badTileSprite;
    private SpriteRenderer tintedBuilding;
    private string hudMessage;
    private float hudMessageUntil;
    private float farmReturnAt = -1f;
    private string farewellLine;
    private string hoverText;
    private float menuReturnAt = -1f;
    private float flashUntil;
    private OnlineRanchHome paintTarget;
    private OnlineRanchPaint[] paintRegions;
    private RanchPaintableView paintRanges;
    private int paintRegion;
    private int activeSlider = -1;
    private Texture2D paintPreview;
    private Rect paintSliderArea;
    private Matrix4x4 paintMatrix;

    private RanchPlanView CurrentBlueprint =>
        blueprints.Count == 0 ? null : blueprints[Mathf.Clamp(blueprintIndex, 0, blueprints.Count - 1)];

    private void PrepareBlueprints(OnlineGameSnapshot snapshot)
    {
        blueprints.Clear();
        var homes = snapshot?.ranchHomes ?? Array.Empty<OnlineRanchHome>();
        bool Built(string kind, int level) => homes.Any(home => home.kind == kind && home.level >= level);
        void Add(string kind, int level)
        {
            RanchPlanView plan = FarmRanchAssets.Plan(kind, level);
            if (plan != null) blueprints.Add(plan);
        }
        Add("Coop", 1);
        Add("Barn", 1);
        Add("Silo", 1);
        if (Built("Coop", 1)) Add("Coop", 2);
        if (Built("Coop", 2)) Add("Coop", 3);
        if (Built("Barn", 1)) Add("Barn", 2);
        if (Built("Barn", 2)) Add("Barn", 3);
        blueprintIndex = Mathf.Clamp(blueprintIndex, 0, Mathf.Max(0, blueprints.Count - 1));
    }

    private static bool RobinBusy(OnlineGameSnapshot snapshot) => snapshot != null &&
        (snapshot.houseUpgradeDay > snapshot.gameDay ||
         snapshot.ranchHomes?.Any(home => home.readyDay > snapshot.gameDay) == true);

    private int CountOwned(string itemId)
    {
        var stacks = inventory?.GetComponent<OnlineGameState>()?.Snapshot?.inventory;
        return stacks == null ? 0 : stacks.Where(stack => stack.itemId == itemId).Sum(stack => stack.amount);
    }

    private bool CanAfford(RanchPlanView plan) => plan != null && GetCurrentFunds() >= plan.price &&
        PlanMaterials(plan).All(material => CountOwned(material.Id) >= material.Amount);

    private static readonly Dictionary<string, string> MaterialNames = new(StringComparer.Ordinal)
        { ["wood"] = "Gỗ", ["stone"] = "Đá", ["object_330"] = "Đất sét", ["object_334"] = "Thỏi đồng" };

    private static IEnumerable<(string Id, int Amount, string Label)> PlanMaterials(RanchPlanView plan)
    {
        if (plan.wood > 0) yield return ("wood", plan.wood, MaterialNames["wood"]);
        if (plan.stone > 0) yield return ("stone", plan.stone, MaterialNames["stone"]);
        if (plan.materials == null) yield break;
        foreach (RanchMaterialView material in plan.materials)
            if (material != null && material.amount > 0)
                yield return (material.id, material.amount,
                    MaterialNames.TryGetValue(material.id, out string name) ? name : material.id);
    }

    // ── Menu ─────────────────────────────────────────────────────────────────────────────────

    private void DrawCarpenter()
    {
        if (farmTask != FarmTask.None) { DrawFarmTask(); return; }
        var online = inventory?.GetComponent<OnlineGameState>();
        PrepareBlueprints(online?.Snapshot);
        float scale = Mathf.Min(1f, Screen.width / (CarpenterWidth + 32f), Screen.height / (CarpenterHeight + 32f));
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - CarpenterWidth * scale) * 0.5f,
            (Screen.height - CarpenterHeight * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try { DrawBlueprintViewer(online); }
        finally { GUI.matrix = oldMatrix; }
        StardewUiAtlas.DrawHudMoneyBox(GetCurrentFunds());
    }

    private void DrawBlueprintViewer(OnlineGameState online)
    {
        Texture2D font = StardewUiAtlas.FontBold;
        RanchPlanView plan = CurrentBlueprint;
        hoverText = null;
        StardewUiAtlas.DrawTextureBox(new Rect(MenuX - 96f, MenuY - 16f, ViewerWidth + 64f, ViewerHeight + 64f),
            StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        if (plan == null || online?.Snapshot == null)
        {
            DrawText(font, new Rect(MenuX, MenuY + 120f, 700f, 45f), "Chưa tải được bản vẽ.", 2.2f);
            DrawCancel(new Rect(ButtonRight - 64f, MenuY + ViewerHeight + 64f, 64f, 64f));
            return;
        }

        Texture2D art = FarmRanchAssets.Art(plan.name);
        if (art != null)
        {
            Vector2 size = new(art.width * 4f, (art.height - 16) * 4f);
            float fit = Mathf.Min(1f, (ViewerWidth + 32f) / size.x, (ViewerHeight + 32f) / size.y);
            size *= fit;
            Vector2 center = new(MenuX - 96f + (ViewerWidth + 64f) * 0.5f, MenuY - 16f + (ViewerHeight + 64f) * 0.5f);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTextureWithTexCoords(new Rect(center - size * 0.5f, size), art,
                    new Rect(0f, 16f / art.height, 1f, (art.height - 16f) / art.height));
        }
        if (plan.level > 1)
            StardewUiAtlas.DrawCursorsSprite(new Rect(MenuX + ViewerWidth - 128f + 32f, MenuY + 8f, 36f, 52f), UpgradeArrowSource);

        // Name scroll.
        float scrollWidth = Mathf.Max(NpcBitmapText.GetStringWidth(" Deluxe  Barn   ", 2.6f),
            NpcBitmapText.GetStringWidth(plan.name + " ", 2.6f)) + 32f;
        float scrollCenter = MenuX + ViewerWidth - 16f - 16f + 64f + (MenuWidth - (ViewerWidth + 128f)) * 0.5f;
        var scroll = new Rect(scrollCenter - scrollWidth * 0.5f, MenuY, scrollWidth, 72f);
        DrawScroll(scroll);
        NpcBitmapText.Draw(font, new Rect(scroll.x, scroll.y + 18f, scroll.width, 40f), plan.name, 2.6f, centered: true);

        // Description, price and materials.
        var description = new Rect(MenuX + ViewerWidth - 16f, MenuY + 80f, DescriptionWidth + 64f, ViewerHeight - 32f);
        StardewUiAtlas.DrawTextureBox(description, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        string text = BlueprintText.TryGetValue(plan.name, out string about) ? about : plan.name;
        text += $" Thời gian xây: {plan.days} ngày.";
        DrawColored(new Rect(MenuX + ViewerWidth, MenuY + 96f, DescriptionWidth + 32f, 170f), text, 1.75f, TextColor);

        Vector2 at = new(MenuX + ViewerWidth + 16f, MenuY + 256f + 32f);
        NpcBitmapText.Draw(font, new Rect(at.x, at.y, 48f, 48f), "$", 2.6f);
        DrawColored(new Rect(at.x + 68f, at.y + 4f, 340f, 48f), $"{plan.price:N0}g", 2.2f,
            GetCurrentFunds() >= plan.price ? TextColor : Color.red);
        at.x -= 16f;
        at.y -= 21f;
        foreach ((string id, int amount, string label) in PlanMaterials(plan))
        {
            if (amount <= 0) continue;
            at.y += 68f;
            var slot = new Rect(at.x, at.y, 64f, 64f);
            DrawItemIcon(online.ResolveItem(id), slot);
            StardewUiAtlas.DrawStackCount(slot, amount);
            DrawColored(new Rect(at.x + 80f, at.y + 20f, 320f, 40f), label, 2f,
                CountOwned(id) >= amount ? TextColor : Color.red);
        }
        if (!string.IsNullOrEmpty(feedback))
            DrawColored(new Rect(MenuX + ViewerWidth, MenuY + ViewerHeight - 40f, DescriptionWidth + 32f, 40f), feedback, 1.6f, Color.red);

        // Bottom row.
        float rowY = MenuY + ViewerHeight + 64f;
        var back = new Rect(MenuX + 64f, rowY, 48f, 44f);
        var forward = new Rect(MenuX + ViewerWidth - 256f + 16f, rowY, 48f, 44f);
        var move = new Rect(ButtonRight - 256f - 20f, rowY, 64f, 64f);
        var ok = new Rect(ButtonRight - 192f - 12f, rowY, 64f, 64f);
        var cancel = new Rect(ButtonRight - 64f, rowY, 64f, 64f);
        bool affordable = CanAfford(plan);
        if (SpriteButton(back, BackSource, Color.white) && !purchasePending)
        {
            blueprintIndex = (blueprintIndex + blueprints.Count - 1) % blueprints.Count;
            feedback = "";
            GameAudio.Play("shwip");
        }
        if (SpriteButton(forward, ForwardSource, Color.white) && !purchasePending)
        {
            blueprintIndex = (blueprintIndex + 1) % blueprints.Count;
            feedback = "";
            GameAudio.Play("shwip");
        }
        var homes = online.Snapshot.ranchHomes ?? Array.Empty<OnlineRanchHome>();
        if (homes.Length > 0)
        {
            if (move.Contains(Event.current.mousePosition)) hoverText = "Di chuyển công trình";
            if (SpriteButton(move, MoveSource, Color.white) && !purchasePending)
                BeginFarmTask(FarmTask.Move);
            var demolish = new Rect(ButtonRight - 128f - 8f, rowY - 4f, 64f, 64f);
            if (demolish.Contains(Event.current.mousePosition)) hoverText = "Phá công trình";
            if (SpriteButton(demolish, DemolishSource, Color.white) && !purchasePending)
                BeginFarmTask(FarmTask.Demolish);
        }
        Texture2D cursors2 = FarmRanchAssets.Instance?.cursors2;
        if (cursors2 != null && homes.Any(CanPaint))
        {
            var paint = new Rect(ButtonRight - 320f - 20f, rowY, 64f, 64f);
            if (paint.Contains(Event.current.mousePosition)) hoverText = "Sơn công trình";
            if (AtlasButton(cursors2, paint, PaintSource) && !purchasePending)
                BeginFarmTask(FarmTask.Paint);
        }
        if (ok.Contains(Event.current.mousePosition))
            hoverText = plan.level > 1 ? "Nâng cấp " + FarmRanchAssets.Plan(plan.kind, plan.level - 1)?.name : "Xây dựng";
        if (SpriteButton(ok, OkSource, affordable ? Color.white : new Color(.5f, .5f, .5f, .8f)) && !purchasePending)
        {
            if (!affordable) { GameAudio.Play("cancel"); feedback = "Bạn không đủ tiền hoặc vật liệu."; }
            else BeginFarmTask(plan.level > 1 ? FarmTask.Upgrade : FarmTask.Build);
        }
        DrawCancel(cancel);
        if (!string.IsNullOrEmpty(hoverText)) DrawHoverText(hoverText);
    }

    private void DrawCancel(Rect rect)
    {
        if (!SpriteButton(rect, CancelSource, Color.white) || purchasePending) return;
        Close();
    }

    // ── Farm view ────────────────────────────────────────────────────────────────────────────

    private void BeginFarmTask(FarmTask task)
    {
        var manager = LocationManager.Instance;
        Action restore = manager?.ShowCutaway("Farm", hideFarmer: true);
        if (restore == null) { feedback = "Không mở được bản đồ nông trại."; return; }
        restoreFarmView = restore;
        farmView = manager.CurrentLocation;
        farmTask = task;
        buildingToMove = null;
        feedback = "";
        hudMessage = null;
        farmReturnAt = -1f;
        menuReturnAt = -1f;
        paintTarget = null;
        CollectOccupiedTiles();
        farmViewTarget = new GameObject("Carpenter View").transform;
        farmViewTarget.SetParent(farmView.transform, false);
        farmViewTarget.position = farmView.TileCenter(59, 11);
        manager.SetCutsceneCameraTarget(farmViewTarget);
        placementOverlay = new GameObject("Carpenter Placement").transform;
        placementOverlay.SetParent(farmView.transform, false);
        GameAudio.Play("smallSelect");
    }

    private void EndFarmTask()
    {
        ClearBuildingTint();
        if (placementOverlay != null) Destroy(placementOverlay.gameObject);
        if (farmViewTarget != null) Destroy(farmViewTarget.gameObject);
        placementOverlay = null;
        farmViewTarget = null;
        placementTiles.Clear();
        placementGhost = null;
        buildingToMove = null;
        farmTask = FarmTask.None;
        farmReturnAt = -1f;
        menuReturnAt = -1f;
        paintTarget = null;
        activeSlider = -1;
        Action restore = restoreFarmView;
        restoreFarmView = null;
        farmView = null;
        restore?.Invoke();
    }

    private void CollectOccupiedTiles()
    {
        occupiedFarmTiles.Clear();
        foreach (CropPlot plot in FindObjectsByType<CropPlot>(FindObjectsSortMode.None))
            if (plot.IsTilled || plot.IsPlanted)
                occupiedFarmTiles.Add(NpcPathfinder.WorldToTile(plot.transform.position));
        var chests = inventory?.GetComponent<OnlineGameState>()?.Snapshot?.chests;
        if (chests != null)
            foreach (OnlineChest chest in chests)
                if (chest.location == "Farm") occupiedFarmTiles.Add(new Vector2Int(chest.tileX, chest.tileY + 1));
    }

    private void UpdateCarpenter()
    {
        if (farmTask == FarmTask.None)
        {
            if (!purchasePending && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
            return;
        }
        if (farmReturnAt >= 0f)
        {
            if (Time.unscaledTime < farmReturnAt) return;
            string line = farewellLine;
            farewellLine = null;
            EndFarmTask();
            Close();
            if (!string.IsNullOrEmpty(line)) NpcConversation.ShowNotice(line);
            return;
        }
        if (menuReturnAt >= 0f)
        {
            if (Time.unscaledTime >= menuReturnAt) EndFarmTask();
            return;
        }
        if (!purchasePending && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            if (paintTarget != null) { ClosePaintMenu(); GameAudio.Play("bigDeSelect"); }
            else if (namingAnimal) CancelNaming();
            else ReturnToCarpenterMenu();
            return;
        }
        if (paintTarget != null) { UpdatePaintSlider(); return; }
        if (namingAnimal) return;
        PanFarmView();
        UpdatePlacementPreview();
    }

    private void ReturnToCarpenterMenu()
    {
        EndFarmTask();
        GameAudio.Play("smallSelect");
    }

    private void PanFarmView()
    {
        if (farmViewTarget == null) return;
        Vector2 pan = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
        }
        if (Mouse.current != null && Application.isFocused)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            const float edge = 32f;
            if (mouse.x < edge) pan.x -= 1f;
            else if (mouse.x > Screen.width - edge) pan.x += 1f;
            if (mouse.y < edge) pan.y -= 1f;
            else if (mouse.y > Screen.height - edge) pan.y += 1f;
        }
        if (pan == Vector2.zero) return;
        Vector3 next = farmViewTarget.position + (Vector3)(Vector2.ClampMagnitude(pan, 1f) * (12f * Time.unscaledDeltaTime));
        Vector3 min = farmView.TileCenter(0, FarmHeight - 1), max = farmView.TileCenter(FarmWidth - 1, 0);
        next.x = Mathf.Clamp(next.x, min.x, max.x);
        next.y = Mathf.Clamp(next.y, min.y, max.y);
        farmViewTarget.position = next;
    }

    private Vector2Int MouseTile()
    {
        Camera camera = Camera.main;
        if (camera == null || Mouse.current == null) return new Vector2Int(int.MinValue, int.MinValue);
        Vector3 world = camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector3 local = farmView.transform.InverseTransformPoint(world);
        return new Vector2Int(Mathf.FloorToInt(local.x), -Mathf.FloorToInt(local.y));
    }

    private RanchFootprintView PlacingFootprint() => farmTask switch
    {
        FarmTask.Build => FarmRanchAssets.Footprint(CurrentBlueprint?.kind),
        FarmTask.Move when buildingToMove != null => FarmRanchAssets.Footprint(buildingToMove.kind),
        _ => null
    };

    private bool TileBuildable(int x, int y, OnlineRanchHome ignore)
    {
        if (x < 1 || y < 1 || x >= FarmWidth - 1 || y >= FarmHeight - 1) return false;
        var cell = new Vector3Int(x, -y, 0);
        Tilemap Layer(string name) => farmView.transform.Find(name)?.GetComponent<Tilemap>();
        Tilemap back = Layer("Back");
        if (back == null || !back.HasTile(cell)) return false;
        if (Layer("MapCollision")?.HasTile(cell) == true || Layer("Buildings")?.HasTile(cell) == true) return false;
        foreach (RectInt mark in FarmLandmarks)
            if (mark.Contains(new Vector2Int(x, y))) return false;
        foreach (OnlineRanchHome home in FarmRanchView.Homes)
            if (home != ignore && (ignore == null || home.id != ignore.id) && home.Reserves(x, y)) return false;
        if (occupiedFarmTiles.Contains(new Vector2Int(x, y))) return false;
        Vector2 center = farmView.TileCenter(x, y);
        Transform farmer = LocationManager.Instance?.LocalFarmer?.transform;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, new Vector2(0.8f, 0.8f), 0f))
        {
            if (hit.isTrigger || !hit.gameObject.activeInHierarchy) continue;
            if (farmer != null && hit.transform.IsChildOf(farmer)) continue;
            if (hit.gameObject.name.StartsWith("RanchBuilding:", StringComparison.Ordinal)) continue;
            if (hit is TilemapCollider2D || hit is CompositeCollider2D) continue;
            return false;
        }
        return true;
    }

    private bool SiteBuildable(RanchFootprintView footprint, Vector2Int origin, OnlineRanchHome ignore)
    {
        for (int y = 0; y < footprint.height; y++)
        for (int x = 0; x < footprint.width; x++)
            if (!TileBuildable(origin.x + x, origin.y + y, ignore)) return false;
        return footprint.doorX < 0 || TileBuildable(origin.x + footprint.doorX, origin.y + footprint.height, ignore);
    }

    private void UpdatePlacementPreview()
    {
        if (placementOverlay == null || farmView == null) return;
        RanchFootprintView footprint = PlacingFootprint();
        Vector2Int origin = MouseTile();
        int needed = footprint == null ? 0 : footprint.width * footprint.height + (footprint.doorX >= 0 ? 1 : 0);
        EnsurePlacementTiles(needed);
        for (int i = 0; i < placementTiles.Count; i++) placementTiles[i].enabled = i < needed;
        if (placementGhost != null) placementGhost.enabled = footprint != null;
        UpdateUpgradeHover(origin);
        if (footprint == null) return;

        int index = 0;
        void Place(int x, int y)
        {
            SpriteRenderer tile = placementTiles[index++];
            tile.sprite = TileBuildable(x, y, buildingToMove) ? goodTileSprite : badTileSprite;
            tile.transform.position = farmView.TileCenter(x, y) + new Vector3(-0.5f, -0.5f, 0f);
        }
        for (int y = 0; y < footprint.height; y++)
        for (int x = 0; x < footprint.width; x++)
            Place(origin.x + x, origin.y + y);
        if (footprint.doorX >= 0) Place(origin.x + footprint.doorX, origin.y + footprint.height);

        int level = farmTask == FarmTask.Move ? buildingToMove.level : 1;
        Sprite art = farmTask == FarmTask.Move ? FarmRanchView.HomeSprite(buildingToMove)
            : FarmRanchView.BuildingSprite(footprint.kind, level);
        if (placementGhost == null)
        {
            placementGhost = new GameObject("Ghost").AddComponent<SpriteRenderer>();
            placementGhost.transform.SetParent(placementOverlay, false);
            placementGhost.color = new Color(1f, 1f, 1f, 0.5f);
        }
        placementGhost.sprite = art;
        placementGhost.transform.position = FarmRanchView.FootprintOrigin(farmView, origin.x, origin.y, footprint.height);
        placementGhost.sortingOrder = WorldSortOrder.GetOrder(placementGhost.transform.position.y);
    }

    private void EnsurePlacementTiles(int count)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        if (goodTileSprite == null)
        {
            goodTileSprite = AtlasSprites.FromTop(cursors, 194, 388, 16, 16, Vector2.zero);
            badTileSprite = AtlasSprites.FromTop(cursors, 210, 388, 16, 16, Vector2.zero);
        }
        while (placementTiles.Count < count)
        {
            var tile = new GameObject("Placement Tile").AddComponent<SpriteRenderer>();
            tile.transform.SetParent(placementOverlay, false);
            tile.sortingOrder = short.MaxValue - 10;
            placementTiles.Add(tile);
        }
    }

    private OnlineRanchHome BuildingAt(Vector2Int tile) =>
        FarmRanchView.Homes.FirstOrDefault(home => home.Contains(tile.x, tile.y));

    private SpriteRenderer BuildingRenderer(OnlineRanchHome home) => home == null || farmView == null ? null :
        farmView.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(renderer =>
            renderer.gameObject.name == "RanchBuilding:" + home.id);

    private void UpdateUpgradeHover(Vector2Int tile)
    {
        bool hovering = paintTarget == null && !namingAnimal &&
            (farmTask is FarmTask.Upgrade or FarmTask.Demolish or FarmTask.Paint or FarmTask.PlaceAnimal ||
            farmTask == FarmTask.Move && buildingToMove == null);
        OnlineRanchHome home = hovering ? BuildingAt(tile) : null;
        SpriteRenderer renderer = BuildingRenderer(home);
        if (renderer != tintedBuilding) ClearBuildingTint();
        if (renderer == null) return;
        bool valid = farmTask switch
        {
            FarmTask.Upgrade => IsUpgradeTarget(home),
            FarmTask.Paint => CanPaint(home),
            FarmTask.PlaceAnimal => CanHouse(home),
            _ => true
        };
        renderer.color = farmTask == FarmTask.Demolish || !valid
            ? new Color(1f, 0.4f, 0.4f, 0.85f) : new Color(0.5f, 1f, 0.5f, 0.85f);
        tintedBuilding = renderer;
    }

    private void ClearBuildingTint()
    {
        if (tintedBuilding != null) tintedBuilding.color = Color.white;
        tintedBuilding = null;
    }

    private bool IsUpgradeTarget(OnlineRanchHome home)
    {
        RanchPlanView plan = CurrentBlueprint;
        return home != null && plan != null && home.kind == plan.kind && home.level == plan.level - 1 &&
            home.readyDay <= (inventory?.GetComponent<OnlineGameState>()?.Snapshot?.gameDay ?? 0);
    }

    private void ShowHud(string message)
    {
        hudMessage = message;
        hudMessageUntil = Time.unscaledTime + 3.5f;
    }

    private void FarmClick()
    {
        if (purchasePending || farmReturnAt >= 0f) return;
        var online = inventory?.GetComponent<OnlineGameState>();
        if (online == null) return;
        Vector2Int tile = MouseTile();
        RanchPlanView plan = CurrentBlueprint;
        switch (farmTask)
        {
            case FarmTask.Build:
            {
                RanchFootprintView footprint = PlacingFootprint();
                if (footprint == null || plan == null) return;
                if (!SiteBuildable(footprint, tile, null))
                {
                    GameAudio.Play("cancel");
                    ShowHud("Không thể xây ở đây.");
                    return;
                }
                purchasePending = true;
                online.OrderRanchHome(plan.kind, plan.level, "", tile.x, tile.y, error =>
                {
                    purchasePending = false;
                    if (error != null) { GameAudio.Play("cancel"); ShowHud(error); return; }
                    GameAudio.Play("axe");
                    HideOverlay();
                    farewellLine = $"Robin: Cảm ơn nhé! Mình sẽ bắt đầu ngay. {plan.name} sẽ xong trong {plan.days} ngày.";
                    farmReturnAt = Time.unscaledTime + 2f;
                });
                return;
            }
            case FarmTask.Upgrade:
            {
                OnlineRanchHome home = BuildingAt(tile);
                if (home == null || plan == null) return;
                if (!IsUpgradeTarget(home))
                {
                    GameAudio.Play("cancel");
                    ShowHud("Công trình này không nâng cấp được thành " + plan.name + ".");
                    return;
                }
                purchasePending = true;
                online.OrderRanchHome(plan.kind, plan.level, home.id, home.x, home.y, error =>
                {
                    purchasePending = false;
                    if (error != null) { GameAudio.Play("cancel"); ShowHud(error); return; }
                    GameAudio.Play("axe");
                    ClearBuildingTint();
                    farewellLine = $"Robin: Được rồi! {plan.name} sẽ xong trong {plan.days} ngày.";
                    farmReturnAt = Time.unscaledTime + 1.5f;
                });
                return;
            }
            case FarmTask.PlaceAnimal:
                if (!namingAnimal) ChooseAnimalHome(BuildingAt(tile));
                return;
            case FarmTask.Demolish:
            {
                OnlineRanchHome home = BuildingAt(tile);
                if (home == null) return;
                purchasePending = true;
                online.DemolishRanchHome(home.id, error =>
                {
                    purchasePending = false;
                    if (error != null) { GameAudio.Play("cancel"); ShowHud(error); return; }
                    tintedBuilding = null;
                    flashUntil = Time.unscaledTime + 0.35f;
                    GameAudio.Play("explosion");
                    CollectOccupiedTiles();
                    menuReturnAt = Time.unscaledTime + 1.5f;
                });
                return;
            }
            case FarmTask.Paint:
            {
                OnlineRanchHome home = BuildingAt(tile);
                if (home == null) return;
                if (!CanPaint(home))
                {
                    GameAudio.Play("cancel");
                    ShowHud(home.readyDay > online.Snapshot.gameDay
                        ? "Không thể sơn công trình đang thi công." : "Công trình này không sơn được.");
                    return;
                }
                OpenPaintMenu(home);
                return;
            }
            case FarmTask.Move:
            {
                if (buildingToMove == null)
                {
                    OnlineRanchHome home = BuildingAt(tile);
                    if (home == null) return;
                    if (home.readyDay > online.Snapshot.gameDay)
                    {
                        GameAudio.Play("cancel");
                        ShowHud("Không thể di chuyển công trình đang thi công.");
                        return;
                    }
                    ClearBuildingTint();
                    buildingToMove = home;
                    BuildingRenderer(home)?.gameObject.SetActive(false);
                    GameAudio.Play("axchop");
                    return;
                }
                RanchFootprintView footprint = PlacingFootprint();
                if (!SiteBuildable(footprint, tile, buildingToMove))
                {
                    GameAudio.Play("cancel");
                    ShowHud("Không thể đặt ở đây.");
                    return;
                }
                OnlineRanchHome moving = buildingToMove;
                purchasePending = true;
                online.MoveRanchHome(moving.id, tile.x, tile.y, error =>
                {
                    purchasePending = false;
                    if (error != null) { GameAudio.Play("cancel"); ShowHud(error); return; }
                    buildingToMove = null;
                    CollectOccupiedTiles();
                    GameAudio.Play("axchop");
                    GameAudio.Play("dirtyHit");
                });
                return;
            }
        }
    }

    private void HideOverlay()
    {
        foreach (SpriteRenderer tile in placementTiles) tile.enabled = false;
        if (placementGhost != null) placementGhost.enabled = false;
        if (placementOverlay != null) placementOverlay.gameObject.SetActive(false);
    }

    private void DrawFarmTask()
    {
        Texture2D font = StardewUiAtlas.FontBold;
        float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 1.5f);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            float width = Screen.width / scale, height = Screen.height / scale;
            if (Time.unscaledTime < flashUntil)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, (flashUntil - Time.unscaledTime) / 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
                GUI.color = previous;
            }
            if (paintTarget != null)
            {
                GUI.matrix = oldMatrix;
                DrawPaintMenu();
                return;
            }
            if (namingAnimal)
            {
                DrawNamingBox(width, height);
                return;
            }
            if (farmReturnAt < 0f && menuReturnAt < 0f)
            {
                string prompt = farmTask switch
                {
                    FarmTask.Upgrade => "Chọn " + FarmRanchAssets.Plan(CurrentBlueprint?.kind, (CurrentBlueprint?.level ?? 2) - 1)?.name + " để nâng cấp",
                    FarmTask.Move => buildingToMove == null ? "Chọn công trình cần di chuyển" : "Chọn vị trí mới",
                    FarmTask.Demolish => "Chọn công trình cần phá",
                    FarmTask.Paint => "Chọn công trình cần sơn",
                    FarmTask.PlaceAnimal => $"Chọn chuồng cho {animalBeingPurchased?.name} mới",
                    _ => "Chọn vị trí xây dựng"
                };
                float promptWidth = NpcBitmapText.GetStringWidth(prompt, 2.4f) + 80f;
                var scroll = new Rect((width - promptWidth) * 0.5f, 16f, promptWidth, 72f);
                DrawScroll(scroll);
                NpcBitmapText.Draw(font, new Rect(scroll.x, scroll.y + 18f, scroll.width, 40f), prompt, 2.4f, centered: true);
                var cancel = new Rect(width - 96f, height - 96f, 64f, 64f);
                bool overCancel = cancel.Contains(Event.current.mousePosition);
                if (SpriteButton(cancel, CancelSource, Color.white) && !purchasePending)
                {
                    if (farmTask == FarmTask.Move && buildingToMove != null)
                    {
                        BuildingRenderer(buildingToMove)?.gameObject.SetActive(true);
                        buildingToMove = null;
                        GameAudio.Play("smallSelect");
                    }
                    else ReturnToCarpenterMenu();
                    return;
                }
                if (!overCancel && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Event.current.Use();
                    FarmClick();
                }
            }
            if (!string.IsNullOrEmpty(hudMessage) && Time.unscaledTime < hudMessageUntil)
            {
                var box = new Rect(32f, height - 112f, Mathf.Min(width - 160f, NpcBitmapText.GetStringWidth(hudMessage, 1.9f) + 56f), 72f);
                StardewUiAtlas.DrawTextureBox(box, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
                DrawColored(new Rect(box.x + 28f, box.y + 22f, box.width - 40f, 36f), hudMessage, 1.9f, Color.red);
            }
        }
        finally { GUI.matrix = oldMatrix; }
    }

    // ── Paint menu ───────────────────────────────────────────────────────────────────────────

    private bool CanPaint(OnlineRanchHome home)
    {
        int day = inventory?.GetComponent<OnlineGameState>()?.Snapshot?.gameDay ?? 0;
        string planName = FarmRanchAssets.Plan(home.kind, Mathf.Max(1, home.level))?.name;
        return home.level > 0 && home.readyDay <= day && FarmRanchAssets.Paintable(planName) != null &&
            FarmRanchAssets.PaintMask(planName) != null;
    }

    private void OpenPaintMenu(OnlineRanchHome home)
    {
        string planName = FarmRanchAssets.Plan(home.kind, home.level)?.name;
        paintRanges = FarmRanchAssets.Paintable(planName);
        if (paintRanges == null) return;
        ClearBuildingTint();
        paintTarget = home;
        paintRegion = 0;
        activeSlider = -1;
        paintRegions = new OnlineRanchPaint[paintRanges.regions.Length];
        for (int i = 0; i < paintRegions.Length; i++)
        {
            OnlineRanchPaint saved = home.paint != null && i < home.paint.Length ? home.paint[i] : null;
            paintRegions[i] = saved != null && saved.custom
                ? new OnlineRanchPaint { custom = true, hue = saved.hue, saturation = saved.saturation, lightness = saved.lightness }
                : DefaultPaint(i);
        }
        foreach (SpriteRenderer tile in placementTiles) tile.enabled = false;
        RefreshPaintPreview();
        GameAudio.Play("bigSelect");
    }

    private OnlineRanchPaint DefaultPaint(int region) => new()
    {
        custom = false, hue = 0, saturation = RanchPainter.MaxSaturation,
        lightness = (paintRanges.regions[region].min + paintRanges.regions[region].max) / 2
    };

    private void ClosePaintMenu()
    {
        paintTarget = null;
        activeSlider = -1;
    }

    private void RefreshPaintPreview()
    {
        if (paintTarget == null) return;
        string planName = FarmRanchAssets.Plan(paintTarget.kind, paintTarget.level)?.name;
        paintPreview = RanchPainter.Paint(FarmRanchAssets.Art(planName), FarmRanchAssets.PaintMask(planName),
            paintRegions, paintPreview);
    }

    private void SetPaintValue(int slider, int value)
    {
        OnlineRanchPaint region = paintRegions[paintRegion];
        RanchPaintRangeView range = paintRanges.regions[paintRegion];
        switch (slider)
        {
            case 0:
                value = Mathf.Clamp(value, 0, 360);
                if (region.custom && region.hue == value) return;
                region.hue = value;
                break;
            case 1:
                value = Mathf.Clamp(value, 0, RanchPainter.MaxSaturation);
                if (region.custom && region.saturation == value) return;
                region.saturation = value;
                break;
            default:
                value = Mathf.Clamp(value, range.min, range.max);
                if (region.custom && region.lightness == value) return;
                region.lightness = value;
                break;
        }
        region.custom = true;
        RefreshPaintPreview();
    }

    private (int Min, int Max, int Value) SliderValues(int slider)
    {
        OnlineRanchPaint region = paintRegions[paintRegion];
        RanchPaintRangeView range = paintRanges.regions[paintRegion];
        return slider switch
        {
            0 => (0, 360, region.hue),
            1 => (0, RanchPainter.MaxSaturation, region.saturation),
            _ => (range.min, range.max, region.lightness)
        };
    }

    private void UpdatePaintSlider()
    {
        if (activeSlider < 0 || Mouse.current == null) return;
        if (!Mouse.current.leftButton.isPressed) { activeSlider = -1; return; }
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector2 gui = paintMatrix.inverse.MultiplyPoint(new Vector3(screen.x, Screen.height - screen.y, 0f));
        float t = Mathf.Clamp01((gui.x - paintSliderArea.x) / paintSliderArea.width);
        var values = SliderValues(activeSlider);
        SetPaintValue(activeSlider, values.Min + Mathf.RoundToInt(t * (values.Max - values.Min)));
    }

    private void DrawPaintMenu()
    {
        const float paneWidth = 512f, colorWidth = 448f, paneHeight = 576f, gap = 16f;
        const float total = paneWidth + gap + colorWidth;
        float scale = Mathf.Min(1f, Screen.width / (total + 48f), Screen.height / (paneHeight + 48f));
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        Matrix4x4 oldMatrix = GUI.matrix;
        paintMatrix = Matrix4x4.TRS(new Vector3((Screen.width - total * scale) * 0.5f,
            (Screen.height - paneHeight * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.matrix = paintMatrix;
        try { DrawPaintPanes(paneWidth, colorWidth, paneHeight, gap); }
        finally { GUI.matrix = oldMatrix; }
    }

    private void DrawPaintPanes(float paneWidth, float colorWidth, float paneHeight, float gap)
    {
        Texture2D font = StardewUiAtlas.FontBold;
        Texture2D cursors2 = FarmRanchAssets.Instance?.cursors2;
        var preview = new Rect(0f, 0f, paneWidth, paneHeight);
        var colorPane = new Rect(paneWidth + gap, 0f, colorWidth, paneHeight);
        StardewUiAtlas.DrawTextureBox(preview, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        StardewUiAtlas.DrawTextureBox(colorPane, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        if (paintPreview != null)
        {
            Vector2 size = new(paintPreview.width * 4f, (paintPreview.height - 16) * 4f);
            float fit = Mathf.Min(1f, (paneWidth - 32f) / size.x, (paneHeight - 32f) / size.y);
            size *= fit;
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTextureWithTexCoords(new Rect(preview.center - new Vector2(0f, 16f) - size * 0.5f, size),
                    paintPreview, new Rect(0f, 16f / paintPreview.height, 1f, (paintPreview.height - 16f) / paintPreview.height));
        }

        var inner = new Rect(colorPane.x + 32f, colorPane.y + 32f, colorPane.width - 64f, colorPane.height - 64f);
        var prev = new Rect(inner.x, inner.y, 64f, 64f);
        var next = new Rect(inner.xMax - 64f, inner.y, 64f, 64f);
        string regionKey = paintRanges.regions[paintRegion].name;
        string regionName = PaintRegionNames.TryGetValue(regionKey, out string label) ? label : regionKey;
        NpcBitmapText.Draw(font, new Rect(inner.x + 64f, inner.y + 14f, inner.width - 128f, 40f), regionName, 2.6f,
            centered: true);
        if (SpriteButton(prev, PreviousRegionSource, Color.white))
        {
            paintRegion = (paintRegion + paintRegions.Length - 1) % paintRegions.Length;
            GameAudio.Play("shwip");
        }
        if (SpriteButton(next, NextRegionSource, Color.white))
        {
            paintRegion = (paintRegion + 1) % paintRegions.Length;
            GameAudio.Play("shwip");
        }

        float y = inner.y + 64f;
        float x = inner.x;
        if (cursors2 != null && AtlasButton(cursors2, new Rect(x, y, 64f, 64f), DefaultColorSource))
        {
            paintRegions[paintRegion] = DefaultPaint(paintRegion);
            RefreshPaintPreview();
            GameAudio.Play("coin");
        }
        x += 80f;
        for (int i = 0; i < SavedPaintColors.Count; i++)
        {
            if (x + 64f > inner.xMax) { x = inner.x; y += 72f; }
            Vector3 saved = SavedPaintColors[i];
            var swatch = new Rect(x, y, 64f, 64f);
            if (cursors2 != null &&
                AtlasButton(cursors2, swatch, SavedColorSource, RanchPainter.Swatch(saved.x, saved.y, saved.z)))
            {
                RanchPaintRangeView range = paintRanges.regions[paintRegion];
                SetPaintValue(0, Mathf.RoundToInt(saved.x));
                SetPaintValue(1, Mathf.RoundToInt(saved.y));
                SetPaintValue(2, Mathf.RoundToInt(Mathf.Lerp(range.min, range.max, saved.z)));
                GameAudio.Play("coin");
            }
            x += 80f;
        }
        if (x + 64f > inner.xMax) { x = inner.x; y += 72f; }
        if (SpriteButton(new Rect(x, y, 64f, 64f), CopyColorSource, Color.white))
        {
            OnlineRanchPaint region = paintRegions[paintRegion];
            if (!region.custom) GameAudio.Play("cancel");
            else
            {
                RanchPaintRangeView range = paintRanges.regions[paintRegion];
                if (SavedPaintColors.Count >= 8) SavedPaintColors.RemoveAt(0);
                SavedPaintColors.Add(new Vector3(region.hue, region.saturation,
                    Mathf.InverseLerp(range.min, range.max, region.lightness)));
                GameAudio.Play("coin");
            }
        }

        y += 80f;
        OnlineRanchPaint current = paintRegions[paintRegion];
        RanchPaintRangeView currentRange = paintRanges.regions[paintRegion];
        if (current.custom)
        {
            var swatch = new Rect(inner.xMax - 64f, y, 64f, 64f);
            Fill(new Rect(swatch.x - 4f, swatch.y - 4f, 72f, 72f), TextColor);
            Fill(swatch, RanchPainter.Swatch(current.hue, current.saturation,
                Mathf.InverseLerp(currentRange.min, currentRange.max, current.lightness)));
        }
        paintSliderArea = new Rect(inner.x, y, inner.width - 100f, 12f);
        for (int slider = 0; slider < 3; slider++)
        {
            var bar = new Rect(inner.x, y + slider * 24f, inner.width - 100f, 12f);
            var values = SliderValues(slider);
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float value = Mathf.Lerp(values.Min, values.Max, i / (float)segments);
                Color color = slider switch
                {
                    0 => RanchPainter.Swatch(value, 100f, 1f),
                    1 => RanchPainter.Swatch(current.hue, value, 1f),
                    _ => RanchPainter.Swatch(current.hue, current.saturation,
                        Mathf.InverseLerp(values.Min, values.Max, value))
                };
                Fill(new Rect(bar.x + bar.width / segments * i, bar.y, Mathf.Ceil(bar.width / segments), bar.height), color);
            }
            float t = Mathf.InverseLerp(values.Min, values.Max, values.Value);
            StardewUiAtlas.DrawCursorsSprite(new Rect(Mathf.Lerp(bar.x, bar.xMax, t) - 8f, bar.y - 4f, 16f, 20f),
                SliderHandleSource);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                new Rect(bar.x - 8f, bar.y - 6f, bar.width + 16f, bar.height + 12f).Contains(e.mousePosition))
            {
                activeSlider = slider;
                float position = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width);
                SetPaintValue(slider, values.Min + Mathf.RoundToInt(position * (values.Max - values.Min)));
                e.Use();
            }
        }

        var ok = new Rect(colorPane.xMax - 64f - 16f, colorPane.yMax - 64f - 16f, 64f, 64f);
        if (SpriteButton(ok, ConfirmSource, Color.white) && !purchasePending) SubmitPaint();
        if (!string.IsNullOrEmpty(hudMessage) && Time.unscaledTime < hudMessageUntil)
            DrawColored(new Rect(colorPane.x + 32f, ok.y + 14f, colorPane.width - 140f, 40f), hudMessage, 1.6f, Color.red);
    }

    private void SubmitPaint()
    {
        var online = inventory?.GetComponent<OnlineGameState>();
        if (online == null || paintTarget == null) return;
        purchasePending = true;
        online.PaintRanchHome(paintTarget.id, paintRegions, error =>
        {
            purchasePending = false;
            if (error != null) { GameAudio.Play("cancel"); ShowHud(error); return; }
            GameAudio.Play("bigDeSelect");
            ClosePaintMenu();
        });
    }

    private static void Fill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static bool AtlasButton(Texture2D atlas, Rect rect, Rect source, Color? tint = null)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        Rect art = hover ? new Rect(rect.center - rect.size * 0.55f, rect.size * 1.1f) : rect;
        if (Event.current.type == EventType.Repaint)
        {
            Color previous = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(art, atlas, StardewUiAtlas.ToUv(source, atlas.width, atlas.height));
            GUI.color = previous;
        }
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    // ── Drawing helpers ──────────────────────────────────────────────────────────────────────

    private static void DrawScroll(Rect rect)
    {
        const float end = 48f;
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.x, rect.y, end, rect.height), new Rect(325, 318, 12, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.x + end, rect.y, rect.width - 2 * end, rect.height),
            new Rect(337, 318, 1, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.xMax - end, rect.y, end, rect.height), new Rect(338, 318, 12, 18));
    }

    private static bool SpriteButton(Rect rect, Rect source, Color tint)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        Rect art = hover ? new Rect(rect.center - rect.size * 0.55f, rect.size * 1.1f) : rect;
        StardewUiAtlas.DrawCursorsSprite(art, source, tint);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private static void DrawColored(Rect rect, string text, float scale, Color color) =>
        GameText.Draw(rect, text, GameText.Face.Dialogue, color);

    private static void DrawItemIcon(ItemData item, Rect slot)
    {
        Sprite icon = item?.GetIcon();
        if (icon == null || Event.current.type != EventType.Repaint) return;
        Rect source = icon.textureRect;
        GUI.DrawTextureWithTexCoords(slot, icon.texture,
            new Rect(source.x / icon.texture.width, source.y / icon.texture.height,
                source.width / icon.texture.width, source.height / icon.texture.height));
    }

    private static void DrawHoverText(string text) => GameText.DrawTooltip(text, null, CarpenterWidth, CarpenterHeight);
}
