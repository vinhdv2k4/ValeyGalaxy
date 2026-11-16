using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Places stones and ore in the standard mines and quarry.
/// </summary>
public sealed class MineResourceSpawner : MonoBehaviour
{
    [SerializeField] private Tilemap back;
    [SerializeField] private Tilemap buildings;
    [SerializeField] private Tilemap front;
    [SerializeField] private Texture2D objectSheet;
    [SerializeField, Min(1)] private int mineLevel = 1;
    [SerializeField] private int seed = 1;
    [SerializeField] private Transform resourceParent;

    private readonly Dictionary<int, Sprite> sprites = new();
    private readonly Dictionary<Tile, Tile> themedTiles = new();
    private bool hasSpawned;
    private Tilemap collision;
    private GameLocation location;
    private MapDoorCatalog catalog;
    private readonly HashSet<Vector3Int> entranceCells = new();
    private readonly HashSet<Vector3Int> occupied = new();
    private readonly List<Vector3Int> brownSpots = new();
    private static readonly Vector3Int[] Neighbors = { Vector3Int.right, Vector3Int.left, Vector3Int.down, Vector3Int.up };
    private bool IsQuarry => mineLevel == 77377;
    private bool IsFrost => mineLevel is >= 40 and < 80;
    private bool IsLava => mineLevel is >= 80 and <= 120;

    public static bool IsMineOrCave(GameLocation loc)
    {
        return MineManager.IsMineFloor(loc?.LocationName);
    }

    /// <summary>
    /// Attaches or invokes MineResourceSpawner on a mine floor or cave to activate minerals and rocks.
    /// </summary>
    public static void SpawnForLocation(GameLocation location, int floor = 1, bool infestedFloor = false)
    {
        if (location == null) return;
        var spawner = location.GetComponent<MineResourceSpawner>();
        if (spawner == null)
        {
            spawner = location.gameObject.AddComponent<MineResourceSpawner>();
        }
        spawner.mineLevel = floor > 0 ? floor : 1;
        spawner.seed = unchecked(floor * 100 + (FarmDayClock.Instance?.Day ?? 1));
        if (infestedFloor && spawner.hasSpawned)
        {
            spawner.ClearResources();
            spawner.hasSpawned = false;
        }
        if (!spawner.hasSpawned) spawner.SpawnFloor(infestedFloor);
    }

    private void Start() { if (!hasSpawned) SpawnFloor(); }

    [ContextMenu("Spawn Mine Resources")]
    public void SpawnFloor() => SpawnFloor(false);

    private void SpawnFloor(bool infestedFloor)
    {
        ResolveReferences();
        if (back == null || objectSheet == null) return;
        hasSpawned = true;
        ApplyFloorTerrain();
        ClearResources();
        occupied.Clear();
        if (MineTreasure.TryGetCell(mineLevel, out Vector3Int chestCell))
        {
            occupied.Add(chestCell);
            foreach (Vector3Int direction in Neighbors) occupied.Add(chestCell + direction);
        }
        FindEntranceCells();
        if (infestedFloor) return;
        if (mineLevel % 10 == 0) return;

        var random = new System.Random(seed);
        double stoneChance = random.Next(10, 30) / 100d;
        if (IsQuarry) stoneChance *= 2;
        double dailyLuck = FarmDayClock.Instance?.DailyLuck ?? 0f;
        var skillLevels = FindFirstObjectByType<OnlineGameState>()?.Snapshot?.skillLevels;
        int mining = skillLevels != null && skillLevels.Length > 3 ? skillLevels[3] : 0;
        BoundsInt bounds = back.cellBounds;
        brownSpots.Clear();
        if (IsQuarry)
            for (int i = 0; i < bounds.size.x * bounds.size.y / 100; i++)
                brownSpots.Add(RandomCell(bounds, random));
        ItemData fiber = IsFrost ? Resources.Load<ItemData>("OnlineItems/Item_Fiber") : null;
        ItemData mixedSeeds = IsFrost ? Resources.Load<ItemData>("OnlineItems/Item_MixedSeeds") : null;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
        {
            var cell = new Vector3Int(x, y, 0);
            if (!IsClearStoneFloor(cell) || random.NextDouble() > stoneChance) continue;
            if (IsFrost && random.NextDouble() < 0.15)
            {
                Sprite sprite = GetStoneSprite(random.Next(319, 322));
                CreateResource(cell, "Mine Weed").AddComponent<FarmWeed>().Setup(sprite, fiber, mixedSeeds);
                continue;
            }
            var stone = ChooseStone(cell, random, dailyLuck, mining);
            if (stone.Index >= 0) PlaceStone(cell, stone.Index, stone.Hits);
        }

        if (!IsQuarry && mineLevel > 1 && mineLevel % 5 != 0 && random.NextDouble() < 0.95)
        {
            Vector3Int cell = RandomCell(bounds, random);
            if (IsClearStoneFloor(cell)) MineManager.TryPlaceDownLadder(buildings, cell);
        }
        if (mineLevel > 2 && mineLevel % 5 != 0 && random.NextDouble() < 0.55 + dailyLuck)
            for (int cluster = 0; cluster == 0 || random.NextDouble() < 0.25 + dailyLuck; cluster++)
            {
                Vector3Int center = RandomCell(bounds, random);
                if (!IsClearStoneFloor(center)) continue;
                var ore = IsFrost && random.NextDouble() < 0.8 ? (290, 4)
                    : IsLava && random.NextDouble() < 0.8 ? (764, 8) : (751, 3);
                if (!IsFrost && random.NextDouble() < 0.25) ore = (668, 2);
                GrowOreCluster(center, ore.Item1, ore.Item2, 0.95, random);
            }
    }

