using UnityEngine;

/// <summary>Starter furniture for the standard farm house.</summary>
public sealed class FarmHouseFurnishings : MonoBehaviour
{
    [SerializeField] private Texture2D furnitureSheet;

    private readonly struct FurniturePlacement
    {
        public readonly int Id;
        public readonly int X;
        public readonly int Y;
        public readonly int SourceWidth;
        public readonly int SourceHeight;
        public readonly int FootprintWidth;
        public readonly int FootprintHeight;
        public readonly bool BlocksMovement;
        public readonly bool BelowCharacters;

        public FurniturePlacement(int id, int x, int y, int sourceWidth, int sourceHeight,
            int footprintWidth, int footprintHeight, bool blocksMovement = true,
            bool belowCharacters = false)
        {
            Id = id;
            X = x;
            Y = y;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            FootprintWidth = footprintWidth;
            FootprintHeight = footprintHeight;
            BlocksMovement = blocksMovement;
            BelowCharacters = belowCharacters;
        }
    }

    private static readonly FurniturePlacement[] StandardFurniture =
    {
        new(2048, 9, 8, 2, 4, 2, 3),     // Single bed
        new(1120, 5, 4, 2, 3, 2, 2),     // Oak table
        new(1364, 5, 4, 1, 1, 1, 1, false), // Bowl on the table
        new(1376, 1, 10, 1, 2, 1, 1),    // House plant
        new(0, 4, 4, 1, 2, 1, 1),        // Oak chair
        new(1466, 1, 4, 2, 3, 2, 2),    // Budget TV
        new(1614, 3, 1, 1, 2, 1, 1, false, true), // Window
        new(1618, 6, 8, 3, 2, 3, 2, false, true), // Rug
        new(1602, 5, 1, 1, 2, 1, 2, false, true), // Painting
        new(1792, 8, 4, 2, 5, 2, 1)     // Brick fireplace
    };

    // Default bed position for each house level.
    private static readonly FurniturePlacement UpgradedBed = new(2052, 21, 3, 3, 4, 3, 3);

    private static FurniturePlacement Bed(GameLocation house) =>
        house.ContentMapName == "FarmHouse1" ? UpgradedBed : StandardFurniture[0];

    internal static Vector2Int BedSpot(GameLocation house)
    {
        var bed = Bed(house);
        return new Vector2Int(bed.X + 1, bed.Y + 1);
    }

    internal static bool IsBedRow(GameLocation house, Vector2 world)
    {
        if (house?.LocationName != "FarmHouse") return false;
        var bed = Bed(house);
        var tile = NpcPathfinder.WorldToTile(world);
        return tile.y == bed.Y + 1 && tile.x >= bed.X && tile.x < bed.X + bed.FootprintWidth;
    }

    private SpriteRenderer windowRenderer;
    private Sprite windowDay, windowNight;

    private void Awake() => BuildIfNeeded();

    private void Update()
    {
        if (windowRenderer == null || windowNight == null) return;
        var clock = FarmDayClock.Instance;
        bool night = WorldLightingController.IsDarkOut(clock) || clock != null && clock.IsRainingToday;
        Sprite wanted = night ? windowNight : windowDay;
        if (windowRenderer.sprite != wanted) windowRenderer.sprite = wanted;
    }

    private void BuildIfNeeded()
    {
        if (furnitureSheet == null) furnitureSheet = Resources.Load<NpcCatalog>("NpcCatalog")?.furnitureAtlas;
        if (furnitureSheet == null || transform.Find("StarterFurniture") != null) return;
        var root = new GameObject("StarterFurniture");
        root.transform.SetParent(transform, false);
        if (GetComponent<GameLocation>()?.ContentMapName == "FarmHouse1")
        {
            CreateFurniture(root.transform, UpgradedBed);
            return;
        }
        foreach (FurniturePlacement placement in StandardFurniture)
            CreateFurniture(root.transform, placement);
    }

