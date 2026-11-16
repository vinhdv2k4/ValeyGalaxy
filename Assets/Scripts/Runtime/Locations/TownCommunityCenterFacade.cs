using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Town.refurbishCommunityCenter: once every room of the Community Center is restored, each Town
/// tilesheet tile above index 1200 in (47, 11)-(58, 20) moves 12 tiles along the sheet (the repaired
/// facade), and Town.drawAboveAlwaysFrontLayer adds the moving clock hands. Undone again when the
/// rooms are reset (admin testing).
/// </summary>
internal sealed class TownCommunityCenterFacade : MonoBehaviour
{
    private static readonly string[] Layers = { "Back", "Buildings", "Front", "AlwaysFront" };
    private static readonly RectInt Area = new(47, 11, 11, 9); // inclusive of Right and Bottom, like the original loop
    private const int Offset = 12;
    // Town: clockCenter (3392, 1056); Cursors hourHandSource, minuteHandSource and clockNub.
    private static readonly Vector2 ClockCenterPixels = new(3392f, 1056f);
    private static readonly string[] RoomIds = { "pantry", "crafts", "fish", "boiler", "vault", "bulletin" };

    private GameLocation location;
    private readonly List<(Tilemap Map, Vector3Int Cell, TileBase Tile)> originals = new();
    private readonly Dictionary<(Texture2D, int), Tile> tiles = new();
    private Transform hourHand, minuteHand;
    private GameObject clock;
    private bool refurbished;
    private GameObject jojaFacade;
    // Town.ccFacadePosition / ccFacadePositionBottom and Cursors jojaFacadeTop / jojaFacadeBottom.
    private static readonly Vector2 JojaTopPixels = new(3044f, 940f), JojaBottomPixels = new(3044f, 1140f);
    private float nextCheck;

