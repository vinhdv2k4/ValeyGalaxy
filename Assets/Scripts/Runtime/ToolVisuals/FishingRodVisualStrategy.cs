using UnityEngine;

/// <summary>
/// Port of the FishingRod branches of Game1.drawTool. Cast sprites are 48x48 (192 px at 4x)
/// with a top-left pivot; offsets below are that corner relative to playerPosition in
/// source pixels, converted with the body centre at playerPosition + (32, -32).
/// </summary>
public sealed class FishingRodVisualStrategy : IToolVisualStrategy
{
    private const float SpriteUnits = 192f / 64f;
    /// <summary>FishingController holds the last cast frame at 0.98 while the line is out.</summary>
    private const float WaitingProgress = 0.975f;
    /// <summary>While fishing the rod shows column 5 ("rectangle.X += (5 - index) * 48").</summary>
    private const int LineOutFrame = 5;

    // Cast frames facing sideways (FarmerSprite 295-298: 100, 40, 40, 80, 200 ms).
    private static readonly Vector2[] SideCast =
    {
        new(-64f, -160f), new(-64f, -152f), new(-64f, -152f), new(-72f, -160f), new(-64f, -156f)
    };

    public ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        bool waiting = progress >= WaitingProgress;
        int frame = waiting ? 4 : ToolAnimationTimeline.FishingFrame(progress);
        var rod = slot?.itemData as FishingRodData;
        Sprite sprite = rod != null ? rod.GetCastSprite(direction, waiting ? LineOutFrame : frame)
            : slot?.GetHeldSprite(direction);
        // Older rod assets without the line-out column fall back to the last cast frame.
        if (rod != null && waiting && sprite == rod.GetHeldSprite(direction))
            sprite = rod.GetCastSprite(direction, 4);

        bool side = direction == 1 || direction == 3;
        bool flipY = false;
        bool hide = false;
        Vector2 corner;
        if (side)
        {
            // isFishing && !isReeling: "playerPosition.Y += 8" on top of case 4 (-64, -160).
            corner = waiting ? new Vector2(-64f, -152f) : SideCast[frame];
        }
        else if (direction == 2)
        {
            // Facing up the rod shifts 16 px right; frames 3+ are hidden while casting, and
            // while fishing case 4 draws it flipped vertically at (X - 80, Y - 96).
            if (waiting)
            {
                corner = new Vector2(-64f, -96f);
                flipY = true;
            }
            else
            {
                corner = new Vector2(-48f, -124f);
                hide = frame >= 3;
            }
        }
        else
        {
            corner = new Vector2(-64f, -124f);
        }

        float x = (corner.x - 32f) / 64f;
        float y = -(corner.y + 32f) / 64f;
        // Unity flips around the top-left pivot; XNA keeps the same screen rectangle.
        if (direction == 3) x += SpriteUnits;
        if (flipY) y -= SpriteUnits;

        return new ToolPose
        {
            LocalPosition = new Vector3(x, y, 0f),
            LocalRotation = Quaternion.identity,
            SortingOrder = direction == 2 ? 20 : 27,
            FlipX = direction == 3,
            FlipY = flipY,
            Hide = hide,
            OverrideSprite = sprite
        };
    }
}
