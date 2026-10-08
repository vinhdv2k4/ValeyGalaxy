using UnityEngine;

/// <summary>
/// Converts clip progress to the discrete FarmerSprite frame used by the
/// generated clips. Keep these start times aligned with FarmerAnimationGenerator.
/// </summary>
public static class ToolAnimationTimeline
{
    private const float SampleMilliseconds = 1000f / 60f;
    private static readonly float[] ToolDown = { 0f, 150f, 190f, 230f, 400f };
    private static readonly float[] ToolOther = { 0f, 100f, 140f, 180f, 400f };
    private static readonly float[] Watering = { 0f, 75f, 175f, 675f };
    private static readonly float[] Sword = { 0f, 55f, 100f, 125f, 150f, 175f };
    private static readonly float[] Fishing = { 0f, 100f, 140f, 180f, 260f };

    public static int ToolFrame(int direction, float progress)
        => Frame(progress, 475f + SampleMilliseconds, direction == 0 ? ToolDown : ToolOther);

    public static int WateringFrame(float progress)
        => Frame(progress, 675f + SampleMilliseconds, Watering);

    public static int SwordFrame(float progress)
        => Frame(progress, 275f + SampleMilliseconds, Sword);

    public static int FishingFrame(float progress)
        => Frame(progress, 460f + SampleMilliseconds, Fishing);

    private static int Frame(float progress, float clipMilliseconds, float[] starts)
    {
        float elapsed = Mathf.Clamp01(progress) * clipMilliseconds;
        for (int frame = starts.Length - 1; frame > 0; frame--)
            if (elapsed >= starts[frame]) return frame;
        return 0;
    }
}
