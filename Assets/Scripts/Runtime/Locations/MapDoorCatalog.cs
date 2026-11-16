using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class MapSurfaceRule
{
    public string map;
    public string sheet;
    public int tile;
    public string type;
}

[Serializable]
public sealed class MapSurfaceCell
{
    public string map, layer, type;
    public int x, y;
}

[Serializable]
public sealed class MapDoorLocation
{
    public string locationName;
    public List<LocationWarp> doors = new();
}

[Serializable]
public sealed class MapWaterMaskData
{
    public MapWaterMask[] masks;
}

[Serializable]
public sealed class MapWaterMask
{
    public string map;
    public int width, height;
    public string cellsBase64, invisibleBase64;
    private static MapWaterMask[] masks;
    private byte[] cells, invisibleCells;

    internal static MapWaterMask ForMap(string map)
    {
        if (masks == null)
        {
            var source = Resources.Load<TextAsset>("MapWaterMasks");
            if (source == null) return null;
            masks = JsonUtility.FromJson<MapWaterMaskData>(source.text)?.masks ?? Array.Empty<MapWaterMask>();
        }
        var result = Array.Find(masks, mask => mask != null &&
            string.Equals(mask.map, map, StringComparison.OrdinalIgnoreCase));
        return result != null && result.GetCells().Length > 0 ? result : null;
    }

    private byte[] GetCells() => cells ??= Decode(cellsBase64);

    private byte[] Decode(string encoded)
    {
        if (width <= 0 || height <= 0 || string.IsNullOrEmpty(encoded))
            return Array.Empty<byte>();

        long expectedBytes = ((long)width * height + 7) / 8;
        try
        {
            byte[] decoded = Convert.FromBase64String(encoded);
            return decoded.LongLength == expectedBytes ? decoded : Array.Empty<byte>();
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    public bool Contains(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        return IsSet(GetCells(), (long)y * width + x);
    }

    internal bool IsVisible(int x, int y)
    {
        if (!Contains(x, y)) return false;
        invisibleCells ??= Decode(invisibleBase64);
        return !IsSet(invisibleCells, (long)y * width + x);
    }

    private static bool IsSet(byte[] data, long index) =>
        index / 8 < data.LongLength && (data[(int)(index / 8)] & (1 << (int)(index % 8))) != 0;
}

public sealed class MapDoorCatalog : ScriptableObject
{
    public List<MapDoorLocation> locations = new();
    public int surfaceVersion;
    public List<MapSurfaceRule> surfaces = new();
    public List<MapSurfaceCell> surfaceCells = new();
    public Texture2D mineFrost, mineFrostDark, mineLava, mineLavaDark;
    public Texture2D waterRippleAtlas;
    public Shader waterRippleShader;
    public Material litWorldMaterial;
    public Texture2D[] seasonalWaterfalls;
    public Texture2D townFountain;
    public TextAsset mineTreasureRewards;
    private Dictionary<string, string> surfaceLookup, cellLookup;

    public string SurfaceAt(string map, Tilemap layer, Vector3 position, string sourceSheet = null)
    {
        if (layer == null) return null;
        if (surfaceLookup == null)
        {
            surfaceLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            cellLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in surfaces) surfaceLookup[$"{rule.map}|{rule.sheet}|{rule.tile}"] = rule.type;
            foreach (var cell in surfaceCells) cellLookup[$"{cell.map}|{cell.layer}|{cell.x}|{cell.y}"] = cell.type;
        }
        Vector3Int point = layer.WorldToCell(position);
        if (cellLookup.TryGetValue($"{map}|{layer.name}|{point.x}|{-point.y}", out string type)) return type;
        Sprite sprite = layer.GetSprite(point);
        if (sprite == null) return null;
        string key = $"{sourceSheet ?? sprite.texture.name}|{TileSpriteIndex.Get(sprite)}";
        if (surfaceLookup.TryGetValue(map + "|" + key, out type)) return type;
        return surfaceLookup.TryGetValue("|" + key, out type) ? type : null;
    }

    public IReadOnlyList<LocationWarp> DoorsFor(string locationName)
    {
        foreach (MapDoorLocation location in locations)
        {
            if (string.Equals(location.locationName, locationName, StringComparison.OrdinalIgnoreCase))
                return location.doors;
        }
        return Array.Empty<LocationWarp>();
    }
}
