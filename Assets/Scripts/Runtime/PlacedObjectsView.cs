using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class OnlinePlacedObject
{
    public string id, itemId, location, output, variant;
    public int x, y, outputCount;
    public long readyAt;
    public bool open;
}

/// <summary>
/// Objects the player has put down: machines and producers, tappers on trees, sprinklers,
/// torches and lamps, fences and gates, and floors. Everything except floors and open gates
/// blocks movement. Machines show their finished product in a bubble.
/// </summary>
internal sealed class PlacedObjectsView : MonoBehaviour
{
    private sealed class Entry
    {
        public OnlinePlacedObject Data;
        public SpriteRenderer Body;
        public SpriteRenderer Bubble;
        public SpriteRenderer BubbleItem;
        public Light2D Light;
    }

    private static readonly HashSet<string> Fertilizers = new()
        { "object_368", "object_369", "object_919", "object_370", "object_371", "object_920", "object_465", "object_466", "object_918" };
    private static readonly Dictionary<string, int> Bombs = new() { ["object_286"] = 3, ["object_287"] = 5, ["object_288"] = 7 };
    private static readonly Dictionary<int, int> FenceGuide = new()
    {
        [0] = 5, [10] = 9, [100] = 10, [1000] = 3, [500] = 5, [1010] = 8, [1100] = 6, [1500] = 3, [600] = 0,
        [510] = 2, [110] = 7, [1600] = 0, [1610] = 4, [1510] = 2, [1110] = 7, [610] = 4
    };
    private static readonly Dictionary<int, int> FloorGuide = new()
    {
        [0] = 0, [6] = 1, [14] = 2, [12] = 3, [4] = 16, [7] = 17, [15] = 18, [13] = 19,
        [5] = 32, [3] = 33, [11] = 34, [9] = 35, [1] = 48, [2] = 49, [10] = 50, [8] = 51
    };
    private static readonly Color FlameLight = new(1f, 0.8f, 0.55f, 1f);

    private static OnlineGameSnapshot lastSnapshot;
    private static bool requestPending;
    private readonly List<Entry> entries = new();
    private GameLocation map;
    private Transform display;
    private string displayedState;

    internal static int MinuteOfDay()
    {
        int time = FarmDayClock.Instance?.TimeOfDay ?? 600;
        return time / 100 * 60 + time % 100;
    }

    private static long Now(OnlineGameSnapshot snapshot) => (long)snapshot.gameDay * 1440 + MinuteOfDay();

    internal static void Refresh(GameLocation location, OnlineGameSnapshot snapshot)
    {
        if (snapshot != null) lastSnapshot = snapshot;
        if (location == null || lastSnapshot == null) return;
        var view = location.GetComponent<PlacedObjectsView>();
        if (view == null) view = location.gameObject.AddComponent<PlacedObjectsView>();
        view.map = location;
        view.Redraw();
    }

    private static IEnumerable<OnlinePlacedObject> In(string location) =>
        (lastSnapshot?.placedObjects ?? Array.Empty<OnlinePlacedObject>()).Where(item => item.location == location);

    /// <summary>True when a tapper sits on the tree standing at this world position.</summary>
    internal static bool HasTapper(Vector3 treePosition)
    {
        Vector2Int tile = NpcPathfinder.WorldToTile(treePosition);
        return In("Farm").Any(item => item.x == tile.x && item.y == tile.y && ArtisanData.IsTapper(item.itemId));
    }

    // ── Drawing ──────────────────────────────────────────────────────────────────────────────

    private void Redraw()
    {
        var list = In(map.LocationName).ToList();
        string state = string.Join(";", list.Select(item => $"{item.id}{item.output}{item.readyAt}{item.open}"));
        if (displayedState == state && display != null) return;
        displayedState = state;
        entries.Clear();
        if (display != null) Destroy(display.gameObject);
        display = new GameObject("Placed Objects").transform;
        display.SetParent(map.transform, false);
        var floors = new HashSet<Vector2Int>(list.Where(item => ArtisanData.FloorIndex(item.itemId) >= 0)
            .Select(item => new Vector2Int(item.x, item.y)));
        foreach (OnlinePlacedObject item in list)
        {
            Entry entry = ArtisanData.FloorIndex(item.itemId) >= 0 ? DrawFloor(item, floors)
                : ArtisanData.Fence(item.itemId) != null ? DrawFence(item, list)
                : item.itemId.StartsWith("big_craftable_", StringComparison.Ordinal) ? DrawBigCraftable(item)
                : DrawObject(item);
            if (entry != null) entries.Add(entry);
        }
    }

