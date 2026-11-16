using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Loads the farm's initial objects from the Paths layer based on
/// Map object loading: each marker has a fixed meaning.
/// </summary>
public sealed partial class FarmResourceSpawner : MonoBehaviour
{
    [Header("Map References")]
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap weedTilemap;

    [Header("Item Drop References")]
    [SerializeField] private ItemData woodItem;
    [SerializeField] private ItemData stoneItem;
    [SerializeField] private ItemData fiberItem;
    [SerializeField] private ItemData mixedSeedItem;

    [Header("Tree Sheets")]
    [SerializeField] private Texture2D mapleSheet;
    [SerializeField] private Texture2D oakSheet;
    [SerializeField] private Texture2D pineSheet;

    [Header("Debris Sprites")]
    [SerializeField] private Texture2D objectSheet;
    [SerializeField] private Texture2D grassSheet;
    [SerializeField] private Texture2D bushSheet;

    [Header("Spawn Settings")]
    [SerializeField] private Transform resourceParent;
    [SerializeField] private Vector2 playerSpawn = new Vector2(64f, -18f);
    [SerializeField] private float playerClearRadius = 2.0f;
    [SerializeField] private int randomSeed = 1337;
    private readonly Dictionary<int, Sprite> createdSprites = new();
    private readonly Dictionary<string, Sprite> terrainSprites = new();
    private ItemData hardwoodItem;

