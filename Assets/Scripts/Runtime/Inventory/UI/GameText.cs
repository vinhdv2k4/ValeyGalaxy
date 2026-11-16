using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regular fonts for IMGUI: dialogueFont for names, prices,
/// descriptions and dialogue, and smallFont for tooltip bodies.
/// stays for titles and scrolls. Vietnamese letters are drawn as the base glyph plus pixel
/// diacritics in the font's own pixel size, with the original drop shadow.
/// </summary>
internal static class GameText
{
    internal enum Face { Dialogue, Small }

    internal static readonly Color TextColor = new(86f / 255f, 22f / 255f, 12f / 255f);
    internal static readonly Color ShadowColor = new(221f / 255f, 148f / 255f, 84f / 255f);

    private enum Mark { None, Grave, Acute, Hook, Tilde, Dot, Circumflex, Breve, Horn, Stroke }

    private static readonly Dictionary<char, (char Base, Mark Shape, Mark Tone)> Vietnamese = BuildTable();
    private static Texture2D dialogueAtlas, smallAtlas;

    private static Dictionary<char, (char, Mark, Mark)> BuildTable()
    {
        var table = new Dictionary<char, (char, Mark, Mark)>();
        Mark[] tones = { Mark.None, Mark.Grave, Mark.Acute, Mark.Hook, Mark.Tilde, Mark.Dot };
        void Row(string letters, char baseChar, Mark shape)
        {
            for (int i = 0; i < letters.Length && i < tones.Length; i++)
                if (letters[i] != baseChar || shape != Mark.None) table[letters[i]] = (baseChar, shape, tones[i]);
        }
        Row("aàáảãạ", 'a', Mark.None); Row("ăằắẳẵặ", 'a', Mark.Breve); Row("âầấẩẫậ", 'a', Mark.Circumflex);
        Row("eèéẻẽẹ", 'e', Mark.None); Row("êềếểễệ", 'e', Mark.Circumflex); Row("iìíỉĩị", 'i', Mark.None);
        Row("oòóỏõọ", 'o', Mark.None); Row("ôồốổỗộ", 'o', Mark.Circumflex); Row("ơờớởỡợ", 'o', Mark.Horn);
        Row("uùúủũụ", 'u', Mark.None); Row("ưừứửữự", 'u', Mark.Horn); Row("yỳýỷỹỵ", 'y', Mark.None);
        Row("AÀÁẢÃẠ", 'A', Mark.None); Row("ĂẰẮẲẴẶ", 'A', Mark.Breve); Row("ÂẦẤẨẪẬ", 'A', Mark.Circumflex);
        Row("EÈÉẺẼẸ", 'E', Mark.None); Row("ÊỀẾỂỄỆ", 'E', Mark.Circumflex); Row("IÌÍỈĨỊ", 'I', Mark.None);
        Row("OÒÓỎÕỌ", 'O', Mark.None); Row("ÔỒỐỔỖỘ", 'O', Mark.Circumflex); Row("ƠỜỚỞỠỢ", 'O', Mark.Horn);
        Row("UÙÚỦŨỤ", 'U', Mark.None); Row("ƯỪỨỬỮỰ", 'U', Mark.Horn); Row("YỲÝỶỸỴ", 'Y', Mark.None);
        table['đ'] = ('d', Mark.Stroke, Mark.None);
        table['Đ'] = ('D', Mark.Stroke, Mark.None);
        return table;
    }

    private static Texture2D Atlas(Face face) => face == Face.Dialogue
        ? dialogueAtlas != null ? dialogueAtlas : dialogueAtlas = Resources.Load<Texture2D>("Fonts/SpriteFont1")
        : smallAtlas != null ? smallAtlas : smallAtlas = Resources.Load<Texture2D>("Fonts/SmallFont");

    private static ClockSpriteFont.Glyph Glyph(Face face, char c)
    {
        int index = c >= 32 && c <= 126 ? c - 32 : 0;
        return face == Face.Dialogue ? ClockSpriteFont.Ascii[index] : SmallSpriteFont.Ascii[index];
    }

    /// <summary>Size of one font pixel: SpriteFont1 is drawn at 3x, SmallFont at 2x.</summary>
    private static float Pixel(Face face) => face == Face.Dialogue ? 3f : 2f;

