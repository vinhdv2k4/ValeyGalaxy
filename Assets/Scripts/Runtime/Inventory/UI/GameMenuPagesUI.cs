using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// The eight GameMenu pages use the standard GameMenu tab order.
/// Pages only display state that this project actually records.
/// </summary>
public sealed class GameMenuPagesUI : MonoBehaviour
{
    private static readonly string[] PageNames =
        { "Inventory", "Skills", "Social", "Map", "Crafting", "Collections", "Options", "Exit" };
    private static readonly string[] SkillNames =
        { "Farming", "Mining", "Foraging", "Fishing", "Combat" };
    private static readonly int[] SkillOrder = { 0, 3, 2, 1, 4 };
    private static readonly Rect[] SkillIcons =
        { new(10, 428, 10, 10), new(30, 428, 10, 10),
            new(60, 428, 10, 10), new(20, 428, 10, 10),
            new(120, 428, 10, 10) };
    private static readonly Rect[] CollectionTabs =
        { new(640, 80, 16, 16), new(640, 64, 16, 16),
            new(656, 64, 16, 16), new(672, 64, 16, 16),
            new(688, 64, 16, 16), new(656, 80, 16, 16),
            new(688, 80, 16, 16) };
    private static readonly int[] ShippedItems = {
        16, 18, 20, 22, 24, 78, 88, 90, 92, 188, 190, 192, 248, 250, 252, 254, 256, 257, 258, 259, 260, 262,
        264, 266, 268, 270, 272, 274, 276, 278, 280, 281, 282, 283, 284, 296, 300, 303, 304, 306, 307, 308, 340,
        342, 344, 346, 348, 350, 376, 378, 380, 382, 384, 386, 388, 390, 392, 393, 394, 396, 397, 398, 399, 400,
        402, 404, 406, 408, 410, 412, 414, 416, 417, 418, 420, 421, 422, 424, 426, 428, 430, 432, 433, 436, 438,
        440, 442, 444, 446, 454, 459, 591, 593, 595, 597, 613, 634, 635, 636, 637, 638, 724, 725, 726, 771
    };
    private static readonly int[] ArtifactItems = {
        96, 97, 98, 99, 100, 101, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119,
        120, 121, 122, 123, 124, 125, 126, 127, 579, 580, 581, 582, 583, 584, 585, 586, 587, 588, 589
    };
    private static readonly int[] MineralItems = {
        60, 62, 64, 66, 68, 70, 72, 74, 80, 82, 84, 86, 538, 539, 540, 541, 542, 543, 544, 545, 546, 547, 548, 549,
        550, 551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561, 562, 563, 564, 565, 566, 567, 568, 569, 570, 571,
        572, 573, 574, 575, 576, 577, 578
    };
    private readonly List<List<(string Name, string Ingredients, int Tile, bool Big, Rect Bounds)>> recipePages = new();
    private Dictionary<string, string> craftingDefinitions;
    private int[] recipeSkillLevels;
    private string[] shopRecipes;
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform menuRect;
    private Texture2D mapTexture;
    private Texture2D craftablesTexture;
    private Texture2D objectsTexture;
    private IReadOnlyInventory inventory;
    private Action closeMenu;
    private MenuTabStripUI tabStrip;
    private Texture2D secondaryCursors;
    private NpcDefinition[] villagers;
    private OnlineGameState online;
    private Texture2D font;
    private FullInventoryMenuUI profile;
    private CharacterSheetUI characterSheet;
    private int page;
    private int previousNonMapPage;
    private int socialOffset;
    private int recipePage;
    private int collectionTab;
    private int collectionPage;
    private int optionsOffset;
    private int activeSlider = -1;
    private bool draggingScroll;
    private int[] achievementIds = Array.Empty<int>();
    private Dictionary<int, string> achievementDefinitions;
    private Dictionary<int, string> fishNames = new();
    private int[] fishItems = Array.Empty<int>();
    private int[] cookingItems = Array.Empty<int>();
    private int heldCraftingSlot = -1;
    private bool inventoryChangePending;
    private int pendingExit;

