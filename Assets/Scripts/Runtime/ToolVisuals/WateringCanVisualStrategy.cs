using UnityEngine;

/// <summary>WateringCan branch of Game1.drawTool, using Tool.Update atlas rows.</summary>
public sealed class WateringCanVisualStrategy : IToolVisualStrategy
{
    public ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        int frame = ToolAnimationTimeline.WateringFrame(progress);
        Tool tool = ToolCatalog.Resolve(slot);
        // Game1.drawTool switches to the second tool sprite only on frame 3.
        int motionFrame = frame == 3 ? 5 : 0;
        int atlasOffset = tool == null ? 0
            : tool.GetAtlasIndex(direction, motionFrame) - tool.InitialAtlasIndex;
        Sprite sprite = slot?.GetHeldAnimationSprite(atlasOffset);
        if (sprite == null) sprite = slot?.GetHeldSprite(direction);

        float x;
        float y;
        float angle = 0f;
        bool flipX = direction == 3;
        switch (direction)
        {
            case 1: // Stardew facing right
                x = frame < 2 ? 44f : frame == 2 ? 56f : 40f;
                y = frame < 2 ? -40f : frame == 2 ? -56f : -72f;
                if (frame == 2) angle = 15f;
                break;
            case 3: // Stardew facing left
                x = frame < 2 ? -52f : -48f;
                y = frame < 2 ? -40f : frame == 2 ? -48f : -72f;
                if (frame == 2) angle = -15f;
                break;
            case 2: // Stardew facing up (source direction 0)
                x = 0f;
                y = frame < 2 ? -32f : frame == 2 ? -80f : -112f;
                break;
            default: // Stardew facing down (source direction 2)
                x = frame == 3 ? -4f : 0f;
                y = frame < 2 ? -32f : frame == 2 ? -44f : -24f;
                break;
        }

        // XNA draws at 4x with origin (0,16) in a 16x32 source tile.
        // Adjust for the Unity sprite's serialized pivot before converting
        // from source screen pixels to local Unity units.
        Vector2 sourcePivot = sprite == null ? new Vector2(8f, 24f)
            : new Vector2(sprite.pivot.x, sprite.rect.height - sprite.pivot.y);
        Vector2 fromOrigin = (sourcePivot - new Vector2(0f, 16f)) * 4f;
        float radians = angle * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 rotated = new Vector2(fromOrigin.x * cosine - fromOrigin.y * sine,
            fromOrigin.x * sine + fromOrigin.y * cosine);
        Vector2 point = new Vector2(x, y) + rotated;

        return new ToolPose
        {
            LocalPosition = new Vector3((point.x - 32f) / 64f,
                -(point.y + 32f) / 64f, 0f),
            LocalRotation = Quaternion.Euler(0f, 0f, -angle),
            SortingOrder = direction == 2 ? 20 : 26,
            FlipX = flipX,
            OverrideSprite = sprite
        };
    }
}
