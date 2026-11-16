using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Map lighting properties (Resources/MapLighting.json): ambient light, lamps, window
/// lights and the day/night tile swaps.
/// </summary>
[Serializable]
internal sealed class MapLightingData
{
#pragma warning disable CS0649 // Filled by JsonUtility.
    [Serializable]
    internal sealed class LightEntry { public int x, y, type; }

    [Serializable]
    internal sealed class TileEntry { public string layer; public int x, y, index; }

    [Serializable]
    internal sealed class MapEntry
    {
        public string map;
        public int[] ambient;
        public LightEntry[] lights;
        public LightEntry[] windowLights;
        public TileEntry[] dayTiles;
        public TileEntry[] nightTiles;
    }

    public MapEntry[] maps;
#pragma warning restore CS0649

    private static Dictionary<string, MapEntry> byMap;
    private static readonly Dictionary<(Texture2D, int), Tile> RuntimeTiles = new();

    internal static MapEntry For(string map)
    {
        if (byMap == null)
        {
            byMap = new Dictionary<string, MapEntry>(StringComparer.OrdinalIgnoreCase);
            TextAsset source = Resources.Load<TextAsset>("MapLighting");
            var data = source == null ? null : JsonUtility.FromJson<MapLightingData>(source.text);
            if (data?.maps != null)
                foreach (MapEntry entry in data.maps)
                    if (!string.IsNullOrEmpty(entry.map)) byMap[entry.map] = entry;
        }
        return map != null && byMap.TryGetValue(map, out MapEntry found) ? found : null;
    }

    /// <summary>Light radius in tiles for each light texture type.</summary>
    internal static float LightRadius(int type) => type switch
    {
        1 => 1f,     // lantern 128px
        2 => 1.5f,   // windowLight 192px
        4 => 4f,     // sconceLight 512px
        5 => 3.5f,   // greenLight (cauldron) 448px
        6 => 4.5f,   // indoorWindowLight 493x638px
        _ => 2f
    };

    /// <summary>
    /// Builds the tile for a new index from the same sheet as the tile at that cell. The
    /// converted maps only ship the tiles they use, so night variants usually have no asset.
    /// </summary>
    internal static TileBase TileFromSameSheet(TileBase reference, int index)
    {
        if (reference is not Tile tile || tile.sprite == null || index < 0) return null;
        Sprite basis = tile.sprite;
        Texture2D texture = basis.texture;
        const int size = 16;
        int columns = texture.width / size;
        if (columns == 0 || index / columns >= texture.height / size) return null;
        if (RuntimeTiles.TryGetValue((texture, index), out Tile cached) && cached != null) return cached;
        var rect = new Rect(index % columns * size, texture.height - (index / columns + 1) * size, size, size);
        var sprite = Sprite.Create(texture, rect, basis.pivot / basis.rect.size, basis.pixelsPerUnit, 0,
            SpriteMeshType.FullRect);
        var created = ScriptableObject.CreateInstance<Tile>();
        created.sprite = sprite;
        created.color = tile.color;
        created.transform = tile.transform;
        created.flags = tile.flags;
        created.colliderType = tile.colliderType;
        RuntimeTiles[(texture, index)] = created;
        return created;
    }

    /// <summary>Day/night tile pair for every NightTiles cell of a loaded map.</summary>
    internal sealed class TileSwap
    {
        internal Tilemap Layer;
        internal Vector3Int Cell;
        internal TileBase Day, Night;
    }

    internal static List<TileSwap> BuildSwaps(GameLocation location, MapEntry entry)
    {
        var swaps = new List<TileSwap>();
        if (entry?.nightTiles == null) return swaps;
        foreach (TileEntry night in entry.nightTiles)
        {
            Tilemap layer = location.transform.Find(night.layer)?.GetComponent<Tilemap>();
            if (layer == null) continue;
            var cell = new Vector3Int(night.x, -night.y, 0);
            TileBase current = layer.GetTile(cell);
            if (current == null) continue;
            // The authored map tile is the day tile (DayTiles restores the same index).
            TileBase nightTile = TileFromSameSheet(current, night.index);
            if (nightTile == null) continue;
            swaps.Add(new TileSwap { Layer = layer, Cell = cell, Day = current, Night = nightTile });
        }
        return swaps;
    }
}
