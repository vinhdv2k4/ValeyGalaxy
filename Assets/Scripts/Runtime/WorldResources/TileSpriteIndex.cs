using UnityEngine;

/// <summary>Reads the original top-left tile index from a sliced Stardew sheet.</summary>
public static class TileSpriteIndex
{
    public static int Get(Sprite sprite, int tileSize = 16)
    {
        if (sprite == null || sprite.texture == null) return -1;
        Rect rect = sprite.textureRect;
        int columns = sprite.texture.width / tileSize;
        int x = Mathf.RoundToInt(rect.x) / tileSize;
        int y = (sprite.texture.height - Mathf.RoundToInt(rect.yMax)) / tileSize;
        return y * columns + x;
    }
}
