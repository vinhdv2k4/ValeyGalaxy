using UnityEngine;

/// <summary>
/// Data structure containing the computed transformation, sorting order, and sprite override for a tool during animation.
/// </summary>
public struct ToolPose
{
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public int SortingOrder;
    public bool FlipX;
    public bool FlipY;
    public Sprite OverrideSprite;
    public bool Hide;

    public static ToolPose Default(Sprite sprite, int sortingOrder = 26)
    {
        return new ToolPose
        {
            LocalPosition = Vector3.zero,
            LocalRotation = Quaternion.identity,
            SortingOrder = sortingOrder,
            FlipX = false,
            OverrideSprite = sprite
        };
    }
}
