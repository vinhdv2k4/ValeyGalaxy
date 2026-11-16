using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the original SpriteFont1 glyphs inside one authored UI element.</summary>
public sealed class ClockBitmapText : MaskableGraphic
{
    [SerializeField] private Texture2D fontAtlas;
    [SerializeField] private string value = "Tue. 16";

    public override Texture mainTexture => fontAtlas != null ? fontAtlas : base.mainTexture;

    public void SetText(string text)
    {
        text = text ?? string.Empty;
        if (value == text) return;
        value = text;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (fontAtlas == null || string.IsNullOrEmpty(value)) return;

        float width = 0f;
        foreach (char c in value) width += GetGlyph(c).Advance;
        float startX = Mathf.Round((rectTransform.rect.width - width) * 0.5f);
        for (int layer = 0; layer < 4; layer++)
        {
            float x = startX;
            foreach (char c in value)
            {
                ClockSpriteFont.Glyph glyph = GetGlyph(c);
                if (c != ' ' && c >= 32 && c <= 126)
                {
                    float left = x + glyph.Left - (layer == 0 || layer == 1 ? 3f : 0f);
                    float top = -glyph.Top - (layer == 0 || layer == 2 ? 3f : 0f);
                    AddGlyph(mesh, glyph, left, top,
                        layer == 3 ? new Color32(34, 17, 34, 255) : new Color32(221, 148, 84, 255));
                }
                x += glyph.Advance;
            }
        }
    }

    private void AddGlyph(VertexHelper mesh, ClockSpriteFont.Glyph glyph, float left, float top, Color32 tint)
    {
        float u0 = (float)glyph.X / fontAtlas.width;
        float u1 = (float)(glyph.X + glyph.Width) / fontAtlas.width;
        float v0 = 1f - (float)(glyph.Y + glyph.Height) / fontAtlas.height;
        float v1 = 1f - (float)glyph.Y / fontAtlas.height;
        int start = mesh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = tint;
        vertex.position = new Vector3(left, top - glyph.Height);
        vertex.uv0 = new Vector2(u0, v0);
        mesh.AddVert(vertex);
        vertex.position = new Vector3(left, top);
        vertex.uv0 = new Vector2(u0, v1);
        mesh.AddVert(vertex);
        vertex.position = new Vector3(left + glyph.Width, top);
        vertex.uv0 = new Vector2(u1, v1);
        mesh.AddVert(vertex);
        vertex.position = new Vector3(left + glyph.Width, top - glyph.Height);
        vertex.uv0 = new Vector2(u1, v0);
        mesh.AddVert(vertex);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    private static ClockSpriteFont.Glyph GetGlyph(char c)
        => ClockSpriteFont.Ascii[c >= 32 && c <= 126 ? c - 32 : 0];
}