    private bool IsClearStoneFloor(Vector3Int cell)
    {
        if (occupied.Contains(cell) || entranceCells.Contains(cell) || buildings != null && buildings.HasTile(cell) ||
            front != null && front.HasTile(cell) || collision != null && collision.HasTile(cell)) return false;
        int index = TileSpriteIndex.Get(back.GetSprite(cell));
        return index >= 0 && index != 77 && catalog?.SurfaceAt(location?.ContentMapName, back,
            back.GetCellCenterWorld(cell), location?.MineSurfaceSheet) == "Stone";
    }

    private void FindEntranceCells()
    {
        entranceCells.Clear();
        if (buildings == null) return;
        foreach (Vector3Int cell in buildings.cellBounds.allPositionsWithin)
        {
            int index = TileSpriteIndex.Get(buildings.GetSprite(cell));
            if (index == 115 || index == 112) entranceCells.Add(cell + Vector3Int.down);
        }
    }

    private (int Index, int Hits) ChooseStone(Vector3Int cell, System.Random random, double dailyLuck, int mining)
    {
        if (IsQuarry)
        {
            bool adjacent = System.Array.Exists(Neighbors, direction => occupied.Contains(cell + direction));
            if (!adjacent && random.NextDouble() < 0.45) return (-1, 0);
            bool brown = false;
            foreach (Vector3Int spot in brownSpots)
            {
                int distance = (spot - cell).sqrMagnitude;
                if (distance < 16) { brown = true; break; }
                if (distance < 36) return (-1, 0);
            }
            int stone = brown ? (random.NextDouble() < 0.5 ? 32 : 38) : (random.NextDouble() < 0.5 ? 34 : 36);
            return random.NextDouble() < 0.01 ? (brown ? 751 : 290, 3) : (stone, 5);
        }

        int index;
        int hits;
        if (IsFrost) { index = random.Next(47, 54); hits = 3; }
        else if (IsLava)
        {
            index = random.NextDouble() >= 0.3 || mineLevel >= 110
                ? random.NextDouble() >= 0.3 ? (random.NextDouble() >= 0.5 ? 762 : 760) : random.Next(55, 58)
                : random.NextDouble() >= 0.5 ? 32 : 38;
            hits = 4;
        }
        else
        {
            index = random.Next(31, 42);
            if (mineLevel >= 30) index = random.NextDouble() < 0.5 ? 34 : 36;
            else if (index >= 33 && index < 38) index = random.NextDouble() < 0.5 ? 32 : 38;
            hits = 1;
        }

        if (mineLevel != 1 && mineLevel % 5 != 0 && random.NextDouble() < 0.029)
            return IsFrost ? (290, 4) : IsLava ? (764, 8) : (751, 3);

        double luckAndSkill = dailyLuck + mining * 0.005;
        double gemChance = mineLevel > 1 && mineLevel % 5 != 0 ? 0.0015 : 0;
        if (mineLevel > 50 && random.NextDouble() < 0.00025 + mineLevel / 120000d + 0.00025 * luckAndSkill)
        { index = 2; hits = 10; }
        else if (gemChance > 0 && random.NextDouble() < gemChance * (1 + luckAndSkill) + mineLevel / 24000d)
            return (ChooseGemNode(random), 5);

        if (random.NextDouble() < 0.001 * (0.5 + mining * 0.008 + dailyLuck / 2)) index = 44;
        if (mineLevel > 100 && random.NextDouble() < 0.00005 * (1 + mining * 0.008 + dailyLuck / 2)) index = 46;
        if (!IsFrost && random.NextDouble() < 0.1) return (random.NextDouble() < 0.5 ? 668 : 670, 2);
        return (index + index % 2, hits);
    }

