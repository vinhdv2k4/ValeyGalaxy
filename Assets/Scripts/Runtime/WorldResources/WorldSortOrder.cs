using UnityEngine;

/// <summary>
/// Central utility for top-down 2D depth sorting.
/// Tilemaps are partitioned into static background/foreground ranges,
/// while interactive world objects and the player sort dynamically by foot Y position.
/// </summary>
public static class WorldSortOrder
{
    // Static tilemap layers
    public const int LayerBack = -100;
    public const int LayerPaths = -50;
    public const int LayerBuildings = -10;
    public const int LayerFront = 10000;
    public const int LayerAlwaysFront = 20000;

    // Precision multiplier: 32 sort units per world unit (2 units per pixel at 16 PPU).
    // Farm Y ranges from 0 down to -120 -> sort orders range from 0 to ~3840,
    // comfortably between Buildings (-10) and Front (10000).
    public const float SortPrecision = 32f;

    /// <summary>
    /// Computes the dynamic sorting order for an object based on its ground/foot Y coordinate.
    /// Lower Y (closer to bottom of screen) produces a HIGHER sorting order.
    /// </summary>
    public static int GetOrder(float worldY, float yOffset = 0f, int orderOffset = 0)
    {
        return Mathf.RoundToInt(-(worldY + yOffset) * SortPrecision) + orderOffset;
    }
}
