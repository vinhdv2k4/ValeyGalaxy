using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Recolors a building texture through its paint mask. Red, lime and blue mask pixels mark the
/// three paint regions; a custom region keeps each pixel's lightness and replaces its hue and
/// saturation.
/// </summary>
internal static class RanchPainter
{
    internal const int MaxSaturation = 75;

    private static readonly Dictionary<string, Sprite> PaintedSprites = new();
    private static readonly Dictionary<Texture2D, int[][]> MaskRegions = new();
    private static readonly Dictionary<Texture2D, Color32[]> BasePixels = new();

    internal static bool HasPaint(OnlineRanchPaint[] paint)
    {
        if (paint == null) return false;
        foreach (OnlineRanchPaint region in paint)
            if (region != null && region.custom) return true;
        return false;
    }

    private static string Signature(string planName, OnlineRanchPaint[] paint)
    {
        var text = new StringBuilder(planName);
        foreach (OnlineRanchPaint region in paint)
            text.Append(region != null && region.custom ? $"|{region.hue},{region.saturation},{region.lightness}" : "|-");
        return text.ToString();
    }

    /// <summary>Painted building sprite, shared by every building with the same colors.</summary>
    internal static Sprite PaintedSprite(string planName, Texture2D art, OnlineRanchPaint[] paint)
    {
        Texture2D mask = FarmRanchAssets.PaintMask(planName);
        if (art == null || mask == null || !HasPaint(paint)) return null;
        string key = Signature(planName, paint);
        if (PaintedSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Texture2D painted = Paint(art, mask, paint, null);
        if (painted == null) return null;
        Sprite sprite = Sprite.Create(painted, new Rect(0, 16, painted.width, painted.height - 16), Vector2.zero, 16f);
        sprite.name = key;
        PaintedSprites[key] = sprite;
        return sprite;
    }

    /// <summary>Writes the painted art into <paramref name="target"/> (created when null) and returns it.</summary>
    internal static Texture2D Paint(Texture2D art, Texture2D mask, OnlineRanchPaint[] paint, Texture2D target)
    {
        Color32[] source = Pixels(art);
        int[][] regions = Regions(mask);
        if (source == null || regions == null || mask.width != art.width || mask.height != art.height) return null;
        var pixels = (Color32[])source.Clone();
        for (int region = 0; region < regions.Length && paint != null && region < paint.Length; region++)
        {
            OnlineRanchPaint color = paint[region];
            if (color == null || !color.custom) continue;
            foreach (int index in regions[region])
                pixels[index] = Recolor(pixels[index], color);
        }
        if (target == null || target.width != art.width || target.height != art.height)
        {
            target = new Texture2D(art.width, art.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = art.name + "_Painted"
            };
        }
        target.SetPixels32(pixels);
        target.Apply(false);
        return target;
    }

    private static Color32 Recolor(Color32 pixel, OnlineRanchPaint color)
    {
        float max = Mathf.Max(pixel.r, pixel.g, pixel.b) / 255f;
        float min = Mathf.Min(pixel.r, pixel.g, pixel.b) / 255f;
        float lightness = Mathf.Clamp01((max + min) * 0.5f + color.lightness / 100f);
        float saturation = Mathf.Clamp01(color.saturation / 100f);
        Color rgb = FromHsl(Mathf.Repeat(color.hue, 360f), saturation, lightness);
        return new Color32((byte)Mathf.RoundToInt(rgb.r * 255f), (byte)Mathf.RoundToInt(rgb.g * 255f),
            (byte)Mathf.RoundToInt(rgb.b * 255f), pixel.a);
    }

    /// <summary>Swatch color for slider values, matching the paint menu's preview colors.</summary>
    internal static Color Swatch(float hue, float saturation, float lightness01) =>
        FromHsl(Mathf.Repeat(hue, 360f), Mathf.Clamp01(saturation / 100f), Mathf.Lerp(0.25f, 0.5f, lightness01));

    private static Color FromHsl(float hue, float saturation, float lightness)
    {
        if (saturation <= 0f) return new Color(lightness, lightness, lightness);
        float q = lightness < 0.5f ? lightness * (1f + saturation) : lightness + saturation - lightness * saturation;
        float p = 2f * lightness - q;
        float h = hue / 360f;
        return new Color(Channel(p, q, h + 1f / 3f), Channel(p, q, h), Channel(p, q, h - 1f / 3f));
    }

    private static float Channel(float p, float q, float t)
    {
        t = Mathf.Repeat(t, 1f);
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 0.5f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    private static int[][] Regions(Texture2D mask)
    {
        if (MaskRegions.TryGetValue(mask, out int[][] cached)) return cached;
        Color32[] pixels = Pixels(mask);
        if (pixels == null) return null;
        var red = new List<int>();
        var lime = new List<int>();
        var blue = new List<int>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = pixels[i];
            if (c.a < 128) continue;
            if (c.r > 200 && c.g < 60 && c.b < 60) red.Add(i);
            else if (c.g > 200 && c.r < 60 && c.b < 60) lime.Add(i);
            else if (c.b > 200 && c.r < 60 && c.g < 60) blue.Add(i);
        }
        cached = new[] { red.ToArray(), lime.ToArray(), blue.ToArray() };
        MaskRegions[mask] = cached;
        return cached;
    }

    private static Color32[] Pixels(Texture2D texture)
    {
        if (texture == null) return null;
        if (BasePixels.TryGetValue(texture, out Color32[] cached)) return cached;
        Color32[] pixels;
        if (texture.isReadable) pixels = texture.GetPixels32();
        else
        {
            // Fallback for textures imported without Read/Write: copy through the GPU.
            RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            pixels = copy.GetPixels32();
            Object.Destroy(copy);
        }
        BasePixels[texture] = pixels;
        return pixels;
    }
}
