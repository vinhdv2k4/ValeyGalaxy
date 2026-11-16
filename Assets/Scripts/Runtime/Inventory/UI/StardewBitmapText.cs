using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI renderer for font used by CharacterCustomization.
/// Glyph pixels and metrics.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StardewBitmapText : MaskableGraphic
{
    [SerializeField] private Texture2D fontAtlas;
    [TextArea(1, 3)]
    [SerializeField] private string text = "";
    [SerializeField] private float fontScale = 0.5f;
    [SerializeField] private bool drawShadow = true;
    [SerializeField] private Color32 shadowColor = new Color32(221, 148, 84, 255);
    [SerializeField] private TextAnchor alignment = TextAnchor.MiddleLeft;

    public override Texture mainTexture
    {
        get
        {
            if (fontAtlas == null) LoadDefaultAtlas();
            return fontAtlas != null ? fontAtlas : base.mainTexture;
        }
    }

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value ?? string.Empty;
            SetVerticesDirty();
        }
    }

    public float FontScale
    {
        get => fontScale;
        set
        {
            if (Mathf.Approximately(fontScale, value)) return;
            fontScale = value;
            SetVerticesDirty();
        }
    }

    public bool DrawShadow
    {
        get => drawShadow;
        set
        {
            if (drawShadow == value) return;
            drawShadow = value;
            SetVerticesDirty();
        }
    }

    public Color32 ShadowColor
    {
        get => shadowColor;
        set
        {
            shadowColor = value;
            SetVerticesDirty();
        }
    }

    public TextAnchor Alignment
    {
        get => alignment;
        set
        {
            if (alignment == value) return;
            alignment = value;
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (fontAtlas == null) LoadDefaultAtlas();
    }

    private void LoadDefaultAtlas()
    {
#if UNITY_EDITOR
        fontAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Fonts/SmallFont.png");
#endif
        if (fontAtlas == null)
            fontAtlas = Resources.Load<Texture2D>("Fonts/SmallFont");
    }

    public float PreferredWidth
    {
        get
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float w = 0f;
            foreach (char c in text) w += GetGlyph(c).Advance * fontScale;
            return w;
        }
    }

    public float PreferredHeight => 34f * fontScale;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (mainTexture == null || string.IsNullOrEmpty(text)) return;

        Texture atlas = mainTexture;
        float totalWidth = 0f;
        foreach (char c in text) totalWidth += GetGlyph(c).Advance * fontScale;

        Rect rect = rectTransform.rect;
        float startX = 0f;
        if (alignment == TextAnchor.UpperCenter || alignment == TextAnchor.MiddleCenter || alignment == TextAnchor.LowerCenter)
            startX = Mathf.Round((rect.width - totalWidth) * 0.5f);
        else if (alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight)
            startX = Mathf.Round(rect.width - totalWidth);

        // SpriteFont glyphs use a top bearing rather than a centred origin.
        // Centre the visible ink, not the font's nominal line box. This also
        // keeps short button labels such as BACK inside their sprite frame.
        float topBearing = float.MaxValue;
        float bottomBearing = float.MinValue;
        foreach (char c in text)
        {
            if (c == ' ' || c < 32 || c > 126) continue;
            ClockSpriteFont.Glyph glyph = GetGlyph(c);
            topBearing = Mathf.Min(topBearing, glyph.Top);
            bottomBearing = Mathf.Max(bottomBearing, glyph.Top + glyph.Height);
        }
        if (topBearing == float.MaxValue) return;

        float baselineY;
        if (alignment == TextAnchor.UpperLeft || alignment == TextAnchor.UpperCenter || alignment == TextAnchor.UpperRight)
            baselineY = rect.yMax + topBearing * fontScale;
        else if (alignment == TextAnchor.LowerLeft || alignment == TextAnchor.LowerCenter || alignment == TextAnchor.LowerRight)
            baselineY = rect.yMin + bottomBearing * fontScale;
        else // Middle
            baselineY = (topBearing + bottomBearing) * 0.5f * fontScale;

        // Pass 1: Drop shadow (+2px offset right & down at default scale)
        if (drawShadow)
        {
            float shadowOffset = Mathf.Max(1f, Mathf.Round(2f * fontScale));
            float x = startX + shadowOffset;
            float y = baselineY - shadowOffset;
            foreach (char c in text)
            {
                ClockSpriteFont.Glyph glyph = GetGlyph(c);
                if (c != ' ' && c >= 32 && c <= 126)
                {
                    float left = rect.xMin + x + glyph.Left * fontScale;
                    float top = y - glyph.Top * fontScale;
                    AddGlyphQuad(mesh, atlas, glyph, left, top, glyph.Width * fontScale, glyph.Height * fontScale, shadowColor);
                }
                x += glyph.Advance * fontScale;
            }
        }

        // Pass 2: Main text glyphs
        Color32 mainColor = color;
        float curX = startX;
        float curY = baselineY;
        foreach (char c in text)
        {
            ClockSpriteFont.Glyph glyph = GetGlyph(c);
            if (c != ' ' && c >= 32 && c <= 126)
            {
                float left = rect.xMin + curX + glyph.Left * fontScale;
                float top = curY - glyph.Top * fontScale;
                AddGlyphQuad(mesh, atlas, glyph, left, top, glyph.Width * fontScale, glyph.Height * fontScale, mainColor);
            }
            curX += glyph.Advance * fontScale;
        }
    }

    private static void AddGlyphQuad(VertexHelper mesh, Texture atlas, ClockSpriteFont.Glyph glyph,
        float left, float top, float width, float height, Color32 tint)
    {
        float u0 = (float)glyph.X / atlas.width;
        float u1 = (float)(glyph.X + glyph.Width) / atlas.width;
        float v0 = 1f - (float)(glyph.Y + glyph.Height) / atlas.height;
        float v1 = 1f - (float)glyph.Y / atlas.height;

        int start = mesh.currentVertCount;
        UIVertex vert = UIVertex.simpleVert;
        vert.color = tint;

        vert.position = new Vector3(left, top - height);
        vert.uv0 = new Vector2(u0, v0);
        mesh.AddVert(vert);

        vert.position = new Vector3(left, top);
        vert.uv0 = new Vector2(u0, v1);
        mesh.AddVert(vert);

        vert.position = new Vector3(left + width, top);
        vert.uv0 = new Vector2(u1, v1);
        mesh.AddVert(vert);

        vert.position = new Vector3(left + width, top - height);
        vert.uv0 = new Vector2(u1, v0);
        mesh.AddVert(vert);

        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    private static ClockSpriteFont.Glyph GetGlyph(char c)
        => SmallSpriteFont.Ascii[c >= 32 && c <= 126 ? c - 32 : 0];
}

