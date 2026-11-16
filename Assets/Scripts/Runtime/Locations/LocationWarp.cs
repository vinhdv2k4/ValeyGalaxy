using System;
using UnityEngine;

/// <summary>
/// Serializable Warp representation.
/// Holds tile coordinates and target destination info.
/// </summary>
[Serializable]
public sealed class LocationWarp
{
    [Tooltip("Tile X coordinate in the current location")]
    public int x;

    [Tooltip("Tile Y coordinate in the current location")]
    public int y;

    [Tooltip("Target location name (e.g. FarmHouse, FarmCave, BusStop, Farm)")]
    public string targetName = string.Empty;

    [Tooltip("Tile X coordinate in target location")]
    public int targetX;

    [Tooltip("Tile Y coordinate in target location")]
    public int targetY;

    [Tooltip("Whether to flip farmer facing direction")]
    public bool flipFarmer;

    [Tooltip("Warp is only for NPCs")]
    public bool npcOnly;

    [Tooltip("Whether this warp is a door entrance")]
    public bool isDoor;

    [Tooltip("Door action read from a map tile rather than the map Warp property")]
    public bool fromMapAction;

    public bool hasOpeningHours;
    public int opensAt;
    public int closesAt;

    public LocationWarp() { }

    public LocationWarp(int x, int y, string targetName, int targetX, int targetY, bool flipFarmer = false, bool npcOnly = false)
    {
        this.x = x;
        this.y = y;
        this.targetName = targetName;
        this.targetX = targetX;
        this.targetY = targetY;
        this.flipFarmer = flipFarmer;
        this.npcOnly = npcOnly;
    }

    /// <summary>
    /// Converts map tile coordinates (x, y) to Unity world position (center of tile).
    /// The map builder puts map tile (X, Y) in Unity cell (X, -Y).
    /// </summary>
    public Vector3 WorldPosition => new Vector3(x + 0.5f, 0.5f - y, 0f);

    // MapDoorCatalogBuilder already moves Buildings-layer actions one tile down
    // to the farmer's standing tile. Adding another tile here triggers too early.
    public Vector2 DoorContactPosition => WorldPosition;

    public bool IsAtDoorContact(Vector2 standingPosition)
        => (standingPosition - DoorContactPosition).sqrMagnitude <= 0.4f * 0.4f;

    public bool IsWithinDoorInteractionRange(Vector2 standingPosition, bool pointerAction)
    {
        Vector2 offset = standingPosition - DoorContactPosition;
        return Mathf.Abs(offset.y) <= 0.65f &&
               Mathf.Abs(offset.x) <= (pointerAction ? 1.1f : 0.55f);
    }

    public bool ContainsDoorPointer(Vector2 pointer)
    {
        int tileX = Mathf.FloorToInt(pointer.x);
        int tileY = -Mathf.FloorToInt(pointer.y);
        if (tileX != x) return false;
        return fromMapAction && isDoor
            ? tileY >= y - 2 && tileY <= y - 1
            : tileY == y;
    }

    /// <summary>
    /// Converts target map tile coordinates to Unity world spawn position.
    /// </summary>
    public Vector3 TargetWorldPosition => new Vector3(targetX + 0.5f, 0.5f - targetY, 0f);

    /// <summary>
    /// Checks movement through a warp tile. Map action doors are entered from below.
    /// </summary>
    public bool CanPass(Vector2 movement, Vector2 facing)
    {
        if (npcOnly || !IsOpenAtCurrentTime()) return false;
        if (!isDoor) return true;

        if (fromMapAction) return movement.y > 0.05f;

        // Entering house/cave -> player must be moving or facing UP into the doorway
        if (targetName.Contains("House") || targetName.Contains("Cave"))
        {
            return movement.y > 0.05f;
        }

        // Exiting house/cave back outside -> player must be moving or facing DOWN through the doorway
        return movement.y < -0.05f;
    }

    public bool IsOpenAtCurrentTime()
    {
        if (!hasOpeningHours) return true;
        FarmDayClock clock = FarmDayClock.Instance;
        int time = clock == null ? 600 : clock.TimeOfDay;
        return time >= opensAt && time < closesAt;
    }

    /// <summary>
    /// Formats 24h clock integer (e.g. 900 -> 9:00am, 1700 -> 5:00pm).
    /// </summary>
    public static string FormatTime(int time)
    {
        int hour = (time / 100) % 24;
        int minute = time % 100;
        string ampm = (hour >= 12 && hour < 24) ? "pm" : "am";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour}:{minute:00}{ampm}";
    }

    /// <summary>
    /// Generates the locked door notice string.
    /// </summary>
    public string GetLockedMessage()
    {
        FarmDayClock clock = FarmDayClock.Instance;
        int day = clock == null ? 1 : clock.Day;
        // Wednesday is day 3, 10, 17, 24 (day % 7 == 3)
        bool isWednesday = (day % 7) == 3;
        if ((targetName.Equals("SeedShop", System.StringComparison.OrdinalIgnoreCase) ||
             targetName.IndexOf("SeedShop", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             targetName.IndexOf("Pierre", System.StringComparison.OrdinalIgnoreCase) >= 0) && isWednesday)
        {
            return "Closed on Wednesdays.";
        }

        if (hasOpeningHours)
        {
            return $"Locked. Open from {FormatTime(opensAt)} to {FormatTime(closesAt)}.";
        }

        return "Locked.";
    }
}