    internal static void Attach(GameLocation map)
    {
        if (map == null || map.LocationName != "Town" || map.GetComponent<TownCommunityCenterFacade>() != null) return;
        map.gameObject.AddComponent<TownCommunityCenterFacade>().location = map;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            bool joja = IsJojaWarehouse();
            bool complete = !joja && IsComplete();
            if (complete && !refurbished) Refurbish();
            else if (!complete && refurbished) Restore();
            if (joja && jojaFacade == null) CreateJojaFacade();
            else if (!joja && jojaFacade != null) Destroy(jojaFacade);
        }
        if (refurbished) UpdateClock();
    }

    /// <summary>Town.ccJoja: after Morris's ceremony the center wears the Joja warehouse facade.</summary>
    private static bool IsJojaWarehouse() =>
        LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>()?.Snapshot?.milestones?.HasSeen("joja_opening") == true;

    private void CreateJojaFacade()
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        jojaFacade = new GameObject("Joja warehouse facade");
        jojaFacade.transform.SetParent(transform, false);
        // Top half is drawn above everything (drawAboveAlwaysFrontLayer); the bottom sorts at y 1280 px.
        FacadePart("Top", cursors, new RectInt(424, 1275, 174, 50), JojaTopPixels, WorldSortOrder.LayerFront + 30);
        Vector3 bottomEdge = PixelToWorld(new Vector2(0f, 1280f));
        FacadePart("Bottom", cursors, new RectInt(424, 1325, 174, 51), JojaBottomPixels, WorldSortOrder.GetOrder(bottomEdge.y));
    }

    private void FacadePart(string name, Texture2D cursors, RectInt source, Vector2 topLeftPixels, int order)
    {
        var part = new GameObject(name);
        part.transform.SetParent(jojaFacade.transform, false);
        part.transform.position = PixelToWorld(topLeftPixels);
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(cursors, source, Vector2.up);
        renderer.sortingOrder = order;
    }

    private Vector3 PixelToWorld(Vector2 pixels) =>
        location.TileCenter(0, 0) + new Vector3(pixels.x / 64f - 0.5f, 0.5f - pixels.y / 64f, 0f);

    private static bool IsComplete()
    {
        var snapshot = LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>()?.Snapshot;
        if (snapshot == null) return false;
        if (snapshot.milestones?.HasSeen("community_opening") == true) return true;
        int done = 0;
        foreach (string id in RoomIds)
        {
            bool restored = false;
            foreach (var room in snapshot.milestones?.rooms ?? Array.Empty<OnlineBundleRoom>())
                if (room != null && room.done && room.id == id) restored = true;
            if (restored) done++;
        }
        if (done == RoomIds.Length) return true;
        done = 0;
        foreach (var room in snapshot.bundles?.areas ?? Array.Empty<OnlineBundleArea>())
            if (room != null && room.complete) done++;
        return done >= RoomIds.Length;
    }

    private void Refurbish()
    {
        refurbished = true;
        originals.Clear();
        foreach (string layerName in Layers)
        {
            var tilemap = transform.Find(layerName)?.GetComponent<Tilemap>();
            if (tilemap == null) continue;
            for (int x = Area.xMin; x <= Area.xMax; x++)
                for (int y = Area.yMin; y <= Area.yMax; y++)
                {
                    var cell = new Vector3Int(x, -y, 0);
                    TileBase tile = tilemap.GetTile(cell);
                    Sprite sprite = tilemap.GetSprite(cell);
                    if (tile == null || sprite == null || TownIndex(tile, sprite) is not int index || index <= 1200) continue;
                    Tile moved = Shifted(sprite, index + Offset, tile as Tile);
                    if (moved == null) continue;
                    originals.Add((tilemap, cell, tile));
                    Matrix4x4 matrix = tilemap.GetTransformMatrix(cell);
                    Color color = tilemap.GetColor(cell);
                    tilemap.SetTile(cell, moved);
                    tilemap.SetTileFlags(cell, TileFlags.None);
                    tilemap.SetTransformMatrix(cell, matrix);
                    tilemap.SetColor(cell, color);
                }
        }
        CreateClock();
    }

    private void Restore()
    {
        refurbished = false;
        foreach (var (map, cell, tile) in originals)
            if (map != null) map.SetTile(cell, tile);
        originals.Clear();
        if (clock != null) Destroy(clock);
    }

    /// <summary>The Town tilesheet index of a cell, from "4_Town_1208" tiles or "spring_town_1208" sprites.</summary>
    private static int? TownIndex(TileBase tile, Sprite sprite)
    {
        foreach (string name in new[] { tile.name, sprite.name })
        {
            int cut = name.LastIndexOf('_');
            if (cut <= 0 || !int.TryParse(name.AsSpan(cut + 1), out int index)) continue;
            string sheet = name.Substring(0, cut);
            if (sheet.EndsWith("_Town", StringComparison.Ordinal) || sheet.EndsWith("_town", StringComparison.Ordinal))
                return index;
        }
        return null;
    }

    /// <summary>A tile showing another cell of the same sheet, cut like the original sprite.</summary>
    private Tile Shifted(Sprite source, int index, Tile template)
    {
        Texture2D texture = source.texture;
        if (tiles.TryGetValue((texture, index), out var cached)) return cached;
        Rect rect = source.textureRect;
        int size = Mathf.RoundToInt(rect.width);
        if (size <= 0) return null;
        int columns = texture.width / size;
        int sourceIndex = TownIndexFromName(source.name) ?? -1;
        if (sourceIndex < 0) return null;
        // Pixel x of column 0 and the top edge of row 0 (texture y grows upward).
        float left = rect.x - sourceIndex % columns * size;
        float top = rect.yMax + sourceIndex / columns * size;
        var target = new Rect(left + index % columns * size, top - (index / columns + 1) * size, size, size);
        if (target.x < 0 || target.y < 0 || target.xMax > texture.width || target.yMax > texture.height) return null;
        Vector2 pivot = new(source.pivot.x / rect.width, source.pivot.y / rect.height);
        var sprite = Sprite.Create(texture, target, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = "town_refurbished_" + index;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = template != null ? template.colliderType : Tile.ColliderType.None;
        tile.name = "Town_" + index;
        tiles[(texture, index)] = tile;
        return tile;
    }

    private static int? TownIndexFromName(string name)
    {
        int cut = name.LastIndexOf('_');
        return cut > 0 && int.TryParse(name.AsSpan(cut + 1), out int index) ? index : null;
    }

    private void CreateClock()
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        clock = new GameObject("Community Center clock");
        clock.transform.SetParent(transform, false);
        clock.transform.position = PixelToWorld(ClockCenterPixels);
        // Origins (2.5, 8), (2.5, 12) and (2, 2) in source pixels, measured from the top-left.
        hourHand = Hand("Hour hand", cursors, new RectInt(369, 399, 5, 9), new Vector2(2.5f / 5f, 1f - 8f / 9f), 1);
        minuteHand = Hand("Minute hand", cursors, new RectInt(363, 395, 5, 13), new Vector2(0.5f, 1f - 12f / 13f), 2);
        Hand("Clock nub", cursors, new RectInt(375, 404, 4, 4), new Vector2(0.5f, 0.5f), 3);
        UpdateClock();
    }

    private Transform Hand(string name, Texture2D cursors, RectInt source, Vector2 pivot, int order)
    {
        var hand = new GameObject(name);
        hand.transform.SetParent(clock.transform, false);
        var renderer = hand.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(cursors, source, pivot);
        renderer.sortingOrder = WorldSortOrder.LayerFront + 40 + order;
        return hand.transform;
    }

    private void UpdateClock()
    {
        var clockTime = FarmDayClock.Instance;
        if (hourHand == null || clockTime == null) return;
        int time = clockTime.TimeOfDay;
        // Town.drawAboveAlwaysFrontLayer, clockwise on screen.
        float hour = 360f * (time % 1200) / 1200f;
        float minute = 360f * (time % 1000 % 100 % 60) / 60f;
        hourHand.localRotation = Quaternion.Euler(0f, 0f, -hour);
        minuteHand.localRotation = Quaternion.Euler(0f, 0f, -minute);
    }

    private void OnDestroy()
    {
        foreach (var tile in tiles.Values)
        {
            if (tile == null) continue;
            if (tile.sprite != null) Destroy(tile.sprite);
            Destroy(tile);
        }
    }
}
