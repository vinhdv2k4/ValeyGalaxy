using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Turns server item ids into ItemData: bundled assets from Resources/OnlineItems, then
/// preserves ("#"), quality ("@"), seeds, fish, animal tools, shop, mine and atlas objects.
/// Every id is built once and cached.
/// </summary>
internal sealed class OnlineItemCatalog
{
    private readonly Dictionary<string, ItemData> items = new(StringComparer.Ordinal);

    /// <summary>Supplies the objects and big craftables atlases for icons.</summary>
    internal FullInventoryMenuUI InventoryUi { get; set; }

    internal void LoadBundled()
    {
        foreach (var item in Resources.LoadAll<ItemData>("OnlineItems"))
            Register(item);
    }

    /// <summary>Remembers an item the client built itself (a picked resource, a caught fish).</summary>
    internal void Register(ItemData item)
    {
        if (item != null && !string.IsNullOrEmpty(item.itemId)) items[item.itemId] = item;
    }

    internal static ItemData ResolveAnimalTool(string id)
    {
        if (id is not ("tool_MilkPail" or "tool_Shears")) return null;
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = id;
        item.displayName = id == "tool_MilkPail" ? "Xô vắt sữa" : "Kéo xén lông";
        item.maxStackSize = 1;
        item.kind = PlayerItemKind.Material;
        item.icon = ItemData.CreateAtlasSprite(Resources.Load<NpcCatalog>("NpcCatalog")?.toolsAtlas,
            id == "tool_MilkPail" ? 6 : 7);
        return item;
    }

    internal ItemData Resolve(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (items.TryGetValue(id, out var item)) return item;
        if (id.IndexOf('#') > 0)
        {
            ItemData output = Resolve(id.Substring(0, id.IndexOf('#')));
            if (output == null) return null;
            item = Object.Instantiate(output);
            item.name = output.name;
            item.itemId = id;
            item.icon = output.GetIcon();
            item.displayName = ArtisanData.PreservedName(id, source => Resolve(source)?.displayName) ?? output.displayName;
            items[id] = item;
            return item;
        }
        int quality = ItemQuality.Of(id);
        if (quality > 0)
        {
            ItemData plain = Resolve(ItemQuality.BaseId(id));
            if (plain == null) return null;
            item = Object.Instantiate(plain);
            item.name = plain.name;
            item.itemId = id;
            item.icon = plain.GetIcon();
            items[id] = item;
            return item;
        }
        item = DailyEventAssets.CreateSeed(id, Resolve);
        if (id.StartsWith("fish_", StringComparison.Ordinal) &&
            int.TryParse(id.Substring(5), out int fishId))
        {
            FishData fish = FishDatabase.GetFish(fishId);
            if (fish != null && fish.id == fishId)
                item = FishDatabase.CreateFishItem(fish, 0, false);
        }
        item ??= ResolveAnimalTool(id);
        item ??= NpcShop.ResolveMerchantItem(id);
        item ??= MineTreasure.ResolveWeapon(id);
        item ??= MineRock.ResolveSavedDrop(id);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.displayName = FarmRanchAssets.ItemName(id) ?? ArtisanData.Name(id) ?? ItemNames.Get(id) ??
                id.Replace('_', ' ');
            bool big = id.StartsWith("big_craftable_", StringComparison.Ordinal);
            bool ring = id.StartsWith("ring_", StringComparison.Ordinal);
            int lightRing = WorldLightingController.LightRingId(id);
            item.maxStackSize = big || ring || lightRing != 0 ? 1 : 999;
            if (id == "object_93") item.displayName = "Torch";
            string prefix = big ? "big_craftable_" : ring ? "ring_" : "object_";
            if (id.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(id.Substring(prefix.Length), out int index))
            {
                if (ring || lightRing != 0)
                    item.displayName = ItemNames.Get(id) ?? MineMonsterInstance.GetGearName(index, false);
                Texture2D atlas = big ? InventoryUi?.CraftablesAtlas :
                    InventoryUi?.ObjectsAtlas ?? Resources.Load<Texture2D>("Maps/springobjects");
                int columns = big ? 8 : 24;
                int height = big ? 32 : 16;
                int x = index % columns * 16;
                int y = atlas == null ? -1 : atlas.height - (index / columns + 1) * height;
                if (atlas != null && x + 16 <= atlas.width && y >= 0)
                    item.icon = AtlasSprites.Get(atlas, new Rect(x, y, 16, height),
                        new Vector2(0.5f, 0.5f), 16f);
            }
        }
        items[id] = item;
        return item;
    }
}
