using System;
using UnityEngine;

/// <summary>
/// Draws the Latin font atlas using an 8x16 glyph grid.
/// Mirrors character offsets,
/// baseline alignment, kerning, and 3x integer pixel scaling.
/// </summary>
internal static class NpcBitmapText
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 16;
    public const float DefaultScale = 3.0f;
    private const int VietnameseGlyphHeight = 22;
    private const int VietnameseTopPadding = 4;
    // Vietnamese character mapping.
    private const string VietnameseGlyphs =
        "àáảãạăằắẳẵặâầấẩẫậÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬ" +
        "èéẻẽẹêềếểễệÈÉẺẼẸÊỀẾỂỄỆìíỉĩịÌÍỈĨỊ" +
        "òóỏõọôồốổỗộơờớởỡợÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢ" +
        "ùúủũụưừứửữựÙÚỦŨỤƯỪỨỬỮỰỳýỷỹỵỲÝỶỸỴđĐ";
    private static Texture2D vietnameseAtlas;

    private static Texture2D VietnameseAtlas =>
        vietnameseAtlas != null ? vietnameseAtlas :
            vietnameseAtlas = Resources.Load<Texture2D>("Fonts/VietnameseSpriteText");

    internal static bool CanDraw(Texture2D atlas, string value)
    {
        if (atlas == null) return false;
        int glyphCount = (atlas.width / GlyphWidth) * (atlas.height / GlyphHeight);
        foreach (char character in value)
        {
            if (character is '\r' or '\n' or '^') continue;
            if (VietnameseGlyphs.IndexOf(character) >= 0 && VietnameseAtlas != null) continue;
            int glyph = GlyphIndex(character);
            if (glyph < 0 || glyph >= glyphCount) return false;
        }
        return true;
    }

    public static float GetStringWidth(string value, float scale = DefaultScale)
    {
        if (string.IsNullOrEmpty(value)) return 0f;
        float width = 0f;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\r' || c == '\n' || c == '^') continue;
            width += (GlyphWidth + WidthOffset(c)) * scale;
            if (i + 1 < value.Length)
                width += WidthOffset(value[i + 1]) * scale;
        }
        return width;
    }

    /// <param name="visibleCount">Typewriter reveal: the whole string is laid out (so words
    /// never jump lines mid-reveal) but only
    /// the first characters are drawn.</param>
    public static void Draw(Texture2D atlas, Rect bounds, string value, float scale = DefaultScale, bool centered = false,
        int visibleCount = int.MaxValue)
    {
        if (atlas == null || string.IsNullOrEmpty(value)) return;
        Layout(atlas, bounds, value, scale, centered, visibleCount);
    }

    internal static float MeasureWrappedHeight(string value, float width, float scale)
    {
        if (string.IsNullOrEmpty(value)) return 0f;
        int lines = Layout(null, new Rect(0f, 0f, width, float.PositiveInfinity), value, scale, false);
        return (VietnameseGlyphHeight + (lines - 1) * 18f) * scale;
    }

    private static int Layout(Texture2D atlas, Rect bounds, string value, float scale, bool centered,
        int visibleCount = int.MaxValue)
    {
        float baseAdvance = GlyphWidth * scale;
        float lineHeight = 18f * scale; // Default line spacing
        float lineStart = centered ? Mathf.Max(0f, (bounds.width - GetStringWidth(value, scale)) * 0.5f) : 0f;
        float x = lineStart;
        float y = 0f;
        int lines = 1;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (character == '\r') continue;
            if (character == '\n' || character == '^')
            {
                x = lineStart;
                y += lineHeight;
                lines++;
                continue;
            }

            // Word wrap lookahead if not centered
            if (!centered && character == ' ' && x > 0f)
            {
                float nextWordWidth = 0f;
                for (int next = i + 1; next < value.Length && value[next] != ' ' && value[next] != '\n' && value[next] != '^'; next++)
                {
                    nextWordWidth += (GlyphWidth + WidthOffset(value[next])) * scale;
                    if (next + 1 < value.Length && value[next + 1] != ' ')
                        nextWordWidth += WidthOffset(value[next + 1]) * scale;
                }
                if (x + baseAdvance + nextWordWidth > bounds.width + .01f)
                {
                    x = 0f;
                    y += lineHeight;
                    lines++;
                    continue; // Skip drawing this space at the line break
                }
            }

            // Fallback wrap if a single long word exceeds line bounds
            if (!centered && x + baseAdvance > bounds.width + .01f && x > 0f)
            {
                x = 0f;
                y += lineHeight;
                lines++;
                if (character == ' ') continue;
            }

            if (y > 0f && y + GlyphHeight * scale > bounds.height) break;
            if (atlas != null && i < visibleCount) DrawGlyph(atlas, character, bounds.x + x, bounds.y + y, scale);

            // Character kerning adjustment
            float charAdvance = baseAdvance + WidthOffset(character) * scale;
            if (i + 1 < value.Length)
                charAdvance += WidthOffset(value[i + 1]) * scale;
            x += charAdvance;
        }
        return lines;
    }

    private static void DrawGlyph(Texture2D atlas, char character, float x, float y, float scale)
    {
        int vietnameseIndex = VietnameseGlyphs.IndexOf(character);
        Texture2D supplement = vietnameseIndex >= 0 ? VietnameseAtlas : null;
        Texture2D sheet = supplement != null ? supplement : atlas;
        int cellHeight = supplement != null ? VietnameseGlyphHeight : GlyphHeight;
        int cols = sheet.width / GlyphWidth;
        int glyph = supplement != null ? vietnameseIndex : GlyphIndex(character);
        if (glyph < 0 || glyph >= cols * (sheet.height / cellHeight))
            glyph = '?' - 32;

        // Uppercase vertical baseline offset.
        bool isUpper = char.IsUpper(character) || character == 'ß';
        float yOffset = (isUpper ? -4f : -1f) * scale;
        if (character == 'Ç') yOffset += 2f * scale;
        if (supplement != null) yOffset -= VietnameseTopPadding * scale;

        int col = glyph % cols;
        int row = glyph / cols;
        var screenRect = new Rect(x, y + yOffset, GlyphWidth * scale, cellHeight * scale);
        var uvRect = new Rect((float)(col * GlyphWidth) / sheet.width,
            1f - (float)((row + 1) * cellHeight) / sheet.height,
            (float)GlyphWidth / sheet.width, (float)cellHeight / sheet.height);
        GUI.DrawTextureWithTexCoords(screenRect, sheet, uvRect, true);
    }

    private static int GlyphIndex(char character)
    {
        switch (character)
        {
            case 'Œ': return 96;
            case 'œ': return 97;
            case 'İ': return 98;
            case 'ı': return 99;
            case 'Ş': return 100;
            case 'ş': return 101;
            case 'Ğ': return 102;
            case 'ğ': return 103;
            case '’': return 104;
            case 'Ő': return 105;
            case 'ő': return 106;
            case 'Ű': return 107;
            case 'ű': return 108;
            default: return character - 32;
        }
    }

    public static int WidthOffset(char character)
    {
        switch (character)
        {
            case '!':
            case 'j':
            case 'l':
            case '¡':
            case 'i':
            case 'ì':
            case 'í':
            case 'ỉ':
            case 'ĩ':
            case 'ị':
            case 'î':
            case 'ï':
            case 'ı':
                return -1;
            case ',':
            case '.':
                return -2;
            case '$':
                return 1;
            default:
                return 0;
        }
    }
}
