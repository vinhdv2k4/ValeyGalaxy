using UnityEngine;

/// <summary>Draws the farmhouse exterior at Farm.GetMainFarmHouseEntry.</summary>
public sealed class FarmHouseExterior : MonoBehaviour
{
    [SerializeField] private Texture2D houseSheet;
    [SerializeField] private Vector2Int entryCell = new(64, 15);
    private Sprite houseSprite;

    private void Awake()
    {
        if (houseSheet == null || transform.Find("FarmHouseExterior") != null) return;

        // Farm.draw uses the first 160x144 region of Buildings/houses.
        houseSprite = Sprite.Create(houseSheet,
            new Rect(0f, houseSheet.height - 144f, 160f, 144f),
            new Vector2(0.5f, 0f), 16f);

        var house = new GameObject("FarmHouseExterior");
        house.transform.SetParent(transform, false);
        // Decompiled draw position: entry * 64 - (384, 440) pixels.
        // At 16 PPU the 10x9 sprite ends 2.125 tiles below the entry.
        house.transform.localPosition = new Vector3(entryCell.x - 1f, -entryCell.y - 2.125f, 0f);
        var renderer = house.AddComponent<SpriteRenderer>();
        renderer.sprite = houseSprite;
        renderer.sortingOrder = 21;

        var collider = house.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(8f, 2f);
        collider.offset = new Vector2(0f, 1f);
    }

    private void OnDestroy()
    {
        if (houseSprite != null) Destroy(houseSprite);
    }
}