    private void Awake()
    {
        if (PlayerPrefs.HasKey("GameMenu.Fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt("GameMenu.Fullscreen") != 0;
        if (PlayerPrefs.HasKey("GameMenu.VSync"))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("GameMenu.VSync") != 0 ? 1 : 0;
    }

    internal void Initialize(RectTransform rect, Texture2D map, Texture2D craftables,
        Texture2D objects, TextAsset recipes,
        IReadOnlyInventory playerInventory, Action close)
    {
        menuRect = rect;
        mapTexture = map;
        craftablesTexture = craftables;
        objectsTexture = objects;
        inventory = playerInventory;
        closeMenu = close;
        villagers = Resources.Load<NpcCatalog>("NpcCatalog")?.characters
            .Where(npc => npc != null && !string.IsNullOrEmpty(npc.name))
            .OrderBy(npc => npc.name, StringComparer.OrdinalIgnoreCase).ToArray()
            ?? Array.Empty<NpcDefinition>();
        font = StardewUiAtlas.FontBold;
        profile = GetComponentInParent<FullInventoryMenuUI>();
        characterSheet = GetComponentInChildren<CharacterSheetUI>(true);
        TextAsset achievementData = Resources.Load<TextAsset>("Data/Achievements");
        if (achievementData != null)
        {
            achievementDefinitions = JsonConvert.DeserializeObject<Dictionary<int, string>>(achievementData.text);
            achievementIds = achievementDefinitions?.Keys.OrderBy(id => id).ToArray() ?? Array.Empty<int>();
        }
        if (recipes == null) return;
        try
        {
            craftingDefinitions = JsonConvert.DeserializeObject<Dictionary<string, string>>(recipes.text);
            if (craftingDefinitions != null) LayoutRecipes(craftingDefinitions);
        }
        catch (JsonException error)
        {
            Debug.LogWarning("Invalid crafting recipe data: " + error.Message);
        }
    }

    internal void BindInventory(IReadOnlyInventory playerInventory)
    {
        inventory = playerInventory;
    }

    internal void BindMenuControls(MenuTabStripUI tabs, Texture2D cursors2, TextAsset fish, TextAsset cooking)
    {
        tabStrip = tabs;
        secondaryCursors = cursors2;
        fishNames = ReadDefinitions(fish)
            .Where(entry => int.TryParse(entry.Key, out _))
            .ToDictionary(entry => int.Parse(entry.Key), entry => entry.Value.Split('/')[0]);
        // CollectionsPage excludes trash and the extended-family quest fish.
        fishItems = fishNames.Keys.Where(id => id < 898 && id is not (152 or 153 or 157)
            && id is not (>= 167 and <= 172)).OrderBy(id => id).ToArray();
        cookingItems = ReadDefinitions(cooking).Values.Select(value => value.Split('/'))
            .Where(fields => fields.Length > 2)
            .Select(fields => int.TryParse(fields[2].Split(' ')[0], out int id) ? id : -1)
            .Where(id => id >= 0 && id is not (217 or 279 or 772 or 773 or 873))
            .Distinct().OrderBy(id => id).ToArray();
    }

    private static Dictionary<string, string> ReadDefinitions(TextAsset data) =>
        (data == null ? null : JsonConvert.DeserializeObject<Dictionary<string, string>>(data.text)) ?? new();

    private static float FrameWidth(int tab) => tab switch
    {
        2 => 900f,
        5 or 7 => 784f,
        _ => 864f
    };

    public void SelectPage(int index)
    {
        if (activeSlider >= 0) PlayerPrefs.Save();
        if (page != 3) previousNonMapPage = page;
        page = Mathf.Clamp(index, 0, PageNames.Length - 1);
        tabStrip?.ShowPage(page, FrameWidth(page));
        heldCraftingSlot = -1;
        activeSlider = -1;
        draggingScroll = false;
        pendingExit = 0;
    }

    private void OnGUI()
    {
        if (menuRect == null || font == null) return;
        GUI.depth = -35;
        menuRect.GetWorldCorners(corners);
        Canvas canvas = menuRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        Rect bounds = new(bottomLeft.x, Screen.height - topRight.y,
            topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        if (online == null && page != 0) online = LocationManager.LocalFarmerComponent<OnlineGameState>();
        Matrix4x4 oldMatrix = GUI.matrix;
        if (page == 3)
        {
            try { DrawMap(bounds.width / 864f); }
            finally { GUI.matrix = oldMatrix; }
            return;
        }
        GUI.matrix = Matrix4x4.TRS(new Vector3(bounds.x, bounds.y, 0f),
            Quaternion.identity, new Vector3(bounds.width / 864f, bounds.height / 664f, 1f));
        try
        {
            float width = FrameWidth(page);
            Rect body = new(32f, 36f, width - 64f, 560f);
            if (page != 0)
                StardewUiAtlas.DrawGameMenuFrame(new Rect(0f, 0f, width, 616f));
            characterSheet?.DrawMiniPortrait(new Rect(136f, page == 1 ? -32f : -40f, 48f, 45f));
            switch (page)
            {
                case 0: DrawInventoryText(); break;
                case 1: DrawSkills(); break;
                case 2: DrawSocial(body); break;
                case 4: DrawCrafting(body); break;
                case 5: DrawCollections(body); break;
                case 6: DrawOptions(body); break;
                case 7: DrawExit(); break;
            }
        }
        finally { GUI.matrix = oldMatrix; }
    }

    private void DrawInventoryText()
    {
        if (profile == null) return;
        const string keys = "1234567890-=";
        int selected = inventory?.SelectedIndex ?? -1;
        for (int i = 0; i < keys.Length; i++)
        {
            Rect cell = BackpackGridUI.InventorySlotRect(i);
            Color previous = GUI.color;
            GUI.color = i == selected ? new Color(0.72f, 0.16f, 0.10f) :
                new Color(0.35f, 0.23f, 0.13f);
            DrawText(keys[i].ToString(), new Rect(cell.x, cell.y - 17f, cell.width, 20f), 1f, true);
            GUI.color = previous;
        }
        // Two columns of equipment icons around portrait.
        DrawLightRingSlot(new Rect(48, 320, 64, 64), false);
        DrawLightRingSlot(new Rect(48, 384, 64, 64), true);
        StardewUiAtlas.DrawMenuTilesSprite(new Rect(48, 448, 64, 64), StardewUiAtlas.MenuTileRect(40));
        StardewUiAtlas.DrawMenuTilesSprite(new Rect(256, 320, 64, 64), StardewUiAtlas.MenuTileRect(42));
        StardewUiAtlas.DrawMenuTilesSprite(new Rect(256, 384, 64, 64), StardewUiAtlas.MenuTileRect(69));
        StardewUiAtlas.DrawMenuTilesSprite(new Rect(256, 448, 64, 64), StardewUiAtlas.MenuTileRect(68));
        characterSheet?.DrawPortrait(new Rect(120, 320, 128, 192));

        DrawProfileLine(profile.PlayerName, new Rect(72f, 528f, 224f, 42f));
        string farm = profile.FarmName ?? "";
        if (!string.IsNullOrWhiteSpace(farm))
            DrawProfileLine(farm.EndsWith(" Farm", StringComparison.OrdinalIgnoreCase) ? farm : farm + " Farm",
                new Rect(330f, 332f, 492f, 44f));
        DrawProfileLine("Current Funds: " + profile.CurrentFunds.ToString("N0") + "g",
            new Rect(330f, 392f, 492f, 44f));
        DrawProfileLine("Total Earnings: " + profile.TotalEarnings.ToString("N0") + "g",
            new Rect(330f, 452f, 492f, 44f));
        if (!string.IsNullOrEmpty(profile.PetName))
        {
            CreatedFarmer farmer = StartupSession.Farmer;
            int petX = (farmer?.pet == 0 ? 160 : 208) + Mathf.Clamp(farmer?.petBreed ?? 0, 0, 2) * 16;
            StardewUiAtlas.DrawCursorsSprite(new Rect(352f, 516f, 64f, 64f), new Rect(petX, 208, 16, 16));
            DrawText(profile.PetName, new Rect(416f, 528f, 360f, 42f), 2f);
        }
        if (profile.HasHeldItem || inventory == null || Event.current.type != EventType.Repaint) return;
        for (int i = 0; i < inventory.UnlockedSlots; i++)
        {
            if (!BackpackGridUI.InventorySlotRect(i).Contains(Event.current.mousePosition)) continue;
            var slot = inventory.GetSlot(i);
            if (slot?.Data != null && !slot.IsEmpty)
            {
                string tooltip = slot.Data.displayName;
                if (!string.IsNullOrWhiteSpace(slot.Data.description))
                    tooltip += "\n" + slot.Data.description;
                DrawTooltip(tooltip, FrameWidth(page), 616);
            }
            break;
        }
    }

    private void DrawLightRingSlot(Rect bounds, bool right)
    {
        StardewUiAtlas.DrawMenuTilesSprite(bounds, StardewUiAtlas.MenuTileRect(41));
        ItemData ring = profile.EquippedLightRing(right);
        if (ring != null)
            DrawSprite(ring.GetIcon(), new Rect(bounds.x + 8f, bounds.y + 8f, 48f, 48f));

        Event input = Event.current;
        if (input.type == EventType.MouseDown && input.button == 0 && bounds.Contains(input.mousePosition))
        {
            profile.ClickLightRingSlot(right);
            input.Use();
        }
    }

    private void DrawProfileLine(string value, Rect bounds)
    {
        float width = NpcBitmapText.GetStringWidth(value, 2f);
        DrawText(value, bounds, width > bounds.width ? 2f * bounds.width / width : 2f, true);
    }

    private void DrawSkills()
    {
        characterSheet?.DrawPortrait(new Rect(52, 48, 128, 192));
        DrawProfileLine(profile?.PlayerName ?? "Farmer", new Rect(40, 240, 152, 40));
        int[] levels = online?.Snapshot?.skillLevels;
        for (int row = 0; row < SkillNames.Length; row++)
        {
            int skill = SkillOrder[row];
            int level = levels != null && skill < levels.Length ? levels[skill] : 0;
            float y = 48 + row * 56;
            DrawText(SkillNames[row], new Rect(188, y, 116, 36), 1.75f, true);
            DrawIconWithShadow(new Rect(308, y - 4, 40, 40), SkillIcons[row], SkillIcons[row], Color.white);
            float x = 360;
            for (int column = 0; column < 10; column++)
            {
                bool milestone = (column + 1) % 5 == 0;
                bool filled = column < level;
                Rect empty = milestone ? new Rect(145, 338, 14, 9) : new Rect(129, 338, 8, 9);
                Rect source = empty;
                if (filled) source.x += empty.width;
                DrawIconWithShadow(new Rect(x, y - 4, empty.width * 4, 36), source, empty,
                    new Color(1, 1, 1, filled ? 1f : 0.65f));
                x += milestone ? 60 : 36;
            }
            DrawText(level.ToString(), new Rect(772, y, 48, 36), 1.75f, true);
        }
        StardewUiAtlas.DrawGameMenuFrame(new Rect(36, 384, 792, 200));
        DrawText("Special Items & Powers", new Rect(64, 410, 736, 38), 1.8f, true);
    }

    private static void DrawIconWithShadow(Rect destination, Rect source, Rect shadow, Color color)
    {
        Rect offset = destination;
        offset.position += new Vector2(-4, 4);
        StardewUiAtlas.DrawCursorsSprite(offset, shadow, new Color(0, 0, 0, 0.35f));
        StardewUiAtlas.DrawCursorsSprite(destination, source, color);
    }

    private void DrawSocial(Rect area)
    {
        const int visible = 5;
        DrawListScroll(area, ref socialOffset, villagers.Length - visible);
        for (int row = 0; row < visible && row + socialOffset < villagers.Length; row++)
        {
            NpcDefinition npc = villagers[row + socialOffset];
            float y = 40 + row * 112;
            var friendship = online?.GetNpcFriendship(npc.name);
            int hearts = (friendship?.points ?? 0) / 250;
            DrawNpcMugshot(npc, new Rect(44, y, 64, 96));
            DrawText(npc.name, new Rect(124, y + 24, 172, 42), 2f, true);
            for (int heart = 0; heart < 10; heart++)
                StardewUiAtlas.DrawCursorsSprite(new Rect(316 + heart * 32, y + 36, 28, 24),
                    new Rect(heart < hearts ? 211 : 218, 428, 7, 6));

            if (secondaryCursors != null && online?.Snapshot != null)
            {
                int day = Mathf.Max(1, online.Snapshot.gameDay);
                int gifts = friendship != null && friendship.lastGiftDay > 0 &&
                    (friendship.lastGiftDay - 1) / 7 == (day - 1) / 7 ? friendship.giftsThisWeek : 0;
                DrawAtlas(secondaryCursors, new Rect(688, y + 4, 56, 48), new Rect(166, 174, 14, 12));
                for (int gift = 0; gift < 2; gift++)
                    StardewUiAtlas.DrawCursorsSprite(new Rect(680 + gift * 40, y + 56, 36, 36),
                        new Rect(gifts >= 2 - gift ? 236 : 227, 425, 9, 9));
            }
            if (row < visible - 1)
                StardewUiAtlas.DrawCursorsSprite(new Rect(104, y + 100, 744, 4), StardewUiAtlas.DividerStripeRect);
        }
    }

    private static readonly (string Id, string Name, Rect Bounds)[] MapPoints =
    {
        ("Farm", null, new Rect(324, 252, 188, 132)),
        ("Backwoods", "Backwoods", new Rect(360, 96, 188, 132)),
        ("BusStop", "Bus Stop", new Rect(516, 224, 76, 100)),
        ("WizardHouse", "Wizard's Tower", new Rect(196, 352, 36, 76)),
        ("AnimalShop", "Marnie's Ranch", new Rect(420, 392, 76, 40)),
        ("CommunityCenter", "Community Center", new Rect(692, 204, 44, 36)),
        ("SeedShop", "Pierre's General Store", new Rect(696, 296, 28, 40)),
        ("Saloon", "Stardrop Saloon", new Rect(716, 352, 28, 40)),
        ("ManorHouse", "Mayor's Manor", new Rect(768, 388, 44, 56)),
        ("Blacksmith", "Blacksmith", new Rect(852, 388, 80, 36)),
        ("ArchaeologyHouse", "Museum", new Rect(892, 416, 32, 28)),
        ("ScienceHouse", "Carpenter's Shop", new Rect(732, 148, 48, 32)),
        ("Mine", "The Mines", new Rect(880, 96, 16, 24)),
        ("AdventureGuild", "Adventurer's Guild", new Rect(900, 108, 32, 36)),
        ("Railroad", "Railroad", new Rect(576, 60, 48, 36)),
        ("FishShop", "Fish Shop", new Rect(844, 608, 36, 40))
    };

    private void DrawMap(float uiScale)
    {
        const float width = 1264f;
        const float height = 880f;
        float scale = Mathf.Min(uiScale, (Screen.width - 24f) / width, (Screen.height - 24f) / height);
        if (scale <= 0f) return;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
            (Screen.height - height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        Rect frame = new(0, 0, width, 784);
        Rect art = new(32, 32, 1200, 720);
        StardewUiAtlas.DrawGameMenuFrame(frame);
        DrawAtlas(mapTexture, art, new Rect(0, 0, 300, 180));
        int farmType = StartupSession.Farmer?.farmType ?? 1;
        if (farmType is >= 1 and <= 6)
            DrawAtlas(mapTexture, new Rect(art.x, art.y + 172, 524, 244),
                new Rect((farmType - 1) % 2 * 131, 180 + (farmType - 1) / 2 * 61, 131, 61));

        string farm = profile?.FarmName;
        if (string.IsNullOrWhiteSpace(farm)) farm = "Farm";
        else if (!farm.EndsWith(" Farm", StringComparison.OrdinalIgnoreCase)) farm += " Farm";
        string locationId = LocationManager.Instance?.CurrentLocation?.LocationName ?? "";
        string location = locationId;
        Vector2? marker = null;
        if (locationId is "Farm" or "FarmHouse" or "FarmCave" or "Greenhouse" or "Barn" or "Coop")
        {
            location = farm;
            marker = new Vector2(384, 288);
        }
        else
        {
            if (locationId.StartsWith("UndergroundMine", StringComparison.Ordinal) || locationId == "MineLobby" ||
                int.TryParse(locationId, out _))
                locationId = "Mine";
            foreach (var point in MapPoints)
            {
                if (point.Id != locationId) continue;
                location = point.Name;
                marker = point.Bounds.center;
                break;
            }
            var tile = profile?.PlayerMapTile ?? new Vector2Int(58, 50);
            marker ??= locationId switch
            {
                "Town" when tile.x > 84 && tile.y < 68 => new Vector2(900, 324),
                "Town" when tile.x > 80 && tile.y >= 68 => new Vector2(880, 432),
                "Town" when tile.y <= 42 => new Vector2(712, 256),
                "Town" when tile.y < 76 => new Vector2(700, 352),
                "Town" => new Vector2(728, 436),
                "Forest" when tile.y > 51 => new Vector2(280, 540),
                "Forest" when tile.x < 58 => new Vector2(252, 416),
                "Forest" => new Vector2(436, 428),
                "Mountain" when tile.x < 38 => new Vector2(740, 144),
                "Mountain" when tile.x < 96 => new Vector2(880, 152),
                "Mountain" => new Vector2(1012, 160),
                "Beach" => new Vector2(808, 564),
                _ => null
            };
        }
        if (marker is Vector2 pin)
            characterSheet?.DrawMiniPortrait(new Rect(art.x + pin.x - 32, art.y + pin.y - 32, 64, 60));

        if (!string.IsNullOrEmpty(location))
        {
            float span = Mathf.Clamp(NpcBitmapText.GetStringWidth(location, 2.25f) + 32f, 128f, 1000f);
            float x = (width - span) / 2;
            const float y = 800f;
            StardewUiAtlas.DrawCursorsSprite(new Rect(x - 28, y, 28, 68), new Rect(324, 299, 7, 17));
            StardewUiAtlas.DrawCursorsSprite(new Rect(x, y, span, 68), new Rect(331, 299, 1, 17));
            StardewUiAtlas.DrawCursorsSprite(new Rect(x + span, y, 28, 68), new Rect(332, 299, 7, 17));
            DrawProfileLine(location, new Rect(x, y + 10, span, 46));
        }
        Rect close = new(width - 56, 4, 48, 48);
        if (DrawSpriteButton(close, StardewUiAtlas.UpperRightCloseButtonRect))
        {
            closeMenu?.Invoke();
            return;
        }
        Vector2 mouse = Event.current.mousePosition;
        foreach (var point in MapPoints)
        {
            if (!point.Bounds.Contains(mouse - art.position)) continue;
            DrawTooltip(point.Name ?? farm, width, height);
            break;
        }
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Event.current.Use();
            if (tabStrip != null) tabStrip.SelectTab(previousNonMapPage);
            else closeMenu?.Invoke();
        }
    }

    private void LayoutRecipes(Dictionary<string, string> definitions)
    {
        recipePages.Clear();
        const int columns = 10;
        const int cellsPerPage = columns * 4;
        var occupied = new bool[cellsPerPage];
        int cell = 0;
        recipePages.Add(new());
        foreach (var recipe in definitions)
        {
            string[] fields = recipe.Value.Split('/');
            if (fields.Length < 5 || !int.TryParse(fields[2].Split(' ')[0], out int tile)) continue;
            bool learnedFromSkill = SkillRecipeRequirement.TryRead(fields[4], out int skill, out int level) &&
                recipeSkillLevels != null && skill < recipeSkillLevels.Length && recipeSkillLevels[skill] >= level;
            bool learnedFromShop = shopRecipes != null && Array.IndexOf(shopRecipes, recipe.Key) >= 0;
            if (fields[4] != "default" && recipe.Key != "Furnace" && !learnedFromSkill && !learnedFromShop) continue;
            bool big = fields[3] == "true";
            while (occupied[cell] || (big && (cell >= cellsPerPage - columns || occupied[cell + columns])))
            {
                if (++cell < cellsPerPage) continue;
                recipePages.Add(new());
                Array.Clear(occupied, 0, occupied.Length);
                cell = 0;
            }
            var bounds = new Rect(48 + cell % columns * 72, 48 + cell / columns * 72, 64, big ? 128 : 64);
            recipePages[^1].Add((recipe.Key, fields[0], tile, big, bounds));
            occupied[cell] = true;
            if (big) occupied[cell + columns] = true;
        }
    }

    private void DrawCrafting(Rect area)
    {
        var levels = online?.Snapshot?.skillLevels;
        var learned = online?.Snapshot?.learnedShopRecipes;
        if (!ReferenceEquals(levels, recipeSkillLevels) || !ReferenceEquals(learned, shopRecipes))
        {
            bool changed = !(levels ?? Array.Empty<int>()).SequenceEqual(recipeSkillLevels ?? Array.Empty<int>()) ||
                !(learned ?? Array.Empty<string>()).SequenceEqual(shopRecipes ?? Array.Empty<string>());
            recipeSkillLevels = levels;
            shopRecipes = learned;
            if (changed && craftingDefinitions != null)
            {
                LayoutRecipes(craftingDefinitions);
                recipePage = Mathf.Clamp(recipePage, 0, recipePages.Count - 1);
            }
        }
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && heldCraftingSlot >= 0)
        {
            heldCraftingSlot = -1;
            Event.current.Use();
        }
        string tooltip = null;
        if (recipePages.Count > 0)
        {
            DrawListScroll(new Rect(area.x, area.y, area.width, 296), ref recipePage, recipePages.Count - 1);
            foreach (var recipe in recipePages[recipePage])
            {
                bool unlocked = recipe.Name != "Furnace" ||
                    online?.Snapshot?.milestones?.HasSeen("furnace_blueprint") == true;
                bool canCraft = unlocked && DoesFarmerHaveIngredients(recipe.Ingredients);
                DrawItemTile(recipe.Big ? craftablesTexture : objectsTexture, recipe.Tile, recipe.Bounds,
                    recipe.Big ? 32 : 16, canCraft ? Color.white : new Color(0, 0, 0, 0.30f));
                if (!recipe.Bounds.Contains(Event.current.mousePosition)) continue;
                bool advanced = PlayerPrefs.GetInt("GameMenu.AdvancedCrafting", 0) != 0;
                tooltip = recipe.Name + "\n" + (unlocked ? FormatIngredients(recipe.Ingredients, advanced) :
                    "Clint sẽ mang bản vẽ sau khi bạn khai thác quặng đồng.");
                if (Pressed(recipe.Bounds) && canCraft && !inventoryChangePending && heldCraftingSlot < 0)
                {
                    if (online == null) NpcConversation.ShowNotice("Đăng nhập để chế tạo.");
                    else
                    {
                        inventoryChangePending = true;
                        online.Craft(recipe.Name, FinishInventoryChange);
                    }
                }
            }
        }

        StardewUiAtlas.DrawMenuTile(4, new Rect(0, 328, 64, 64));
        StardewUiAtlas.DrawMenuTile(6, new Rect(64, 328, 736, 64));
        StardewUiAtlas.DrawMenuTile(7, new Rect(800, 328, 64, 64));
        string inventoryTooltip = DrawCraftingInventory();
        tooltip ??= inventoryTooltip;

        Rect trash = new(880, 240, 72, 104);
        bool overTrash = trash.Contains(Event.current.mousePosition);
        StardewUiAtlas.DrawTrashCan(trash, online?.Snapshot?.trashCanLevel ?? 0, overTrash && heldCraftingSlot >= 0);
        IReadOnlyItemSlot held = heldCraftingSlot < 0 ? null : inventory?.GetSlot(heldCraftingSlot);
        if (held?.Data != null && !held.IsEmpty)
        {
            if (Pressed(trash) && !inventoryChangePending && online != null)
            {
                inventoryChangePending = true;
                online.DiscardInventory(heldCraftingSlot, held.Data.itemId, FinishInventoryChange);
                GameAudio.Play("trashcan");
                heldCraftingSlot = -1;
            }
            else
            {
                Vector2 mouse = Event.current.mousePosition;
                Rect item = new(mouse.x + 12, mouse.y + 12, 64, 64);
                DrawSprite(held.Data.GetIcon(), item);
                StardewUiAtlas.DrawStackCount(item, held.Amount, held.Data?.itemId);
            }
        }
        else if (heldCraftingSlot < 0 && tooltip != null)
        {
            DrawTooltip(tooltip, FrameWidth(page), 616);
        }
    }

    private string DrawCraftingInventory()
    {
        if (inventory == null) return null;
        string tooltip = null;
        for (int i = 0; i < InventoryModel.MaxCapacity; i++)
        {
            Rect cell = BackpackGridUI.InventorySlotRect(i);
            cell.y += 328;
            StardewUiAtlas.DrawMenuTilesSprite(cell, StardewUiAtlas.SlotNormalRect);
            if (i >= inventory.UnlockedSlots)
            {
                StardewUiAtlas.DrawMenuTilesSprite(cell, StardewUiAtlas.SlotLockedRect,
                    new Color(1, 1, 1, 0.5f));
                continue;
            }

            IReadOnlyItemSlot slot = inventory.GetSlot(i);
            if (i == heldCraftingSlot)
                StardewUiAtlas.DrawMenuTilesSprite(cell, StardewUiAtlas.SlotSelectedRect);
            else if (slot?.Data != null && !slot.IsEmpty)
            {
                DrawSprite(slot.Data.GetIcon(), new Rect(cell.x + 8, cell.y + 8, 48, 48));
                StardewUiAtlas.DrawStackCount(cell, slot.Amount, slot.Data?.itemId);
                if (cell.Contains(Event.current.mousePosition)) tooltip = slot.Data.displayName;
            }

            if (!Pressed(cell) || inventoryChangePending) continue;
            if (heldCraftingSlot < 0)
            {
                if (slot?.Data != null && !slot.IsEmpty) heldCraftingSlot = i;
                continue;
            }
            if (heldCraftingSlot != i)
            {
                if (online == null) NpcConversation.ShowNotice("Đăng nhập để thay đổi túi đồ.");
                else
                {
                    inventoryChangePending = true;
                    online.SwapInventory(heldCraftingSlot, i, FinishInventoryChange);
                }
            }
            heldCraftingSlot = -1;
        }
        return tooltip;
    }

    private void FinishInventoryChange(string error)
    {
        inventoryChangePending = false;
        if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
    }

    private string FormatIngredients(string encoded, bool showHeldCount)
    {
        string[] tokens = encoded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> lines = new();
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            string item = tokens[i] switch
            {
                "388" => "Wood",
                "390" => "Stone",
                "771" => "Fiber",
                "92" => "Sap",
                "382" => "Coal",
                "378" => "Copper Ore",
                "380" => "Iron Ore",
                "384" => "Gold Ore",
                _ when RecipeCategories.IsCategory(tokens[i]) => RecipeCategories.Label(tokens[i]),
                _ => FarmRanchAssets.ItemName(MapStardewIdToItemId(tokens[i])) ?? tokens[i]
            };
            string amount = showHeldCount ? GetCountOfItem(tokens[i]) + "/" + tokens[i + 1] : tokens[i + 1];
            lines.Add(item + " " + amount);
        }
        return string.Join("\n", lines);
    }

    private static readonly string[] CollectionNames =
        { "Items Shipped", "Fish", "Artifacts", "Minerals", "Cooking", "Achievements", "Letters" };
    private static readonly Color Undiscovered = new(0, 0, 0, 0.2f);
    private const int CollectionPageSize = 70;

    private void DrawCollections(Rect area)
    {
        string tooltip = null;
        for (int i = 0; i < CollectionTabs.Length; i++)
        {
            Rect tab = new(i == collectionTab ? -40 : -48, 64 + i * 64, 64, 64);
            StardewUiAtlas.DrawCursorsSprite(tab, CollectionTabs[i]);
            if (tab.Contains(Event.current.mousePosition)) tooltip = CollectionNames[i];
            if (!Pressed(tab) || collectionTab == i) continue;
            collectionTab = i;
            collectionPage = 0;
            GameAudio.Play("smallSelect");
        }

        if (collectionTab == 5)
        {
            string achievementTooltip = DrawAchievements();
            tooltip ??= achievementTooltip;
        }
        else if (collectionTab == 6)
        {
            DrawText("Letters", new Rect(48, 56, 680, 44), 2f);
        }
        else
        {
            int[] items = collectionTab switch
            {
                0 => ShippedItems,
                1 => fishItems,
                2 => ArtifactItems,
                3 => MineralItems,
                _ => cookingItems
            };
            DrawCollectionPager(area, items.Length);
            int start = collectionPage * CollectionPageSize;
            for (int i = start; i < Mathf.Min(items.Length, start + CollectionPageSize); i++)
            {
                int tile = items[i];
                int count = GetCollectionCount(tile);
                Rect bounds = CollectionCell(i - start);
                DrawItemTile(objectsTexture, tile, bounds, 16, count > 0 ? Color.white : Undiscovered);
                if (bounds.Contains(Event.current.mousePosition))
                {
                    string name = collectionTab == 1 && fishNames.TryGetValue(tile, out string fish)
                        ? fish : GetItemName(tile);
                    tooltip = count <= 0 ? "???" : collectionTab == 0
                        ? $"{name}\nĐã gửi {count}"
                        : $"{name}\nĐã câu {count} con";
                }
            }
        }

        if (tooltip != null) DrawTooltip(tooltip, FrameWidth(page), 616);
    }

    private int GetCollectionCount(int tile)
    {
        OnlineCollectionCount[] counts = collectionTab switch
        {
            0 => online?.Snapshot?.itemsShipped,
            1 => online?.Snapshot?.fishCaught,
            _ => null
        };
        if (counts == null) return 0;
        string id = tile.ToString();
        string itemId = MapStardewIdToItemId(id);
        int total = 0;
        foreach (var entry in counts)
            if (entry != null && (entry.itemId == id || entry.itemId == itemId))
                total += entry.amount;
        return total;
    }

    private string DrawAchievements()
    {
        int[] earnedIds = online?.Snapshot?.achievements ?? Array.Empty<int>();
        var visible = new List<int>(achievementIds.Length);
        foreach (int id in achievementIds)
        {
            string[] fields = achievementDefinitions[id].Split('^');
            if (fields.Length < 4) continue;
            bool earned = Array.IndexOf(earnedIds, id) >= 0;
            bool visibleBeforeEarned = string.Equals(fields[2], "true", StringComparison.OrdinalIgnoreCase)
                && (fields[3] == "-1" || fields[3].Split(' ').All(value =>
                    int.TryParse(value, out int prerequisite) && Array.IndexOf(earnedIds, prerequisite) >= 0));
            if (earned || visibleBeforeEarned) visible.Add(id);
        }

        DrawCollectionPager(new Rect(32, 36, FrameWidth(page) - 64, 560), visible.Count);
        string tooltip = null;
        int start = collectionPage * CollectionPageSize;
        for (int i = start; i < Mathf.Min(visible.Count, start + CollectionPageSize); i++)
        {
            int id = visible[i];
            bool earned = Array.IndexOf(earnedIds, id) >= 0;
            Rect icon = CollectionCell(i - start);
            StardewUiAtlas.DrawCursorsSprite(icon, new Rect(192, 128, 64, 64),
                earned ? Color.white : Undiscovered);
            if (earned)
            {
                int medal = new System.Random(id).Next(12);
                StardewUiAtlas.DrawCursorsSprite(new Rect(icon.x + 16, icon.y + 20, 32, 32),
                    new Rect(256 + medal % 6 * 32, 128 + medal / 6 * 32, 32, 32));
            }
            if (icon.Contains(Event.current.mousePosition))
            {
                string[] fields = achievementDefinitions[id].Split('^');
                tooltip = fields[0] + "\n" + fields[1];
            }
        }
        return tooltip;
    }

    private static Rect CollectionCell(int index) => new(48 + index % 10 * 68, 48 + index / 10 * 68, 64, 64);

    private void DrawCollectionPager(Rect area, int count)
    {
        int maximum = Mathf.Max(0, (count - 1) / CollectionPageSize);
        collectionPage = Mathf.Clamp(collectionPage, 0, maximum);
        HandleScroll(area, ref collectionPage, maximum);
        if (collectionPage > 0 && DrawSpriteButton(new Rect(48, 536, 48, 44), new Rect(352, 495, 12, 11)))
            collectionPage--;
        if (collectionPage < maximum && DrawSpriteButton(new Rect(704, 536, 48, 44), new Rect(365, 495, 12, 11)))
            collectionPage++;
    }

    private static readonly string[] OptionRows =
    {
        "General", "Show Portraits", "Show Merchant Portraits", "Show Advanced Crafting Information",
        "Sound", "Master Volume", "Music Volume", "Sound Volume", "Ambient Volume", "Footstep Volume",
        "Graphics", "Fullscreen", "VSync"
    };

    private void DrawOptions(Rect area)
    {
        const int visible = 7;
        DrawListScroll(area, ref optionsOffset, OptionRows.Length - visible);
        for (int row = 0; row < visible; row++)
        {
            int option = optionsOffset + row;
            string label = OptionRows[option];
            float y = 48 + row * 74;
            switch (option)
            {
                case 0 or 4 or 10:
                    DrawText(label + ":", new Rect(48, y + 4, 760, 52), 2.4f);
                    break;
                case 1:
                    DrawPreference(label, y, "GameMenu.ShowPortraits", true);
                    break;
                case 2:
                    DrawPreference(label, y, "GameMenu.ShowMerchantPortraits", true);
                    break;
                case 3:
                    DrawPreference(label, y, "GameMenu.AdvancedCrafting", false);
                    break;
                case 5:
                    DrawVolume(option, label, y, AudioListener.volume, value =>
                    {
                        AudioListener.volume = value;
                        PlayerPrefs.SetFloat("GameMenu.Volume", value);
                    });
                    break;
                case >= 6 and <= 9:
                    var channel = (GameAudio.Channel)(option - 6);
                    DrawVolume(option, label, y, GameAudio.GetChannelVolume(channel),
                        value => GameAudio.SetChannelVolume(channel, value));
                    break;
                case 11:
                    if (DrawOptionCheckbox(label, y, Screen.fullScreen))
                    {
                        bool fullscreen = !Screen.fullScreen;
                        Screen.fullScreen = fullscreen;
                        SavePreference("GameMenu.Fullscreen", fullscreen);
                    }
                    break;
                case 12:
                    if (DrawOptionCheckbox(label, y, QualitySettings.vSyncCount > 0))
                    {
                        QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
                        SavePreference("GameMenu.VSync", QualitySettings.vSyncCount > 0);
                    }
                    break;
            }
        }

        if (Event.current.type == EventType.MouseUp && activeSlider >= 0)
        {
            activeSlider = -1;
            PlayerPrefs.Save();
        }
    }

    private void DrawPreference(string label, float y, string key, bool fallback)
    {
        bool enabled = PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
        if (DrawOptionCheckbox(label, y, enabled)) SavePreference(key, !enabled);
    }

    private static void SavePreference(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private bool DrawOptionCheckbox(string label, float y, bool enabled)
    {
        StardewUiAtlas.DrawCursorsSprite(new Rect(48, y + 4, 36, 36),
            new Rect(enabled ? 236 : 227, 425, 9, 9));
        DrawText(label, new Rect(96, y + 8, 712, 44), 2f);
        if (!Pressed(new Rect(48, y, 760, 56))) return false;
        GameAudio.Play("drumkit6");
        return true;
    }

    private void DrawVolume(int option, string label, float y, float value, Action<float> change)
    {
        DrawText(label, new Rect(48, y + 9, 252, 40), 1.8f);
        Rect slider = new(310, y + 8, 440, 24);
        StardewUiAtlas.DrawTextureBox(slider, StardewUiAtlas.Cursors,
            new Rect(403, 383, 6, 6), Color.white, 4f, false);
        StardewUiAtlas.DrawCursorsSprite(new Rect(slider.x + (slider.width - 40) * value, slider.y, 40, 24),
            new Rect(420, 441, 10, 6));
        DrawText(Mathf.RoundToInt(value * 100) + "%", new Rect(766, y + 9, 72, 40), 1.6f);
        Event current = Event.current;
        bool start = Pressed(new Rect(slider.x, y, slider.width, 48));
        if (start) activeSlider = option;
        if (start || (activeSlider == option && current.type == EventType.MouseDrag))
        {
            change(Mathf.Clamp01((current.mousePosition.x - slider.x - 20) / (slider.width - 40)));
            if (!start) current.Use();
        }
    }

    private void DrawExit()
    {
        Rect title = new((FrameWidth(page) - 304) / 2, 160, 304, 96);
        Rect desktop = new((FrameWidth(page) - 352) / 2, 296, 352, 96);
        if (DrawExitButton(title, pendingExit == 1 ? "Confirm Exit to Title" : "Exit to Title"))
        {
            if (pendingExit == 1)
            {
                closeMenu?.Invoke();
                StartupSession.ReturnToTitle();
            }
            else pendingExit = 1;
        }
        if (DrawExitButton(desktop, pendingExit == 2 ? "Confirm Exit to Desktop" : "Exit to Desktop"))
        {
            if (pendingExit == 2)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else pendingExit = 2;
        }
    }

    private bool DrawExitButton(Rect bounds, string label)
    {
        StardewUiAtlas.DrawTextureBox(bounds, StardewUiAtlas.Cursors,
            new Rect(432, 439, 9, 9), bounds.Contains(Event.current.mousePosition) ?
                new Color(0.96f, 0.87f, 0.7f) : Color.white, 4f);
        DrawProfileLine(label, new Rect(bounds.x + 16, bounds.y + 36, bounds.width - 32, 44));
        if (!Pressed(bounds)) return false;
        GameAudio.Play("smallSelect");
        return true;
    }

    private void DrawListScroll(Rect area, ref int offset, int maximum)
    {
        if (maximum <= 0) return;
        HandleScroll(area, ref offset, maximum);
        float x = FrameWidth(page) + 16;
        Rect up = new(x, area.y, 44, 48);
        Rect down = new(x, area.yMax - 48, 44, 48);
        if (DrawSpriteButton(up, new Rect(421, 459, 11, 12))) offset = Mathf.Max(0, offset - 1);
        if (DrawSpriteButton(down, new Rect(421, 472, 11, 12))) offset = Mathf.Min(maximum, offset + 1);
        Rect track = new(x + 10, up.yMax + 4, 24, down.y - up.yMax - 8);
        StardewUiAtlas.DrawTextureBox(track, StardewUiAtlas.Cursors, new Rect(403, 383, 6, 6),
            Color.white, 4f, false);
        Event current = Event.current;
        bool start = Pressed(track);
        if (start) draggingScroll = true;
        if (start || (draggingScroll && current.type == EventType.MouseDrag))
        {
            float position = (current.mousePosition.y - track.y - 20) / (track.height - 40);
            offset = Mathf.RoundToInt(Mathf.Clamp01(position) * maximum);
            if (!start) current.Use();
        }
        if (current.type == EventType.MouseUp) draggingScroll = false;
        StardewUiAtlas.DrawCursorsSprite(new Rect(track.x, track.y + (track.height - 40) * offset / maximum, 24, 40),
            new Rect(435, 463, 6, 10));
    }

    private static bool DrawSpriteButton(Rect bounds, Rect sprite)
    {
        StardewUiAtlas.DrawCursorsSprite(bounds, sprite);
        if (!Pressed(bounds)) return false;
        GameAudio.Play("smallSelect");
        return true;
    }

    private static bool Pressed(Rect bounds)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 ||
            !bounds.Contains(current.mousePosition)) return false;
        current.Use();
        return true;
    }

