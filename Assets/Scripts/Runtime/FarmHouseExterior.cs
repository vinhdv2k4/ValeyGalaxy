using UnityEngine;

/// <summary>Draws the farmhouse exterior at Farm.GetMainFarmHouseEntry.</summary>
public sealed class FarmHouseExterior : MonoBehaviour
{
    [SerializeField] private Texture2D houseSheet;
    [SerializeField] private Vector2Int entryCell = new(64, 15);
    private Sprite houseSprite;

    public void CopyTo(GameObject farm)
    {
        if (farm == null || houseSheet == null) return;
        var exterior = farm.GetComponent<FarmHouseExterior>();
        if (exterior == null) exterior = farm.AddComponent<FarmHouseExterior>();
        exterior.houseSheet = houseSheet;
        exterior.entryCell = entryCell;
        exterior.BuildHouse();
    }

    private void Awake() => BuildHouse();

    private void BuildHouse()
    {
        if (houseSheet == null || transform.Find("FarmHouseExterior") != null) return;

        // Farm.draw uses the first 160x144 region of Buildings/houses.
        houseSprite = Sprite.Create(houseSheet,
            new Rect(0f, houseSheet.height - 144f, 160f, 144f),
            new Vector2(0.5f, 0f), 16f);

        var house = new GameObject("FarmHouseExterior");
        house.transform.SetParent(transform, false);
        // Reference draw position: entry * 64 - (384, 440) pixels.
        // At 16 PPU the 10x9 sprite ends 2.125 tiles below the entry.
        house.transform.localPosition = new Vector3(entryCell.x - 1f, -entryCell.y - 2.125f, 0f);
        var renderer = house.AddComponent<SpriteRenderer>();
        renderer.sprite = houseSprite;
        // Farm.draw sorts the house at the draw origin plus 230 original pixels.
        renderer.sortingOrder = WorldSortOrder.GetOrder(-entryCell.y + (440f - 230f) / 64f);

        // 1. Left side: Solid wood storage shed and stacked woodpile
        // Covers local X from -5.0 to 0.0, local Y from 0.0 to 8.5
        var woodpileCollider = house.AddComponent<BoxCollider2D>();
        woodpileCollider.size = new Vector2(5.0f, 8.5f);
        woodpileCollider.offset = new Vector2(-2.5f, 4.25f);

        // Leave the one-tile threshold open at Farm.GetMainFarmHouseEntry.
        AddWall(0f, 1f, 2.2f, 8.5f);
        AddWall(2f, 5f, 2.2f, 8.5f);
        AddWall(1f, 2f, 3f, 8.5f);

        // 3. Porch right railing / foundation
        // Covers local X from 2.5 to 5.0, local Y from 0.0 to 2.2
        var porchRailingCollider = house.AddComponent<BoxCollider2D>();
        porchRailingCollider.size = new Vector2(2.5f, 2.2f);
        porchRailingCollider.offset = new Vector2(3.75f, 1.1f);

        // The Farm prefab already owns this door warp and its trigger.
        void AddWall(float left, float right, float bottom, float top)
        {
            var collider = house.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(right - left, top - bottom);
            collider.offset = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (houseSprite != null) Destroy(houseSprite);
    }
}
