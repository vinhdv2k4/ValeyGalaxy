using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class MapWaterTileClips
{
    public MapWaterTileClip[] clips;
}

[Serializable]
public sealed class MapWaterTileClip
{
    public string map, layer;
    public int x, y, interval;
    public bool waterfall;
    public int[] frames;
}

public sealed class MapWaterTileView : MonoBehaviour
{
    private const int TilePixels = 16;
    private static MapWaterTileClip[] clips;
    private readonly Dictionary<(Texture2D, int), Tile> tiles = new();
    private readonly List<(MapWaterTileClip clip, Tilemap layer, Vector3Int cell)> cells = new();
    private MapDoorCatalog catalog;
    private double nextFrameTime;
    private int lastSeason = -1;

    internal static void Attach(GameLocation location)
    {
        if (clips == null)
        {
            var data = Resources.Load<TextAsset>("WaterTileAnimations");
            clips = (data == null ? null : JsonUtility.FromJson<MapWaterTileClips>(data.text)?.clips)
                ?? Array.Empty<MapWaterTileClip>();
        }

        MapWaterTileView view = null;
        foreach (var clip in clips)
        {
            if (!string.Equals(clip.map, location.ContentMapName, StringComparison.OrdinalIgnoreCase)) continue;
            var layer = location.transform.Find(clip.layer)?.GetComponent<Tilemap>();
            var cell = new Vector3Int(clip.x, -clip.y, 0);
            if (layer == null || !layer.HasTile(cell)) continue;
            if (view == null) view = location.gameObject.AddComponent<MapWaterTileView>();
            view.cells.Add((clip, layer, cell));
        }

        if (view == null) return;
        view.catalog = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
        view.Update();
    }

    private void Update()
    {
        if (catalog == null) return;
        double milliseconds = Time.timeAsDouble * 1000;
        int season = Mathf.Clamp(FarmDayClock.Instance?.SeasonIndex ?? 0, 0, 3);
        if (milliseconds < nextFrameTime && season == lastSeason) return;

        Texture2D[] waterfalls = catalog.seasonalWaterfalls;
        Texture2D waterfall = waterfalls != null && waterfalls.Length == 4
            ? waterfalls[season] : null;
        nextFrameTime = double.PositiveInfinity;
        foreach (var (clip, layer, cell) in cells)
        {
            if (clip.interval == 0 && season == lastSeason) continue;
            Texture2D texture = clip.waterfall ? waterfall : catalog.townFountain;
            if (texture == null) continue;
            int frame = 0;
            if (clip.interval > 0)
            {
                long elapsedFrames = (long)(milliseconds / clip.interval);
                frame = (int)(elapsedFrames % clip.frames.Length);
                nextFrameTime = Math.Min(nextFrameTime, (elapsedFrames + 1) * (double)clip.interval);
            }
            Tile tile = GetTile(texture, clip.frames[frame]);
            if (tile != null && layer.GetTile(cell) != tile) layer.SetTile(cell, tile);
        }
        lastSeason = season;
    }

    private Tile GetTile(Texture2D texture, int index)
    {
        if (tiles.TryGetValue((texture, index), out Tile tile)) return tile;
        int columns = texture.width / TilePixels;
        int y = texture.height - (index / columns + 1) * TilePixels;
        if (y < 0) return null;
        int x = index % columns * TilePixels;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(texture, new Rect(x, y, TilePixels, TilePixels),
            new Vector2(0.5f, 0.5f), TilePixels, 0, SpriteMeshType.FullRect);
        tile.colliderType = Tile.ColliderType.None;
        tiles.Add((texture, index), tile);
        return tile;
    }

    private void OnDestroy()
    {
        foreach (Tile tile in tiles.Values)
        {
            Destroy(tile.sprite);
            Destroy(tile);
        }
    }
}
