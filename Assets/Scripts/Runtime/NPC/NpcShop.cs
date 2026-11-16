using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Opens the merchant interface when the player interacts with a working shopkeeper.</summary>
public sealed partial class NpcShop : MonoBehaviour
{
    private readonly struct StockItem
    {
        public readonly string ItemId;
        public readonly int Price;
        public StockItem(string itemId, int price) { ItemId = itemId; Price = price; }
    }

    private static readonly Dictionary<string, (int sheetIndex, string name, int defaultPrice)> ItemCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        // Spring Seeds
        ["seeds_parsnip"] = (472, "Parsnip Seeds", 20),
        ["seeds_green_bean"] = (473, "Bean Starter", 60),
        ["seeds_cauliflower"] = (474, "Cauliflower Seeds", 80),
        ["seeds_potato"] = (475, "Potato Seeds", 50),
        ["seeds_tulip"] = (427, "Tulip Bulb", 20),
        ["seeds_kale"] = (477, "Kale Seeds", 70),
        ["seeds_jazz"] = (429, "Jazz Seeds", 30),
        ["seeds_garlic"] = (476, "Garlic Seeds", 40),
        ["seeds_rice"] = (273, "Rice Shoot", 40),

        // Summer Seeds
        ["seeds_melon"] = (479, "Melon Seeds", 80),
        ["seeds_tomato"] = (480, "Tomato Seeds", 50),
        ["seeds_blueberry"] = (481, "Blueberry Seeds", 80),
        ["seeds_pepper"] = (482, "Pepper Seeds", 40),
        ["seeds_wheat"] = (483, "Wheat Seeds", 10),
        ["seeds_radish"] = (484, "Radish Seeds", 40),
        ["seeds_poppy"] = (453, "Poppy Seeds", 100),
        ["seeds_spangle"] = (455, "Spangle Seeds", 50),
        ["seeds_hops"] = (302, "Hops Starter", 60),
        ["seeds_corn"] = (487, "Corn Seeds", 150),
        ["seeds_sunflower"] = (431, "Sunflower Seeds", 100),
        ["seeds_red_cabbage"] = (485, "Red Cabbage Seeds", 100),

        // Fall Seeds
        ["seeds_eggplant"] = (488, "Eggplant Seeds", 20),
        ["seeds_pumpkin"] = (490, "Pumpkin Seeds", 100),
        ["seeds_bok_choy"] = (491, "Bok Choy Seeds", 50),
        ["seeds_yam"] = (492, "Yam Seeds", 60),
        ["seeds_cranberry"] = (493, "Cranberry Seeds", 240),
        ["seeds_fairy"] = (425, "Fairy Seeds", 200),
        ["seeds_amaranth"] = (299, "Amaranth Seeds", 70),
        ["seeds_grape"] = (301, "Grape Starter", 60),
        ["seeds_artichoke"] = (489, "Artichoke Seeds", 30),

        // Year-Round & General Store Goods (All seasons including Winter)
        ["grass_starter"] = (297, "Grass Starter", 100),
        ["sugar"] = (245, "Sugar", 100),
        ["wheat_flour"] = (246, "Wheat Flour", 100),
        ["rice"] = (423, "Rice", 200),
        ["oil"] = (247, "Oil", 200),
        ["vinegar"] = (419, "Vinegar", 200),
        ["basic_fertilizer"] = (368, "Basic Fertilizer", 100),
        ["basic_retaining_soil"] = (370, "Basic Retaining Soil", 100),
        ["speed_gro"] = (465, "Speed-Gro", 100),
        ["cherry_sapling"] = (628, "Cherry Sapling", 3400),
        ["apricot_sapling"] = (629, "Apricot Sapling", 2000),
        ["orange_sapling"] = (630, "Orange Sapling", 4000),
        ["peach_sapling"] = (631, "Peach Sapling", 6000),
        ["pomegranate_sapling"] = (632, "Pomegranate Sapling", 6000),
        ["apple_sapling"] = (633, "Apple Sapling", 4000),
        ["bouquet"] = (458, "Bouquet", 200),

