using System.Collections;
using UnityEngine;

/// <summary>
/// Saves where the player is (map, position, clock, energy, health) whenever they leave the
/// game and resumes it on the next login of the same server day, instead of waking the
/// player in bed.
/// </summary>
internal sealed class PlayerSessionKeeper : MonoBehaviour
{
    private const float AutosaveSeconds = 30f;
    private const float QuitTimeoutSeconds = 3f;

    internal static PlayerSessionKeeper Active { get; private set; }

    private StartupApiClient api;
    private OnlineGameState online;
    private CharacterMotor motor;
    private NetworkCharacterStats stats;
    private bool resumeOnLoad;
    private bool ready;
    private bool saving;
    private bool quitSaved;
    private float nextAutosave;
    private OnlinePlayerSession lastSafe;

    private OnlinePlayerSession hiddenResume;

    internal void Initialize(StartupApiClient client, bool resume)
    {
        api = client;
        resumeOnLoad = resume;
        online = GetComponent<OnlineGameState>();
        stats = GetComponent<NetworkCharacterStats>();
        OnlinePlayerSession saved = online?.SavedSession;
        if (!resume || saved == null || online.Snapshot == null || !saved.IsFor(online.Snapshot.gameDay)) return;

        // The session is already known on the first frame: show the saved time right away, keep
        // the clock still and the screen black until the farmer stands on the saved spot.
        hiddenResume = saved;
        FarmDayClock clock = FarmDayClock.Instance;
        clock?.RestoreTimeOfDay(saved.time);
        if (clock != null) clock.IsCutscenePaused = true;
        stats?.RestoreVitals(saved.stamina, saved.health);
        LocationManager.Instance?.SetCutsceneFade(1f);
    }

    private void Awake()
    {
        Active = this;
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        if (Active == this) Active = null;
    }

    private IEnumerator Start()
    {
        online ??= GetComponent<OnlineGameState>();
        motor = GetComponent<CharacterMotor>();
        stats ??= GetComponent<NetworkCharacterStats>();
        yield return new WaitUntil(() => online != null && online.Snapshot != null &&
            LocationManager.Instance?.CurrentLocation != null && !StartupSession.IsOpening);
        if (hiddenResume != null)
        {
            yield return Resume(hiddenResume, hidden: true);
            if (FarmDayClock.Instance != null) FarmDayClock.Instance.IsCutscenePaused = false;
            GameLoadingScreen.Hide();
            yield return LocationManager.Instance.RevealOpening();
            // WarpForOpening leaves movement off for its caller (as StartupFarmArrival restores
            // it); without this the farmer stays frozen until some action re-enables it.
            if (motor != null) motor.IsMovementEnabled = true;
        }
        else
        {
            GameLoadingScreen.Hide();
            // Without a snapshot from the menu the session arrives a moment later.
            OnlinePlayerSession saved = online.SavedSession;
            if (resumeOnLoad && saved != null && saved.IsFor(online.Snapshot.gameDay))
                yield return Resume(saved, hidden: false);
        }
        ready = true;
        nextAutosave = Time.unscaledTime + AutosaveSeconds;
    }

    private IEnumerator Resume(OnlinePlayerSession saved, bool hidden)
    {
        var locations = LocationManager.Instance;
        Vector2 facing = new Vector2(saved.facingX, saved.facingY);
        if (facing.sqrMagnitude < 0.01f) facing = Vector2.down;
        int tileX = Mathf.FloorToInt(saved.x);
        int tileY = -Mathf.FloorToInt(saved.y);

        if (locations.CurrentLocation.LocationName != saved.location)
        {
            // While hidden, warp without the usual fade so the start map never flashes.
            if (hidden) yield return locations.WarpForOpening(saved.location, new Vector2Int(tileX, tileY));
            else
            {
                locations.WarpFarmer(saved.location, tileX, tileY, facing);
                yield return null;
                yield return new WaitUntil(() => !locations.IsWarping);
            }
        }

        GameLocation here = locations.CurrentLocation;
        if (here != null && here.LocationName == saved.location && motor != null)
        {
            // WarpFarmer snaps to the map entry (FarmHouse always uses its door), so place
            // the standing collider back on the saved spot when it is still clear.
            var desired = new Vector3(saved.x, saved.y, 0f);
            if (locations.TryGetCutsceneStandingPosition(here, desired, out Vector3 standing))
            {
                Vector3 pivot = standing - (motor.StandingPosition - motor.transform.position);
                motor.transform.position = pivot;
                if (motor.body != null)
                {
                    motor.body.position = pivot;
                    motor.body.linearVelocity = Vector2.zero;
                }
                Physics2D.SyncTransforms();
            }
            motor.FaceTowards(facing);
        }

        FarmDayClock.Instance?.RestoreTimeOfDay(saved.time);
        stats?.RestoreVitals(saved.stamina, saved.health);
        GameAudio.RefreshLocationMusic();
    }

    private void Update()
    {
        if (!ready) return;
        OnlinePlayerSession current = Capture();
        if (current != null) lastSafe = current;
        if (Time.unscaledTime < nextAutosave || saving || lastSafe == null) return;
        nextAutosave = Time.unscaledTime + AutosaveSeconds;
        StartCoroutine(SaveRoutine(lastSafe));
    }

    private IEnumerator SaveRoutine(OnlinePlayerSession session)
    {
        saving = true;
        yield return api.SaveSession(session, error =>
        {
            if (error != null) Debug.LogWarning("Could not save the play session: " + error);
        });
        saving = false;
    }

    /// <summary>Current state, or null while it must not be saved (night, warps, cutscenes).</summary>
    private OnlinePlayerSession Capture()
    {
        var clock = FarmDayClock.Instance;
        var locations = LocationManager.Instance;
        GameLocation here = locations?.CurrentLocation;
        if (online?.Snapshot == null || clock == null || here == null || motor == null ||
            StartupSession.IsOpening || clock.IsChangingDay || clock.IsCutscenePaused || locations.IsWarping)
            return null;
        string name = here.LocationName;
        // Mine floors and temporary festival maps are rebuilt on entry; keep the last
        // ordinary map instead (time and energy still come from the newest capture).
        if (string.IsNullOrEmpty(name) || MineManager.IsMineFloor(name) || name.Contains("-"))
        {
            if (lastSafe == null) return null;
            lastSafe.time = clock.TimeOfDay;
            lastSafe.stamina = stats != null ? Mathf.Max(0, stats.Stamina) : lastSafe.stamina;
            lastSafe.health = stats != null ? stats.Health : lastSafe.health;
            return lastSafe;
        }
        Vector3 standing = motor.StandingPosition;
        return new OnlinePlayerSession
        {
            day = online.Snapshot.gameDay,
            location = name,
            x = standing.x,
            y = standing.y,
            facingX = motor.Facing.x,
            facingY = motor.Facing.y,
            time = clock.TimeOfDay,
            stamina = stats != null ? Mathf.Max(0, stats.Stamina) : 270,
            health = stats != null ? stats.Health : 100
        };
    }

    /// <summary>Saves synchronously; used right before leaving the GamePlay scene.</summary>
    internal void SaveNow()
    {
        if (!ready || quitSaved) return;
        OnlinePlayerSession session = Capture() ?? lastSafe;
        if (session == null) return;
        quitSaved = api.SaveSessionBlocking(session, QuitTimeoutSeconds);
    }

    private bool OnWantsToQuit()
    {
        if (quitSaved || !ready) return true;
        SaveNow();
        quitSaved = true;
        return true;
    }

    // In the editor, stopping Play mode skips wantsToQuit.
    private void OnApplicationQuit() => SaveNow();
}
