using System.Collections.Generic;
using UnityEngine;

/// <summary>Charged Hoe and Watering Can rules. Tiles use map coordinates (y grows downward).</summary>
internal static class ToolArea
{
    internal const float HoldPerPowerLevelSeconds = 0.6f;

    // Charge gauge colours: copper, steel, gold, iridium.
    internal static readonly Color[] PowerColors =
    {
        Color.white,
        new Color32(198, 108, 43, 255),
        new Color32(197, 226, 222, 255),
        new Color32(248, 255, 73, 255),
        new Color32(144, 135, 181, 255)
    };

    /// <summary>Facing direction: 0 up, 1 right, 2 down, 3 left.</summary>
    internal static int FacingDirection(Vector2 facing)
    {
        if (facing.sqrMagnitude < 0.0001f) return 2;
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y)) return facing.x > 0f ? 1 : 3;
        return facing.y > 0f ? 0 : 2;
    }

    /// <summary>Power 0: the target tile; 1: three in a line; 2: five; 3: a 3x3 square; 4: 6x3.</summary>
    internal static List<Vector2Int> TilesAffected(Vector2Int origin, int toolPower, int direction)
    {
        int power = toolPower + 1;
        var tiles = new List<Vector2Int> { origin };
        Vector2Int forward = direction switch
        {
            0 => new Vector2Int(0, -1),
            1 => new Vector2Int(1, 0),
            2 => new Vector2Int(0, 1),
            _ => new Vector2Int(-1, 0)
        };
        Vector2Int side = direction is 0 or 2 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
        if (power >= 2)
        {
            tiles.Add(origin + forward);
            tiles.Add(origin + forward * 2);
        }
        if (power >= 3)
        {
            tiles.Add(origin + forward * 3);
            tiles.Add(origin + forward * 4);
        }
        if (power >= 4)
        {
            tiles.RemoveAt(tiles.Count - 1);
            tiles.RemoveAt(tiles.Count - 1);
            foreach (Vector2Int offset in new[] { side, -side })
            {
                tiles.Add(origin + offset + forward * 2);
                tiles.Add(origin + offset + forward);
                tiles.Add(origin + offset);
            }
        }
        if (power >= 5)
            for (int i = tiles.Count - 1; i >= 0; i--)
                tiles.Add(tiles[i] + forward * 3);
        return tiles;
    }

    /// <summary>Watering can capacity per upgrade level.</summary>
    internal static int WaterCapacity(int upgradeLevel) => 40 + 15 * Mathf.Clamp(upgradeLevel, 0, 4);

    /// <summary>Stamina for a Hoe or Watering Can use: 2 * (power + 1).</summary>
    internal static int StaminaCost(int toolPower) => 2 * (toolPower + 1);
}
