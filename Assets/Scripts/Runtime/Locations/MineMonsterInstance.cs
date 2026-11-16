using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Mine monster movement, contact damage, and loot.</summary>
public sealed class MineMonsterInstance : MonoBehaviour
{
    private readonly struct DropRule
    {
        public readonly int objectId;
        public readonly float chance;
        public readonly int minCount;
        public readonly int maxCount;

        public DropRule(int id, float probability, int min = 1, int max = 1)
        {
            objectId = id;
            chance = probability;
            minCount = min;
            maxCount = max;
        }
    }

    private static readonly Dictionary<string, DropRule[]> LootTables = new(StringComparer.Ordinal)
    {
        // Object IDs and independent drop probabilities.
        ["GreenSlime"] = new[] { new DropRule(766, .75f), new DropRule(766, .05f), new DropRule(153, .1f), new DropRule(66, .015f), new DropRule(92, .15f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["RedSlime"] = new[] { new DropRule(766, .8f), new DropRule(157, .1f), new DropRule(-4, .1f, 1, 3), new DropRule(72, .01f), new DropRule(92, .5f), new DropRule(98, .005f), new DropRule(99, .001f) },
        ["RockCrab"] = new[] { new DropRule(717, .15f), new DropRule(286, .4f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["LavaCrab"] = new[] { new DropRule(717, .25f), new DropRule(287, .4f), new DropRule(98, .005f), new DropRule(99, .001f) },
        ["Bug"] = new[] { new DropRule(684, .76f), new DropRule(157, .02f), new DropRule(114, .005f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["Grub"] = new[] { new DropRule(684, .6f), new DropRule(273, .05f), new DropRule(273, .05f), new DropRule(157, .02f), new DropRule(114, .005f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["RockGolem"] = new[] { new DropRule(390, .9f), new DropRule(80, .1f), new DropRule(382, .1f), new DropRule(380, .1f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["Bat"] = new[] { new DropRule(767, .9f), new DropRule(767, .4f), new DropRule(108, .001f), new DropRule(287, .02f), new DropRule(96, .005f), new DropRule(99, .001f) },
        ["FrostBat"] = new[] { new DropRule(767, .9f), new DropRule(767, .55f), new DropRule(108, .001f), new DropRule(287, .02f), new DropRule(97, .005f), new DropRule(99, .001f) },
        ["DustSpirit"] = new[] { new DropRule(382, .5f), new DropRule(433, .01f), new DropRule(336, .001f), new DropRule(84, .02f), new DropRule(414, .02f), new DropRule(97, .005f), new DropRule(99, .001f) },
        ["Skeleton"] = new[] { new DropRule(881, .5f), new DropRule(881, .4f), new DropRule(881, .2f), new DropRule(579, .005f) },
        ["Ghost"] = new[] { new DropRule(768, .95f), new DropRule(768, .1f), new DropRule(156, .08f), new DropRule(338, .08f), new DropRule(-6, .2f, 1, 3), new DropRule(97, .005f), new DropRule(99, .001f) },
        ["MetalHead"] = new[] { new DropRule(768, .65f), new DropRule(378, .1f), new DropRule(378, .1f), new DropRule(380, .1f), new DropRule(380, .1f), new DropRule(382, .1f), new DropRule(98, .005f), new DropRule(99, .001f) },
        ["ShadowBrute"] = new[] { new DropRule(769, .75f), new DropRule(769, .1f), new DropRule(337, .002f), new DropRule(336, .01f), new DropRule(335, .02f), new DropRule(334, .04f), new DropRule(203, .04f), new DropRule(108, .003f), new DropRule(-4, .1f, 1, 3), new DropRule(98, .005f), new DropRule(99, .001f), new DropRule(74, .0005f) },
        ["ShadowShaman"] = new[] { new DropRule(769, .75f), new DropRule(769, .2f), new DropRule(337, .002f), new DropRule(336, .01f), new DropRule(335, .02f), new DropRule(334, .04f), new DropRule(108, .003f), new DropRule(-4, .1f, 1, 3), new DropRule(98, .005f), new DropRule(99, .001f), new DropRule(74, .0005f) },
        ["SquidKid"] = new[] { new DropRule(768, .75f), new DropRule(814, .2f), new DropRule(336, .05f), new DropRule(287, .1f), new DropRule(288, .05f), new DropRule(98, .005f), new DropRule(99, .001f) },
        ["Fly"] = new[] { new DropRule(684, .9f), new DropRule(157, .02f), new DropRule(114, .005f), new DropRule(96, .005f), new DropRule(99, .001f) }
    };

    private static readonly Dictionary<int, ItemData> DropItems = new();
    private static readonly Dictionary<Texture2D, (Sprite[] Body, Sprite Face)> SlimeSheets = new();
    private static Texture2D springObjects;

    [SerializeField] private Sprite[] animationFrames = Array.Empty<Sprite>();
    [SerializeField, Min(1)] private int startingHealth = 24;
    [SerializeField, Min(0)] private int contactDamage = 5;
    [SerializeField, Min(0f)] private float movementSpeed = 0.8f;
    [SerializeField, Min(0.03f)] private float animationFrameSeconds = 0.18f;

    public int MineLevel { get; private set; }
    public string MonsterKind { get; private set; }
    public bool HasSpecialItem { get; private set; }
    public bool IsRockDisguised => MonsterKind == nameof(MineMonsterSpawner.MineMonsterKind.RockCrab) ||
                                   MonsterKind == nameof(MineMonsterSpawner.MineMonsterKind.LavaCrab);

    private static readonly Vector2 Footprint = new(0.72f, 0.72f);
    private MineMonsterSpawner floor;
    private CharacterMotor target;
    private Collider2D targetCollider;
    private NetworkPlayerController targetController;
    private bool flies;
    private float moveSpeed;
    private System.Random lootRandom;
    private bool lootDropped;
    private SpriteRenderer spriteRenderer, slimeFace;
    private Color baseColor = Color.white;
    private int health;
    private int animationFrame;
    private float nextFrameTime;
    private float hurtFlashUntil;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (GetComponent<Collider2D>() == null)
        {
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Footprint;
        }
        animationFrames ??= Array.Empty<Sprite>();
        for (int i = 0; i < animationFrames.Length; i++)
            animationFrames[i] = NpcSprites.AtWorldScale(animationFrames[i]);
        if (animationFrames != null && animationFrames.Length > 0)
            spriteRenderer.sprite = animationFrames[0];
    }

    internal void Initialize(MineMonsterSpawner spawner, int level, string kind, bool hasSpecialItem,
        CharacterMotor chaseTarget, float speed = 0f)
    {
        MineLevel = level;
        MonsterKind = kind;
        HasSpecialItem = hasSpecialItem;
        floor = spawner;
        target = chaseTarget;
        targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
        targetController = target != null ? target.GetComponent<NetworkPlayerController>() : null;
        flies = kind is "Bat" or "FrostBat" or "Ghost" or "SquidKid" or "Fly";
        moveSpeed = speed > 0f ? speed : movementSpeed;
        health = startingHealth + Mathf.Max(0, level / 20);
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        int seed = unchecked(level * 486187739 +
            Mathf.RoundToInt(transform.position.x * 100f) * 16777619 +
            Mathf.RoundToInt(transform.position.y * 100f));
        if (kind is "GreenSlime" or "RedSlime")
        {
            baseColor = SlimeColor(level, hasSpecialItem, new System.Random(seed ^ 9176));
            SetSlimeFrames();
        }
        if (animationFrames.Length > 0) spriteRenderer.sprite = animationFrames[0];
        spriteRenderer.color = baseColor;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
        nextFrameTime = Time.time + animationFrameSeconds;
        lootRandom = new System.Random(seed);
        lootDropped = false;
    }

    private static Color SlimeColor(int level, bool special, System.Random random)
    {
        // GreenSlime's mine constructor colors the same neutral body sheet by mine band.
        if (level == 77377)
        {
            byte gray = (byte)random.Next(120, 200);
            return new Color32(gray, gray, gray, 255);
        }
        if (special && level % 5 is not (0 or 1))
        {
            if (level < 40) return new Color32(143, 178, 0, 255);
            if (level < 80) return Color.black;
            return new Color32(35, 7, 35, 255);
        }
        byte channel = (byte)random.Next(200, 256);
        byte low = (byte)(channel / random.Next(2, 10));
        byte inverse = (byte)(255 - channel);
        if (level < 40)
            return new Color32(low, channel, random.NextDouble() < 0.01 ? (byte)255 : inverse, 255);
        if (level < 80)
            return new Color32(random.NextDouble() < 0.01 ? (byte)180 : low,
                random.NextDouble() < 0.1 ? (byte)255 : (byte)(255 - channel / 3), channel, 255);
        return new Color32(channel, random.NextDouble() < 0.01 ? (byte)255 : inverse, low, 255);
    }

    private void SetSlimeFrames()
    {
        if (animationFrames.Length == 0 || animationFrames[0] == null) return;
        Texture2D sheet = animationFrames[0].texture;
        if (sheet == null || sheet.width < 64 || sheet.height < 144) return;
        if (!SlimeSheets.TryGetValue(sheet, out var frames))
        {
            var body = new Sprite[4];
            for (int frame = 0; frame < body.Length; frame++)
                body[frame] = SlimeSprite(16 * frame, 0);
            frames = (body, SlimeSprite(48, 120));
            SlimeSheets.Add(sheet, frames);
        }
        animationFrames = frames.Body;
        // The original draws the face separately in white over the tinted body.
        var face = new GameObject("Slime Face");
        face.transform.SetParent(transform, false);
        slimeFace = face.AddComponent<SpriteRenderer>();
        slimeFace.sprite = frames.Face;
        slimeFace.sharedMaterial = spriteRenderer.sharedMaterial;

        Sprite SlimeSprite(int x, int y) => Sprite.Create(sheet, new Rect(x, sheet.height - y - 24, 16, 24),
            new Vector2(0.5f, 1f / 3f), 16, 0, SpriteMeshType.FullRect);
    }

    /// <summary>
    /// Drops use the project's pooled WorldItemDrop/inventory flow. This is also
    /// called automatically by TakeDamage when the monster reaches zero health.
    /// </summary>
    public void DropLoot(ItemData specialFloorReward = null)
    {
        if (lootDropped) return;
        lootDropped = true;
        if (HasSpecialItem)
        {
            ItemData reward = specialFloorReward != null ? specialFloorReward : CreateSpecialFloorReward();
            if (reward != null)
                WorldItemDrop.Spawn(reward, 1, transform.position, UnityEngine.Random.insideUnitCircle);
            else
                Debug.LogWarning($"[MineMonsterInstance] Couldn't create the rare reward for mine floor {MineLevel}.", this);
        }
        if (!LootTables.TryGetValue(MonsterKind ?? string.Empty, out DropRule[] rules)) return;
        foreach (DropRule rule in rules)
        {
            if (lootRandom.NextDouble() >= rule.chance) continue;
            // Negative monster-drop IDs map to special object IDs:
            // -4 is Wood and -6 is Fiber. They are not the positive IDs 4/6.
            int objectId = rule.objectId switch { -4 => 388, -6 => 771, _ => rule.objectId };
            ItemData item = GetDropItem(objectId);
            int count = rule.minCount == rule.maxCount
                ? rule.minCount
                : lootRandom.Next(rule.minCount, rule.maxCount + 1);
            if (item != null) WorldItemDrop.Spawn(item, count, transform.position, UnityEngine.Random.insideUnitCircle);
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackDirection)
    {
        if (!isActiveAndEnabled || health <= 0) return;
        health -= Mathf.Max(1, damage);
        hurtFlashUntil = Time.time + 0.08f;
        if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.45f, 0.45f, 1f);
        Move(knockbackDirection.normalized * 0.12f);
        GameAudio.Play("hitEnemy");
        if (health > 0) return;

        GameAudio.Play("monsterdead");
        DropLoot();
        Destroy(gameObject);
    }

    private ItemData CreateSpecialFloorReward()
    {
        string[] choices = MineLevel switch
        {
            < 20 => new[] { "weapon_16", "weapon_24", "boot_504", "boot_505", "ring_516", "ring_518" },
            < 40 => new[] { "weapon_22", "weapon_24", "boot_504", "boot_505", "ring_516", "ring_518", "weapon_15" },
            < 60 => new[] { "weapon_6", "weapon_26", "weapon_15", "boot_510", "ring_517", "ring_519", "weapon_27" },
            < 80 => new[] { "weapon_26", "weapon_27", "boot_508", "boot_510", "ring_517", "ring_519", "weapon_19" },
            < 100 => new[] { "weapon_48", "weapon_48", "boot_511", "boot_513", "weapon_18", "weapon_28", "weapon_52" },
            < 120 => new[] { "weapon_19", "weapon_50", "boot_511", "boot_513", "weapon_18", "weapon_46", "ring_887" },
            _ => new[] { "weapon_45", "weapon_50", "boot_511", "boot_513", "weapon_18", "weapon_28", "weapon_52", "object_787", "boot_878", "object_856", "ring_859", "ring_887" }
        };

        Vector3Int cell = LocationManager.Instance?.CurrentLocation?.transform.Find("Back")?
            .GetComponent<Tilemap>()?.WorldToCell(transform.position) ?? Vector3Int.zero;
        int day = FarmDayClock.Instance?.Day ?? 1;
        var rewardRandom = new System.Random(unchecked(MineLevel + day + cell.x + -cell.y * 10000));
        string itemId = choices[rewardRandom.Next(choices.Length)];
        if (itemId.StartsWith("object_", StringComparison.Ordinal) &&
            int.TryParse(itemId.Substring(7), out int objectId)) return GetDropItem(objectId);

        NpcCatalog catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (itemId.StartsWith("weapon_", StringComparison.Ordinal))
        {
            MeleeWeaponData known = MineTreasure.ResolveWeapon(itemId);
            if (known != null) return known;
            if (!int.TryParse(itemId.Substring(7), out int weaponId)) return null;
            Sprite weaponIcon = ItemData.CreateAtlasSprite(catalog?.weaponsAtlas, weaponId);
            return MeleeWeaponData.Create(itemId, $"Mine Weapon {weaponId}", weaponIcon,
                GetWeaponType(weaponId));
        }

        bool isBoot = itemId.StartsWith("boot_", StringComparison.Ordinal);
        string number = itemId.Substring(5);
        if (!int.TryParse(number, out int gearId)) return null;
        string gearName = GetGearName(gearId, isBoot);
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = itemId;
        item.displayName = gearName;
        item.description = gearName;
        item.maxStackSize = 1;
        item.kind = PlayerItemKind.Material;
        springObjects ??= Resources.Load<Texture2D>("Maps/springobjects");
        item.icon = ItemData.CreateAtlasSprite(springObjects, gearId);
        return item;
    }

    private static MeleeWeaponType GetWeaponType(int weaponId) => weaponId switch
    {
        15 or 16 or 18 or 19 or 22 or 24 or 26 or 28 or 45 or 46 or 48 or 50 or 52 => MeleeWeaponType.Sword,
        6 => MeleeWeaponType.Dagger,
        _ => MeleeWeaponType.Sword
    };

    internal static string GetGearName(int id, bool boots) => boots ? id switch
    {
        504 => "Sneakers", 505 => "Rubber Boots", 508 => "Combat Boots", 510 => "Thermal Boots",
        511 => "Dark Boots", 512 => "Firewalker Boots", 513 => "Genie Shoes",
        514 => "Space Boots", 878 => "Crystal Shoes", 853 => "Cinderclown Shoes", _ => $"Boots {id}"
    } : id switch
    {
        516 => "Small Glow Ring", 518 => "Small Magnet Ring", 517 => "Glow Ring",
        519 => "Magnet Ring", 527 => "Iridium Band", 888 => "Glowstone Ring",
        887 => "Immunity Band", 859 => "Phoenix Ring", _ => $"Ring {id}"
    };

    private static ItemData GetDropItem(int objectId)
    {
        if (DropItems.TryGetValue(objectId, out ItemData cached)) return cached;
        if (springObjects == null) springObjects = Resources.Load<Texture2D>("Maps/springobjects");

        string name = objectId switch
        {
            4 => "Parsnip", 66 => "Amethyst", 72 => "Diamond", 74 => "Prismatic Shard", 80 => "Quartz",
            84 => "Frozen Tear", 92 => "Sap", 96 => "Dwarf Scroll I", 97 => "Dwarf Scroll II",
            98 => "Dwarf Scroll III", 99 => "Dwarf Scroll IV", 108 => "Rare Disc",
            114 => "Ancient Seed", 153 => "Acorn", 156 => "Ghostfish",
            157 => "White Algae", 203 => "Lucky Lunch", 273 => "Bug Steak", 286 => "Earth Crystal",
            287 => "Cherry Bomb", 288 => "Mega Bomb", 334 => "Copper Bar", 335 => "Iron Bar",
            336 => "Gold Bar", 337 => "Iridium Bar", 338 => "Refined Quartz", 378 => "Copper Ore",
            380 => "Iron Ore", 382 => "Coal", 390 => "Stone", 414 => "Crystal Fruit",
            388 => "Wood", 433 => "Coffee", 579 => "Alamite", 684 => "Bug Meat", 766 => "Slime",
            767 => "Bat Wing", 768 => "Solar Essence", 769 => "Void Essence", 814 => "Squid Ink",
            771 => "Fiber", 881 => "Bone Fragment", _ => ItemNames.Get($"object_{objectId}") ?? $"Object {objectId}"
        };
        string itemId = objectId switch
        {
            382 => "coal", 378 => "copper_ore", 380 => "iron_ore", 390 => "stone",
            388 => "wood", 771 => "fiber",
            _ => $"object_{objectId}"
        };
        ItemData item = objectId switch
        {
            378 => Resources.Load<ItemData>("OnlineItems/Item_CopperOre"),
            380 => Resources.Load<ItemData>("OnlineItems/Item_IronOre"),
            390 => Resources.Load<ItemData>("OnlineItems/Item_Stone"),
            _ => null
        };
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = itemId;
            item.displayName = name;
            item.description = name;
            item.maxStackSize = 999;
            item.kind = PlayerItemKind.Material;
            item.icon = ItemData.CreateAtlasSprite(springObjects, objectId);
        }
        DropItems[objectId] = item;
        return item;
    }

    private void Update()
    {
        if (spriteRenderer != null)
        {
            if (Time.time >= hurtFlashUntil) spriteRenderer.color = baseColor;
            if (animationFrames != null && animationFrames.Length > 1 && Time.time >= nextFrameTime)
            {
                animationFrame = (animationFrame + 1) % animationFrames.Length;
                spriteRenderer.sprite = animationFrames[animationFrame];
                nextFrameTime = Time.time + animationFrameSeconds;
            }
            spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
            if (slimeFace != null)
            {
                slimeFace.sortingOrder = spriteRenderer.sortingOrder + 1;
                slimeFace.transform.localPosition = Vector3.up * (animationFrame % 3 == 0 ? 0f : 1f / 16f);
            }
        }

    }

    private void FixedUpdate()
    {
        if (target == null || health <= 0 || LocationManager.Instance?.IsWarping == true ||
            FarmDayClock.Instance?.IsCutscenePaused == true || FarmDayClock.Instance?.IsChangingDay == true ||
            NpcConversation.IsOpen || FullInventoryMenuUI.IsOpen || MineElevatorUI.IsOpen) return;

        Vector2 standing = target.StandingPosition;
        Vector2 position = transform.position;
        Move(Vector2.MoveTowards(position, standing, moveSpeed * Time.fixedDeltaTime) - position);

        if (contactDamage <= 0 || targetCollider == null || !targetCollider.enabled) return;
        Bounds playerBounds = targetCollider.bounds;
        Vector2 offset = transform.position - playerBounds.center;
        Vector2 contact = Footprint * 0.5f + (Vector2)playerBounds.extents;
        if (Mathf.Abs(offset.x) > contact.x || Mathf.Abs(offset.y) > contact.y) return;
        targetController?.TakeMineContactDamage(contactDamage);
    }

    private void Move(Vector2 delta)
    {
        Vector3 position = transform.position;
        if (flies) position += (Vector3)delta;
        else if (floor != null)
        {
            // Separate the axes so a blocked step can slide along the wall.
            var horizontal = position + new Vector3(delta.x, 0f);
            if (delta.x != 0f && floor.CanMonsterOccupy(horizontal, Footprint)) position = horizontal;
            var vertical = position + new Vector3(0f, delta.y);
            if (delta.y != 0f && floor.CanMonsterOccupy(vertical, Footprint)) position = vertical;
        }
        transform.position = position;
    }
}