    private int ChooseGemNode(System.Random random)
    {
        int item = random.Next(59, 70);
        item += item % 2;
        if (mineLevel < 40 && item is not (66 or 68)) item = random.NextDouble() < 0.5 ? 66 : 68;
        else if (mineLevel < 80 && item is 60 or 64)
            item = random.NextDouble() < 0.5 ? (random.NextDouble() < 0.5 ? 66 : 70) : (random.NextDouble() < 0.5 ? 68 : 62);
        return item switch { 60 => 12, 62 => 14, 64 => 4, 66 => 8, 68 => 10, 70 => 6, _ => 40 };
    }

    private void GrowOreCluster(Vector3Int cell, int index, int hits, double growth, System.Random random)
    {
        if (!IsClearStoneFloor(cell)) return;
        if (random.NextDouble() > 0.1)
            PlaceStone(cell, index == 668 ? 668 + random.Next(2) * 2 : index, hits);
        growth -= 0.3;
        if (growth <= 0) return;
        foreach (Vector3Int direction in Neighbors)
            if (random.NextDouble() < growth) GrowOreCluster(cell + direction, index, hits, growth, random);
    }

    private static Vector3Int RandomCell(BoundsInt bounds, System.Random random) =>
        new(random.Next(bounds.xMin, bounds.xMax), random.Next(bounds.yMin, bounds.yMax), 0);

    private void PlaceStone(Vector3Int cell, int index, int hits)
    {
        Sprite sprite = GetStoneSprite(index);
        if (sprite == null) return;
        CreateResource(cell, "Mine Stone " + index).AddComponent<MineRock>().Setup(sprite, index, mineLevel, hits);
    }

    private GameObject CreateResource(Vector3Int cell, string resourceName)
    {
        occupied.Add(cell);
        var go = new GameObject(resourceName);
        go.transform.SetParent(resourceParent, false);
        go.transform.position = back.GetCellCenterWorld(cell);
        return go;
    }

    private void ApplyFloorTerrain()
    {
        if (catalog == null || themedTiles.Count != 0) return;
        Texture2D texture = IsFrost ? (mineLevel >= 70 ? catalog.mineFrostDark : catalog.mineFrost)
            : IsLava ? (mineLevel is >= 110 and < 120 ? catalog.mineLavaDark : catalog.mineLava) : null;
        if (texture == null) return;

        foreach (Tilemap layer in GetComponentsInChildren<Tilemap>(true))
        {
            var tiles = new TileBase[layer.GetUsedTilesCount()];
            int count = layer.GetUsedTilesNonAlloc(tiles);
            for (int i = 0; i < count; i++)
            {
                if (tiles[i] is not Tile tile || tile.sprite == null || tile.sprite.texture == texture ||
                    tile.sprite.texture.name is not ("mine" or "mine_dark")) continue;
                if (!themedTiles.TryGetValue(tile, out Tile variant))
                {
                    Sprite sprite = ItemData.CreateAtlasSprite(texture, TileSpriteIndex.Get(tile.sprite));
                    if (sprite == null) continue;
                    variant = Instantiate(tile);
                    variant.sprite = sprite;
                    themedTiles.Add(tile, variant);
                }
                if (location != null) location.MineSurfaceSheet = tile.sprite.texture.name;
                layer.SwapTile(tile, variant);
            }
        }
    }

    private Sprite GetStoneSprite(int index)
    {
        if (sprites.TryGetValue(index, out Sprite sprite)) return sprite;
        sprite = ItemData.CreateAtlasSprite(objectSheet, index);
        sprites.Add(index, sprite);
        return sprite;
    }

    private void ResolveReferences()
    {
        if (back == null) back = transform.Find("Back")?.GetComponent<Tilemap>();
        if (buildings == null) buildings = transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (front == null) front = transform.Find("Front")?.GetComponent<Tilemap>();
        collision = transform.Find("MapCollision")?.GetComponent<Tilemap>();
        location = GetComponent<GameLocation>();
        catalog = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
        if (resourceParent == null)
        {
            Transform existing = transform.Find("MineResources");
            if (existing != null) resourceParent = existing;
            else
            {
                var root = new GameObject("MineResources");
                root.transform.SetParent(transform, false);
                resourceParent = root.transform;
            }
        }

        if (objectSheet == null) objectSheet = Resources.Load<Texture2D>("Maps/springobjects");
    }

    private void ClearResources()
    {
        if (resourceParent == null) return;
        for (int i = resourceParent.childCount - 1; i >= 0; i--)
        {
            var child = resourceParent.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void OnDestroy()
    {
        foreach (Tile tile in themedTiles.Values)
        {
            Destroy(tile.sprite);
            Destroy(tile);
        }
        foreach (Sprite sprite in sprites.Values) if (sprite != null) Destroy(sprite);
        sprites.Clear();
    }
}