    private static void HandleScroll(Rect area, ref int offset, int maximum)
    {
        Event current = Event.current;
        if (current.type != EventType.ScrollWheel || !area.Contains(current.mousePosition)) return;
        offset = Mathf.Clamp(offset + (current.delta.y > 0 ? 1 : -1), 0, maximum);
        current.Use();
    }

    private void DrawTooltip(string value, float width, float height)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
        int split = value.IndexOf('\n');
        GameText.DrawTooltip(split < 0 ? value : value.Substring(0, split),
            split < 0 ? null : value.Substring(split + 1), width, height);
    }

    private void DrawBoldTooltip(string value, float width, float height)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
        string[] lines = value.Split('\n');
        const float scale = 1.65f, horizontalPadding = 20f, verticalPadding = 16f, gap = 16f;
        float maxWidth = Mathf.Min(460f, width);
        float measuredWidth = lines.Max(line => NpcBitmapText.GetStringWidth(line, scale));
        float w = Mathf.Clamp(Mathf.Ceil(measuredWidth + 2f * horizontalPadding + 8f), 112f, maxWidth);
        float textWidth = w - 2f * horizontalPadding;
        float textHeight = NpcBitmapText.MeasureWrappedHeight(value, textWidth, scale);
        float h = Mathf.Min(height, Mathf.Ceil(textHeight + 2f * verticalPadding));
        Vector2 mouse = Event.current.mousePosition;
        float x = mouse.x + gap + w <= width ? mouse.x + gap : mouse.x - w - gap;
        float y = mouse.y + gap + h <= height ? mouse.y + gap : mouse.y - h - gap;
        Rect bounds = new(Mathf.Clamp(x, 0f, width - w), Mathf.Clamp(y, 0f, height - h), w, h);
        StardewUiAtlas.DrawTextureBox(bounds, StardewUiAtlas.Cursors, new Rect(384, 373, 18, 18),
            Color.white, 4f, false);
        DrawText(value, new Rect(bounds.x + horizontalPadding, bounds.y + verticalPadding,
            textWidth, h - 2f * verticalPadding), scale);
    }

    private static void DrawAtlas(Texture2D atlas, Rect destination, Rect source, Color? tint = null)
    {
        if (atlas == null || source.x < 0 || source.y < 0 || source.xMax > atlas.width ||
            source.yMax > atlas.height || Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        if (tint.HasValue) GUI.color = tint.Value;
        GUI.DrawTextureWithTexCoords(destination, atlas, StardewUiAtlas.ToUv(source, atlas.width, atlas.height));
        GUI.color = previous;
    }

    private static void DrawItemTile(Texture2D atlas, int id, Rect destination, int tileHeight, Color tint)
    {
        if (atlas == null || atlas.width < 16 || id < 0) return;
        int columns = atlas.width / 16;
        DrawAtlas(atlas, destination, new Rect(id % columns * 16, id / columns * tileHeight, 16, tileHeight), tint);
    }

    private void DrawText(string value, Rect bounds, float scale, bool centered = false)
        => NpcBitmapText.Draw(font, bounds, value, scale, centered);

    private static void DrawSprite(Sprite sprite, Rect destination)
    {
        if (sprite == null || sprite.texture == null) return;
        Texture2D texture = sprite.texture;
        Rect pixels = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(destination, texture,
            new Rect(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height));
    }

    private static void DrawNpcMugshot(NpcDefinition npc, Rect destination)
    {
        if (npc.animationFrames == null || npc.animationFrames.Length == 0) return;
        DrawAtlas(npc.animationFrames[0]?.texture, destination, new Rect(0, 0, 16, 24));
    }

    private static string GetItemName(int tileId)
    {
        return tileId switch
        {
            24 => "Parsnip",
            188 => "Green Bean",
            190 => "Cauliflower",
            192 => "Potato",
            388 => "Wood",
            390 => "Stone",
            771 => "Fiber",
            92 => "Sap",
            382 => "Coal",
            378 => "Copper Ore",
            380 => "Iron Ore",
            384 => "Gold Ore",
            16 => "Wild Horseradish",
            18 => "Daffodil",
            20 => "Leek",
            22 => "Dandelion",
            88 => "Coconut",
            90 => "Cactus Fruit",
            128 => "Pufferfish",
            129 => "Anchovy",
            130 => "Tuna",
            131 => "Sardine",
            132 => "Bream",
            136 => "Largemouth Bass",
            137 => "Smallmouth Bass",
            138 => "Rainbow Trout",
            139 => "Salmon",
            140 => "Walleye",
            60 => "Emerald",
            62 => "Aquamarine",
            64 => "Ruby",
            66 => "Amethyst",
            68 => "Topaz",
            70 => "Jade",
            72 => "Diamond",
            74 => "Prismatic Shard",
            80 => "Quartz",
            82 => "Fire Quartz",
            194 => "Fried Egg",
            195 => "Omelet",
            196 => "Salad",
            197 => "Cheese Cauliflower",
            _ => ItemNames.Get("object_" + tileId) ?? "Item #" + tileId
        };
    }

    private static string MapStardewIdToItemId(string id)
    {
        return id switch
        {
            "388" => "wood",
            "390" => "stone",
            "771" => "fiber",
            "382" => "coal",
            "378" => "copper_ore",
            "380" => "iron_ore",
            "384" => "gold_ore",
            "24" => "parsnip",
            "188" => "green_bean",
            "190" => "cauliflower",
            "192" => "potato",
            _ when id.StartsWith("(BC)", StringComparison.Ordinal) => "big_craftable_" + id.Substring(4),
            _ when id.StartsWith("(O)", StringComparison.Ordinal) => MapStardewIdToItemId(id.Substring(3)),
            _ => "object_" + id
        };
    }

    private int GetCountOfItem(string id)
    {
        if (inventory == null) return 0;
        string mappedId = MapStardewIdToItemId(id);
        int total = 0;
        for (int slotIndex = 0; slotIndex < inventory.UnlockedSlots; slotIndex++)
        {
            IReadOnlyItemSlot slot = inventory.GetSlot(slotIndex);
            if (slot?.Data == null || slot.IsEmpty) continue;
            string held = ItemQuality.BaseId(slot.Data.itemId);
            bool match = RecipeCategories.IsCategory(id)
                ? RecipeCategories.Matches(id, held)
                : string.Equals(held, mappedId, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(held, id, StringComparison.OrdinalIgnoreCase);
            if (match) total += slot.Amount;
        }
        return total;
    }

    private bool DoesFarmerHaveIngredients(string recipeIngredients)
    {
        if (inventory == null) return false;
        string[] tokens = recipeIngredients.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!int.TryParse(tokens[i + 1], out int required)) continue;
            if (GetCountOfItem(tokens[i]) < required) return false;
        }
        return true;
    }
}
