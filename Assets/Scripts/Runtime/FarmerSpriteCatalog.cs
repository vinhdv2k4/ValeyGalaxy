using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slices and recolors farmer appearance components exactly matching
/// Character customization layout.
/// Facing Directions: 0 = Up (Back), 1 = Right, 2 = Down (Front), 3 = Left.
/// </summary>
public sealed class FarmerSpriteCatalog : ScriptableObject
{
    public Texture2D maleBase;
    public Texture2D femaleBase;
    public Texture2D skinColors;
    public Texture2D hairstyles;
    public Texture2D hairstyles2;
    public Texture2D shirts;
    public Texture2D pants;
    public Texture2D accessories;
    public Texture2D cursors;

    // Cache of metadata for custom hairstyles 100..117
    private static readonly Dictionary<int, HairMetadata> CustomHairMeta = new()
    {
        { 100, new HairMetadata(0, 0, true) },
        { 101, new HairMetadata(1, 0, true) },
        { 102, new HairMetadata(2, 0, true) },
        { 103, new HairMetadata(3, 0, true) },
        { 104, new HairMetadata(4, 0, true) },
        { 105, new HairMetadata(5, 0, true) },
        { 106, new HairMetadata(6, 0, true) },
        { 107, new HairMetadata(7, 0, true) },
        { 108, new HairMetadata(0, 8, true) },
        { 109, new HairMetadata(1, 8, true) },
        { 110, new HairMetadata(2, 8, true) },
        { 111, new HairMetadata(3, 8, true) },
        { 112, new HairMetadata(4, 8, true) },
        { 113, new HairMetadata(5, 8, true) },
        { 114, new HairMetadata(6, 8, true) },
        { 115, new HairMetadata(7, 8, true) },
        { 116, new HairMetadata(0, 16, true) },
        { 117, new HairMetadata(1, 16, true) }
    };

    private readonly struct HairMetadata
    {
        public readonly int tileX;
        public readonly int tileY;
        public readonly bool usesUniqueLeft;
        public HairMetadata(int tileX, int tileY, bool usesUniqueLeft)
        {
            this.tileX = tileX;
            this.tileY = tileY;
            this.usesUniqueLeft = usesUniqueLeft;
        }
    }

    public static FarmerSpriteCatalog Load()
    {
        var catalog = Resources.Load<FarmerSpriteCatalog>("FarmerSpriteCatalog");
#if UNITY_EDITOR
        if (catalog == null)
            catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<FarmerSpriteCatalog>("Assets/Resources/FarmerSpriteCatalog.asset");
#endif
        return catalog;
    }

    /// <summary>
    /// Slices hairstyle.
    /// Direction: 0 = Up (Back), 1 = Right, 2 = Down (Front), 3 = Left.
    /// </summary>
    public Sprite Hair(int style, int direction)
    {
        if (style >= 100 && CustomHairMeta.TryGetValue(style, out var meta))
        {
            // Custom hairstyles
            Texture2D sheet = hairstyles2 != null ? hairstyles2 : hairstyles;
            int x = meta.tileX * 16;
            int top = meta.tileY * 16;
            int yOffset = direction == 2 ? 0 : direction == 1 ? 32 : direction == 0 ? 64 : meta.usesUniqueLeft ? 96 : 32;
            return Slice(sheet, x, top + yOffset, 16, 32);
        }
        else
        {
            // Base hairstyles (8 styles per row, 96px tall per row)
            int clampedStyle = Mathf.Clamp(style, 0, 55);
            int x = (clampedStyle % 8) * 16;
            int row = (clampedStyle / 8) * 96;
            int yOffset = direction == 2 ? 0 : direction == 1 ? 32 : direction == 0 ? 64 : 32;
            return Slice(hairstyles, x, row + yOffset, 16, 32);
        }
    }

    public bool HairUsesUniqueLeft(int style)
    {
        return style >= 100 && CustomHairMeta.TryGetValue(style, out var meta) && meta.usesUniqueLeft;
    }

    /// <summary>
    /// Slices accessories.cs:779-848.
    /// Direction: 0 = Up (Back, null), 1 = Right, 2 = Down (Front), 3 = Left.
    /// </summary>
    public Sprite Accessory(int style, int direction)
    {
        if (style < 0 || direction == 0) return null; // Not drawn facing away
        int clamped = Mathf.Clamp(style, 0, 19);
        int x = (clamped % 8) * 16;
        int row = (clamped / 8) * 32;
        int yOffset = direction == 2 ? 0 : 16;
        return Slice(accessories, x, row + yOffset, 16, 16);
    }

    /// <summary>
    /// Slices shirt tile according to FarmerRenderer.cs:771-845.
    /// 16 shirts per row (each tile 8x8, 4 direction frames in 32px block).
    /// If dyeLayer is true, returns the tint overlay (+128 horizontally).
    /// </summary>
    public Sprite Shirt(int style, int direction, bool dyeLayer = false)
    {
        int clamped = Mathf.Clamp(style, 0, 303);
        int x = (clamped % 16) * 8 + (dyeLayer ? 128 : 0);
        int row = (clamped / 16) * 32;
        int yOffset = direction == 2 ? 0 : direction == 1 ? 8 : direction == 3 ? 16 : 24;
        return Slice(shirts, x, row + yOffset, 8, 8);
    }

