using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý kết nối tự động vào thế giới nhiều người chơi (Zero-Touch Multiplayer World Manager).
/// Người chơi chỉ cần đăng nhập: khi vào GamePlay, lớp này mở phiên thế giới và chạy vòng
/// đồng bộ hiện diện. Mỗi phản hồi được giao cho <see cref="RemoteFarmerRoster"/> (nhân vật
/// của người chơi khác) và <see cref="WorldPlayerGifts"/> (tặng quà giữa người chơi).
/// </summary>
public sealed class WorldMultiplayerManager : MonoBehaviour
{
    public static WorldMultiplayerManager Instance { get; private set; }

    [Header("Sync Tuning")]
    [Tooltip("Tần số đồng bộ vị trí (giây). 0.1s = 10 lần/giây, mượt mà và tối ưu băng thông.")]
    public float syncInterval = 0.1f;

    private readonly RemoteFarmerRoster remotes = new();
    private WorldPlayerGifts gifts;
    private StartupApiClient api;
    private OnlineGameState localPlayer;
    private Coroutine sessionStartup;
    private Coroutine syncCoroutine;
    private bool isSyncing;
    private float nextSyncErrorLog;

    internal StartupApiClient Api => api;
    internal OnlineGameState LocalState => localPlayer;
    internal RemoteFarmerRoster Remotes => remotes;

    /// <summary>
    /// True while this farmer watches a cutscene of their own. Cutscenes are private: other
    /// farmers are hidden for the watcher, and the watcher is hidden from them (see presence sync).
    /// </summary>
    internal static bool IsLocalCutscenePlaying
    {
        get
        {
            if (StartupSession.IsOpening || CommunityCenterRooms.IsCelebrating ||
                NetworkPlayerController.IsKnockedOut) return true;
            var farmer = LocationManager.Instance?.LocalFarmer;
            // Checked every frame: look the director up again only for a new farmer, or until
            // OnlineGameState has added it.
            if (farmer != cutsceneFarmer || cutsceneDirector == null)
            {
                cutsceneFarmer = farmer;
                cutsceneDirector = farmer != null ? farmer.GetComponent<CutsceneDirector>() : null;
            }
            return cutsceneDirector != null && cutsceneDirector.Service.IsBusy;
        }
    }

    private static CharacterMotor cutsceneFarmer;
    private static CutsceneDirector cutsceneDirector;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            var go = new GameObject("WorldMultiplayerManager");
            go.AddComponent<WorldMultiplayerManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        // An online farmer must keep sending presence and heartbeats while the window is
        // unfocused; otherwise the game freezes and other players see them teleport later.
        Application.runInBackground = true;
        gifts = gameObject.AddComponent<WorldPlayerGifts>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopSessionStartup();
        StopSync();
    }

    private void Update() => remotes.SetHidden(IsLocalCutscenePlaying);

    private void OnApplicationQuit()
    {
        if (api != null && api.IsSignedIn)
        {
            StartCoroutine(api.LeaveWorld());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopSessionStartup();
        if (scene.name == "GamePlay")
        {
            api = StartupSession.Api;
            sessionStartup = StartCoroutine(InitializeMultiplayerSession());
        }
        else
        {
            StopSync();
            remotes.Clear();
            if (gifts != null) gifts.ResetState();
        }
    }

    private void StopSessionStartup()
    {
        if (sessionStartup != null) StopCoroutine(sessionStartup);
        sessionStartup = null;
    }

    private IEnumerator InitializeMultiplayerSession()
    {
        // Chờ các đối tượng trong scene GamePlay khởi tạo xong
        yield return new WaitUntil(() => !StartupSession.IsOpening);

        if (api == null || !api.IsSignedIn)
        {
            Debug.Log("[WorldMultiplayer] Chưa có phiên đăng nhập, chạy ở chế độ Offline.");
            yield break;
        }

        Debug.Log("[WorldMultiplayer] Đang xác thực với Máy chủ Thế Giới...");

        StartupApiClient.WorldSessionResult sessionInfo = null;
        string fetchError = null;

        yield return api.GetWorldSession((result, err) =>
        {
            sessionInfo = result;
            fetchError = err;
        });

        if (fetchError != null || sessionInfo == null)
        {
            Debug.LogWarning("[WorldMultiplayer] Không thể lấy thông tin máy chủ từ Backend: " + fetchError);
            yield break;
        }

        if (!sessionInfo.isActive)
        {
            Debug.LogWarning("[WorldMultiplayer] Máy chủ thế giới đang tắt. Bạn đang chơi một mình.");
            yield break;
        }

        Debug.Log($"[WorldMultiplayer] API thế giới '{sessionInfo.serverName}' đang hoạt động. Bắt đầu đồng bộ hiện diện qua HTTP...");
        StartSync();
    }

    private void StartSync()
    {
        // Always restart: a reload of GamePlay (admin day jump, checkpoint) destroys the old player,
        // and a loop still holding it would stop syncing while isSyncing stays true. The server then
        // drops the stale presence after 8 s and refuses every position-checked action.
        localPlayer = FindFirstObjectByType<OnlineGameState>();
        isSyncing = true;
        if (syncCoroutine != null) StopCoroutine(syncCoroutine);
        syncCoroutine = StartCoroutine(PresenceSyncLoop());
    }

    public void StopSync()
    {
        isSyncing = false;
        localPlayer = null;
        if (syncCoroutine != null)
        {
            StopCoroutine(syncCoroutine);
            syncCoroutine = null;
        }
    }

    private IEnumerator PresenceSyncLoop()
    {
        bool requestInFlight = false;
        string lastLocation = null;

        while (isSyncing && api != null && api.IsSignedIn)
        {
            if (SceneManager.GetActiveScene().name != "GamePlay")
                break;

            if (localPlayer == null) localPlayer = FindFirstObjectByType<OnlineGameState>();
            if (localPlayer != null && !requestInFlight)
            {
                var motor = localPlayer.GetComponent<CharacterMotor>();
                Vector3 pos = motor != null ? motor.StandingPosition : localPlayer.transform.position;
                Vector2 facing = motor != null ? motor.Facing : Vector2.down;
                string location = LocationManager.Instance?.FarmerLocation?.LocationName ?? "Farm";
                if (!string.Equals(lastLocation, location, StringComparison.OrdinalIgnoreCase))
                {
                    remotes.Clear();
                    lastLocation = location;
                }

                requestInFlight = true;
                StartCoroutine(api.SyncWorldPresence(pos.x, pos.y, facing.x, facing.y, location, (response, error) =>
                {
                    requestInFlight = false;
                    // Position-checked claims fail once presence is older than 8 seconds,
                    // so a failing sync must not stay silent.
                    if (error != null && Time.unscaledTime >= nextSyncErrorLog)
                    {
                        nextSyncErrorLog = Time.unscaledTime + 10f;
                        Debug.LogWarning("[WorldMultiplayer] Presence sync failed: " + error);
                    }
                    if (error != null || response == null) return;
                    if (gifts != null) gifts.OnPresence(response);
                    // A reply for the map the farmer just left would respawn its farmers here.
                    if (string.Equals(LocationManager.Instance?.CurrentLocation?.LocationName,
                            location, StringComparison.OrdinalIgnoreCase))
                        remotes.Apply(response.players, location, localPlayer);
                }));
            }

            yield return new WaitForSeconds(syncInterval);
        }
    }
}