    internal static float LineHeight(Face face, float scale = 1f) => (face == Face.Dialogue ? 44f : 30f) * scale;

    /// <summary>True when every character is printable ASCII or a Vietnamese letter.</summary>
    internal static bool CanDraw(string text)
    {
        if (text == null) return true;
        foreach (char c in text)
            if (!(c >= 32 && c <= 126) && c != (char)10 && c != (char)13 && !Vietnamese.ContainsKey(c)) return false;
        return true;
    }

    private static char BaseOf(char c) => Vietnamese.TryGetValue(c, out var entry) ? entry.Base : c;

    internal static float Measure(string text, Face face, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float widest = 0f, width = 0f;
        foreach (char c in text)
        {
            if (c == '\n') { widest = Mathf.Max(widest, width); width = 0f; continue; }
            width += Glyph(face, BaseOf(c)).Advance * scale;
        }
        return Mathf.Max(widest, width);
    }

    /// <summary>Breaks text into lines no wider than <paramref name="width"/>.</summary>
    internal static List<string> Wrap(string text, float width, Face face, float scale = 1f)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        foreach (string paragraph in text.Replace("^", "\n").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(candidate, face, scale) > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }

    internal static float MeasureHeight(string text, float width, Face face, float scale = 1f) =>
        Wrap(text, width, face, scale).Count * LineHeight(face, scale);

