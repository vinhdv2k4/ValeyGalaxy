using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A breakable mine stone. Ore nodes and ordinary stones have different drops.</summary>
public sealed class MineRock : WorldResource
{
    private static readonly Dictionary<int, ItemData> DropItems = new();
    private int stoneIndex, mineLevel;
    private SpriteRenderer spriteRenderer;
    protected override Transform ShakeTarget => spriteRenderer.transform;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Pickaxe;
        var visual = new GameObject("Stone Sprite");
        visual.transform.SetParent(transform, false);
        spriteRenderer = visual.AddComponent<SpriteRenderer>();
    }

    public void Setup(Sprite sprite, int index, int level, int hits)
    {
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
        stoneIndex = index;
        mineLevel = level;
        maxHealth = health = hits;
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        // Pickaxe.DoFunction plays hammer on every hit and stoneCrack on destruction.
        // boulderBreak belongs to large boulders, not ordinary ore nodes.
        GameAudio.Play("hammer");
        GameAudio.Play("stoneCrack");
        GetComponent<Collider2D>().enabled = false;
        RollDrops(hitDirection);
        MineManager.OnRockBroken(transform.position, lastHitStats == null ? 0 : lastHitStats.LuckBonus);
        Destroy(gameObject);
    }

    private void RollDrops(Vector2 direction)
    {
        int x = Mathf.FloorToInt(transform.position.x), y = Mathf.FloorToInt(-transform.position.y);
        var random = new System.Random(x * 1000 + y + mineLevel);
        int luck = lastHitStats == null ? 0 : lastHitStats.LuckBonus;
        var state = lastHitStats == null ? null : lastHitStats.GetComponent<OnlineGameState>()?.Snapshot;
        int mining = (state?.skillLevels != null && state.skillLevels.Length > 3 ? state.skillLevels[3] : 0) +
            (lastHitStats == null ? 0 : lastHitStats.MiningBonus);
        int Quantity(int max) => random.Next(1, max + 1) + (random.NextDouble() < luck / 100d ? 1 : 0) +
            (random.NextDouble() < mining / 100d ? 1 : 0);
        int index = stoneIndex == 44 ? random.Next(1, 8) * 2 : stoneIndex;
        int gem = index switch { 2 => 72, 4 => 64, 6 => 70, 8 => 66, 10 => 68, 12 => 60, 14 => 62, _ => 0 };
        if (gem != 0) { Drop(gem, 1, direction); return; }
        switch (index)
        {
            case 668: case 751: case 849: Drop(378, Quantity(3), direction); return;
            case 290: case 850: Drop(380, Quantity(3), direction); return;
            case 764: Drop(384, Quantity(3), direction); return;
            case 765:
                Drop(386, Quantity(3), direction);
                if (random.NextDouble() < 0.04) Drop(74, 1, direction);
                return;
            case 670: case 845: case 846: case 847:
                Drop(390, Quantity(2), direction);
                if (random.NextDouble() < 0.08) Drop(382, 1, direction);
                return;
            case 75: case 76: case 77: Drop(535 + index - 75, 1, direction); return;
            case 819: Drop(749, 1, direction); return;
            case 95: Drop(909, Quantity(2), direction); return;
            case 843: case 844: Drop(848, Quantity(2), direction); return;
            case 816: case 817: Drop(881, Quantity(2), direction); return;
            case 818: Drop(330, Quantity(2), direction); return;
            case 25: Drop(719, random.Next(2, 5), direction); return;
            case 46:
                Drop(386, random.Next(1, 4), direction); Drop(384, random.Next(1, 5), direction);
                if (random.NextDouble() < 0.25) Drop(74, 1, direction);
                return;
        }
        // Ordinary rocks roll geodes, ore/coal, then Stone.
        double bonus = (FarmDayClock.Instance?.DailyLuck ?? 0f) / 2d + mining * 0.005 + luck * 0.001;
        if (random.NextDouble() < 0.022 * (1 + bonus))
            Drop(mineLevel is >= 80 and <= 120 ? 537 : mineLevel is >= 40 and < 80 ? 536 : 535, 1, direction);
        if (mineLevel > 20 && random.NextDouble() < 0.005 * (1 + bonus)) Drop(749, 1, direction);
        if (random.NextDouble() < 0.05 * (1 + bonus) * (index == 40 || index == 42 ? 1.2 : 0.8))
        {
            if (random.NextDouble() < 0.25) Drop(382, 1, direction);
            int ore = mineLevel == 77377 ? 380 : mineLevel < 40
                ? mineLevel >= 20 && random.NextDouble() < 0.1 ? 380 : 378
                : mineLevel < 80 ? mineLevel >= 60 && random.NextDouble() < 0.1 ? 384 : random.NextDouble() < 0.75 ? 380 : 378
                : random.NextDouble() < 0.75 ? 384 : random.NextDouble() < 0.75 ? 380 : 378;
            Drop(ore, 1, direction);
        }
        else if (random.NextDouble() < 0.5) Drop(390, 1, direction);
    }

    private void Drop(int objectId, int count, Vector2 direction)
    {
        ItemData item = GetDropItem(objectId);
        if (item != null) WorldItemDrop.Spawn(item, count, transform.position, direction);
    }

    internal static ItemData ResolveSavedDrop(string itemId)
    {
        int objectId = itemId switch { "coal" => 382, "gold_ore" => 384, _ => -1 };
        if (objectId < 0 && (!itemId.StartsWith("object_", StringComparison.Ordinal) ||
            !int.TryParse(itemId.AsSpan(7), out objectId))) return null;

        ItemData item = GetDropItem(objectId);
        return item != null && item.itemId == itemId ? item : null;
    }

    private static ItemData GetDropItem(int objectId)
    {
        if (DropItems.TryGetValue(objectId, out var item) && item != null) return item;

        string name = objectId switch
        {
            60 => "Emerald", 62 => "Aquamarine", 64 => "Ruby", 66 => "Amethyst",
            68 => "Topaz", 70 => "Jade", 72 => "Diamond", 74 => "Prismatic Shard",
            330 => "Clay", 378 => "Copper Ore", 380 => "Iron Ore", 382 => "Coal",
            384 => "Gold Ore", 386 => "Iridium Ore", 390 => "Stone",
            535 => "Geode", 536 => "Frozen Geode", 537 => "Magma Geode",
            719 => "Mussel", 749 => "Omni Geode", 848 => "Cinder Shard",
            881 => "Bone Fragment", 909 => "Radioactive Ore",
            _ => null
        };
        if (name == null) return null;

        string assetName = objectId switch
        {
            378 => "Item_CopperOre", 380 => "Item_IronOre", 390 => "Item_Stone", _ => null
        };
        if (assetName != null) item = Resources.Load<ItemData>("OnlineItems/" + assetName);
        if (item == null)
        {
            Sprite icon = ItemData.CreateAtlasSprite(Resources.Load<Texture2D>("Maps/springobjects"), objectId);
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = objectId switch
            {
                378 => "copper_ore", 380 => "iron_ore", 382 => "coal", 384 => "gold_ore", 390 => "stone",
                _ => "object_" + objectId
            };
            item.displayName = name;
            item.icon = icon;
        }

        // Loot remains in inventory after the spawner and its node sprites are destroyed.
        DropItems[objectId] = item;
        return item;
    }
}