    /// <summary>
    /// Slices pants frame according to FarmerRenderer.cs:947-951.
    /// 10 styles per row of 688px (192px per style; male at x, female at x + 96).
    /// </summary>
    public Sprite Pants(int style, int direction, bool female)
    {
        int clamped = Mathf.Clamp(style, 0, 19);
        int col = (clamped % 10) * 192 + (female ? 96 : 0);
        int row = (clamped / 10) * 688;
        // Standing frame offsets: Down (direction 2) = frame 0, Right/Left (1/3) = frame 6, Up (0) = frame 12
        int yOffset = direction == 2 ? 0 : direction == 0 ? 64 : 32;
        return Slice(pants, col, row + yOffset, 16, 32);
    }

    public Sprite PetPortrait(int pet, int breed)
    {
        int x = 160 + (pet == 0 ? 0 : 48) + Mathf.Clamp(breed, 0, 2) * 16;
        return Slice(cursors, x, 208, 16, 16);
    }

    /// <summary>
    /// Slices body frame from recolored body texture (16x32 per frame, 6 frames per row).
    /// Standing frames: Down = frame 0, Right = frame 6, Up = frame 12.
    /// </summary>
    public Sprite Body(Texture2D recolored, int frame, bool arms = false, bool alternateArms = false)
    {
        int x = (frame % 6) * 16 + (arms ? (alternateArms ? 192 : 96) : 0);
        int top = (frame / 6) * 32;
        Sprite result = Slice(recolored, x, top, 16, 32);
        if (result != null)
            result.name = (arms ? (alternateArms ? "SecondaryArms_" : "Arms_") : "Body_") + frame.ToString("D3");
        return result;
    }

    /// <summary>
    /// Performs authentic pixel-swapping recoloring of the base body texture.
    /// Replaces skin pixels (260, 261, 262) and eye pixels (276, 277).
    /// </summary>
    public Texture2D RecolorBody(int gender, int skin, Color eye)
    {
        Texture2D source = gender == 1 ? femaleBase : maleBase;
        if (source == null || skinColors == null) return null;

        var pixels = source.GetPixels32();
        var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        // Palette pixels are located on top row (source.height - 1 in Unity coords)
        Color32[] oldSkin = {
            source.GetPixel(260, source.height - 1),
            source.GetPixel(261, source.height - 1),
            source.GetPixel(262, source.height - 1)
        };

        int clampedSkin = Mathf.Clamp(skin, 0, skinColors.height - 1);
        Color32[] newSkin = {
            skinColors.GetPixel(0, skinColors.height - 1 - clampedSkin),
            skinColors.GetPixel(1, skinColors.height - 1 - clampedSkin),
            skinColors.GetPixel(2, skinColors.height - 1 - clampedSkin)
        };

        Color32 oldEye = source.GetPixel(276, source.height - 1);
        Color32 oldEyeDark = source.GetPixel(277, source.height - 1);

        Color32 newEye = eye;
        // Brightness adjustment: brightness -75, blue adjusted by -75 * 8 / 7
        byte r2 = (byte)Mathf.Clamp(Mathf.RoundToInt(eye.r * 255f) - 75, 0, 255);
        byte g2 = (byte)Mathf.Clamp(Mathf.RoundToInt(eye.g * 255f) - 75, 0, 255);
        byte b2 = (byte)Mathf.Clamp(Mathf.RoundToInt(eye.b * 255f) + (-75 * 8 / 7), 0, 255);
        Color32 newEyeDark = new Color32(r2, g2, b2, 255);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) continue;
            Color32 p = pixels[i];
            for (int k = 0; k < 3; k++)
            {
                if (SameRgb(p, oldSkin[k]))
                {
                    pixels[i] = WithAlpha(newSkin[k], p.a);
                    goto nextPixel;
                }
            }
            if (SameRgb(p, oldEye)) pixels[i] = WithAlpha(newEye, p.a);
            else if (SameRgb(p, oldEyeDark)) pixels[i] = WithAlpha(newEyeDark, p.a);
        nextPixel:;
        }

        output.SetPixels32(pixels);
        output.Apply(false, false);
        return output;
    }

    private static bool SameRgb(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
    private static Color32 WithAlpha(Color32 color, byte alpha) => new Color32(color.r, color.g, color.b, alpha);

    public static Sprite Slice(Texture2D sheet, int x, int top, int width, int height)
    {
        if (sheet == null || x < 0 || top < 0 || x + width > sheet.width || top + height > sheet.height) return null;
        return Sprite.Create(sheet, new Rect(x, sheet.height - top - height, width, height),
            new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
    }

    public static Color DecodeColor(int packed, Color fallback) => packed == 0 ? fallback :
        new Color(((packed >> 16) & 255) / 255f, ((packed >> 8) & 255) / 255f, (packed & 255) / 255f);

    public static int EncodeColor(Color color) => 0x1000000 | (Mathf.RoundToInt(color.r * 255) << 16) |
        (Mathf.RoundToInt(color.g * 255) << 8) | Mathf.RoundToInt(color.b * 255);
}
