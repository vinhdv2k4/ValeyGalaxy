using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FullInventoryMenuBuilder
{
    private const string UIDir = "Assets/UI/Inventory";
    private const string PrefabsDir = "Assets/Prefabs/UI";

    [MenuItem("Tools/Stardew/Build Full Stardew Inventory Menu")]
    public static void BuildMenu()
    {
        // 1. Configure Sprites
        ConfigureSprite($"{UIDir}/Menu_Window_Frame_864x664.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Portrait_Farmer.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Button_Close.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Button_Junimo.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Button_Organize.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/TrashCan.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Icon_Pet_Dog.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Icon_Calendar.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Equipment_Hat.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Equipment_Ring.png", Vector4.zero);
        ConfigureSprite($"{UIDir}/Equipment_Boots.png", Vector4.zero);

        string[] tabNames = { "Inventory", "Skills", "Social", "Map", "Crafting", "Collections", "Options", "Exit", "Extra_Hammer" };
        foreach (var t in tabNames)
        {
            ConfigureSprite($"{UIDir}/Tab_{t}.png", Vector4.zero);
        }

        AssetDatabase.Refresh();

        // 2. Load Assets
        Sprite menuFrame = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Menu_Window_Frame_864x664.png");
        Sprite portraitFarmer = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Portrait_Farmer.png");
        Sprite normalSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Normal.png");
        Sprite selectedSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Selected.png");
        Sprite lockedSlot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Slot_Locked.png");
        Sprite toolbarFrame = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Toolbar_Frame.png");

        Sprite closeBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Button_Close.png");
        Sprite junimoBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Button_Junimo.png");
        Sprite orgBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Button_Organize.png");
        Sprite trashCanSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/TrashCan.png");
        Sprite dogIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Pet_Dog.png");
        Sprite calendarIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Icon_Calendar.png");

        Sprite hatIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Equipment_Hat.png");
        Sprite ringIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Equipment_Ring.png");
        Sprite bootsIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/Equipment_Boots.png");

        GameObject slotPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/InventorySlot.prefab");
        InventorySlotUI slotPrefab = slotPrefabGO.GetComponent<InventorySlotUI>();

        // 3. Load Canvas Prefab
        string canvasPrefabPath = $"{PrefabsDir}/InventoryCanvas.prefab";
        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(canvasPrefabPath);

        // Remove old FullInventoryMenu if exists
        Transform oldMenu = canvasRoot.transform.Find("FullInventoryMenu");
        if (oldMenu != null)
        {
            UnityEngine.Object.DestroyImmediate(oldMenu.gameObject);
        }

        FullInventoryMenuUI existingComp = canvasRoot.GetComponent<FullInventoryMenuUI>();
        if (existingComp != null)
        {
            UnityEngine.Object.DestroyImmediate(existingComp);
        }

        // 4. Create FullInventoryMenu Root (864 x 664)
        GameObject menuRoot = new GameObject("FullInventoryMenu", typeof(RectTransform));
        menuRoot.transform.SetParent(canvasRoot.transform, false);
        RectTransform menuRect = menuRoot.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(864, 664);
        menuRect.anchoredPosition = Vector2.zero;

        // 4a. Menu Window Background Frame
        GameObject frameGO = new GameObject("MenuFrame", typeof(RectTransform), typeof(Image));
        frameGO.transform.SetParent(menuRoot.transform, false);
        RectTransform frameRect = frameGO.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(864, 664);
        frameRect.anchoredPosition = Vector2.zero;
        Image frameImg = frameGO.GetComponent<Image>();
        frameImg.sprite = menuFrame;
        frameImg.type = Image.Type.Simple;
        frameImg.raycastTarget = false;

        // 5. Build Tab Strip (protruding above top edge of frame Y = +332)
        GameObject tabStripGO = new GameObject("MenuTabStrip", typeof(RectTransform), typeof(MenuTabStripUI));
        tabStripGO.transform.SetParent(menuRoot.transform, false);
        RectTransform tabStripRect = tabStripGO.GetComponent<RectTransform>();
        tabStripRect.anchorMin = new Vector2(0.5f, 0.5f);
        tabStripRect.anchorMax = new Vector2(0.5f, 0.5f);
        tabStripRect.pivot = new Vector2(0.5f, 0.5f);
        tabStripRect.anchoredPosition = Vector2.zero;
        tabStripRect.sizeDelta = new Vector2(864, 664);
        MenuTabStripUI tabStripUI = tabStripGO.GetComponent<MenuTabStripUI>();

        SerializedObject soTabs = new SerializedObject(tabStripUI);
        SerializedProperty tabsProp = soTabs.FindProperty("tabs");
        tabsProp.ClearArray();

        string[] tabSpriteNames = { "Tab_Inventory", "Tab_Skills", "Tab_Social", "Tab_Map", "Tab_Crafting", "Tab_Collections", "Tab_Options", "Tab_Exit" };
        float[] tabXPositions = { -336, -272, -208, -144, -80, -16, 48, 112 };

        for (int i = 0; i < tabSpriteNames.Length; i++)
        {
            GameObject tabGO = new GameObject($"Tab_{tabNames[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
            tabGO.transform.SetParent(tabStripGO.transform, false);
            RectTransform tRect = tabGO.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.5f);
            tRect.anchorMax = new Vector2(0.5f, 0.5f);
            tRect.pivot = new Vector2(0.5f, 0.5f);
            // Store the base position; MenuTabStripUI raises the active tab.
            float yPos = 352f;
            tRect.anchoredPosition = new Vector2(tabXPositions[i], yPos);
            tRect.sizeDelta = new Vector2(64, 64);

            Image tabImg = tabGO.GetComponent<Image>();
            tabImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIDir}/{tabSpriteNames[i]}.png");
            tabImg.preserveAspect = true;

            tabsProp.InsertArrayElementAtIndex(i);
            SerializedProperty entry = tabsProp.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("tabName").stringValue = tabNames[i];
            entry.FindPropertyRelative("tabTransform").objectReferenceValue = tRect;
            entry.FindPropertyRelative("tabButton").objectReferenceValue = tabGO.GetComponent<Button>();
        }

        // Close Red 'X' Button
        GameObject closeGO = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(menuRoot.transform, false);
        RectTransform closeRect = closeGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0.5f);
        closeRect.anchorMax = new Vector2(0.5f, 0.5f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(420, 356);
        closeRect.sizeDelta = new Vector2(48, 48);
        Image closeImg = closeGO.GetComponent<Image>();
        closeImg.sprite = closeBtnSprite;
        closeImg.preserveAspect = true;
        soTabs.FindProperty("closeButton").objectReferenceValue = closeGO.GetComponent<Button>();
        soTabs.ApplyModifiedProperties();

        // 6. Build Top Section: Backpack Grid Area (3 rows x 12 cols = 36 slots)
        GameObject topSectionGO = new GameObject("BackpackGridArea", typeof(RectTransform), typeof(BackpackGridUI));
        topSectionGO.transform.SetParent(menuRoot.transform, false);
        RectTransform topRect = topSectionGO.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0.5f, 0.5f);
        topRect.anchorMax = new Vector2(0.5f, 0.5f);
        topRect.pivot = new Vector2(0.5f, 0.5f);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(864, 664);
        BackpackGridUI backpackGridUI = topSectionGO.GetComponent<BackpackGridUI>();

        // Hotbar Numbers Row (1..9, 0, -, =)
        GameObject numbersRowGO = new GameObject("HotbarNumbersRow", typeof(RectTransform));
        numbersRowGO.transform.SetParent(topSectionGO.transform, false);
        RectTransform numRowRect = numbersRowGO.GetComponent<RectTransform>();
        numRowRect.anchorMin = new Vector2(0.5f, 0.5f);
        numRowRect.anchorMax = new Vector2(0.5f, 0.5f);
        numRowRect.pivot = new Vector2(0.5f, 0.5f);
        numRowRect.anchoredPosition = new Vector2(0, 288);
        numRowRect.sizeDelta = new Vector2(768, 20);

        string[] numberLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        TextMeshProUGUI[] numberTMPs = new TextMeshProUGUI[12];
        for (int i = 0; i < 12; i++)
        {
            GameObject nGO = new GameObject($"Num_{numberLabels[i]}", typeof(RectTransform), typeof(TextMeshProUGUI));
            nGO.transform.SetParent(numbersRowGO.transform, false);
            RectTransform nRect = nGO.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0f, 0.5f);
            nRect.anchorMax = new Vector2(0f, 0.5f);
            nRect.pivot = new Vector2(0.5f, 0.5f);
            nRect.anchoredPosition = new Vector2(i * 64 + 32, 0);
            nRect.sizeDelta = new Vector2(64, 20);

            TextMeshProUGUI nTMP = nGO.GetComponent<TextMeshProUGUI>();
            nTMP.text = numberLabels[i];
            nTMP.fontSize = 15;
            nTMP.alignment = TextAlignmentOptions.Center;
            nTMP.fontStyle = FontStyles.Bold;
            nTMP.color = (i == 0) ? new Color(0.88f, 0.19f, 0.19f) : new Color(0.32f, 0.22f, 0.15f);
            nTMP.raycastTarget = false;
            numberTMPs[i] = nTMP;
        }

        // Grid Container (768 x 192) - 3 rows of 64px
        GameObject gridContainerGO = new GameObject("GridContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        gridContainerGO.transform.SetParent(topSectionGO.transform, false);
        RectTransform gridRect = gridContainerGO.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0, 178);
        gridRect.sizeDelta = new Vector2(768, 192);

        GridLayoutGroup glg = gridContainerGO.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(64, 64);
        glg.spacing = Vector2.zero;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 12;

        // Right side buttons attached to frame edge
        // Junimo Note / Community Center Button
        GameObject junimoGO = new GameObject("Button_CommunityCenter", typeof(RectTransform), typeof(Image), typeof(Button));
        junimoGO.transform.SetParent(topSectionGO.transform, false);
        RectTransform jRect = junimoGO.GetComponent<RectTransform>();
        jRect.anchorMin = new Vector2(0.5f, 0.5f);
        jRect.anchorMax = new Vector2(0.5f, 0.5f);
        jRect.pivot = new Vector2(0.5f, 0.5f);
        jRect.anchoredPosition = new Vector2(456, 230);
        jRect.sizeDelta = new Vector2(52, 48);
        Image jImg = junimoGO.GetComponent<Image>();
        jImg.sprite = junimoBtnSprite;
        jImg.preserveAspect = true;

        // Organize Button
        GameObject orgGO = new GameObject("Button_Organize", typeof(RectTransform), typeof(Image), typeof(Button));
        orgGO.transform.SetParent(topSectionGO.transform, false);
        RectTransform oRect = orgGO.GetComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0.5f, 0.5f);
        oRect.anchorMax = new Vector2(0.5f, 0.5f);
        oRect.pivot = new Vector2(0.5f, 0.5f);
        oRect.anchoredPosition = new Vector2(456, 170);
        oRect.sizeDelta = new Vector2(56, 56);
        Image oImg = orgGO.GetComponent<Image>();
        oImg.sprite = orgBtnSprite;
        oImg.preserveAspect = true;

        // Trash Can Button (attached to right of bottom section)
        GameObject trashGO = new GameObject("Button_TrashCan", typeof(RectTransform), typeof(Image), typeof(Button));
        trashGO.transform.SetParent(menuRoot.transform, false);
        RectTransform trashRect = trashGO.GetComponent<RectTransform>();
        trashRect.anchorMin = new Vector2(0.5f, 0.5f);
        trashRect.anchorMax = new Vector2(0.5f, 0.5f);
        trashRect.pivot = new Vector2(0.5f, 0.5f);
        trashRect.anchoredPosition = new Vector2(464, -130);
        trashRect.sizeDelta = new Vector2(64, 92);
        Image trashImg = trashGO.GetComponent<Image>();
        trashImg.sprite = trashCanSprite;
        trashImg.preserveAspect = true;

        // Wire BackpackGridUI
        SerializedObject soGrid = new SerializedObject(backpackGridUI);
        soGrid.FindProperty("gridContainer").objectReferenceValue = gridContainerGO.transform;
        soGrid.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        SerializedProperty numArr = soGrid.FindProperty("hotbarNumberTexts");
        numArr.ClearArray();
        for (int i = 0; i < 12; i++)
        {
            numArr.InsertArrayElementAtIndex(i);
            numArr.GetArrayElementAtIndex(i).objectReferenceValue = numberTMPs[i];
        }
        soGrid.FindProperty("communityCenterButton").objectReferenceValue = junimoGO.GetComponent<Button>();
        soGrid.FindProperty("organizeButton").objectReferenceValue = orgGO.GetComponent<Button>();
        soGrid.FindProperty("trashCanButton").objectReferenceValue = trashGO.GetComponent<Button>();
        soGrid.ApplyModifiedProperties();

        // 7. Build Bottom Section: Character & Farm Sheet
        GameObject charSheetGO = new GameObject("CharacterSheetArea", typeof(RectTransform), typeof(CharacterSheetUI));
        charSheetGO.transform.SetParent(menuRoot.transform, false);
        RectTransform csRect = charSheetGO.GetComponent<RectTransform>();
        csRect.anchorMin = new Vector2(0.5f, 0.5f);
        csRect.anchorMax = new Vector2(0.5f, 0.5f);
        csRect.pivot = new Vector2(0.5f, 0.5f);
        csRect.anchoredPosition = Vector2.zero;
        csRect.sizeDelta = new Vector2(864, 664);
        CharacterSheetUI charSheetUI = charSheetGO.GetComponent<CharacterSheetUI>();

        // 7a. Equipment Column (4 vertical slots on the left)
        GameObject equipColGO = new GameObject("EquipmentColumn", typeof(RectTransform));
        equipColGO.transform.SetParent(charSheetGO.transform, false);
        RectTransform eqRect = equipColGO.GetComponent<RectTransform>();
        eqRect.anchorMin = new Vector2(0.5f, 0.5f);
        eqRect.anchorMax = new Vector2(0.5f, 0.5f);
        eqRect.pivot = new Vector2(0.5f, 0.5f);
        eqRect.anchoredPosition = new Vector2(-360, -140);
        eqRect.sizeDelta = new Vector2(64, 280);

        (string name, Sprite icon)[] equipSlots = {
            ("HatSlot", hatIcon),
            ("LeftRingSlot", ringIcon),
            ("RightRingSlot", ringIcon),
            ("BootsSlot", bootsIcon)
        };

        float[] equipYPositions = { 108, 36, -36, -108 };
        Image[] equipSlotImages = new Image[4];
        for (int i = 0; i < equipSlots.Length; i++)
        {
            GameObject sGO = new GameObject(equipSlots[i].name, typeof(RectTransform), typeof(Image));
            sGO.transform.SetParent(equipColGO.transform, false);
            RectTransform sr = sGO.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.5f, 0.5f);
            sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(0, equipYPositions[i]);
            sr.sizeDelta = new Vector2(64, 64);

            Image bg = sGO.GetComponent<Image>();
            bg.sprite = normalSlot;

            GameObject iconGO = new GameObject("PlaceholderIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(sGO.transform, false);
            RectTransform ir = iconGO.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.5f, 0.5f);
            ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = Vector2.zero;
            ir.sizeDelta = new Vector2(48, 48);
            Image img = iconGO.GetComponent<Image>();
            img.sprite = equipSlots[i].icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            equipSlotImages[i] = img;
        }

        // 7b. Character Portrait Box + Name
        GameObject portraitAreaGO = new GameObject("PortraitArea", typeof(RectTransform));
        portraitAreaGO.transform.SetParent(charSheetGO.transform, false);
        RectTransform paRect = portraitAreaGO.GetComponent<RectTransform>();
        paRect.anchorMin = new Vector2(0.5f, 0.5f);
        paRect.anchorMax = new Vector2(0.5f, 0.5f);
        paRect.pivot = new Vector2(0.5f, 0.5f);
        paRect.anchoredPosition = new Vector2(-220, -135);
        paRect.sizeDelta = new Vector2(130, 240);

        // Portrait Image (128x192)
        GameObject pBoxGO = new GameObject("PortraitBox", typeof(RectTransform), typeof(Image));
        pBoxGO.transform.SetParent(portraitAreaGO.transform, false);
        RectTransform pbRect = pBoxGO.GetComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0.5f, 0.5f);
        pbRect.anchorMax = new Vector2(0.5f, 0.5f);
        pbRect.pivot = new Vector2(0.5f, 0.5f);
        pbRect.anchoredPosition = new Vector2(0, 16);
        pbRect.sizeDelta = new Vector2(128, 192);
        Image pbImg = pBoxGO.GetComponent<Image>();
        pbImg.sprite = portraitFarmer;
        pbImg.preserveAspect = true;

        // Player Name Text underneath
        GameObject nameGO = new GameObject("PlayerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(portraitAreaGO.transform, false);
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0, -96);
        nameRect.sizeDelta = new Vector2(140, 28);
        TextMeshProUGUI nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
        nameTMP.text = "Aragorn";
        nameTMP.fontSize = 22;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = new Color(0.32f, 0.22f, 0.15f);
        nameTMP.fontStyle = FontStyles.Bold;

        // 7c. Farm Stats Area
        GameObject statsAreaGO = new GameObject("FarmStatsArea", typeof(RectTransform));
        statsAreaGO.transform.SetParent(charSheetGO.transform, false);
        RectTransform stRect = statsAreaGO.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.5f, 0.5f);
        stRect.anchorMax = new Vector2(0.5f, 0.5f);
        stRect.pivot = new Vector2(0.5f, 0.5f);
        stRect.anchoredPosition = new Vector2(125, -135);
        stRect.sizeDelta = new Vector2(490, 240);

        // Farm Name Text
        GameObject farmNameGO = new GameObject("FarmNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        farmNameGO.transform.SetParent(statsAreaGO.transform, false);
        RectTransform fnRect = farmNameGO.GetComponent<RectTransform>();
        fnRect.anchorMin = new Vector2(0f, 0.5f);
        fnRect.anchorMax = new Vector2(0f, 0.5f);
        fnRect.pivot = new Vector2(0f, 0.5f);
        fnRect.anchoredPosition = new Vector2(0, 80);
        fnRect.sizeDelta = new Vector2(300, 36);
        TextMeshProUGUI fnTMP = farmNameGO.GetComponent<TextMeshProUGUI>();
        fnTMP.text = "Thunderpluck Farm";
        fnTMP.fontSize = 26;
        fnTMP.alignment = TextAlignmentOptions.MidlineLeft;
        fnTMP.color = new Color(0.32f, 0.22f, 0.15f);
        fnTMP.fontStyle = FontStyles.Bold;

        // Calendar Button
        GameObject calGO = new GameObject("CalendarButton", typeof(RectTransform), typeof(Image), typeof(Button));
        calGO.transform.SetParent(statsAreaGO.transform, false);
        RectTransform calRect = calGO.GetComponent<RectTransform>();
        calRect.anchorMin = new Vector2(0f, 0.5f);
        calRect.anchorMax = new Vector2(0f, 0.5f);
        calRect.pivot = new Vector2(0f, 0.5f);
        calRect.anchoredPosition = new Vector2(310, 80);
        calRect.sizeDelta = new Vector2(64, 38);
        Image calImg = calGO.GetComponent<Image>();
        calImg.sprite = calendarIconSprite;
        calImg.preserveAspect = true;

        // Calendar Tooltip (Wood box with "Calendar")
        GameObject tipGO = new GameObject("CalendarTooltip", typeof(RectTransform), typeof(Image));
        tipGO.transform.SetParent(calGO.transform, false);
        RectTransform tipRect = tipGO.GetComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0.5f, 0f);
        tipRect.anchorMax = new Vector2(0.5f, 0f);
        tipRect.pivot = new Vector2(0.5f, 1f);
        tipRect.anchoredPosition = new Vector2(40, -4);
        tipRect.sizeDelta = new Vector2(100, 36);
        Image tipImg = tipGO.GetComponent<Image>();
        tipImg.sprite = toolbarFrame;
        tipImg.type = Image.Type.Sliced;

        GameObject tipTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tipTextGO.transform.SetParent(tipGO.transform, false);
        RectTransform ttRect = tipTextGO.GetComponent<RectTransform>();
        ttRect.anchorMin = Vector2.zero;
        ttRect.anchorMax = Vector2.one;
        ttRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI ttTMP = tipTextGO.GetComponent<TextMeshProUGUI>();
        ttTMP.text = "Calendar";
        ttTMP.fontSize = 18;
        ttTMP.alignment = TextAlignmentOptions.Center;
        ttTMP.color = new Color(0.32f, 0.22f, 0.15f);
        ttTMP.fontStyle = FontStyles.Bold;
        tipGO.SetActive(false);

        // Current Funds Text
        GameObject fundsGO = new GameObject("CurrentFundsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        fundsGO.transform.SetParent(statsAreaGO.transform, false);
        RectTransform fundsRect = fundsGO.GetComponent<RectTransform>();
        fundsRect.anchorMin = new Vector2(0f, 0.5f);
        fundsRect.anchorMax = new Vector2(0f, 0.5f);
        fundsRect.pivot = new Vector2(0f, 0.5f);
        fundsRect.anchoredPosition = new Vector2(0, 30);
        fundsRect.sizeDelta = new Vector2(400, 32);
        TextMeshProUGUI fundsTMP = fundsGO.GetComponent<TextMeshProUGUI>();
        fundsTMP.text = "Current Funds: 20,080g";
        fundsTMP.fontSize = 24;
        fundsTMP.alignment = TextAlignmentOptions.MidlineLeft;
        fundsTMP.color = new Color(0.32f, 0.22f, 0.15f);
        fundsTMP.fontStyle = FontStyles.Bold;

        // Total Earnings Text
        GameObject earnGO = new GameObject("TotalEarningsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        earnGO.transform.SetParent(statsAreaGO.transform, false);
        RectTransform earnRect = earnGO.GetComponent<RectTransform>();
        earnRect.anchorMin = new Vector2(0f, 0.5f);
        earnRect.anchorMax = new Vector2(0f, 0.5f);
        earnRect.pivot = new Vector2(0f, 0.5f);
        earnRect.anchoredPosition = new Vector2(0, -20);
        earnRect.sizeDelta = new Vector2(400, 32);
        TextMeshProUGUI earnTMP = earnGO.GetComponent<TextMeshProUGUI>();
        earnTMP.text = "Total Earnings: 165,267g";
        earnTMP.fontSize = 24;
        earnTMP.alignment = TextAlignmentOptions.MidlineLeft;
        earnTMP.color = new Color(0.32f, 0.22f, 0.15f);
        earnTMP.fontStyle = FontStyles.Bold;

        // Pet Section
        GameObject petGO = new GameObject("PetRow", typeof(RectTransform));
        petGO.transform.SetParent(statsAreaGO.transform, false);
        RectTransform petRect = petGO.GetComponent<RectTransform>();
        petRect.anchorMin = new Vector2(0f, 0.5f);
        petRect.anchorMax = new Vector2(0f, 0.5f);
        petRect.pivot = new Vector2(0f, 0.5f);
        petRect.anchoredPosition = new Vector2(0, -75);
        petRect.sizeDelta = new Vector2(400, 48);

        GameObject petIconGO = new GameObject("PetIcon", typeof(RectTransform), typeof(Image));
        petIconGO.transform.SetParent(petGO.transform, false);
        RectTransform piRect = petIconGO.GetComponent<RectTransform>();
        piRect.anchorMin = new Vector2(0f, 0.5f);
        piRect.anchorMax = new Vector2(0f, 0.5f);
        piRect.pivot = new Vector2(0f, 0.5f);
        piRect.anchoredPosition = Vector2.zero;
        piRect.sizeDelta = new Vector2(48, 48);
        Image piImg = petIconGO.GetComponent<Image>();
        piImg.sprite = dogIconSprite;
        piImg.preserveAspect = true;

        GameObject petNameGO = new GameObject("PetNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        petNameGO.transform.SetParent(petGO.transform, false);
        RectTransform pnRect = petNameGO.GetComponent<RectTransform>();
        pnRect.anchorMin = new Vector2(0f, 0.5f);
        pnRect.anchorMax = new Vector2(1f, 0.5f);
        pnRect.pivot = new Vector2(0f, 0.5f);
        pnRect.anchoredPosition = new Vector2(56, 0);
        pnRect.sizeDelta = new Vector2(-56, 36);
        TextMeshProUGUI pnTMP = petNameGO.GetComponent<TextMeshProUGUI>();
        pnTMP.text = "Yogi";
        pnTMP.fontSize = 24;
        pnTMP.alignment = TextAlignmentOptions.MidlineLeft;
        pnTMP.color = new Color(0.32f, 0.22f, 0.15f);
        pnTMP.fontStyle = FontStyles.Bold;

        // Wire CharacterSheetUI
        SerializedObject soCS = new SerializedObject(charSheetUI);
        soCS.FindProperty("hatSlotImage").objectReferenceValue = equipSlotImages[0];
        soCS.FindProperty("leftRingSlotImage").objectReferenceValue = equipSlotImages[1];
        soCS.FindProperty("rightRingSlotImage").objectReferenceValue = equipSlotImages[2];
        soCS.FindProperty("bootsSlotImage").objectReferenceValue = equipSlotImages[3];
        soCS.FindProperty("portraitBackgroundImage").objectReferenceValue = pbImg;
        soCS.FindProperty("characterPreviewImage").objectReferenceValue = pbImg;
        soCS.FindProperty("portraitDay").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/daybg.png");
        soCS.FindProperty("portraitNight").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/nightbg.png");
        soCS.FindProperty("playerNameText").objectReferenceValue = nameTMP;
        soCS.FindProperty("farmNameText").objectReferenceValue = fnTMP;
        soCS.FindProperty("currentFundsText").objectReferenceValue = fundsTMP;
        soCS.FindProperty("totalEarningsText").objectReferenceValue = earnTMP;
        soCS.FindProperty("calendarButton").objectReferenceValue = calGO.GetComponent<Button>();
        soCS.FindProperty("calendarTooltip").objectReferenceValue = tipGO;
        soCS.FindProperty("petIconImage").objectReferenceValue = piImg;
        soCS.FindProperty("petNameText").objectReferenceValue = pnTMP;
        soCS.ApplyModifiedProperties();

        // 8. Wire FullInventoryMenuUI on CanvasRoot
        FullInventoryMenuUI fullMenuUI = canvasRoot.AddComponent<FullInventoryMenuUI>();
        SerializedObject soFull = new SerializedObject(fullMenuUI);
        soFull.FindProperty("menuRoot").objectReferenceValue = menuRoot;
        Transform tbTr = canvasRoot.transform.Find("Toolbar");
        if (tbTr != null)
        {
            soFull.FindProperty("hudToolbar").objectReferenceValue = tbTr.gameObject;
        }
        soFull.FindProperty("tabStrip").objectReferenceValue = tabStripUI;
        soFull.FindProperty("backpackGrid").objectReferenceValue = backpackGridUI;
        soFull.FindProperty("characterSheet").objectReferenceValue = charSheetUI;
        soFull.FindProperty("worldMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/map.png");
        soFull.FindProperty("secondaryCursors").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/Cursors2.png");
        soFull.FindProperty("fishDefinitions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/Fish.json");
        soFull.FindProperty("cookingRecipes").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/CookingRecipes.json");
        soFull.FindProperty("craftablesAtlas").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/Craftables.png");
        soFull.FindProperty("objectsAtlas").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Maps/springobjects.png");
        soFull.FindProperty("craftingRecipes").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/CraftingRecipes.json");
        soFull.FindProperty("normalSlotSprite").objectReferenceValue = normalSlot;
        soFull.FindProperty("selectedSlotSprite").objectReferenceValue = selectedSlot;
        soFull.FindProperty("lockedSlotSprite").objectReferenceValue = lockedSlot;
        soFull.FindProperty("miniFarmerSprite").objectReferenceValue = portraitFarmer;
        soFull.ApplyModifiedProperties();

        // By default, full inventory menu is closed
        menuRoot.SetActive(false);

        // 9. Save Prefab
        PrefabUtility.SaveAsPrefabAsset(canvasRoot, canvasPrefabPath);
        PrefabUtility.UnloadPrefabContents(canvasRoot);

        // 10. Update active scene instance
        var existingCanvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in existingCanvas)
        {
            if (c.name.Contains("InventoryCanvas"))
            {
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }
        GameObject newCanvas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(canvasPrefabPath));
        if (newCanvas != null)
        {
            newCanvas.name = "InventoryCanvas";
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Full Stardew Inventory Menu successfully built into InventoryCanvas.prefab and scene with 100% visual fidelity!");
    }

    [MenuItem("Tools/Stardew/Preview Inventory Menu in Scene (Toggle)")]
    public static void TogglePreview()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform menuTr = canvas.transform.Find("FullInventoryMenu");
            if (menuTr != null)
            {
                bool willActive = !menuTr.gameObject.activeSelf;
                menuTr.gameObject.SetActive(willActive);
                if (willActive)
                {
                    var fullMenuUI = canvas.GetComponent<FullInventoryMenuUI>();
                    if (fullMenuUI != null)
                    {
                        var playerInv = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                        if (playerInv != null)
                        {
                            fullMenuUI.BindInventory(playerInv, playerInv);
                        }
                        fullMenuUI.RefreshAll();
                    }
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log($"FullInventoryMenu is now {(willActive ? "VISIBLE" : "HIDDEN")} in Scene!");
            }
        }
    }

    private static void ConfigureSprite(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 16;
            if (border != Vector4.zero)
            {
                importer.spriteBorder = border;
            }
            importer.SaveAndReimport();
        }
    }
}
