using UnityEngine;

/// <summary>
/// Fallback visual strategy for materials, consumables, and miscellaneous items.
/// Simply presents or holds the item forward during actions.
/// </summary>
public sealed class DefaultHoldVisualStrategy : IToolVisualStrategy
{
    public ToolPose CalculatePose(int direction, float progress, InventorySlot slot)
    {
        float reach = Mathf.Clamp01((progress - 0.2f) / 0.6f);
        Vector3 position = Vector3.zero;
        int sortingOrder = direction == 2 ? 5 : 26;
        bool flipX = (direction == 3);
        Sprite sprite = slot != null ? slot.GetHeldSprite(direction) : null;

        switch (direction)
        {
            case 0:
                position = Vector2.Lerp(new Vector2(0.12f, 0.05f), new Vector2(0.12f, -0.20f), reach);
                break;
            case 2:
                position = Vector2.Lerp(new Vector2(0.0f, 0.10f), new Vector2(0.0f, 0.30f), reach);
                break;
            case 1:
            case 3:
                float side = (direction == 3) ? -1f : 1f;
                position = Vector2.Lerp(new Vector2(0.15f * side, 0.05f), new Vector2(0.35f * side, -0.15f), reach);
                break;
        }

        return new ToolPose
        {
            LocalPosition = position,
            LocalRotation = Quaternion.identity,
            SortingOrder = sortingOrder,
            FlipX = flipX,
            OverrideSprite = sprite
        };
    }
}
