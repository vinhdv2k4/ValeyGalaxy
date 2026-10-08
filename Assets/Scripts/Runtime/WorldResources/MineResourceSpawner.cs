using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Initial stone and ore population for a mine floor.</summary>
public sealed class MineResourceSpawner : MonoBehaviour
{
    [SerializeField] private Tilemap back;
    [SerializeField] private Tilemap buildings;
    [SerializeField] private Tilemap front;
    [SerializeField] private Texture2D objectSheet;
    [SerializeField] private ItemData stoneItem;
    [SerializeField] private ItemData copperOreItem;
    [SerializeField] private ItemData ironOreItem;
    [SerializeField, Min(1)] private int mineLevel = 1;
    [SerializeField] private int seed = 1;
    [SerializeField] private Transform resourceParent;

    private readonly Dictionary<int, Sprite> sprites = new();
    private readonly HashSet<Vector3Int> entranceCells = new();
    private static readonly HashSet<int> StoneFloorTiles = new()
    {
        137, 138, 139, 140, 153, 154, 155, 166, 167, 168,
        169, 170, 171, 172, 182, 184, 185, 186, 187, 188,
        198, 199, 200, 201, 202, 203, 204, 217, 218, 219,
        233, 234, 235
    };

    private void Start() => SpawnFloor();

    [ContextMenu("Spawn Mine Resources")]
    public void SpawnFloor()
    {
        ResolveReferences();
        if (back == null || objectSheet == null) return;
        ClearResources();
        FindEntranceCells();

        var random = new System.Random(seed);
        double stoneChance = random.Next(10, 30) / 100.0;
        foreach (Vector3Int cell in back.cellBounds.allPositionsWithin)
        {
            if (!IsClearStoneFloor(cell) || random.NextDouble() > stoneChance) continue;

            int spriteIndex = ChooseStoneSprite(random);
            bool oreNode = spriteIndex == 668 || spriteIndex == 670;
            ItemData ore = spriteIndex == 670 ? ironOreItem : copperOreItem;
            var go = new GameObject(oreNode ? "Ore Node" : "Mine Stone");
            go.transform.SetParent(resourceParent, false);
            go.transform.position = back.GetCellCenterWorld(cell);
            go.AddComponent<MineRock>().Setup(GetSprite(spriteIndex), stoneItem, ore, oreNode,
                spriteIndex == 670 ? 2 : 3);
        }
    }

    private bool IsClearStoneFloor(Vector3Int cell)
    {
        Sprite floor = back.GetSprite(cell);
        if (floor == null || !StoneFloorTiles.Contains(TileSpriteIndex.Get(floor))) return false;
        if (buildings != null && buildings.HasTile(cell)) return false;
        if (front != null && front.HasTile(cell)) return false;
        return !entranceCells.Contains(cell);
    }

    private void FindEntranceCells()
    {
        entranceCells.Clear();
        if (buildings == null) return;
        foreach (Vector3Int cell in buildings.cellBounds.allPositionsWithin)
        {
            int index = TileSpriteIndex.Get(buildings.GetSprite(cell));
            if (index == 115 || index == 112)
                entranceCells.Add(cell + Vector3Int.down);
        }
    }

    private int ChooseStoneSprite(System.Random random)
    {
        // MineShaft.chooseStoneType, area 0/10 (levels 1-39).
        int index = random.Next(31, 42);
        if (mineLevel % 40 < 30 && index >= 33 && index < 38)
            index = random.NextDouble() < 0.5 ? 32 : 38;
        else if (mineLevel % 40 >= 30)
            index = random.NextDouble() < 0.5 ? 34 : 36;
        if (random.NextDouble() < 0.1)
            return random.NextDouble() < 0.5 ? 668 : 670;
        return index + index % 2;
    }

    private Sprite GetSprite(int index)
    {
        if (sprites.TryGetValue(index, out Sprite sprite)) return sprite;
        const int size = 16;
        int columns = objectSheet.width / size;
        int x = index % columns * size;
        int y = objectSheet.height - (index / columns + 1) * size;
        sprite = Sprite.Create(objectSheet, new Rect(x, y, size, size),
            new Vector2(0.5f, 0.5f), size);
        sprites.Add(index, sprite);
        return sprite;
    }

    private void ResolveReferences()
    {
        if (back == null) back = transform.Find("Back")?.GetComponent<Tilemap>();
        if (buildings == null) buildings = transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (front == null) front = transform.Find("Front")?.GetComponent<Tilemap>();
        if (resourceParent != null) return;
        Transform existing = transform.Find("MineResources");
        if (existing != null) resourceParent = existing;
        else
        {
            var root = new GameObject("MineResources");
            root.transform.SetParent(transform, false);
            resourceParent = root.transform;
        }
    }

    private void ClearResources()
    {
        for (int i = resourceParent.childCount - 1; i >= 0; i--)
        {
            var child = resourceParent.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in sprites.Values) if (sprite != null) Destroy(sprite);
        sprites.Clear();
    }
}