    /// <summary>Draws wrapped text with the original drop shadow (three offsets in the shadow colour).</summary>
    internal static void Draw(Rect bounds, string text, Face face, Color? color = null, float scale = 1f,
        bool centered = false, bool shadow = true)
    {
        if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
        Texture2D atlas = Atlas(face);
        if (atlas == null) return;
        Color tint = color ?? TextColor;
        Color previous = GUI.color;
        float y = bounds.y;
        foreach (string line in Wrap(text, bounds.width, face, scale))
        {
            float x = centered ? bounds.x + (bounds.width - Measure(line, face, scale)) * 0.5f : bounds.x;
            if (shadow)
            {
                GUI.color = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, tint.a);
                DrawLine(atlas, face, line, x + 2f * scale, y + 2f * scale, scale);
                DrawLine(atlas, face, line, x, y + 2f * scale, scale);
                DrawLine(atlas, face, line, x + 2f * scale, y, scale);
            }
            GUI.color = tint;
            DrawLine(atlas, face, line, x, y, scale);
            y += LineHeight(face, scale);
            if (y > bounds.yMax + 1f) break;
        }
        GUI.color = previous;
    }

    private static void DrawLine(Texture2D atlas, Face face, string line, float x, float y, float scale)
    {
        float pixel = Pixel(face) * scale;
        foreach (char c in line)
        {
            Mark shape = Mark.None, tone = Mark.None;
            char letter = c;
            if (Vietnamese.TryGetValue(c, out var entry)) (letter, shape, tone) = entry;
            ClockSpriteFont.Glyph glyph = Glyph(face, letter);
            var rect = new Rect(x + glyph.Left * scale, y + glyph.Top * scale, glyph.Width * scale, glyph.Height * scale);
            GUI.DrawTextureWithTexCoords(rect, atlas, new Rect((float)glyph.X / atlas.width,
                1f - (float)(glyph.Y + glyph.Height) / atlas.height,
                (float)glyph.Width / atlas.width, (float)glyph.Height / atlas.height));
            if (shape != Mark.None || tone != Mark.None) DrawMarks(rect, shape, tone, pixel, char.IsUpper(letter));
            x += glyph.Advance * scale;
        }
    }

    // Diacritic shapes in font pixels: (column, row) cells, with their width and height.
    private static readonly Dictionary<Mark, (int W, int H, Vector2Int[] Cells)> Shapes = new()
    {
        [Mark.Grave] = (2, 2, new[] { new Vector2Int(0, 0), new Vector2Int(1, 1) }),
        [Mark.Acute] = (2, 2, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) }),
        [Mark.Hook] = (2, 3, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 2) }),
        [Mark.Tilde] = (4, 2, new[] { new Vector2Int(1, 0), new Vector2Int(3, 0), new Vector2Int(0, 1), new Vector2Int(2, 1) }),
        [Mark.Circumflex] = (3, 2, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(2, 1) }),
        [Mark.Breve] = (3, 2, new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) }),
        [Mark.Dot] = (1, 1, new[] { new Vector2Int(0, 0) }),
    };

    private static void DrawMarks(Rect glyph, Mark shape, Mark tone, float pixel, bool upper)
    {
        float centerX = glyph.center.x;
        float top = glyph.y - pixel;
        if (shape is Mark.Circumflex or Mark.Breve)
        {
            var s = Shapes[shape];
            top -= s.H * pixel;
            Cells(s, centerX - s.W * pixel * 0.5f, top, pixel);
        }
        else if (shape == Mark.Horn)
        {
            Fill(new Rect(glyph.xMax - pixel * 0.5f, glyph.y - pixel, pixel, pixel * 2f));
            Fill(new Rect(glyph.xMax - pixel * 0.5f - pixel, glyph.y - pixel * 2f, pixel, pixel));
        }
        else if (shape == Mark.Stroke)
        {
            float barY = glyph.y + glyph.height * (upper ? 0.45f : 0.2f);
            float barX = upper ? glyph.x - pixel : glyph.xMax - pixel * 3f;
            Fill(new Rect(barX, barY, pixel * 3f, pixel));
        }
        if (tone == Mark.None) return;
        var t = Shapes[tone];
        if (tone == Mark.Dot)
        {
            Cells(t, centerX - pixel * 0.5f, glyph.yMax + pixel, pixel);
            return;
        }
        float right = shape == Mark.Circumflex ? pixel * 1.5f : 0f;
        Cells(t, centerX - t.W * pixel * 0.5f + right, top - t.H * pixel - (shape == Mark.Circumflex ? 0f : 0f), pixel);
    }

    private static void Cells((int W, int H, Vector2Int[] Cells) shape, float x, float y, float pixel)
    {
        foreach (Vector2Int cell in shape.Cells) Fill(new Rect(x + cell.x * pixel, y + cell.y * pixel, pixel, pixel));
    }

    private static void Fill(Rect rect) => GUI.DrawTexture(rect, Texture2D.whiteTexture);

    /// <summary>
    /// The original hover box: menu-frame box, an optional title in the dialogue font, then the
    /// body in the small font. Placed beside the mouse and kept inside the canvas.
    /// </summary>
    internal static void DrawTooltip(string title, string body, float canvasWidth, float canvasHeight)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return;
        const float padding = 20f, maxText = 360f;
        float titleWidth = Measure(title, Face.Dialogue);
        float bodyWidth = Mathf.Min(maxText, Measure(body, Face.Small));
        float textWidth = Mathf.Clamp(Mathf.Max(titleWidth, bodyWidth), 60f, Mathf.Max(maxText, titleWidth));
        float titleHeight = string.IsNullOrEmpty(title) ? 0f : LineHeight(Face.Dialogue);
        float bodyHeight = string.IsNullOrEmpty(body) ? 0f : MeasureHeight(body, textWidth, Face.Small);
        float w = textWidth + padding * 2f + 4f;
        float h = titleHeight + bodyHeight + padding * 2f - (bodyHeight > 0f && titleHeight > 0f ? 4f : 0f);
        Vector2 mouse = Event.current.mousePosition;
        float x = mouse.x + 32f + w <= canvasWidth ? mouse.x + 32f : mouse.x - w - 16f;
        float y = mouse.y + 32f + h <= canvasHeight ? mouse.y + 32f : mouse.y - h - 16f;
        var box = new Rect(Mathf.Clamp(x, 0f, Mathf.Max(0f, canvasWidth - w)), Mathf.Clamp(y, 0f, Mathf.Max(0f, canvasHeight - h)), w, h);
        StardewUiAtlas.DrawTextureBox(box, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        float cursor = box.y + padding - 2f;
        if (titleHeight > 0f)
        {
            Draw(new Rect(box.x + padding, cursor, textWidth + 4f, titleHeight), title, Face.Dialogue);
            cursor += titleHeight - 4f;
        }
        if (bodyHeight > 0f) Draw(new Rect(box.x + padding, cursor, textWidth + 4f, bodyHeight), body, Face.Small);
    }
}
