using UnityEngine;

/// <summary>
/// Physical 2D trigger attached to a warp tile.
/// When the player steps inside, notifies LocationManager to perform the warp.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WarpTrigger : MonoBehaviour
{
    public LocationWarp warp;

    private void Reset()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 0.8f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTriggerWarp(other);
    private void OnTriggerStay2D(Collider2D other) => TryTriggerWarp(other);

    private static float lastNoticeTime;

    private void TryTriggerWarp(Collider2D other)
    {
        if (warp == null || warp.npcOnly || warp.fromMapAction ||
            string.IsNullOrEmpty(warp.targetName)) return;

        var manager = LocationManager.Instance;
        if (manager == null || manager.IsWarping || manager.CurrentLocation == null ||
            GetComponentInParent<GameLocation>() != manager.CurrentLocation ||
            MineManager.IsMineFloor(manager.CurrentLocation.LocationName)) return;
        var motor = other.GetComponentInParent<CharacterMotor>();
        if (!manager.IsLocalFarmer(motor) || !motor.IsMovementEnabled || motor.Movement.sqrMagnitude < 0.001f) return;
        if (warp.isDoor && !warp.IsAtDoorContact(motor.StandingPosition)) return;
        if (!warp.isDoor && NpcPathfinder.WorldToTile(motor.StandingPosition) != new Vector2Int(warp.x, warp.y))
            return;

        // If door has opening hours and is currently closed, show dialog notification
        if (!warp.IsOpenAtCurrentTime())
        {
            if (motor.Movement.y > 0.05f || motor.Facing.y > 0.05f || warp.fromMapAction)
            {
                if (Time.unscaledTime - lastNoticeTime >= 1.5f)
                {
                    lastNoticeTime = Time.unscaledTime;
                    NpcConversation.ShowNotice(warp.GetLockedMessage());
                }
            }
            return;
        }

        // Verify player is moving in the correct direction for this doorway
        if (!warp.CanPass(motor.Movement, motor.Facing)) return;

        manager.WarpFarmer(warp);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = (warp != null && warp.isDoor) ? new Color(1f, 0.8f, 0.2f, 0.6f) : new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawCube(transform.position, new Vector3(0.9f, 0.9f, 0.1f));
#if UNITY_EDITOR
        string label = warp != null ? $"-> {warp.targetName} ({warp.targetX},{warp.targetY})" : "Warp";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, label);
#endif
    }
}