    private void CreateFurniture(Transform parent, FurniturePlacement placement)
    {
        const int cell = 16;
        int atlasColumns = furnitureSheet.width / cell;
        int sourceX = placement.Id % atlasColumns * cell;
        int sourceY = furnitureSheet.height -
            (placement.Id / atlasColumns + placement.SourceHeight) * cell;
        int pixelWidth = placement.SourceWidth * cell;
        int pixelHeight = placement.SourceHeight * cell;
        if (sourceY < 0 || sourceX + pixelWidth > furnitureSheet.width)
        {
            Debug.LogWarning($"[FarmHouse] Furniture {placement.Id} is outside the atlas.");
            return;
        }

        Sprite sprite = AtlasSprites.Get(furnitureSheet,
            new Rect(sourceX, sourceY, pixelWidth, pixelHeight), Vector2.zero);
        var furniture = new GameObject($"Furniture_{placement.Id}");
        furniture.transform.SetParent(parent, false);
        furniture.transform.localPosition = new Vector3(placement.X,
            -(placement.Y + placement.FootprintHeight), 0f);
        bool isBed = placement.Id is 2048 or 2052;
        if (isBed) furniture.transform.localPosition += Vector3.up;
        if (placement.Id == 1364)
            furniture.transform.localPosition += new Vector3(0.5f, 0.25f, 0f);
        var renderer = furniture.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = placement.Id == 1364
            ? WorldSortOrder.GetOrder(-6f) + 1
            : placement.BelowCharacters
            ? WorldSortOrder.LayerBuildings + 1
            : WorldSortOrder.GetOrder(-(placement.Y + placement.FootprintHeight) + 0.125f);

        if (placement.Id == 1466 || placement.Id == 1468 || placement.Id == 2326 || placement.Id == 1680)
        {
            furniture.AddComponent<InteractableTV>();
        }

        // Brick fireplace: lit and put out with the action button.
        if (placement.Id == 1792)
            furniture.AddComponent<FarmFireplace>().Setup(parent, placement.X, placement.Y,
                placement.FootprintWidth, renderer.sortingOrder);

        // The window furniture uses the next atlas rectangle (the dark window) after dark and
        // while raining.
        if (placement.Id == 1614 && sourceX + pixelWidth * 2 <= furnitureSheet.width)
        {
            windowRenderer = renderer;
            windowDay = sprite;
            windowNight = AtlasSprites.Get(furnitureSheet,
                new Rect(sourceX + pixelWidth, sourceY, pixelWidth, pixelHeight), Vector2.zero);
        }

        if (!placement.BlocksMovement) return;
        if (isBed)
        {
            // The blanket uses the next atlas rectangle.
            if (sourceX + pixelWidth * 2 <= furnitureSheet.width)
            {
                renderer.sortingOrder = WorldSortOrder.GetOrder(1f - placement.Y);
                var front = new GameObject("Bed front");
                front.transform.SetParent(furniture.transform, false);
                var cover = front.AddComponent<SpriteRenderer>();
                cover.sprite = AtlasSprites.Get(furnitureSheet,
                    new Rect(sourceX + pixelWidth, sourceY, pixelWidth, pixelHeight), Vector2.zero);
                cover.sortingOrder = WorldSortOrder.GetOrder(furniture.transform.position.y + 1f / 64f);
            }
            // BedFurniture.IntersectsForCollision leaves the middle (Sleep) row passable.
            AddBedCollider(0.5f);
            AddBedCollider(2.5f);
            return;
        }
        var collider = furniture.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(placement.FootprintWidth, placement.FootprintHeight);
        collider.offset = collider.size * 0.5f;

        void AddBedCollider(float y)
        {
            var strip = furniture.AddComponent<BoxCollider2D>();
            strip.size = new Vector2(placement.FootprintWidth, 1f);
            strip.offset = new Vector2(placement.FootprintWidth * 0.5f, y);
        }
    }
}
