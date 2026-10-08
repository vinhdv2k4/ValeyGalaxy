using UnityEngine;

/// <summary>
/// Motion strategy for scattering/planting seeds.
/// Extends arm forward and scatters seeds downward onto the target tile.
/// </summary>
public sealed class SeedThrowVisualStrategy : IToolVisualStrategy
{
    public ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        float reach = Mathf.Clamp01((progress - 0.2f) / 0.6f);
        float throwArc = Mathf.Sin(reach * Mathf.PI);

        Vector3 position = Vector3.zero;
        float angle = 0f;
        int sortingOrder = 26;
        bool flipX = false;
        Sprite sprite = slot != null ? slot.GetHeldSprite(direction) : null;

        switch (direction)
        {
            case 0: // Down
                position = new Vector2(0.10f, Mathf.Lerp(0.0f, -0.30f, throwArc));
                angle = Mathf.Lerp(0f, -20f, throwArc);
                sortingOrder = 26;
                break;

            case 2: // Up
                position = new Vector2(0.05f, Mathf.Lerp(0.10f, 0.35f, throwArc));
                angle = Mathf.Lerp(0f, 15f, throwArc);
                sortingOrder = 5;
                break;

            case 1: // Right
            case 3: // Left
                float side = (direction == 3) ? -1f : 1f;
                position = new Vector2(Mathf.Lerp(0.18f, 0.40f, throwArc) * side, Mathf.Lerp(0.0f, -0.25f, throwArc));
                angle = Mathf.Lerp(10f, -30f, throwArc) * side;
                sortingOrder = 26;
                flipX = (direction == 3);
                break;
        }

        return new ToolPose
        {
            LocalPosition = position,
            LocalRotation = Quaternion.Euler(0f, 0f, angle),
            SortingOrder = sortingOrder,
            FlipX = flipX,
            OverrideSprite = sprite
        };
    }
}
