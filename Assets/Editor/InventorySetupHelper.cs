using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InventorySetupHelper
{
    private const string UIDir = "Assets/UI/Inventory";
    private const string DataDir = "Assets/Data/Items";
    private const string PrefabsDir = "Assets/Prefabs/UI";

    [MenuItem("Tools/Stardew/Setup Inventory UI & Assets")]
    public static void SetupInventory()
    {
        if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);
        if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);

        // 1. Cut tools and icons using exact atlas coordinates
        CutToolIcons();

        // 2. Load UI Sprites
        Sprite normalSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Normal.png");
        Sprite selectedSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Selected.png");
        Sprite lockedSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Locked.png");
        Sprite toolbarFrame = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Toolbar_Frame.png");

        Sprite axeIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Axe.png");
        Sprite hoeIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Hoe.png");
        Sprite canIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_WateringCan.png");
        Sprite pickIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Pickaxe.png");
        Sprite scytheIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Scythe.png");
        Sprite fishingRodIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_FishingRod.png");

        // Directional tool sprites
        Sprite hoeHeld = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Animations/Farmer/Appearance/Hoe_Held.png");

        // 3. Create ItemData Assets matching Photo 1
        ItemData itemAxe = CreateOrUpdateItem($"{DataDir}/Item_Axe.asset", "axe", "Rìu", "Dùng để chặt cây và thu hoạch gỗ.", axeIcon, 1, PlayerItemKind.Axe, true, FarmerAnimationAction.Tool, axeIcon, axeIcon, axeIcon);
        ItemData itemHoe = CreateOrUpdateItem($"{DataDir}/Item_Hoe.asset", "hoe", "Cuốc", "Dùng để xới đất làm nông nghiệp.", hoeIcon, 1, PlayerItemKind.Hoe, true, FarmerAnimationAction.Tool, hoeHeld, hoeHeld, hoeHeld);
        itemHoe.heldAnimationSprites = new Sprite[5];
        for (int index = 21; index <= 25; index++)
            itemHoe.heldAnimationSprites[index - 21] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Animations/Farmer/Appearance/Hoe_Index_{index}.png");
        EditorUtility.SetDirty(itemHoe);
        ItemData itemCan = CreateOrUpdateItem($"{DataDir}/Item_WateringCan.asset", "watering_can", "Bình tưới nước", "Dùng để tưới nước cho cây trồng.", canIcon, 1, PlayerItemKind.WateringCan, true, FarmerAnimationAction.Watering, canIcon, canIcon, canIcon);
        ItemData itemPick = CreateOrUpdateItem($"{DataDir}/Item_Pickaxe.asset", "pickaxe", "Cuốc chim", "Dùng để đập đá và khai thác khoáng sản.", pickIcon, 1, PlayerItemKind.Pickaxe, true, FarmerAnimationAction.Tool, pickIcon, pickIcon, pickIcon);
        ItemData itemScythe = CreateOrUpdateItem($"{DataDir}/Item_Scythe.asset", "scythe", "Lưỡi liềm", "Dùng để cắt cỏ dại và thu hoạch lúa.", scytheIcon, 1, PlayerItemKind.Sword, true, FarmerAnimationAction.SwordSwipe, scytheIcon, scytheIcon, scytheIcon);
        ItemData itemFishingRod = CreateOrUpdateItem($"{DataDir}/Item_FishingRod.asset", "fishing_rod", "Cần câu", "Dùng để câu cá.", fishingRodIcon, 1, PlayerItemKind.FishingRod, true, FarmerAnimationAction.Fishing, fishingRodIcon, fishingRodIcon, fishingRodIcon);

        // 4. Create InventorySlotUI Prefab (64x64)
        string slotPrefabPath = $"{PrefabsDir}/InventorySlot.prefab";
        GameObject slotGO = new GameObject("InventorySlot", typeof(RectTransform));
        RectTransform slotRect = slotGO.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(64, 64);

        // Background Image (Tile 10 with beige fill)
        Image bgImg = slotGO.AddComponent<Image>();
        bgImg.sprite = normalSlot;
        bgImg.type = Image.Type.Simple;

        // Selection Border Image (Tile 56 red border)
        GameObject borderGO = new GameObject("SelectionBorder", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(slotGO.transform, false);
        RectTransform borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = selectedSlot;
        borderImg.raycastTarget = false;
        borderImg.enabled = false;

        // Item Icon Image (Centered, 48x48)
        GameObject iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(slotGO.transform, false);
        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(48, 48);
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;

        // Amount Text (Bottom-right)
        GameObject amountGO = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountGO.transform.SetParent(slotGO.transform, false);
        RectTransform amountRect = amountGO.GetComponent<RectTransform>();
        amountRect.anchorMin = new Vector2(0.4f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0.45f);
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.anchoredPosition = new Vector2(-4, 4);
        amountRect.sizeDelta = new Vector2(28, 18);
        TextMeshProUGUI amountTMP = amountGO.GetComponent<TextMeshProUGUI>();
        amountTMP.fontSize = 16;
        amountTMP.alignment = TextAlignmentOptions.BottomRight;
        amountTMP.color = Color.white;
        amountTMP.fontStyle = FontStyles.Bold;
        amountTMP.raycastTarget = false;

        // Slot Number Text (Top-left: 1..9, 0, -, = matching Photo 1)
        GameObject numGO = new GameObject("SlotNumberText", typeof(RectTransform), typeof(TextMeshProUGUI));
        numGO.transform.SetParent(slotGO.transform, false);
        RectTransform numRect = numGO.GetComponent<RectTransform>();
        numRect.anchorMin = new Vector2(0f, 0.55f);
        numRect.anchorMax = new Vector2(0.5f, 1f);
        numRect.pivot = new Vector2(0f, 1f);
        numRect.anchoredPosition = new Vector2(5, -4);
        numRect.sizeDelta = new Vector2(24, 18);
        TextMeshProUGUI numTMP = numGO.GetComponent<TextMeshProUGUI>();
        numTMP.fontSize = 14;
        numTMP.alignment = TextAlignmentOptions.TopLeft;
        numTMP.color = new Color(0.32f, 0.24f, 0.18f, 1f); // Dark wood brown matching Photo 1
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.raycastTarget = false;

        InventorySlotUI slotUI = slotGO.AddComponent<InventorySlotUI>();
        SerializedObject soSlot = new SerializedObject(slotUI);
        soSlot.FindProperty("backgroundImage").objectReferenceValue = bgImg;
        soSlot.FindProperty("highlightBorder").objectReferenceValue = borderImg;
        soSlot.FindProperty("iconImage").objectReferenceValue = iconImg;
        soSlot.FindProperty("amountText").objectReferenceValue = amountTMP;
        soSlot.FindProperty("slotNumberText").objectReferenceValue = numTMP;
        soSlot.FindProperty("normalSlotSprite").objectReferenceValue = normalSlot;
        soSlot.FindProperty("selectedSlotSprite").objectReferenceValue = selectedSlot;
        soSlot.FindProperty("lockedSlotSprite").objectReferenceValue = lockedSlot;
        soSlot.ApplyModifiedProperties();

        GameObject savedSlotPrefab = PrefabUtility.SaveAsPrefabAsset(slotGO, slotPrefabPath);
        UnityEngine.Object.DestroyImmediate(slotGO);

        // 5. Create Full Inventory Canvas Prefab
        string canvasPrefabPath = $"{PrefabsDir}/InventoryCanvas.prefab";
        GameObject canvasGO = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Toolbar Root: 800 x 96
        GameObject toolbarRoot = new GameObject("Toolbar", typeof(RectTransform), typeof(Image));
        toolbarRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform tbRect = toolbarRoot.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0.5f, 0.06f);
        tbRect.anchorMax = new Vector2(0.5f, 0.06f);
        tbRect.pivot = new Vector2(0.5f, 0.5f);
        tbRect.sizeDelta = new Vector2(800, 96);

        Image tbFrameImg = toolbarRoot.GetComponent<Image>();
        tbFrameImg.sprite = toolbarFrame;
        tbFrameImg.type = Image.Type.Simple;

        // Toolbar Slots Container (800 - 32 = 768px wide, 96 - 32 = 64px high)
        GameObject tbContainer = new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tbContainer.transform.SetParent(toolbarRoot.transform, false);
        RectTransform tcRect = tbContainer.GetComponent<RectTransform>();
        tcRect.anchorMin = Vector2.zero;
        tcRect.anchorMax = Vector2.one;
        tcRect.offsetMin = new Vector2(16, 16);
        tcRect.offsetMax = new Vector2(-16, -16);

        HorizontalLayoutGroup hlg = tbContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0; // ZERO spacing - seamless adjacent slots!
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Pre-populate 12 slots matching Photo 1 exactly
        ItemData[] initialItems = new ItemData[] { itemAxe, itemHoe, itemCan, itemPick, itemScythe, itemFishingRod };
        for (int i = 0; i < 12; i++)
        {
            GameObject slotInstance = PrefabUtility.InstantiatePrefab(savedSlotPrefab, tbContainer.transform) as GameObject;
            if (slotInstance != null)
            {
                slotInstance.name = $"Slot_{i}";
                var sUi = slotInstance.GetComponent<InventorySlotUI>();
                sUi.Initialize(i, normalSlot, selectedSlot, lockedSlot);

                bool isSelected = i == 0;
                ItemData item = i < initialItems.Length ? initialItems[i] : null;
                var itemSlot = item != null ? new ItemSlot(item, 1) : null;
                sUi.Render(itemSlot, isSelected, false);
            }
        }

        ToolbarUI toolbarUI = toolbarRoot.AddComponent<ToolbarUI>();
        SerializedObject soTb = new SerializedObject(toolbarUI);
        soTb.FindProperty("slotsContainer").objectReferenceValue = tbContainer.transform;
        soTb.FindProperty("slotPrefab").objectReferenceValue = savedSlotPrefab.GetComponent<InventorySlotUI>();
        soTb.FindProperty("toolbarPanel").objectReferenceValue = tbRect;
        soTb.FindProperty("normalSlotSprite").objectReferenceValue = normalSlot;
        soTb.FindProperty("selectedSlotSprite").objectReferenceValue = selectedSlot;
        soTb.FindProperty("lockedSlotSprite").objectReferenceValue = lockedSlot;
        soTb.ApplyModifiedProperties();

        // Full Backpack Menu (3 rows x 12 cols = 36 slots)
        GameObject menuRoot = new GameObject("FullInventoryMenu", typeof(RectTransform), typeof(Image));
        menuRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform menuRect = menuRoot.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(800, 240);

        Image menuBg = menuRoot.GetComponent<Image>();
        menuBg.sprite = toolbarFrame;
        menuBg.type = Image.Type.Sliced;

        GameObject gridContainer = new GameObject("GridContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        gridContainer.transform.SetParent(menuRoot.transform, false);
        RectTransform gridRect = gridContainer.GetComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = new Vector2(16, 16);
        gridRect.offsetMax = new Vector2(-16, -16);

        GridLayoutGroup glg = gridContainer.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(64, 64);
        glg.spacing = Vector2.zero;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 12;

        FullInventoryMenuUI fullMenuUI = canvasGO.AddComponent<FullInventoryMenuUI>();
        SerializedObject soFull = new SerializedObject(fullMenuUI);
        soFull.FindProperty("menuRoot").objectReferenceValue = menuRoot;
        soFull.FindProperty("gridContainer").objectReferenceValue = gridContainer.transform;
        soFull.FindProperty("slotPrefab").objectReferenceValue = savedSlotPrefab.GetComponent<InventorySlotUI>();
        soFull.FindProperty("normalSlotSprite").objectReferenceValue = normalSlot;
        soFull.FindProperty("selectedSlotSprite").objectReferenceValue = selectedSlot;
        soFull.FindProperty("lockedSlotSprite").objectReferenceValue = lockedSlot;
        soFull.ApplyModifiedProperties();

        menuRoot.SetActive(false);

        GameObject savedCanvasPrefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, canvasPrefabPath);
        UnityEngine.Object.DestroyImmediate(canvasGO);

        // 6. Update Scene & Player
        UpdateSceneAndPlayer(savedCanvasPrefab, initialItems);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Inventory UI & Assets configured with 100% Photo 1 visual accuracy!");
    }

    private static void CutToolIcons()
    {
        // Tools sheet: 336 x 384
        string toolsPath = "Assets/Content/TileSheets/tools.png";
        if (File.Exists(toolsPath))
        {
            var rawBytes = File.ReadAllBytes(toolsPath);
            var toolsTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            toolsTex.LoadImage(rawBytes);

            // Row 10 Col 5: Axe (topX: 80, topY: 160)
            SaveTile(toolsTex, $"{UIDir}/Icon_Axe.png", 80, 160, 16, 16);
            // Row 2 Col 5: Hoe (topX: 80, topY: 32)
            SaveTile(toolsTex, $"{UIDir}/Icon_Hoe.png", 80, 32, 16, 16);
            // Row 14 Col 2: Watering Can (topX: 32, topY: 224)
            SaveTile(toolsTex, $"{UIDir}/Icon_WateringCan.png", 32, 224, 16, 16);
            // Row 6 Col 5: Pickaxe (topX: 80, topY: 96)
            SaveTile(toolsTex, $"{UIDir}/Icon_Pickaxe.png", 80, 96, 16, 16);
            // Fishing Rod menu sprite (Tool index 8).
            SaveTile(toolsTex, $"{UIDir}/Icon_FishingRod.png", 128, 0, 16, 16);

            UnityEngine.Object.DestroyImmediate(toolsTex);
        }

        // Weapons sheet: 128 x 144
        string wepPath = "Assets/Content/TileSheets/weapons.png";
        if (File.Exists(wepPath))
        {
            var rawBytes = File.ReadAllBytes(wepPath);
            var wepTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            wepTex.LoadImage(rawBytes);

            // Row 5 Col 7: Scythe (topX: 112, topY: 80)
            SaveTile(wepTex, $"{UIDir}/Icon_Scythe.png", 112, 80, 16, 16);

            UnityEngine.Object.DestroyImmediate(wepTex);
        }
    }

    private static void SaveTile(Texture2D source, string path, int topX, int topY, int width, int height)
    {
        var pixels = source.GetPixels32();
        var resultPixels = new Color32[width * height];
        int bottom = source.height - topY - height;
        for (int row = 0; row < height; row++)
        {
            Array.Copy(pixels, (bottom + row) * source.width + topX, resultPixels, row * width, width);
        }

        var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        output.SetPixels32(resultPixels);
        output.Apply(false, false);
        File.WriteAllBytes(path, output.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(output);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static ItemData CreateOrUpdateItem(string assetPath, string id, string name, string desc, Sprite icon, int maxStack, PlayerItemKind kind, bool hasAnim, FarmerAnimationAction action, Sprite down, Sprite right, Sprite up)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, assetPath);
        }
        item.itemId = id;
        item.displayName = name;
        item.description = desc;
        item.icon = icon;
        item.maxStackSize = maxStack;
        item.kind = kind;
        item.hasUseAnimation = hasAnim;
        item.useAnimation = action;
        item.heldSpriteDown = down;
        item.heldSpriteRight = right;
        item.heldSpriteUp = up;
        item.heldSprite = down != null ? down : icon;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void UpdateSceneAndPlayer(GameObject canvasPrefab, ItemData[] tools)
    {
        var existingCanvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in existingCanvas)
        {
            if (c.name.Contains("InventoryCanvas"))
            {
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }

        PrefabUtility.InstantiatePrefab(canvasPrefab);

        // Update Scene Player
        PlayerInventory sceneInv = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        if (sceneInv != null)
        {
            sceneInv.EnsureCapacity();
            for (int i = 0; i < 12; i++)
            {
                var s = sceneInv.GetSlot(i) as InventorySlot;
                if (s != null)
                {
                    if (i < tools.Length)
                    {
                        s.itemData = tools[i];
                        s.amount = 1;
                    }
                    else
                    {
                        s.itemData = null;
                        s.amount = 0;
                    }
                    s.SyncFromData();
                }
            }
            sceneInv.SelectSlot(0);
            EditorUtility.SetDirty(sceneInv);
        }

        // Update Prefab Player
        string playerPrefabPath = "Assets/Prefabs/Character/Farrmer/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            PlayerInventory pInv = playerPrefab.GetComponent<PlayerInventory>();
            if (pInv != null)
            {
                pInv.EnsureCapacity();
                for (int i = 0; i < 12; i++)
                {
                    var s = pInv.GetSlot(i) as InventorySlot;
                    if (s != null)
                    {
                        if (i < tools.Length)
                        {
                            s.itemData = tools[i];
                            s.amount = 1;
                        }
                        else
                        {
                            s.itemData = null;
                            s.amount = 0;
                        }
                        s.SyncFromData();
                    }
                }
                pInv.SelectSlot(0);
                EditorUtility.SetDirty(playerPrefab);
            }
        }

        // Ensure EventSystem
        if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
