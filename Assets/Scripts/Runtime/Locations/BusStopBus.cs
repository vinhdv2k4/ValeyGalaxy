using UnityEngine;

/// <summary>The parked bus and its door at the bus stop.</summary>
internal sealed class BusStopBus : MonoBehaviour
{
    // Top-left tile of the bus; the map's collision under it covers x 21-28, y 7-9.
    private const int BusTileX = 21, BusTileY = 6;

    internal static void Attach(GameLocation location)
    {
        if (location.ContentMapName != "BusStop" || location.GetComponent<BusStopBus>() != null) return;
        location.gameObject.AddComponent<BusStopBus>().Build(location);
    }

    private void Build(GameLocation location)
    {
        Texture2D atlas = StardewUiAtlas.Cursors;
        if (atlas == null) return;
        Vector3 topLeft = location.TileCenter(BusTileX, BusTileY) + new Vector3(-0.5f, 0.5f);
        // The bus sorts by its bottom row.
        int order = WorldSortOrder.GetOrder(topLeft.y - 3f);
        Add(atlas, "Parked Bus", new RectInt(288, 1247, 128, 64), topLeft, order);
        // A parked bus shows the open door.
        Add(atlas, "Bus Door", new RectInt(288, 1311, 16, 38), topLeft + new Vector3(1f, -26f / 16f), order + 1);
    }

    private void Add(Texture2D atlas, string name, RectInt source, Vector3 position, int sortingOrder)
    {
        var part = new GameObject(name);
        part.transform.SetParent(transform, false);
        part.transform.position = position;
        var sprite = AtlasSprites.FromTop(atlas, source, Vector2.up);
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }
}
