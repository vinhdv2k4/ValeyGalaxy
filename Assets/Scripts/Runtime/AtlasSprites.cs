using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared cache of sprites cut from the game's atlases. Every caller asking for the same region
/// gets the same Sprite instance, so nothing is re-cut per object or per frame. Cached sprites
/// live as long as their atlas and must never be destroyed by the caller.
/// </summary>
internal static class AtlasSprites
{
    private readonly struct Key : System.IEquatable<Key>
    {
        private readonly int texture, x, y, width, height, pivotX, pivotY, pixelsPerUnit;
        private readonly bool fullRect;

        public Key(Texture2D atlas, RectInt rect, Vector2 pivot, float ppu, bool full)
        {
            texture = atlas.GetInstanceID();
            x = rect.x; y = rect.y; width = rect.width; height = rect.height;
            pivotX = Mathf.RoundToInt(pivot.x * 10000f);
            pivotY = Mathf.RoundToInt(pivot.y * 10000f);
            pixelsPerUnit = Mathf.RoundToInt(ppu * 100f);
            fullRect = full;
        }

        public bool Equals(Key other) => texture == other.texture && x == other.x && y == other.y &&
            width == other.width && height == other.height && pivotX == other.pivotX &&
            pivotY == other.pivotY && pixelsPerUnit == other.pixelsPerUnit && fullRect == other.fullRect;

        public override bool Equals(object obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = texture;
                hash = hash * 31 + x; hash = hash * 31 + y; hash = hash * 31 + width; hash = hash * 31 + height;
                hash = hash * 31 + pivotX; hash = hash * 31 + pivotY; hash = hash * 31 + pixelsPerUnit;
                return hash * 2 + (fullRect ? 1 : 0);
            }
        }
    }

    private static readonly Dictionary<Key, Sprite> Cache = new();

    /// <summary>Region given in Unity texture space (origin bottom-left).</summary>
    internal static Sprite Get(Texture2D atlas, Rect rect, Vector2 pivot, float pixelsPerUnit = 16f, bool fullRect = false)
    {
        if (atlas == null) return null;
        var area = new RectInt(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
            Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
        if (area.width <= 0 || area.height <= 0 || area.x < 0 || area.y < 0 ||
            area.xMax > atlas.width || area.yMax > atlas.height) return null;
        var key = new Key(atlas, area, pivot, pixelsPerUnit, fullRect);
        if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
        sprite = Sprite.Create(atlas, new Rect(area.x, area.y, area.width, area.height), pivot, pixelsPerUnit, 0,
            fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight);
        sprite.name = $"{atlas.name}_{area.x}_{area.y}_{area.width}x{area.height}";
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>Region given the way source rectangles are written (origin top-left).</summary>
    internal static Sprite FromTop(Texture2D atlas, int x, int top, int width, int height, Vector2 pivot,
        float pixelsPerUnit = 16f, bool fullRect = true) => atlas == null ? null :
        Get(atlas, new Rect(x, atlas.height - top - height, width, height), pivot, pixelsPerUnit, fullRect);

    internal static Sprite FromTop(Texture2D atlas, RectInt source, Vector2 pivot, float pixelsPerUnit = 16f,
        bool fullRect = true) => FromTop(atlas, source.x, source.y, source.width, source.height, pivot, pixelsPerUnit, fullRect);

    /// <summary>Cell <paramref name="index"/> of a sheet laid out left-to-right, top-to-bottom.</summary>
    internal static Sprite Cell(Texture2D atlas, int index, int cellWidth = 16, int cellHeight = 16,
        Vector2? pivot = null, float pixelsPerUnit = 16f)
    {
        if (atlas == null || index < 0) return null;
        int columns = atlas.width / cellWidth;
        if (columns == 0) return null;
        return FromTop(atlas, index % columns * cellWidth, index / columns * cellHeight, cellWidth, cellHeight,
            pivot ?? new Vector2(0.5f, 0.5f), pixelsPerUnit, false);
    }
}
