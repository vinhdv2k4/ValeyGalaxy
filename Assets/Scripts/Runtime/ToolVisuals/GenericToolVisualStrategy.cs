using UnityEngine;

/// <summary>
/// Motion strategy for generic swinging tools (Hoe, Axe, Pickaxe) based on Stardew Valley Game1.drawTool.
/// Follows SRP, OCP, and LSP. All swinging tools perform an overhead vertical chop/slam into ground.
/// Supports frame-specific sprites (windup, strike, diagonal, up) from InventorySlot.GetHeldAnimationSprite.
/// </summary>
public class GenericToolVisualStrategy : IToolVisualStrategy
{
    public const float ClipMilliseconds = 475f + 1000f / 60f;

    public virtual ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        int phase = ToolAnimationTimeline.ToolFrame(direction, progress);

        // Tool animation frame index according to Stardew Valley Tool.Update:
        // Frame 0: Upright idle / windup (used for Down/Up windup)
        // Frame 1: Downward strike frame (facing Down)
        // Frame 2: Diagonal frame (facing Sides: Right/Left)
        // Frame 3: Up windup frame (facing Up)
        // Frame 4: Up strike frame (facing Up)
        Tool tool = ToolCatalog.Resolve(slot);
        int animFrame = tool == null ? 0 : tool.GetAtlasIndex(direction, phase) - tool.InitialAtlasIndex;

        Sprite sprite = slot != null ? slot.GetHeldAnimationSprite(animFrame) : null;
        if (sprite == null && slot != null) sprite = slot.GetHeldSprite(direction);

        float x, y, originY, radians;
        bool flipX = direction == 3;
        int sortingOrder = 26;

        switch (direction)
        {
            case 0: // Stardew facing 2: down
                x = phase == 0 ? -20f : phase == 1 ? -12f : 0f;
                y = phase == 0 ? -116f : phase == 1 ? -96f
                    : phase == 2 ? -64f : phase == 3 ? -20f : -16f;
                originY = 16f;
                radians = phase == 1 ? -Mathf.PI / 24f : 0f;
                sortingOrder = 26;
                break;

            case 1: // Facing Right: overhead arc from behind shoulder to ground impact
                switch (phase)
                {
                    case 0: x = -36f; y = -104f; originY = 16f; radians = -Mathf.PI / 12f; break;
                    case 1: x = 8f; y = -60f; originY = 32f; radians = Mathf.PI / 12f; break;
                    case 2: x = 28f; y = -68f; originY = 32f; radians = Mathf.PI / 4f; break;
                    case 3: x = 60f; y = -64f; originY = 32f; radians = 7f * Mathf.PI / 12f; break;
                    default: x = 60f; y = -60f; originY = 32f; radians = 7f * Mathf.PI / 12f; break;
                }
                sortingOrder = 26;
                break;

            case 2: // Stardew facing 0: up; source hides the tool after frame 2
                x = phase == 1 ? 4f : 0f;
                y = phase == 0 ? -136f : phase == 1 ? -88f : -64f;
                radians = 0f;
                originY = 16f;
                sortingOrder = 20;
                break;

            default: // Facing Left (3): distinct positions/origins plus horizontal flip
                switch (phase)
                {
                    case 0: x = 40f; y = -120f; radians = Mathf.PI / 12f; break;
                    case 1: x = -16f; y = -112f; radians = -Mathf.PI / 12f; break;
                    case 2: x = -60f; y = -68f; radians = -Mathf.PI / 4f; break;
                    case 3: x = -44f; y = 12f; radians = -7f * Mathf.PI / 12f; break;
                    default: x = -40f; y = 24f; radians = -7f * Mathf.PI / 12f; break;
                }
                originY = 16f;
                sortingOrder = 26;
                break;
        }

        // FarmerRenderer places the 16x32 body with its centre at
        // playerPosition + (32,-32) in XNA's 4x, Y-down coordinates.
        // The generated held sprite has its pivot at (8,8) from the bottom.
        Vector2 pivot = sprite == null ? new Vector2(8f, 24f)
            : new Vector2(sprite.pivot.x, sprite.rect.height - sprite.pivot.y);
        Vector2 fromOrigin = (pivot - new Vector2(0f, originY)) * 4f;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 rotated = new Vector2(fromOrigin.x * cosine - fromOrigin.y * sine,
            fromOrigin.x * sine + fromOrigin.y * cosine);
        Vector2 drawPoint = new Vector2(x, y) + rotated;
        Vector3 localPosition = new Vector3((drawPoint.x - 32f) / 64f,
            -(drawPoint.y + 32f) / 64f, 0f);

        return new ToolPose
        {
            LocalPosition = localPosition,
            LocalRotation = Quaternion.Euler(0f, 0f, -radians * Mathf.Rad2Deg),
            SortingOrder = sortingOrder,
            FlipX = flipX,
            Hide = direction == 2 && phase > 2,
            OverrideSprite = sprite
        };
    }

}
