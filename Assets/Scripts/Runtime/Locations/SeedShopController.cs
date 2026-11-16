using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages Pierre's SeedShop interior counter layer depth sorting and
/// authentic backpack display on the counter above the "FOR SALE" sign.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeedShopController : MonoBehaviour
{
    private Tilemap frontTilemap;
    private Tilemap buildingsTilemap;
    private Tilemap counterTopTilemap;
    private Tilemap counterFaceTilemap;

    private SpriteRenderer backpackRenderer;
    private SpriteRenderer coverRenderer;
    private Sprite redBackpackSprite;
    private Sprite blueBackpackSprite;
    private Sprite coverSprite;

    private PlayerInventory inventory;

    private void Awake()
    {
        SetupCounterLayers();
        SetupBackpackVisual();
    }

    private void Start()
    {
        inventory = LocationManager.LocalFarmerComponent<PlayerInventory>();
        UpdateBackpackDisplay();
    }

    private void OnEnable()
    {
        if (inventory == null) inventory = LocationManager.LocalFarmerComponent<PlayerInventory>();
        UpdateBackpackDisplay();
    }

    private void Update()
    {
        if (inventory == null)
            inventory = LocationManager.LocalFarmerComponent<PlayerInventory>();

        if (redBackpackSprite == null)
            SetupBackpackVisual();

        UpdateBackpackDisplay();
    }

    private void SetupCounterLayers()
    {
        Transform frontTransform = transform.Find("Front");
        Transform buildingsTransform = transform.Find("Buildings");

        if (frontTransform == null || buildingsTransform == null) return;

        frontTilemap = frontTransform.GetComponent<Tilemap>();
        buildingsTilemap = buildingsTransform.GetComponent<Tilemap>();

        if (frontTilemap == null || buildingsTilemap == null) return;

        TilemapRenderer frontRenderer = frontTransform.GetComponent<TilemapRenderer>();
        TilemapRenderer buildingsRenderer = buildingsTransform.GetComponent<TilemapRenderer>();

        // 1. Counter Top at Y = -17 (tiles x = 3..7)
        // The counter surface sits behind the shopkeeper's torso and head.
        // WorldSortOrder.GetOrder(-17f) = 544.
        Transform topObj = transform.Find("Counter_Top");
        if (topObj == null)
        {
            var go = new GameObject("Counter_Top");
            go.transform.SetParent(transform, false);
            counterTopTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            if (frontRenderer != null)
            {
                r.sharedMaterial = frontRenderer.sharedMaterial;
                r.sortingLayerID = frontRenderer.sortingLayerID;
            }
            r.sortingOrder = WorldSortOrder.GetOrder(-17.0f); // 544
            topObj = go.transform;

            for (int x = 3; x <= 7; x++)
            {
                Vector3Int pos = new Vector3Int(x, -17, 0);
                TileBase tile = frontTilemap.GetTile(pos);
                if (tile != null)
                {
                    counterTopTilemap.SetTile(pos, tile);
                    frontTilemap.SetTile(pos, null);
                }
            }
        }
        else
        {
            counterTopTilemap = topObj.GetComponent<Tilemap>();
        }

        // 2. Counter Face at Y = -18 (tiles x = 3..8)
        // The front face of the counter renders in front of the shopkeeper's legs.
        // WorldSortOrder.GetOrder(-18f) = 576.
        Transform faceObj = transform.Find("Counter_Face");
        if (faceObj == null)
        {
            var go = new GameObject("Counter_Face");
            go.transform.SetParent(transform, false);
            counterFaceTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            if (buildingsRenderer != null)
            {
                r.sharedMaterial = buildingsRenderer.sharedMaterial;
                r.sortingLayerID = buildingsRenderer.sortingLayerID;
            }
            r.sortingOrder = WorldSortOrder.GetOrder(-18.0f); // 576
            faceObj = go.transform;

            for (int x = 3; x <= 8; x++)
            {
                Vector3Int pos = new Vector3Int(x, -18, 0);
                TileBase tile = buildingsTilemap.GetTile(pos);
                if (tile != null)
                {
                    counterFaceTilemap.SetTile(pos, tile);
                    buildingsTilemap.SetTile(pos, null);
                }
            }
        }
        else
        {
            counterFaceTilemap = faceObj.GetComponent<Tilemap>();
        }
    }

    private void SetupBackpackVisual()
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;

        // MouseCursors source rects
        // 24-slot pack (red): Rectangle(255, 1436, 12, 14)
        // 36-slot pack (blue): Rectangle(267, 1436, 12, 14)
        // Cover blank counter: Rectangle(258, 1449, 1, 1)
        redBackpackSprite = Sprite.Create(cursors,
            new Rect(255, cursors.height - 1436 - 14, 12, 14),
            new Vector2(0f, 1f), 16f);
        redBackpackSprite.name = "Backpack_24";

        blueBackpackSprite = Sprite.Create(cursors,
            new Rect(267, cursors.height - 1436 - 14, 12, 14),
            new Vector2(0f, 1f), 16f);
        blueBackpackSprite.name = "Backpack_36";

        coverSprite = Sprite.Create(cursors,
            new Rect(258, cursors.height - 1449 - 1, 1, 1),
            new Vector2(0f, 1f), 16f);
        coverSprite.name = "Counter_Cover";

        // Position: On top of the counter above the "FOR SALE" sign.
        // Source coordinates Vector2(456f, 1088f) -> top of counter at tile Y=17.
        // In Unity, the counter top surface is at Y = -15.70f (pivot top-left).
        Transform bpObj = transform.Find("Counter_Backpack");
        if (bpObj == null)
        {
            var go = new GameObject("Counter_Backpack");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(7.125f, -15.70f, 0f);
            backpackRenderer = go.AddComponent<SpriteRenderer>();
            backpackRenderer.sortingOrder = WorldSortOrder.GetOrder(-17.0f) + 1; // 545
            if (counterTopTilemap != null)
            {
                var cr = counterTopTilemap.GetComponent<TilemapRenderer>();
                if (cr != null) backpackRenderer.sharedMaterial = cr.sharedMaterial;
            }
            bpObj = go.transform;
        }
        else
        {
            backpackRenderer = bpObj.GetComponent<SpriteRenderer>();
            backpackRenderer.transform.position = new Vector3(7.125f, -15.70f, 0f);
            backpackRenderer.sortingOrder = WorldSortOrder.GetOrder(-17.0f) + 1;
        }

        Transform covObj = transform.Find("Counter_Cover");
        if (covObj == null)
        {
            var go = new GameObject("Counter_Cover");
            go.transform.SetParent(transform, false);
            // Covers "FOR SALE" sign on the counter front face:
            go.transform.position = new Vector3(7.0625f, -17.25f, 0f);
            go.transform.localScale = new Vector3(1.75f, 0.35f, 1f);
            coverRenderer = go.AddComponent<SpriteRenderer>();
            coverRenderer.sprite = coverSprite;
            coverRenderer.sortingOrder = WorldSortOrder.GetOrder(-18.0f) + 1; // 577
            if (counterFaceTilemap != null)
            {
                var cr = counterFaceTilemap.GetComponent<TilemapRenderer>();
                if (cr != null) coverRenderer.sharedMaterial = cr.sharedMaterial;
            }
            coverRenderer.enabled = false;
            covObj = go.transform;
        }
        else
        {
            coverRenderer = covObj.GetComponent<SpriteRenderer>();
            coverRenderer.transform.position = new Vector3(7.0625f, -17.25f, 0f);
            coverRenderer.transform.localScale = new Vector3(1.75f, 0.35f, 1f);
            coverRenderer.sortingOrder = WorldSortOrder.GetOrder(-18.0f) + 1;
        }
    }

    public void UpdateBackpackDisplay()
    {
        if (backpackRenderer == null) return;
        int unlocked = inventory != null ? inventory.UnlockedSlots : 12;

        if (unlocked <= 12)
        {
            backpackRenderer.enabled = true;
            backpackRenderer.sprite = redBackpackSprite;
            if (coverRenderer != null) coverRenderer.enabled = false;
        }
        else if (unlocked < 36)
        {
            backpackRenderer.enabled = true;
            backpackRenderer.sprite = blueBackpackSprite;
            if (coverRenderer != null) coverRenderer.enabled = false;
        }
        else
        {
            backpackRenderer.enabled = false;
            if (coverRenderer != null) coverRenderer.enabled = true;
        }
    }

    private void OnDestroy()
    {
        if (redBackpackSprite != null) Destroy(redBackpackSprite);
        if (blueBackpackSprite != null) Destroy(blueBackpackSprite);
        if (coverSprite != null) Destroy(coverSprite);
    }
}
