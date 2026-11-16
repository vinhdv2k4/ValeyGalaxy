using UnityEngine;

/// <summary>
/// Continuous tile-based warp detection.
/// Every frame, checks if the farmer is stepping onto a warp tile in the current location.
/// </summary>
public sealed class PlayerWarpDetector : MonoBehaviour
{
    [SerializeField] private CharacterMotor motor;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<CharacterMotor>();
    }

    private static float lastNoticeTime;

    private void Update()
    {
        var manager = LocationManager.Instance;
        if (manager == null || manager.IsWarping || manager.CurrentLocation == null ||
            !manager.IsLocalFarmer(motor) || !motor.IsMovementEnabled || motor.Movement.sqrMagnitude < 0.001f ||
            MineManager.IsMineFloor(manager.CurrentLocation.LocationName))
            return;

        Vector3 pos = motor != null ? motor.StandingPosition : transform.position;
        Vector2Int currentTile = NpcPathfinder.WorldToTile(pos);

        // Town buildings use Buildings-layer door actions instead of Warp
        // properties. Walking north into their doorway must activate them too.
        if (motor != null && motor.Movement.y > 0.05f)
        {
            foreach (LocationWarp door in manager.CurrentLocation.Warps)
            {
                if (!door.fromMapAction || !door.isDoor || door.npcOnly ||
                    !door.IsAtDoorContact(pos)) continue;
                if (!door.IsOpenAtCurrentTime())
                {
                    if (Time.unscaledTime - lastNoticeTime >= 1.5f)
                    {
                        lastNoticeTime = Time.unscaledTime;
                        NpcConversation.ShowNotice(door.GetLockedMessage());
                    }
                    return;
                }
                manager.WarpFarmer(door);
                return;
            }
        }

        LocationWarp warp = manager.CurrentLocation.IsCollidingWithWarp(currentTile.x, currentTile.y);
        // Interior exit tiles are often at the bottom edge of the map. The
        // farmer's collider can hit that edge before its standing point enters
        // the exit tile, so accept contact from the tile immediately above.
        if (warp == null && motor != null && motor.Movement.y < -0.05f &&
            !manager.CurrentLocation.IsOutdoors)
        {
            LocationWarp exit = manager.CurrentLocation.IsCollidingWithWarp(currentTile.x, currentTile.y + 1);
            if (exit != null && !exit.isDoor && !exit.fromMapAction &&
                Mathf.Abs(pos.x - exit.WorldPosition.x) < 0.45f &&
                pos.y - exit.WorldPosition.y < 0.85f)
                warp = exit;
        }
        if (warp != null && !warp.npcOnly && !warp.fromMapAction &&
            (!warp.isDoor || warp.IsAtDoorContact(pos)))
        {
            if (!warp.IsOpenAtCurrentTime())
            {
                if (motor != null && (motor.Facing.y > 0.05f || motor.Movement.y > 0.05f || warp.fromMapAction))
                {
                    if (Time.unscaledTime - lastNoticeTime >= 1.5f)
                    {
                        lastNoticeTime = Time.unscaledTime;
                        NpcConversation.ShowNotice(warp.GetLockedMessage());
                    }
                }
                return;
            }

            if (motor != null && !warp.CanPass(motor.Movement, motor.Facing))
                return;
            manager.WarpFarmer(warp);
        }
    }
}
