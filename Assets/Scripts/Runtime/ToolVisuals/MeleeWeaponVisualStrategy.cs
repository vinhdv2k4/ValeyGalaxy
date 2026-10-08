using UnityEngine;

/// <summary>
/// Six-frame sword/scythe pose from MeleeWeapon.drawDuringUse (ordinary swipe).
/// Coordinates are the decompiled XNA offsets from playerPosition at 4x scale.
/// The weapon sprite's pivot (1, 1 from the bottom) is the same point as the
/// source origin (1, 15 from the top), so no pivot correction is needed.
/// </summary>
public class MeleeWeaponVisualStrategy : IToolVisualStrategy
{
    private readonly struct FramePose
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Angle;

        public FramePose(float x, float y, float angle)
        {
            X = x;
            Y = y;
            Angle = angle;
        }
    }

    // Unity directions: Down, Right, Up, Left.
    private static readonly FramePose[][] Frames =
    {
        new[] { new FramePose(56, -16, 22.5f), new FramePose(52, -8, 90),
            new FramePose(40, 0, 90), new FramePose(16, 4, 135),
            new FramePose(8, 8, 180), new FramePose(12, 0, 202.5f) },
        new[] { new FramePose(40, -56, -45), new FramePose(56, -36, 0),
            new FramePose(60, -16, 45), new FramePose(60, -4, 90),
            new FramePose(36, 4, 112.5f), new FramePose(16, 4, 135) },
        new[] { new FramePose(32, -32, -135), new FramePose(32, -48, -90),
            new FramePose(48, -52, -67.5f), new FramePose(48, -52, -22.5f),
            new FramePose(56, -40, 0), new FramePose(64, -40, 22.5f) },
        new[] { new FramePose(-16, -80, 45), new FramePose(-48, -44, 0),
            new FramePose(-32, 16, -45), new FramePose(4, 44, -90),
            new FramePose(44, 52, -112.5f), new FramePose(80, 40, -135) }
    };

    public virtual ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        direction = Mathf.Clamp(direction, 0, 3);
        int frame = ToolAnimationTimeline.SwordFrame(progress);

        if (direction == 3) // Facing Left: perfect horizontal mirror of Facing Right (direction 1)
        {
            FramePose rightSource = Frames[1][frame];
            return new ToolPose
            {
                LocalPosition = new Vector3(-(rightSource.X - 32f) / 64f,
                    -(rightSource.Y + 32f) / 64f, 0f),
                LocalRotation = Quaternion.Euler(0f, 0f, rightSource.Angle),
                SortingOrder = frame < 3 ? 20 : 27,
                FlipX = true,
                OverrideSprite = slot?.GetHeldSprite(direction)
            };
        }

        FramePose source = Frames[direction][frame];

        // FarmerRenderer's 16x32 body is centred at playerPosition + (32,-32)
        // in source screen pixels. Unity sprites use 64 screen pixels per unit.
        return new ToolPose
        {
            LocalPosition = new Vector3((source.X - 32f) / 64f,
                -(source.Y + 32f) / 64f, 0f),
            LocalRotation = Quaternion.Euler(0f, 0f, -source.Angle),
            SortingOrder = direction == 2 ? 20 : direction == 0 ? 27
                : frame < 3 ? 20 : 27,
            FlipX = false,
            OverrideSprite = slot?.GetHeldSprite(direction)
        };
    }
}