    public static FarmResourceSpawner Instance { get; private set; }
    public Texture2D ObjectSheet => objectSheet;
    public ItemData StoneItem => stoneItem;
    public ItemData WoodItem => woodItem;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        InitializeReferences();
    }

    private void Start()
    {
        SpawnAllResources();
    }

    public void InitializeReferences()
    {
        if (obstacleTilemap == null || weedTilemap == null)
        {
            var farm = GameObject.Find("Map_Farm_FourCorners");
            if (farm != null)
            {
                if (obstacleTilemap == null)
                {
                    var obs = farm.transform.Find("Paths_Obstacles");
                    if (obs != null) obstacleTilemap = obs.GetComponent<Tilemap>();
                }
                if (weedTilemap == null)
                {
                    var w = farm.transform.Find("Paths_Weeds");
                    if (w != null) weedTilemap = w.GetComponent<Tilemap>();
                }
            }
        }

        // Paths are metadata, not physical tiles. The spawned objects own collision.
        if (obstacleTilemap != null)
        {
            var rend = obstacleTilemap.GetComponent<TilemapRenderer>();
            if (rend != null) rend.enabled = false;
            var collider = obstacleTilemap.GetComponent<TilemapCollider2D>();
            if (collider != null) collider.enabled = false;
            var composite = obstacleTilemap.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.enabled = false;
        }
        if (weedTilemap != null)
        {
            var rend = weedTilemap.GetComponent<TilemapRenderer>();
            if (rend != null) rend.enabled = false;
        }

        if (resourceParent == null)
        {
            var existing = GameObject.Find("WorldResources");
            if (existing != null) resourceParent = existing.transform;
            else
            {
                var parentObj = new GameObject("WorldResources");
                resourceParent = parentObj.transform;
            }
        }

        if (objectSheet != null)
        {
            if (fiberItem != null && fiberItem.icon == null) fiberItem.icon = CreateObjectSprite(771);
            if (mixedSeedItem != null && mixedSeedItem.icon == null) mixedSeedItem.icon = CreateObjectSprite(770);
        }
    }

    [ContextMenu("Spawn All Resources")]
    public void SpawnAllResources()
    {
        InitializeReferences();
        ClearExistingResources();
        var location = GetComponent<GameLocation>();
        var random = new System.Random(randomSeed);
        var clumpCells = new HashSet<Vector3Int>();
        ReserveClumpCells(obstacleTilemap, clumpCells);
        ReserveClumpCells(weedTilemap, clumpCells);
        SpawnMarkers(location, obstacleTilemap, random, clumpCells, true);
        SpawnMarkers(location, weedTilemap, random, clumpCells, true);
        RestoreVegetation(location, FindFirstObjectByType<OnlineGameState>()?.Snapshot);
    }

    /// <summary>
    /// Spawns interactive trees, weeds, rocks, and debris for any GameLocation based on its Paths layers,
    /// using the Paths layer marker rules.
    /// </summary>
    public void SpawnResourcesForLocation(GameLocation location)
    {
        if (location == null) return;
        InitializeReferences();

        // Check if already spawned
        Transform existing = location.transform.Find("LocationResources");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            RestoreVegetation(location, FindFirstObjectByType<OnlineGameState>()?.Snapshot);
            return;
        }

        Tilemap obs = location.transform.Find("Paths_Obstacles")?.GetComponent<Tilemap>();
        Tilemap weeds = location.transform.Find("Paths_Weeds")?.GetComponent<Tilemap>();
        Tilemap paths = location.transform.Find("Paths")?.GetComponent<Tilemap>();
        if (obs == null && weeds == null && paths == null) return;

        DisablePathTilemap(obs);
        DisablePathTilemap(weeds);
        DisablePathTilemap(paths);

        var root = new GameObject("LocationResources");
        root.transform.SetParent(location.transform, false);

        Transform prevParent = resourceParent;
        resourceParent = root.transform;

        var random = new System.Random(randomSeed);
        var clumpCells = new HashSet<Vector3Int>();
        ReserveClumpCells(obs, clumpCells);
        ReserveClumpCells(weeds, clumpCells);
        ReserveClumpCells(paths, clumpCells);

        bool isFarm = location.LocationName.Equals("Farm", System.StringComparison.OrdinalIgnoreCase);
        SpawnMarkers(location, obs, random, clumpCells, isFarm);
        SpawnMarkers(location, weeds, random, clumpCells, isFarm);
        SpawnMarkers(location, paths, random, clumpCells, isFarm);

        resourceParent = prevParent;
        RestoreVegetation(location, FindFirstObjectByType<OnlineGameState>()?.Snapshot);
    }

    private static void DisablePathTilemap(Tilemap tm)
    {
        if (tm == null) return;
        var rend = tm.GetComponent<TilemapRenderer>();
        if (rend != null) rend.enabled = false;
        var col = tm.GetComponent<TilemapCollider2D>();
        if (col != null) col.enabled = false;
        var comp = tm.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.enabled = false;
    }

    private static void ReserveClumpCells(Tilemap markers, HashSet<Vector3Int> cells)
    {
        if (markers == null) return;
        foreach (Vector3Int cell in markers.cellBounds.allPositionsWithin)
        {
            int index = TileSpriteIndex.Get(markers.GetSprite(cell));
            if (index < 19 || index > 21) continue;
            cells.Add(cell);
            cells.Add(cell + Vector3Int.right);
            cells.Add(cell + Vector3Int.down);
            cells.Add(cell + Vector3Int.right + Vector3Int.down);
        }
    }

    private void SpawnMarkers(GameLocation location, Tilemap markers, System.Random random,
        HashSet<Vector3Int> clumpCells, bool isFarm)
    {
        if (markers == null) return;
        foreach (var cell in markers.cellBounds.allPositionsWithin)
        {
            Sprite marker = markers.GetSprite(cell);
            if (marker == null) continue;
            if (isFarm && FarmRanchView.ReservesCell(cell.x, -cell.y)) continue;
            int index = TileSpriteIndex.Get(marker);
            if (clumpCells.Contains(cell) && (index < 19 || index > 21)) continue;
            Vector3 position = markers.GetCellCenterWorld(cell);
            if (isFarm && Vector2.Distance(position, playerSpawn) <= playerClearRadius) continue;

            if (RegisterVegetation(location, position, index, random)) continue;

            switch (index)
            {
                case 16:
                case 17: SpawnRock(position, random); break;
                case 18: SpawnTwig(position, random); break;
                case 19:
                case 20:
                case 21: SpawnClump(position, index); break;
                case 24: SpawnBush(position, 2); break;
                case 25: SpawnBush(position, 1); break;
                case 26: SpawnBush(position, 0); break;
                case 33: SpawnBush(position, 4); break;
            }
        }
    }

    private FarmWeed SpawnWeed(Transform parent, Vector3 position, System.Random random)
    {
        if (objectSheet == null) return null;
        var weedObj = new GameObject("Weed");
        weedObj.transform.SetParent(parent, true);
        weedObj.transform.position = position;

        var weed = weedObj.AddComponent<FarmWeed>();
        int weedIndex = random.NextDouble() < 0.33 ? 784 : random.NextDouble() < 0.5 ? 675 : 674;
        var sprite = CreateObjectSprite(weedIndex);
        weed.Setup(sprite, fiberItem, mixedSeedItem);
        return weed;
    }

    private Sprite CreateObjectSprite(int index, int size = 16)
    {
        if (createdSprites.TryGetValue(index, out Sprite sprite)) return sprite;
        const int tileSize = 16;
        int columns = objectSheet.width / tileSize;
        int x = index % columns * tileSize;
        int y = objectSheet.height - (index / columns + 1) * tileSize;
        sprite = Sprite.Create(objectSheet, new Rect(x, y - (size - tileSize), size, size),
            new Vector2(0.5f, 0.5f), tileSize);
        createdSprites.Add(index, sprite);
        return sprite;
    }

    private Sprite CreateTerrainSprite(Texture2D sheet, int x, int y, int width, int height)
    {
        if (sheet == null) return null;
        string key = $"{sheet.GetInstanceID()}:{x}:{y}:{width}:{height}";
        if (terrainSprites.TryGetValue(key, out Sprite existing)) return existing;
        Sprite sprite = Sprite.Create(sheet,
            new Rect(x, sheet.height - y - height, width, height),
            new Vector2(0.5f, 0.5f), 16f);
        terrainSprites.Add(key, sprite);
        return sprite;
    }

    private FarmGrass SpawnGrass(Transform parent, Vector3 position, System.Random random)
    {
        if (grassSheet == null) return null;
        var grassObj = new GameObject("Grass");
        grassObj.transform.SetParent(parent, true);
        grassObj.transform.position = position;
        var variants = new Sprite[3];
        for (int i = 0; i < variants.Length; i++)
            variants[i] = CreateTerrainSprite(grassSheet, i * 15, 0, 15, 20);
        var grass = grassObj.AddComponent<FarmGrass>();
        grass.Setup(variants, random);
        return grass;
    }

    private void SpawnBush(Vector3 position, int size)
    {
        if (bushSheet == null) return;
        int x = 0;
        int y = size == 0 ? 224 : size == 1 ? 0 : size == 2 ? 128 : 320;
        int width = size == 0 ? 16 : size == 1 || size == 4 ? 32 : 48;
        int height = size == 0 || size == 4 ? 32 : 48;
        var bushObj = new GameObject("Bush");
        bushObj.transform.SetParent(resourceParent, true);
        bushObj.transform.position = position;
        var bush = bushObj.AddComponent<FarmBush>();
        bush.Setup(CreateTerrainSprite(bushSheet, x, y, width, height), size);
        // Berry bushes change with the season and show their berries (DailyEventPlayer.RefreshBushes).
        if (size == 1) bush.UseSeasonalSheet(bushSheet);
    }

    private void SpawnRock(Vector3 position, System.Random random)
    {
        if (objectSheet == null) return;
        var rockObj = new GameObject("Rock");
        rockObj.transform.SetParent(resourceParent, true);
        rockObj.transform.position = position;

        var rock = rockObj.AddComponent<FarmRock>();
        var sprite = CreateObjectSprite(random.NextDouble() < 0.5 ? 343 : 450);
        rock.Setup(sprite, stoneItem);
    }

    private void SpawnClump(Vector3 position, int marker)
    {
        if (objectSheet == null) return;
        if (marker != 20 && hardwoodItem == null)
        {
            hardwoodItem = ScriptableObject.CreateInstance<ItemData>();
            hardwoodItem.itemId = "object_709";
            hardwoodItem.displayName = "Hardwood";
            hardwoodItem.icon = CreateObjectSprite(709);
        }
        int spriteIndex = marker == 19 ? 602 : marker == 20 ? 672 : 600;
        var go = new GameObject("Resource Clump");
        go.transform.SetParent(resourceParent, true);
        go.transform.position = position + new Vector3(0.5f, -0.5f, 0f);
        go.AddComponent<FarmResourceClump>().Setup(marker,
            CreateObjectSprite(spriteIndex, 32), hardwoodItem, stoneItem);
    }

    private void SpawnTwig(Vector3 position, System.Random random)
    {
        if (objectSheet == null) return;
        var twigObj = new GameObject("Twig");
        twigObj.transform.SetParent(resourceParent, true);
        twigObj.transform.position = position;

        var twig = twigObj.AddComponent<FarmTwig>();
        var sprite = CreateObjectSprite(random.NextDouble() < 0.5 ? 294 : 295);
        twig.Setup(sprite, woodItem);
    }

    private FarmTree SpawnTree(Transform parent, Vector3 position, FarmTree.TreeType type,
        Texture2D sheet, VegetationStage stage)
    {
        if (sheet == null) return null;
        var treeObj = new GameObject("Tree");
        treeObj.transform.SetParent(parent, true);
        treeObj.transform.position = position;

        var tree = treeObj.AddComponent<FarmTree>();
        if (stage == VegetationStage.Sapling)
        {
            tree.SetupSapling(type, CreateTerrainSprite(sheet, 16, 128, 16, 16));
            return tree;
        }
        // Tree seed drops need species-specific assets (acorn/maple/pine).
        tree.Setup(type, stage == VegetationStage.Stump ? null : CreateTerrainSprite(sheet, 0, 0, 48, 96),
            CreateTerrainSprite(sheet, 32, 96, 16, 32), woodItem, null,
            stage == VegetationStage.Stump);
        return tree;
    }

    [ContextMenu("Clear Resources")]
    public void ClearExistingResources()
    {
        if (resourceParent == null) return;
        var location = GetComponent<GameLocation>();
        if (location != null) vegetation.Remove(location);

        var toDestroy = new List<GameObject>();
        for (int i = 0; i < resourceParent.childCount; i++)
        {
            toDestroy.Add(resourceParent.GetChild(i).gameObject);
        }

        foreach (var obj in toDestroy)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Sprite sprite in createdSprites.Values) if (sprite != null) Destroy(sprite);
        createdSprites.Clear();
        foreach (Sprite sprite in terrainSprites.Values) if (sprite != null) Destroy(sprite);
        terrainSprites.Clear();
    }
}
