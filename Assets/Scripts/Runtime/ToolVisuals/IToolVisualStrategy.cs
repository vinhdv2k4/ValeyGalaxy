/// <summary>
/// Strategy interface defining tool-specific animation poses and directional rendering.
/// Follows the Open/Closed Principle (OCP) and Single Responsibility Principle (SRP).
/// </summary>
public interface IToolVisualStrategy
{
    ToolPose CalculatePose(int direction, float progress, InventorySlot slot);
}