        // General merchant goods
        ["wood"] = (388, "Wood", 10),
        ["stone"] = (390, "Stone", 20),
        ["copper_ore"] = (378, "Copper Ore", 75),
        ["iron_ore"] = (380, "Iron Ore", 150),
        ["coal"] = (382, "Coal", 150),
        ["gold_ore"] = (384, "Gold Ore", 400),
        ["fishing_rod"] = (-1, "Fishing Rod", 500),
        ["coffee"] = (395, "Coffee", 300),
        ["spicy_eel"] = (226, "Spicy Eel", 350)
    };

    private static readonly Dictionary<string, StockItem[]> DefaultStock = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Robin"] = new[] { new StockItem("wood", 10), new StockItem("stone", 20) },
        ["Clint"] = new[] { new StockItem("copper_ore", 75), new StockItem("iron_ore", 150) },
        ["Willy"] = new[] { new StockItem("fishing_rod", 500) },
        ["Marnie"] = Array.Empty<StockItem>(),
        ["Gus"] = new[] { new StockItem("coffee", 300), new StockItem("spicy_eel", 350) },
        ["Harvey"] = Array.Empty<StockItem>(),
        ["Sandy"] = Array.Empty<StockItem>(),
        ["Krobus"] = Array.Empty<StockItem>(),
        ["Dwarf"] = Array.Empty<StockItem>()
    };

    private static StockItem[] GetStock(string shop)
    {
        if (shop == "Marlon")
            return GuildItems.Values.Where(offer => offer.Floor == 0 && offer.CombatLevel == 0)
                .Select(offer => new StockItem(offer.ItemId, offer.Price)).ToArray();
        string season = FarmDayClock.Instance != null ? FarmDayClock.Instance.CurrentSeason : "spring";
        if (string.Equals(shop, "Pierre", StringComparison.OrdinalIgnoreCase))
        {
            var list = new List<StockItem>();
            if (season == "spring")
            {
                list.Add(new StockItem("seeds_parsnip", 20));
                list.Add(new StockItem("seeds_green_bean", 60));
                list.Add(new StockItem("seeds_cauliflower", 80));
                list.Add(new StockItem("seeds_potato", 50));
                list.Add(new StockItem("seeds_tulip", 20));
                list.Add(new StockItem("seeds_kale", 70));
                list.Add(new StockItem("seeds_jazz", 30));
                list.Add(new StockItem("seeds_garlic", 40));
                list.Add(new StockItem("seeds_rice", 40));
            }
            else if (season == "summer")
            {
                list.Add(new StockItem("seeds_melon", 80));
                list.Add(new StockItem("seeds_tomato", 50));
                list.Add(new StockItem("seeds_blueberry", 80));
                list.Add(new StockItem("seeds_pepper", 40));
                list.Add(new StockItem("seeds_wheat", 10));
                list.Add(new StockItem("seeds_radish", 40));
                list.Add(new StockItem("seeds_poppy", 100));
                list.Add(new StockItem("seeds_spangle", 50));
                list.Add(new StockItem("seeds_hops", 60));
                list.Add(new StockItem("seeds_corn", 150));
                list.Add(new StockItem("seeds_sunflower", 100));
                list.Add(new StockItem("seeds_red_cabbage", 100));
            }
            else if (season == "fall")
            {
                list.Add(new StockItem("seeds_eggplant", 20));
                list.Add(new StockItem("seeds_corn", 150));
                list.Add(new StockItem("seeds_pumpkin", 100));
                list.Add(new StockItem("seeds_bok_choy", 50));
                list.Add(new StockItem("seeds_yam", 60));
                list.Add(new StockItem("seeds_cranberry", 240));
                list.Add(new StockItem("seeds_wheat", 10));
                list.Add(new StockItem("seeds_sunflower", 100));
                list.Add(new StockItem("seeds_fairy", 200));
                list.Add(new StockItem("seeds_amaranth", 70));
                list.Add(new StockItem("seeds_grape", 60));
                list.Add(new StockItem("seeds_artichoke", 30));
            }

            // Year-round general goods available in all seasons (including Winter):
            list.Add(new StockItem("grass_starter", 100));
            list.Add(new StockItem("sugar", 100));
            list.Add(new StockItem("wheat_flour", 100));
            list.Add(new StockItem("rice", 200));
            list.Add(new StockItem("oil", 200));
            list.Add(new StockItem("vinegar", 200));
            list.Add(new StockItem("basic_fertilizer", 100));
            list.Add(new StockItem("basic_retaining_soil", 100));
            list.Add(new StockItem("speed_gro", 100));

            // Fruit tree saplings & bouquet:
            list.Add(new StockItem("cherry_sapling", 3400));
            list.Add(new StockItem("apricot_sapling", 2000));
            list.Add(new StockItem("orange_sapling", 4000));
            list.Add(new StockItem("peach_sapling", 6000));
            list.Add(new StockItem("pomegranate_sapling", 6000));
            list.Add(new StockItem("apple_sapling", 4000));
            list.Add(new StockItem("bouquet", 200));

            return list.ToArray();
        }

        if (string.Equals(shop, "JojaMart", StringComparison.OrdinalIgnoreCase))
        {
            var list = new List<StockItem>();
            if (season == "spring")
            {
                list.Add(new StockItem("seeds_parsnip", 25));
                list.Add(new StockItem("seeds_green_bean", 75));
                list.Add(new StockItem("seeds_cauliflower", 100));
                list.Add(new StockItem("seeds_potato", 62));
            }
            else if (season == "summer")
            {
                list.Add(new StockItem("seeds_melon", 100));
                list.Add(new StockItem("seeds_tomato", 62));
                list.Add(new StockItem("seeds_pepper", 50));
                list.Add(new StockItem("seeds_wheat", 12));
            }
            else if (season == "fall")
            {
                list.Add(new StockItem("seeds_eggplant", 25));
                list.Add(new StockItem("seeds_pumpkin", 125));
                list.Add(new StockItem("seeds_bok_choy", 62));
                list.Add(new StockItem("seeds_cranberry", 300));
            }
            list.Add(new StockItem("grass_starter", 125));
            list.Add(new StockItem("sugar", 125));
            list.Add(new StockItem("wheat_flour", 125));
            return list.ToArray();
        }

        if (DefaultStock.TryGetValue(shop, out StockItem[] items)) return items;
        return Array.Empty<StockItem>();
    }
    private static readonly Dictionary<string, string> ShopLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pierre"] = "SeedShop", ["Robin"] = "ScienceHouse",
        ["Clint"] = "Blacksmith", ["Willy"] = "FishShop",
        ["Marnie"] = "AnimalShop", ["Gus"] = "Saloon",
        ["Harvey"] = "Hospital", ["Sandy"] = "SandyHouse",
        ["Krobus"] = "Sewer", ["Dwarf"] = "Mine", ["Marlon"] = "AdventureGuild"
    };
    // Buildings-layer actions in the original shop maps.
    private static readonly Dictionary<string, Vector2Int[]> CounterTiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pierre"] = new Vector2Int[] { new(4, 18), new(5, 18) },
        ["Robin"] = new Vector2Int[] { new(8, 19) },
        ["Clint"] = new Vector2Int[] { new(3, 14) },
        ["Marlon"] = new Vector2Int[] { new(5, 12) },
        ["Willy"] = new Vector2Int[] { new(4, 5), new(5, 5), new(6, 5) },
        ["Marnie"] = new Vector2Int[] { new(12, 15), new(13, 15) },
        ["Gus"] = new Vector2Int[] { new(10, 19), new(11, 19), new(14, 19) },
        ["Harvey"] = new Vector2Int[] { new(5, 16), new(6, 16), new(7, 16) },
        ["Sandy"] = new Vector2Int[] { new(2, 6) }
    };

    private static NpcShop instance;
    private static Dictionary<string, ItemData> items;
    private NpcActor keeper;
    private string shopName;
    private bool backpackQuestion;
    private bool purchasePending;
    private int questionSelection;
    private PlayerInventory inventory;
    private FullInventoryMenuUI funds;
    private ToolbarUI toolbar;
    private bool toolbarWasActive;
    private string feedback;
    private string resaleHint;
    private int firstStockIndex;
    private StockItem[] serverStock;
    private static readonly int[] ToolUpgradePrices = { 2000, 5000, 10000, 25000 };
    private static readonly string[] ToolUpgradeTiers = { "Copper", "Steel", "Gold", "Iridium" };
    private bool confirmHouseUpgrade;
    private int openingVersion;

    private static readonly Rect ShopFrame = new(384, 373, 18, 18);
    private static readonly Rect StockRow = new(384, 396, 15, 15);
    private static readonly Rect GoldIcon = new(193, 373, 9, 10);
    private static readonly Rect PortraitFrame = new(603, 414, 74, 74);
    private static readonly Rect ScrollUp = new(421, 459, 11, 12);
    private static readonly Rect ScrollDown = new(421, 472, 11, 12);

    public static bool IsOpen => instance != null && instance.enabled;
    internal static void CloseMenu()
    {
        if (IsOpen) instance.Close();
    }

    public static bool TryOpen(Vector2? pointer, Vector3 playerPosition,
                               Vector2 facing, PlayerInventory playerInventory)
    {
        string location = LocationManager.Instance?.CurrentLocation?.LocationName;
        if (string.Equals(location, "SeedShop", StringComparison.OrdinalIgnoreCase) &&
            HitBackpack(pointer, playerPosition, facing))
        {
            return playerInventory.UnlockedSlots < 36 && Open("Pierre", null, playerInventory, true);
        }

        if (string.Equals(location, "JojaMart", StringComparison.OrdinalIgnoreCase) &&
            (HitActionTile(pointer, playerPosition, facing, 10, 24) ||
             HitActionTile(pointer, playerPosition, facing, 10, 25)))
            return Open("JojaMart", null, playerInventory, false);

        NpcActor closest = null;
        float bestDistance = 2.2f * 2.2f;
        foreach (NpcActor actor in FindObjectsByType<NpcActor>(FindObjectsSortMode.None))
        {
            if (!actor.IsVisible) continue;
            if (!ShopLocations.TryGetValue(actor.DisplayName, out string workLocation) ||
                !string.Equals(location, workLocation, StringComparison.OrdinalIgnoreCase)) continue;
            // Requires Pierre at the register and the farmer in front.
            if (actor.DisplayName == "Pierre" &&
                (NpcPathfinder.WorldToTile(actor.transform.position) != new Vector2Int(4, 17) ||
                 NpcPathfinder.WorldToTile(playerPosition).y <= 17)) continue;
            if (CounterTiles.TryGetValue(actor.DisplayName, out Vector2Int[] tiles))
            {
                foreach (Vector2Int tile in tiles)
                {
                    Vector2 counter = NpcPathfinder.TileToWorld(tile);
                    if (playerPosition.y > counter.y || ((Vector2)actor.transform.position - counter).sqrMagnitude > 9f) continue;
                    if (HitActionTile(pointer, playerPosition, facing, tile.x, tile.y))
                        return OpenCounter(actor, playerInventory);
                }
            }
            float distance = ((Vector2)actor.transform.position - (Vector2)playerPosition).sqrMagnitude;
            if (distance >= bestDistance) continue;
            if (pointer.HasValue && !actor.ContainsPointer(pointer.Value)) continue;
            if (!pointer.HasValue && Vector2.Dot(
                    ((Vector2)actor.transform.position - (Vector2)playerPosition).normalized,
                    facing.normalized) < 0.35f) continue;
            closest = actor;
            bestDistance = distance;
        }
        if (closest == null) return false;
        return OpenCounter(closest, playerInventory);
    }

    private static bool HitBackpack(Vector2? pointer, Vector3 playerPosition, Vector2 facing)
    {
        if (HitActionTile(pointer, playerPosition, facing, 7, 18)) return true;
        // The visible backpack is above its Buildings-layer action tile.
        return pointer.HasValue && new Rect(7f, -16.65f, 1f, 1.1f).Contains(pointer.Value) &&
            HitActionTile(null, playerPosition, Vector2.up, 7, 18);
    }

    internal static bool HitActionTile(Vector2? pointer, Vector3 playerPosition,
                                      Vector2 facing, int tileX, int tileY)
    {
        Vector2 center = NpcPathfinder.TileToWorld(new Vector2Int(tileX, tileY));
        Vector2 offset = center - (Vector2)playerPosition;
        if (offset.sqrMagnitude > 1.45f * 1.45f) return false;
        if (pointer.HasValue)
            return Mathf.Abs(pointer.Value.x - center.x) <= 0.55f &&
                   Mathf.Abs(pointer.Value.y - center.y) <= 0.55f;
        return Vector2.Dot(offset.normalized, facing.normalized) >= 0.35f;
    }

    public static int GetCurrentFunds()
    {
        var online = UnityEngine.Object.FindFirstObjectByType<OnlineGameState>();
        long money = online?.Snapshot?.money ?? 0;
        return money > int.MaxValue ? int.MaxValue : (int)money;
    }

    private static bool Open(string name, NpcActor actor, PlayerInventory playerInventory,
                             bool backpackOnly, BlacksmithMode mode = BlacksmithMode.Store, RanchPage ranch = RanchPage.None)
    {
        if (IsOpen || playerInventory == null) return false;
        GameAudio.Play("bigSelect");
        if (instance == null) instance = new GameObject("NPC Shop").AddComponent<NpcShop>();
        instance.keeper = actor;
        instance.shopName = name;
        instance.backpackQuestion = backpackOnly;
        instance.purchasePending = false;
        instance.questionSelection = 0;
        instance.inventory = playerInventory;
        instance.funds = FullInventoryMenuUI.Instance ?? UnityEngine.Object.FindFirstObjectByType<FullInventoryMenuUI>(FindObjectsInactive.Include);
        instance.feedback = string.Empty;
        instance.firstStockIndex = 0;
        instance.serverStock = null;
        instance.ResetBlacksmithMode(mode);
        instance.ResetRanch(ranch);
        instance.confirmHouseUpgrade = false;
        instance.toolbar = FindAnyObjectByType<ToolbarUI>();
        instance.toolbarWasActive = instance.toolbar != null && instance.toolbar.gameObject.activeSelf;
        if (instance.toolbarWasActive) instance.toolbar.gameObject.SetActive(false);
        instance.enabled = true;
        int version = ++instance.openingVersion;
        if (mode == BlacksmithMode.Store && ranch == RanchPage.None && UsesServerStock(name))
        {
            instance.feedback = "Loading shop...";
            instance.LoadServerStock(version);
        }
        return true;
    }

    private void LoadServerStock(int version, string successMessage = "")
    {
        var online = inventory?.GetComponent<OnlineGameState>();
        if (online == null) { feedback = "Sign in to buy items."; return; }
        online.LoadShop(shopName, (response, error) =>
        {
            if (!IsOpen || openingVersion != version) return;
            if (response?.offers == null) { feedback = error ?? "Could not load shop."; return; }
            serverStock = response.offers.Select(offer => new StockItem(offer.itemId, offer.price)).ToArray();
            firstStockIndex = Mathf.Clamp(firstStockIndex, 0, Mathf.Max(0, serverStock.Length - 4));
            feedback = successMessage;
        });
    }

    private void Update()
    {
        if (ranchPage != RanchPage.None) { UpdateCarpenter(); return; }
        if (blacksmithMode != BlacksmithMode.Store)
        {
            UpdateBlacksmith();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || purchasePending) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (!backpackQuestion) return;
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            questionSelection = 1;
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            questionSelection = 0;
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            if (questionSelection == 0) BuyBackpack();
            else Close();
        }
    }

    private void OnGUI()
    {
        if (shopName == null) { Close(); return; }
        GUI.depth = -50;
        if (backpackQuestion) { DrawBackpackQuestion(); return; }
        if (ranchPage == RanchPage.Buildings) { DrawCarpenter(); return; }
        if (ranchPage == RanchPage.Animals) { DrawAnimalShop(); return; }
        const float width = 1032f;
        const float height = 664f;
        float scale = Mathf.Min(1f, Screen.width / (width + 48f), Screen.height / (height + 48f));
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * 0.5f,
            (Screen.height - height * scale) * 0.5f, 0f), Quaternion.identity,
            new Vector3(scale, scale, 1f));
        try
        {
            Rect panel = new(0f, 0f, width, height);
            if (blacksmithMode == BlacksmithMode.Store) DrawShop(panel);
            else DrawBlacksmith(panel);
        }
        finally { GUI.matrix = oldMatrix; }
    }

    private void DrawShop(Rect box)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        Texture2D font = StardewUiAtlas.FontBold;
        if (cursors == null) return;

        // Sale window (height - 256 + 36), four stock rows,
        // then a separate inventory window anchored at height - 256 + 40.
        StardewUiAtlas.DrawTextureBox(new Rect(0f, 0f, box.width, box.height - 220f),
            cursors, ShopFrame, Color.white);
        Rect close = new(box.width - 36f, -8f, 48f, 48f);
        StardewUiAtlas.DrawCursorsSprite(close, StardewUiAtlas.UpperRightCloseButtonRect);
        if (InvisibleButton(close)) { Close(); return; }

        StockItem[] stock = serverStock ?? GetStock(shopName);
        if (confirmHouseUpgrade)
            stock = new[] { new StockItem("__houseConfirm", 10000), new StockItem("__shop", 0) };
        if (stock.Length == 0 && (!UsesServerStock(shopName) || serverStock != null))
            DrawText(font, new Rect(84f, 200f, box.width - 168f, 42f),
                "Out of stock", 2.2f);
        string tooltipTitle = null, tooltipBody = null;
        for (int row = 0; row < 4; row++)
        {
            Rect bounds = new(16f, 16f + row * 96f, box.width - 32f, 94f);
            bool available = firstStockIndex + row < stock.Length;
            Event current = Event.current;
            bool hovered = current != null && bounds.Contains(current.mousePosition) && !purchasePending;
            StardewUiAtlas.DrawTextureBox(bounds, cursors, StockRow,
                !available ? new Color(1f, 1f, 1f, 0.55f) :
                hovered ? new Color(0.96f, 0.88f, 0.72f) : Color.white, 4f, false);
            if (!available) continue;

            StockItem entry = stock[firstStockIndex + row];
            bool isBackpack = entry.ItemId == "__backpack";
            bool isShop = entry.ItemId == "__shop";
            bool confirmHouse = entry.ItemId == "__houseConfirm";
            ItemData item = isBackpack ? null : FindItem(entry.ItemId);
            // Draws each item on its own small slot box.
            StardewUiAtlas.DrawTextureBox(new Rect(bounds.x + 20f, bounds.y + 7f, 80f, 80f), cursors,
                new Rect(296, 363, 18, 18), Color.white, 4f, false);
            if (item != null && item.GetIcon() != null)
                DrawIcon(new Rect(bounds.x + 28f, bounds.y + 15f, 64f, 64f), item.GetIcon());
            if (isBackpack)
                StardewUiAtlas.DrawCursorsSprite(new Rect(bounds.x + 38f, bounds.y + 17f, 43f, 56f),
                    inventory != null && inventory.UnlockedSlots >= 24
                        ? new Rect(268, 1436, 11, 13) : new Rect(257, 1436, 9, 13));
            string name = isBackpack ? inventory != null && inventory.UnlockedSlots >= 36
                    ? "Backpack fully upgraded" : inventory != null && inventory.UnlockedSlots <= 12
                        ? "Large Pack (24 slots)" : "Deluxe Pack (36 slots)"
                : isShop ? "Back to Shop" :
                    confirmHouse ? "Confirm (450 Wood, 3 days)" : ShopDisplayName(entry.ItemId, item);
            var weapon = item as MeleeWeaponData;
            DrawText(font, new Rect(bounds.x + 112f, bounds.y + 22f, 600f, 55f), name, 3f);
            if (hovered && !isShop && !confirmHouse)
            {
                tooltipTitle = name;
                var lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(item?.description)) lines.Add(item.description);
                if (weapon != null) lines.Add($"Sát thương: {weapon.minDamage}-{weapon.maxDamage}");
                int shown = isBackpack ? BackpackPrice : entry.Price;
                if (shown > 0) lines.Add($"Giá: {shown:N0}g");
                tooltipBody = string.Join("\n", lines);
            }
            int price = isBackpack ? BackpackPrice : entry.Price;
            if (price > 0)
            {
                string priceText = price.ToString();
                float priceWidth = NpcBitmapText.GetStringWidth(priceText, 3f);
                DrawText(font, new Rect(bounds.xMax - 82f - priceWidth, bounds.y + 22f,
                    priceWidth + 8f, 55f), priceText, 3f);
                StardewUiAtlas.DrawCursorsSprite(new Rect(bounds.xMax - 58f, bounds.y + 32f, 36f, 40f), GoldIcon);
            }
            if (InvisibleButton(bounds) && !purchasePending)
            {
                if (isBackpack) { backpackQuestion = true; questionSelection = 0; }
                else if (isShop)
                {
                    confirmHouseUpgrade = false;
                    firstStockIndex = 0;
                    feedback = "";
                }
                else if (confirmHouse) OrderHouseUpgrade();
                else Buy(entry, item);
            }
        }

        if (stock.Length > 4)
        {
            Rect up = new(box.width + 16f, 70f, 44f, 48f);
            Rect down = new(box.width + 16f, 384f, 44f, 48f);
            StardewUiAtlas.DrawCursorsSprite(up, ScrollUp);
            StardewUiAtlas.DrawCursorsSprite(down, ScrollDown);
            if (InvisibleButton(up)) firstStockIndex = Mathf.Max(0, firstStockIndex - 1);
            if (InvisibleButton(down)) firstStockIndex = Mathf.Min(stock.Length - 4, firstStockIndex + 1);
        }

        StardewUiAtlas.DrawMoneyBox(new Vector2(-36f, box.height - 196f), GetCurrentFunds());
        resaleHint = null;
        DrawInventory(box, cursors);
        if (!string.IsNullOrEmpty(feedback))
            GameText.Draw(new Rect(40f, 396f, box.width - 120f, 44f), feedback, GameText.Face.Dialogue);

        if (keeper != null && keeper.Portrait != null && Screen.width >= 1600 &&
            PlayerPrefs.GetInt("GameMenu.ShowMerchantPortraits", 1) != 0)
        {
            StardewUiAtlas.DrawCursorsSprite(new Rect(-284f, 8f, 296f, 296f), PortraitFrame);
            DrawIcon(new Rect(-264f, 28f, 256f, 256f), keeper.Portrait);
            // The shopkeeper's line sits in a box under the portrait.
            string line = ShopkeeperLine(shopName);
            float height = GameText.MeasureHeight(line, 248f, GameText.Face.Dialogue) + 44f;
            var speech = new Rect(-284f, 312f, 296f, height);
            StardewUiAtlas.DrawTextureBox(speech, cursors, new Rect(384, 373, 18, 18), Color.white, 4f, false);
            GameText.Draw(new Rect(speech.x + 24f, speech.y + 20f, 252f, height - 24f), line, GameText.Face.Dialogue);
        }
        if (tooltipTitle != null) GameText.DrawTooltip(tooltipTitle, tooltipBody, box.width + 300f, box.height);
        else if (resaleHint != null) GameText.DrawTooltip(resaleHint, null, box.width + 300f, box.height);
    }

    private static string ShopkeeperLine(string shop) => shop switch
    {
        "Pierre" => "Chào mừng đến tiệm tạp hóa! Hạt giống mùa này đều ở đây cả.",
        "Robin" => "Cần gỗ hay đá cho công trình mới không? Cứ chọn thoải mái nhé.",
        "Clint" => "Quặng, than... cứ lấy những gì cậu cần.",
        "Marnie" => "Cỏ khô và đồ cho vật nuôi đây. Chúng sẽ vui lắm đấy!",
        "Marlon" => "Đường xuống hầm mỏ nguy hiểm lắm. Trang bị cho đàng hoàng vào.",
        "Willy" => "Câu cá cần kiên nhẫn, và một cần câu tốt.",
        "Gus" => "Đói bụng à? Quán lúc nào cũng có món ngon.",
        _ => "Cần gì cứ chọn nhé."
    };

    private void DrawInventory(Rect shop, Texture2D cursors, bool allowSales = true)
    {
        const int columns = 12;
        const float slotSize = 64f;
        const float rowGap = 4f;
        float left = shop.width - columns * slotSize - 32f;
        float top = shop.height - 216f;
        var panel = new Rect(left - 24f, top, columns * slotSize + 56f, shop.height - 428f);
        StardewUiAtlas.DrawTextureBox(panel, cursors, ShopFrame, Color.white);
        if (inventory == null) return;
        var online = inventory.GetComponent<OnlineGameState>();
        bool guild = shopName == "Marlon";

        for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
        {
            int column = index % columns;
            int row = index / columns;
            Rect rect = new(left + column * slotSize, top + 28f + row * (slotSize + rowGap), slotSize, slotSize);
            StardewUiAtlas.DrawMenuTilesSprite(rect, StardewUiAtlas.SlotNormalRect);
            if (index >= inventory.UnlockedSlots)
            {
                StardewUiAtlas.DrawMenuTilesSprite(rect, StardewUiAtlas.SlotLockedRect, new Color(1f, 1f, 1f, 0.5f));
                continue;
            }
            if (inventory.GetSlot(index) is not InventorySlot item || item.IsEmpty) continue;
            string itemId = item.itemData?.itemId;
            int guildPrice = guild ? online?.Snapshot?.guildResale?.FirstOrDefault(offer => offer.itemId == itemId)?.price ?? 0 : 0;
            if (rect.Contains(Event.current.mousePosition) && !purchasePending)
                resaleHint = guildPrice > 0 ? $"{ShopDisplayName(itemId, item.itemData)} - bán {guildPrice:N0}g"
                    : ShopDisplayName(itemId, item.itemData);
            Sprite icon = item.inventoryIcon != null ? item.inventoryIcon : item.itemData?.GetIcon();
            Color color = GUI.color;
            if (guild && guildPrice == 0) GUI.color *= new Color(1f, 1f, 1f, 0.45f);
            if (icon != null) DrawIcon(new Rect(rect.x + 8f, rect.y + 8f, 48f, 48f), icon);
            StardewUiAtlas.DrawStackCount(rect, item.amount, item.itemData?.itemId);
            GUI.color = color;
            if (allowSales && !confirmHouseUpgrade && !purchasePending && (!guild || guildPrice > 0) && InvisibleButton(rect))
            {
                if (online != null && online.Snapshot != null)
                {
                    purchasePending = true;
                    void Finished(string error)
                    {
                        purchasePending = false;
                        feedback = error ?? (guild ? $"Đã bán với giá {guildPrice:N0} G." : "Item sold.");
                        if (error == null) GameAudio.Play("sell");
                    }
                    online.Sell(index, itemId, 1, Finished, shopName);
                }
                else feedback = "Selling is unavailable in this save.";
            }
        }
    }

    private static void DrawText(Texture2D font, Rect bounds, string value, float scale)
        => NpcBitmapText.Draw(font, bounds, value, scale);

    private static bool InvisibleButton(Rect bounds)
    {
        bool pressed = GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        if (pressed) GameAudio.Play("smallSelect");
        return pressed;
    }

    private int BackpackPrice => inventory == null || inventory.UnlockedSlots >= 36 ? 0 :
        inventory.UnlockedSlots <= 12 ? 2000 : 10000;

    private void DrawBackpackQuestion()
    {
        Texture2D font = StardewUiAtlas.FontBold;
        if (font == null || StardewUiAtlas.Cursors == null) return;
        float width = Mathf.Min(1200f, Screen.width - 96f);
        float height = 320f;
        Rect box = new((Screen.width - width) * 0.5f,
            Screen.height - height - 64f, width, height);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        if (BackpackPrice == 0)
        {
            DrawText(font, new Rect(box.x + 24f, box.y + 25f, box.width - 48f, 54f),
                "Your backpack is fully upgraded.", 2.5f);
            if (DrawQuestionOption(font, new Rect(box.x + 20f, box.y + 126f,
                    box.width - 40f, 56f), "(Leave)", true)) Close();
            return;
        }
        int next = inventory.UnlockedSlots + 12;
        DrawText(font, new Rect(box.x + 24f, box.y + 20f, box.width - 48f, 72f),
            $"Upgrade your backpack to {next} slots?", 3f);
        Rect purchase = new(box.x + 20f, box.y + 116f, box.width - 40f, 56f);
        Rect cancel = new(box.x + 20f, box.y + 184f, box.width - 40f, 56f);
        if (DrawQuestionOption(font, purchase,
                purchasePending ? "Purchasing..." : $"Purchase ({BackpackPrice}g)",
                questionSelection == 0) && !purchasePending)
            BuyBackpack();
        if (DrawQuestionOption(font, cancel, "(Not now)", questionSelection == 1) &&
            !purchasePending) Close();
        if (!string.IsNullOrEmpty(feedback))
            DrawText(font, new Rect(box.x + 24f, box.y + 250f,
                box.width - 48f, 42f), feedback, 1.8f);
    }

    private bool DrawQuestionOption(Texture2D font, Rect row, string label, bool selected)
    {
        Event current = Event.current;
        bool hover = current != null && row.Contains(current.mousePosition);
        if (selected || hover)
            StardewUiAtlas.DrawTextureBox(row, StardewUiAtlas.Cursors,
                new Rect(375, 357, 3, 3), Color.white, 4f, false);
        DrawText(font, new Rect(row.x + 14f, row.y + 12f, row.width - 28f, 42f),
            label, 3f);
        if (!hover || current.type != EventType.MouseDown || current.button != 0)
            return false;
        current.Use();
        return true;
    }

    private void BuyBackpack()
    {
        int price = BackpackPrice;
        if (purchasePending || inventory == null || price == 0) return;
        OnlineGameState online = inventory.GetComponent<OnlineGameState>();
        if (online == null || online.Snapshot == null)
        {
            feedback = "Sign in and wait for the shop to load.";
            return;
        }
        purchasePending = true;
        online.BuyBackpack(error =>
        {
            purchasePending = false;
            string message = string.IsNullOrEmpty(error)
                ? $"Your backpack now has {inventory.UnlockedSlots} slots!" : error;
            if (string.IsNullOrEmpty(error)) GameAudio.Play("purchase");
            FindFirstObjectByType<SeedShopController>()?.UpdateBackpackDisplay();
            Close();
            NpcConversation.ShowNotice(message);
        });
    }

    private void Buy(StockItem entry, ItemData item)
    {
        if (UsesServerStock(shopName) && serverStock == null)
        { feedback = "Sign in and reopen the shop to load its stock."; return; }
        if (purchasePending || item == null || inventory == null)
        { feedback = "This item is unavailable."; return; }
        bool recipe = entry.ItemId.StartsWith("recipe_", StringComparison.Ordinal);
        if (!recipe && !inventory.CanAccept(item))
        { feedback = "Inventory is full."; return; }
        OnlineGameState online = inventory.GetComponent<OnlineGameState>();
        if (online == null || online.Snapshot == null)
        { feedback = "Sign in and wait for the shop to load."; return; }
        purchasePending = true;
        int version = openingVersion;
        feedback = "Purchasing...";
        online.Buy(entry.ItemId, 1, error =>
        {
            if (!IsOpen || openingVersion != version) return;
            purchasePending = false;
            feedback = string.IsNullOrEmpty(error)
                ? $"Bought {ShopDisplayName(entry.ItemId, item)}." : error;
            if (string.IsNullOrEmpty(error)) GameAudio.Play("purchase");
            if (error == null && recipe) LoadServerStock(version, "Đã học " + item.displayName + ".");
        }, shopName);
    }

    private void OrderHouseUpgrade()
    {
        var online = inventory?.GetComponent<OnlineGameState>();
        if (online == null) return;
        purchasePending = true;
        int version = openingVersion;
        online.UpgradeHouse(error =>
        {
            if (!IsOpen || openingVersion != version) return;
            purchasePending = false;
            if (error != null) { feedback = error; return; }
            confirmHouseUpgrade = false;
            feedback = "Robin will finish the house in 3 days.";
            GameAudio.Play("purchase");
        });
    }

    private static string ShopDisplayName(string id, ItemData item) => id switch
    {
        "seeds_parsnip" => "Parsnip Seeds",
        "seeds_potato" => "Potato Seeds",
        "seeds_cauliflower" => "Cauliflower Seeds",
        "seeds_green_bean" => "Bean Starter",
        "wood" => "Wood",
        "stone" => "Stone",
        "copper_ore" => "Copper Ore",
        "iron_ore" => "Iron Ore",
        "fishing_rod" => "Fishing Rod",
        "object_178" => "Cỏ khô",
        _ when id.StartsWith("recipe_", StringComparison.Ordinal) => item?.displayName ?? id,
        _ => item != null && IsLatinSpriteText(item.displayName)
            ? item.displayName : id.Replace('_', ' ')
    };

    private static bool IsLatinSpriteText(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (char character in value)
            if (character < 32 || character > 126) return false;
        return true;
    }

    private static Texture2D springObjectsTexture;
    private static Dictionary<string, string> furnitureDefinitions;
    private static readonly Dictionary<string, ItemData> merchantItems = new(StringComparer.Ordinal);
    private static Dictionary<string, GuildOffer> guildItems;

    private static bool UsesServerStock(string shop) => shop is "Marlon" or "Clint" or "Robin" or "Marnie";

    private sealed class GuildStock
    {
        public GuildOffer[] Offers { get; set; }
    }

    private sealed class GuildOffer
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public int Floor { get; set; }
        public int CombatLevel { get; set; }
        public int WeaponType { get; set; }
        public bool Slingshot { get; set; }
        public int MinDamage { get; set; }
        public int MaxDamage { get; set; }
        public int Speed { get; set; }
        public int Defense { get; set; }
        public float? CritChance { get; set; }
    }

    private static Dictionary<string, GuildOffer> GuildItems
    {
        get
        {
            if (guildItems != null) return guildItems;
            TextAsset source = Resources.Load<NpcCatalog>("NpcCatalog")?.adventureGuildStock;
            return guildItems = (source != null ? JsonConvert.DeserializeObject<GuildStock>(source.text)?.Offers : null)
                ?.ToDictionary(offer => offer.ItemId, StringComparer.Ordinal) ?? new();
        }
    }

    internal static ItemData ResolveMerchantItem(string id)
    {
        if (id == null) return null;
        if (id.StartsWith("tool_", StringComparison.Ordinal))
        {
            if (merchantItems.TryGetValue(id, out ItemData tool)) return tool;
            var animalTool = OnlineGameState.ResolveAnimalTool(id);
            if (animalTool != null) return merchantItems[id] = animalTool;
            return ResolveUpgradedTool(id);
        }
        if (merchantItems.TryGetValue(id, out ItemData cached)) return cached;
        var counterItem = ResolveCounterItem(id);
        if (counterItem != null) return merchantItems[id] = counterItem;
        bool guildItem = GuildItems.TryGetValue(id, out GuildOffer offer);
        if (!guildItem && !id.StartsWith("furniture_", StringComparison.Ordinal)) return null;
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (catalog == null) return null;
        ItemData item;
        if (guildItem)
        {
            int tile = int.Parse(id.Substring(id.IndexOf('_') + 1));
            bool weaponItem = id.StartsWith("weapon_", StringComparison.Ordinal);
            if (!weaponItem && springObjectsTexture == null)
                springObjectsTexture = Resources.Load<Texture2D>("Maps/springobjects");
            var icon = ItemData.CreateAtlasSprite(weaponItem ? catalog.weaponsAtlas : springObjectsTexture, tile);
            if (icon == null) return null;
            if (weaponItem && !offer.Slingshot)
            {
                var weapon = MeleeWeaponData.Create(id, offer.Name, icon, (MeleeWeaponType)offer.WeaponType);
                if (offer.MinDamage > 0 && offer.MaxDamage >= offer.MinDamage)
                {
                    weapon.minDamage = offer.MinDamage;
                    weapon.maxDamage = offer.MaxDamage;
                    weapon.speed = offer.Speed;
                    weapon.addedDefense = offer.Defense;
                    weapon.critChance = offer.CritChance ?? weapon.critChance;
                }
                item = weapon;
            }
            else item = ScriptableObject.CreateInstance<ItemData>();
            item.displayName = offer.Name;
            item.icon = item.heldSprite = icon;
        }
        else
        {
            if (catalog.furnitureDefinitions == null || catalog.furnitureAtlas == null) return null;
            furnitureDefinitions ??= JsonConvert.DeserializeObject<Dictionary<string, string>>(catalog.furnitureDefinitions.text);
            string key = id.Substring("furniture_".Length);
            if (!int.TryParse(key, out int tile) || !furnitureDefinitions.TryGetValue(key, out string data)) return null;
            string[] fields = data.Split('/');
            var texture = catalog.furnitureAtlas;
            int columns = texture.width / 16;
            int width = 1, height = 2;
            string[] size = fields[2].Split(' ');
            if (size.Length == 2) { int.TryParse(size[0], out width); int.TryParse(size[1], out height); }
            if (width <= 0 || height <= 0)
                (width, height) = fields[1] switch
                {
                    "bench" or "couch" => (3, 2), "armchair" or "stool" or "dresser" => (2, 2),
                    "long table" => (5, 3), "bookcase" or "table" => (2, 3),
                    "rug" => (3, 2), "fireplace" => (2, 5), "lamp" => (1, 3), _ => (1, 2)
                };
            var source = new Rect(tile % columns * 16, texture.height - (tile / columns + height) * 16,
                width * 16, height * 16);
            if (source.yMin < 0 || source.xMax > texture.width) return null;
            item = ScriptableObject.CreateInstance<ItemData>();
            item.displayName = fields[0];
            item.icon = AtlasSprites.Get(texture, source, Vector2.one * 0.5f, 16f);
        }
        item.itemId = id;
        item.maxStackSize = id == "object_441" ? 999 : 1;
        merchantItems[id] = item;
        return item;
    }

    private static ItemData ResolveUpgradedTool(string id)
    {
        if (merchantItems.TryGetValue(id, out ItemData cached)) return cached;
        string code = id.Substring("tool_".Length);
        for (int tier = 0; tier < ToolUpgradeTiers.Length; tier++)
        {
            string prefix = ToolUpgradeTiers[tier];
            if (!code.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string baseId = code.Substring(prefix.Length) switch
            {
                "Axe" => "axe", "Pickaxe" => "pickaxe", "Hoe" => "hoe", "WateringCan" => "watering_can", _ => null
            };
            if (baseId == null || FindItem(baseId) is not ToolData tool) return null;
            var upgraded = Instantiate(tool);
            upgraded.itemId = id;
            upgraded.displayName = prefix + " " + (baseId == "watering_can" ? "Watering Can" : code.Substring(prefix.Length));
            upgraded.upgradeLevel = tier + 1;
            int baseTile = tool.kind switch
            {
                PlayerItemKind.Hoe => 21, PlayerItemKind.Pickaxe => 105,
                PlayerItemKind.Axe => 189, _ => 273
            };
            int level = upgraded.upgradeLevel;
            int iconTile = baseTile + level * 7 + (level > 2 ? 21 : 0) +
                (tool.kind == PlayerItemKind.WateringCan ? 23 : 26);
            var toolsAtlas = Resources.Load<NpcCatalog>("NpcCatalog")?.toolsAtlas;
            upgraded.icon = ItemData.CreateAtlasSprite(toolsAtlas, iconTile) ?? tool.GetIcon();
            merchantItems[id] = upgraded;
            return upgraded;
        }
        return null;
    }

    private static ItemData FindItem(string id)
    {
        var special = ResolveMerchantItem(id);
        if (special != null) return special;
        if (items == null)
        {
            items = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemData item in Resources.LoadAll<ItemData>("OnlineItems"))
                if (item != null && !string.IsNullOrEmpty(item.itemId)) items[item.itemId] = item;
        }
        if (items.TryGetValue(id, out ItemData result)) return result;

        if (ItemCatalog.TryGetValue(id, out var info))
        {
            if (springObjectsTexture == null)
            {
                springObjectsTexture = Resources.Load<Texture2D>("Maps/springobjects");
#if UNITY_EDITOR
                if (springObjectsTexture == null)
                    springObjectsTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Maps/springobjects.png");
#endif
            }

            Sprite icon = null;
            if (springObjectsTexture != null && info.sheetIndex >= 0)
            {
                int cols = springObjectsTexture.width / 16;
                int col = info.sheetIndex % cols;
                int row = info.sheetIndex / cols;
                int y = springObjectsTexture.height - (row + 1) * 16;
                icon = AtlasSprites.Get(springObjectsTexture, new Rect(col * 16, y, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            }

            var newItem = ScriptableObject.CreateInstance<ItemData>();
            newItem.itemId = id;
            newItem.displayName = info.name;
            newItem.description = info.name;
            newItem.icon = icon;
            newItem.maxStackSize = 999;
            newItem.kind = id.StartsWith("seeds_") ? PlayerItemKind.Seeds : PlayerItemKind.Material;

            items[id] = newItem;
            return newItem;
        }

        return null;
    }

    private static void DrawIcon(Rect destination, Sprite sprite)
    {
        Rect rect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        float scale = Mathf.Min(destination.width / rect.width, destination.height / rect.height);
        Vector2 size = rect.size * scale;
        destination = new Rect(destination.center - size * 0.5f, size);
        GUI.DrawTextureWithTexCoords(destination, texture, new Rect(
            rect.x / texture.width, rect.y / texture.height,
            rect.width / texture.width, rect.height / texture.height));
    }

    private void Close()
    {
        if (farmTask != FarmTask.None) EndFarmTask();
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
        backpackQuestion = false;
        purchasePending = false;
        ResetBlacksmithMode(BlacksmithMode.Store);
        if (toolbarWasActive && toolbar != null) toolbar.gameObject.SetActive(true);
        toolbarWasActive = false;
        enabled = false;
    }

    private void OnDestroy()
    {
        if (farmTask != FarmTask.None) EndFarmTask();
        if (instance == this) instance = null;
        if (toolbarWasActive && toolbar != null) toolbar.gameObject.SetActive(true);
    }
}
