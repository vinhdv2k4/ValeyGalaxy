using UnityEngine;

/// <summary>Uses the cast frames selected by Game1.drawTool for FishingRod.</summary>
public sealed class FishingRodVisualStrategy : IToolVisualStrategy
{
    public ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        int frame = ToolAnimationTimeline.FishingFrame(progress);
        var rod = slot?.itemData as FishingRodData;
        Sprite sprite = rod != null ? rod.GetCastSprite(direction, frame)
            : slot?.GetHeldSprite(direction);

        // Cast sprites are 48x48 with a top-left pivot. Source draw positions
        // are about 64 px left and 160 px above playerPosition at 4x scale.
        bool side = direction == 1 || direction == 3;
        float x = -1.5f;
        float y = side ? 2f : 124f / 64f - 0.5f;
        if (side && frame == 1) y -= 8f / 64f;
        if (side && frame == 3) { x -= 8f / 64f; y += 8f / 64f; }

        if (direction == 3)
        {
            x = -x;
        }

        return new ToolPose
        {
            LocalPosition = new Vector3(x, y, 0f),
            LocalRotation = Quaternion.identity,
            SortingOrder = direction == 2 ? 20 : 27,
            FlipX = direction == 3,
            Hide = direction == 2 && frame >= 3,
            OverrideSprite = sprite
        };
    }
}