    private Vector3 TileBottomLeft(int x, int y) => map.TileCenter(x, y) + new Vector3(-0.5f, -0.5f, 0f);

    private SpriteRenderer NewRenderer(OnlinePlacedObject item, Sprite sprite, Vector3 position, int order)
    {
        var go = new GameObject(item.itemId);
        go.transform.SetParent(display, false);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void AddCollider(SpriteRenderer renderer)
    {
        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.9f, 0.9f);
        collider.offset = new Vector2(0.5f, 0.5f);
    }

    private Entry DrawBigCraftable(OnlinePlacedObject item)
    {
        Texture2D craftables = Resources.Load<NpcCatalog>("NpcCatalog")?.craftablesAtlas;
        if (craftables == null || !int.TryParse(item.itemId.Substring("big_craftable_".Length), out int index)) return null;
        Vector3 position = TileBottomLeft(item.x, item.y);
        bool tapper = ArtisanData.IsTapper(item.itemId);
        var body = NewRenderer(item,
            AtlasSprites.FromTop(craftables, index % 8 * 16, index / 8 * 32, 16, 32, Vector2.zero, 16f, false),
            position, WorldSortOrder.GetOrder(position.y + 0.05f) + (tapper ? 3 : 0));
        if (!tapper) AddCollider(body);
        var entry = new Entry { Data = item, Body = body };
        AddLight(entry, new Vector3(0.5f, 1.4f, 0f));
        return entry;
    }

    private Entry DrawObject(OnlinePlacedObject item)
    {
        Texture2D objects = Resources.Load<Texture2D>("Maps/springobjects");
        if (!int.TryParse(item.itemId.Replace("object_", ""), out int index)) return null;
        Vector3 position = TileBottomLeft(item.x, item.y);
        var body = NewRenderer(item, AtlasSprites.Cell(objects, index, pivot: Vector2.zero),
            position, WorldSortOrder.GetOrder(position.y + 0.05f));
        AddCollider(body);
        var entry = new Entry { Data = item, Body = body };
        AddLight(entry, new Vector3(0.5f, 0.75f, 0f));
        return entry;
    }

    private void AddLight(Entry entry, Vector3 offset)
    {
        float radius = ArtisanData.LightRadius(entry.Data.itemId);
        if (radius <= 0f) return;
        entry.Light = WorldLightingController.CreateFurnitureLight(entry.Body.transform, "Object Light", FlameLight, radius);
        entry.Light.transform.localPosition = offset;
    }

