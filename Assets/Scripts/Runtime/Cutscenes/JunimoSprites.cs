using UnityEngine;

/// <summary>
/// Frames of Characters/Junimo (8×8 grid of 16×16 cells) and spirit tints.
/// Rows 0–3 walk down, right, up and left; row 5 holds the arms-up frames used for dancing.
/// </summary>
internal static class JunimoSprites
{
    private const int Cell = 16, Columns = 8, Rows = 8;
    private const float PixelsPerUnit = 16f;

    internal static readonly Color[] Tints =
    {
        new Color32(50, 205, 50, 255),   // LimeGreen
        new Color32(255, 165, 0, 255),   // Orange
        new Color32(144, 238, 144, 255), // LightGreen
        new Color32(210, 180, 140, 255), // Tan
        new Color32(173, 255, 47, 255),  // GreenYellow
        new Color32(255, 215, 0, 255),   // Gold
        new Color32(160, 20, 220, 255),
        new Color32(0, 100, 255, 255),
        new Color32(0, 255, 180, 255)
    };

    private static Texture2D source;
    private static Sprite[] frames;

    /// <summary>All 64 frames, or null when the sheet is not available.</summary>
    internal static Sprite[] Frames(MilestoneSprites sprites)
    {
        Texture2D sheet = sprites?.SpriteFor("Junimos")?.texture;
        if (sheet == null || sheet.width < Cell * Columns || sheet.height < Cell * Rows) return null;
        if (frames != null && source == sheet) return frames;
        source = sheet;
        frames = new Sprite[Columns * Rows];
        for (int i = 0; i < frames.Length; i++)
        {
            int column = i % Columns, row = i / Columns;
            var rect = new Rect(column * Cell, sheet.height - (row + 1) * Cell, Cell, Cell);
            frames[i] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.12f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = "Junimo_" + i;
        }
        return frames;
    }

    internal static Color Tint(int seed) => Tints[Mathf.Abs(seed) % Tints.Length];

    /// <summary>Walk cycle for a facing (0 down, 1 right, 2 up, 3 left).</summary>
    internal static int WalkFrame(int row, bool walking, float time) =>
        row * Columns + (walking ? Mathf.FloorToInt(time * 12f) % Columns : 0);

    /// <summary>Arms-up hopping frames.</summary>
    internal static int DanceFrame(float time, int seed) => 44 + (Mathf.FloorToInt(time * 8f) + seed) % 4;

    /// <summary>Height of a hop, in world units, for a dancer started at <paramref name="phase"/>.</summary>
    internal static float Hop(float time, float phase) => Mathf.Abs(Mathf.Sin((time + phase) * Mathf.PI * 2.4f)) * 0.42f;
}