    private Entry DrawFence(OnlinePlacedObject item, List<OnlinePlacedObject> list)
    {
        ArtisanData.FenceInfo fence = ArtisanData.Fence(item.itemId);
        Texture2D texture = FarmRanchAssets.Instance?.fences?.FirstOrDefault(t => t != null && t.name == fence.texture);
        if (texture == null) return null;
        bool Counts(int x, int y) => list.Any(other => other.x == x && other.y == y &&
            ArtisanData.Fence(other.itemId) is { } info && (info.texture == fence.texture || info.gate || fence.gate));
        int sum = (Counts(item.x + 1, item.y) ? 100 : 0) + (Counts(item.x - 1, item.y) ? 10 : 0) +
                  (Counts(item.x, item.y + 1) ? 500 : 0) + (Counts(item.x, item.y - 1) ? 1000 : 0);
        Vector3 bottomLeft = TileBottomLeft(item.x, item.y);
        Vector3 tileTop = bottomLeft + Vector3.up;
        int order = WorldSortOrder.GetOrder(bottomLeft.y + 0.5f);
        SpriteRenderer body;
        if (fence.gate && sum == 110)
            body = NewRenderer(item, AtlasSprites.FromTop(texture, item.open ? 24 : 0, 128, 24, 32, Vector2.up, 16f),
                tileTop + new Vector3(-0.25f, 1f, 0f), order);
        else if (fence.gate && sum == 1500)
        {
            body = NewRenderer(item, AtlasSprites.FromTop(texture, item.open ? 16 : 0, 160, 16, 16, Vector2.up, 16f),
                tileTop + new Vector3(0.3125f, 1.3125f, 0f), order);
            var lower = NewRenderer(item, AtlasSprites.FromTop(texture, item.open ? 16 : 0, 176, 16, 16, Vector2.up, 16f),
                tileTop + new Vector3(0.3125f, 0.3125f, 0f), order + 1);
            lower.transform.SetParent(body.transform, true);
        }
        else
        {
            int piece = fence.gate ? 17 : FenceGuide.TryGetValue(sum, out int guide) ? guide : 5;
            body = NewRenderer(item, AtlasSprites.FromTop(texture, piece * 16 % texture.width,
                piece * 16 / texture.width * 32, 16, 32, Vector2.zero, 16f), bottomLeft, order);
        }
        if (!(fence.gate && item.open))
        {
            var collider = body.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 0.9f);
            collider.offset = body.transform.InverseTransformPoint(bottomLeft + new Vector3(0.5f, 0.5f, 0f));
        }
        return new Entry { Data = item, Body = body };
    }

    private Entry DrawFloor(OnlinePlacedObject item, HashSet<Vector2Int> floors)
    {
        Texture2D texture = FarmRanchAssets.Instance?.flooring;
        if (texture == null) return null;
        int which = ArtisanData.FloorIndex(item.itemId);
        int mask = (floors.Contains(new Vector2Int(item.x + 1, item.y)) ? 2 : 0) +
                   (floors.Contains(new Vector2Int(item.x - 1, item.y)) ? 8 : 0) +
                   (floors.Contains(new Vector2Int(item.x, item.y + 1)) ? 4 : 0) +
                   (floors.Contains(new Vector2Int(item.x, item.y - 1)) ? 1 : 0);
        int piece = FloorGuide.TryGetValue(mask, out int guide) ? guide : 0;
        var body = NewRenderer(item, AtlasSprites.FromTop(texture, which % 4 * 64 + piece % 16 * 16,
            which / 4 * 64 + piece / 16 * 16, 16, 16, Vector2.zero, 16f), TileBottomLeft(item.x, item.y),
            WorldSortOrder.LayerBuildings + 1);
        return new Entry { Data = item, Body = body };
    }

    private void Update()
    {
        if (lastSnapshot == null || entries.Count == 0) return;
        long now = Now(lastSnapshot);
        float darkness = WorldLightingController.AmbientDarkness;
        foreach (Entry entry in entries)
        {
            if (entry.Body == null) continue;
            if (entry.Light != null)
                entry.Light.intensity = darkness * (0.85f + Mathf.PerlinNoise(Time.time * 3f, entry.Data.x) * 0.15f);
            if (string.IsNullOrEmpty(entry.Data.output)) continue;
            bool ready = now >= entry.Data.readyAt;
            bool machine = ArtisanData.IsMachine(entry.Data.itemId);
            float pulse = machine && !ready ? 1f + Mathf.Max(0f, Mathf.Sin(Time.time * 3f)) * 0.04f : 1f;
            entry.Body.transform.localScale = new Vector3(1f, pulse, 1f);
            if (ready && entry.Bubble == null) CreateBubble(entry);
            if (entry.Bubble == null) continue;
            entry.Bubble.enabled = ready;
            entry.BubbleItem.enabled = ready;
            entry.Bubble.transform.localPosition = new Vector3(0f, 2.1f + Mathf.Sin(Time.time * 2.5f) * 0.08f, 0f);
        }
    }

    private void CreateBubble(Entry entry)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        var online = LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>();
        if (cursors == null || online == null) return;
        var bubble = new GameObject("Ready Bubble");
        bubble.transform.SetParent(entry.Body.transform, false);
        entry.Bubble = bubble.AddComponent<SpriteRenderer>();
        entry.Bubble.sprite = AtlasSprites.FromTop(cursors, 141, 465, 20, 24, Vector2.zero, 16f);
        entry.Bubble.sortingOrder = entry.Body.sortingOrder + 20;
        var icon = new GameObject("Product");
        icon.transform.SetParent(bubble.transform, false);
        icon.transform.localPosition = new Vector3(10f / 16f, 15f / 16f, 0f);
        entry.BubbleItem = icon.AddComponent<SpriteRenderer>();
        entry.BubbleItem.sprite = online.ResolveItem(entry.Data.output)?.GetIcon();
        entry.BubbleItem.sortingOrder = entry.Bubble.sortingOrder + 1;
    }

    // ── Interaction ──────────────────────────────────────────────────────────────────────────

    private static Vector2Int TargetTile(GameLocation map, Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        Vector2 target = pointer ?? standing + (facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down);
        Vector3 local = map.transform.InverseTransformPoint(target);
        return new Vector2Int(Mathf.FloorToInt(local.x), -Mathf.FloorToInt(local.y));
    }

    private static bool InReach(GameLocation map, Vector2Int tile, Vector2 standing) =>
        ((Vector2)map.TileCenter(tile.x, tile.y) - standing).sqrMagnitude <= 1.9f * 1.9f;

    internal static bool IsPlaceable(ItemData item)
    {
        string id = item?.itemId;
        // Chests are saved separately (OnlineGameState.TryPlaceChest); staircases are used, not placed.
        if (id == null || id is ChestId or "big_craftable_71") return false;
        if (id.StartsWith("big_craftable_", StringComparison.Ordinal))
            return int.TryParse(id.Substring("big_craftable_".Length), out _);
        return id == "object_93" || ArtisanData.IsSprinkler(id) || ArtisanData.Fence(id) != null ||
               ArtisanData.FloorIndex(id) >= 0;
    }

    /// <summary>
    /// Use button: place the selected object, pick one up with a pickaxe/axe, fertilize soil,
    /// light a bomb or use a staircase. Action button: gates and machines.
    /// </summary>
    internal static bool TryHandle(Vector2? pointer, bool interaction, Vector2 standing, Vector2 facing, PlayerInventory inventory)
    {
        GameLocation map = LocationManager.Instance?.CurrentLocation;
        var online = inventory?.GetComponent<OnlineGameState>();
        if (map == null || online?.Snapshot == null) return false;
        lastSnapshot = online.Snapshot;
        Vector2Int tile = TargetTile(map, pointer, standing, facing);
        var here = In(map.LocationName).Where(item => item.x == tile.x && item.y == tile.y).ToList();
        OnlinePlacedObject placed = here.FirstOrDefault(item => ArtisanData.FloorIndex(item.itemId) < 0) ?? here.FirstOrDefault();
        InventorySlot selected = inventory.SelectedItem;
        string held = selected?.amount > 0 ? selected.itemData?.itemId : null;

        if (!interaction && held != null)
        {
            if (Bombs.TryGetValue(held, out int radius)) return LightBomb(online, map, tile, standing, held, radius);
            if (held == "big_craftable_71" && int.TryParse(map.LocationName, out _)) return UseStaircase(online, map);
            if (Fertilizers.Contains(held) && map.LocationName == "Farm" && placed == null)
                return Fertilize(online, map, tile, standing, held);
        }

        if (placed != null)
        {
            if (!InReach(map, tile, standing)) return false;
            if (!interaction)
            {
                if (selected?.kind is not (PlayerItemKind.Pickaxe or PlayerItemKind.Axe)) return false;
                Send(online.PickUpObject, placed.id, null, "hammer");
                return true;
            }
            if (ArtisanData.Fence(placed.itemId)?.gate == true)
            {
                Send(online.ToggleGate, placed.id, null, placed.open ? "doorClose" : "doorOpen");
                return true;
            }
            return UseMachine(online, placed, selected);
        }

        if (!interaction && held == ChestId) return online.TryPlaceChest(pointer, standing, facing);
        if (interaction || !IsPlaceable(selected?.itemData)) return false;
        return PlaceSelected(online, map, tile, standing, held);
    }

    private const string ChestId = "big_craftable_130";

    /// <summary>
    /// Action button on a tile nothing answered: places the held object as well, since
    /// Game1.pressActionButton also ends in Utility.tryToPlaceItem. Chests go to the farmhouse.
    /// </summary>
    internal static bool TryPlaceHeld(Vector2? pointer, Vector2 standing, Vector2 facing, PlayerInventory inventory)
    {
        GameLocation map = LocationManager.Instance?.CurrentLocation;
        var online = inventory?.GetComponent<OnlineGameState>();
        if (map == null || online?.Snapshot == null) return false;
        InventorySlot selected = inventory.SelectedItem;
        string held = selected?.amount > 0 ? selected.itemData?.itemId : null;
        if (held == null) return false;
        if (held == ChestId) return online.TryPlaceChest(pointer, standing, facing);
        if (!IsPlaceable(selected.itemData)) return false;
        lastSnapshot = online.Snapshot;
        Vector2Int tile = TargetTile(map, pointer, standing, facing);
        foreach (var item in In(map.LocationName))
            if (item.x == tile.x && item.y == tile.y) return false;
        return PlaceSelected(online, map, tile, standing, held);
    }

    private static bool UseMachine(OnlineGameState online, OnlinePlacedObject placed, InventorySlot selected)
    {
        ArtisanData.Machine machine = ArtisanData.MachineFor(placed.itemId);
        bool producer = ArtisanData.IsProducer(placed.itemId);
        if (machine == null && !producer) return false;
        long now = Now(online.Snapshot);
        if (!string.IsNullOrEmpty(placed.output))
        {
            if (now >= placed.readyAt) Send(online.CollectFromMachine, placed.id, null, "coin");
            else
            {
                long left = placed.readyAt - now;
                string remaining = left >= 1440 ? $"{left / 1440} ngày {left % 1440 / 60} giờ" : $"{left / 60} giờ {left % 60} phút";
                NpcConversation.ShowNotice($"{online.ResolveItem(placed.output)?.displayName}: còn khoảng {remaining}.");
            }
            return true;
        }
        if (machine == null) return true;
        string held = selected?.itemData?.itemId;
        if (placed.itemId is not ("big_craftable_25" or "big_craftable_20") && ArtisanData.RuleFor(machine, held) == null)
        {
            NpcConversation.ShowNotice(held == null ? "Hãy cầm nguyên liệu phù hợp rồi bỏ vào máy." :
                $"{ArtisanData.Name(placed.itemId) ?? "Máy"} không nhận {selected.itemData.displayName}.");
            return true;
        }
        Send(online.LoadMachine, placed.id, held, placed.itemId == "big_craftable_13" ? "furnace" : "Ship");
        return true;
    }

    private static bool PlaceSelected(OnlineGameState online, GameLocation map, Vector2Int tile, Vector2 standing, string itemId)
    {
        string variant = null;
        if (ArtisanData.IsTapper(itemId))
        {
            FarmTree tree = TreeAt(map, tile);
            if (tree == null || !tree.IsMature || map.LocationName != "Farm")
            {
                NpcConversation.ShowNotice("Hãy đặt máy chiết nhựa lên một cây trưởng thành.");
                return true;
            }
            variant = tree.Type switch { FarmTree.TreeType.Maple => "maple", FarmTree.TreeType.Pine => "pine", _ => "oak" };
        }
        else if (!InReach(map, tile, standing) || !CanPlace(map, tile, ArtisanData.FloorIndex(itemId) >= 0))
        {
            GameAudio.Play("cancel");
            return true;
        }
        if (requestPending) return true;
        requestPending = true;
        online.PlaceObject(itemId, map.LocationName, tile.x, tile.y, variant, error =>
        {
            requestPending = false;
            if (error != null) NpcConversation.ShowNotice(error);
            else GameAudio.Play(ArtisanData.FloorIndex(itemId) >= 0 ? "thudStep" : "woodyStep");
        });
        return true;
    }

    private static FarmTree TreeAt(GameLocation map, Vector2Int tile)
    {
        foreach (FarmTree tree in FindObjectsByType<FarmTree>(FindObjectsSortMode.None))
            if (tree.isActiveAndEnabled && NpcPathfinder.WorldToTile(tree.transform.position) == tile) return tree;
        return null;
    }

    private static bool Fertilize(OnlineGameState online, GameLocation map, Vector2Int tile, Vector2 standing, string itemId)
    {
        if (!InReach(map, tile, standing)) return false;
        CropPlot plot = FindObjectsByType<CropPlot>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.IsTilled && NpcPathfinder.WorldToTile(item.transform.position) == tile);
        if (plot == null) return false;
        if (requestPending) return true;
        requestPending = true;
        online.Fertilize(tile.x, tile.y - 1, itemId, error =>
        {
            requestPending = false;
            if (error != null) NpcConversation.ShowNotice(error);
            else GameAudio.Play("dirtyHit");
        });
        return true;
    }

    private static bool LightBomb(OnlineGameState online, GameLocation map, Vector2Int tile, Vector2 standing, string bomb, int radius)
    {
        if (!InReach(map, tile, standing)) return false;
        if (requestPending) return true;
        requestPending = true;
        Vector3 center = map.TileCenter(tile.x, tile.y);
        int index = int.Parse(bomb.Replace("object_", ""));
        online.ThrowBomb(bomb, map.LocationName, tile.x, tile.y, error =>
        {
            requestPending = false;
            if (error != null) { NpcConversation.ShowNotice(error); return; }
            BombBlast.Spawn(map, center, index, radius, LocationManager.Instance?.LocalFarmer?.transform);
        });
        return true;
    }

    private static bool UseStaircase(OnlineGameState online, GameLocation map)
    {
        if (MineManager.CurrentFloor is < 1 or >= 120) return false;
        if (requestPending) return true;
        requestPending = true;
        online.UseStaircase(map.LocationName, error =>
        {
            requestPending = false;
            if (error != null) { NpcConversation.ShowNotice(error); return; }
            GameAudio.Play("stairsdown");
            LocationManager.Instance?.WarpFarmerToMineFloor(MineManager.CurrentFloor + 1, false);
        });
        return true;
    }

    private static void Send(Action<string, string, Action<string>> call, string objectId, string itemId, string sound)
    {
        if (requestPending) return;
        requestPending = true;
        call(objectId, itemId, error =>
        {
            requestPending = false;
            if (error != null) NpcConversation.ShowNotice(error);
            else GameAudio.Play(sound);
        });
    }

    private static bool CanPlace(GameLocation map, Vector2Int tile, bool floor)
    {
        var cell = new Vector3Int(tile.x, -tile.y, 0);
        Tilemap Layer(string name) => map.transform.Find(name)?.GetComponent<Tilemap>();
        if (Layer("Back")?.HasTile(cell) != true) return false;
        if (Layer("MapCollision")?.HasTile(cell) == true || Layer("Buildings")?.HasTile(cell) == true) return false;
        if (map.LocationName == "Farm" && FarmRanchView.ReservesCell(tile.x, tile.y)) return false;
        if (In(map.LocationName).Any(item => item.x == tile.x && item.y == tile.y &&
                (floor || ArtisanData.FloorIndex(item.itemId) < 0)))
            return false;
        Transform farmer = LocationManager.Instance?.LocalFarmer?.transform;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(map.TileCenter(tile.x, tile.y), new Vector2(0.8f, 0.8f), 0f))
        {
            if (farmer != null && hit.transform.IsChildOf(farmer)) continue;
            if (hit.GetComponentInParent<CropPlot>()?.IsTilled == true) return false;
            if (hit.isTrigger || hit is TilemapCollider2D || hit is CompositeCollider2D) continue;
            return false;
        }
        return true;
    }
}
