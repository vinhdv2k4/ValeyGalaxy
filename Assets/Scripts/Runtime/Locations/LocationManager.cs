using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages active game locations and performs farmer warps.
/// Manages location transitions and farmer warp execution.
/// </summary>
public sealed class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    [Header("Current State")]
    [SerializeField] private GameLocation currentLocation;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterMotor playerMotor;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Transition Settings")]
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.25f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Preloaded Map Prefabs")]
    [SerializeField] private List<GameObject> preloadedMapPrefabs = new();

    private readonly Dictionary<string, GameLocation> loadedLocations = new(System.StringComparer.OrdinalIgnoreCase);
    private Canvas fadeCanvas;
    private Image fadeImage;
    private GameObject worldResources;
    private bool isWarping;
    private float nextWarpTime;
    private float lastLockedDoorNoticeTime = float.NegativeInfinity;

    public GameLocation CurrentLocation => currentLocation;
    internal event System.Action LocationChanged;
    internal void SetCutsceneFade(float alpha, Color? tint = null)
    {
        Color color = tint ?? fadeColor;
        if (fadeImage != null) fadeImage.color = new Color(color.r, color.g, color.b, alpha);
    }
    internal void SnapCutsceneCamera() => cameraFollow?.SnapToTarget();
    internal void SetCutsceneCameraTarget(Transform target) => cameraFollow?.SetTarget(target != null ? target : playerTransform);
    internal CharacterMotor LocalFarmer => playerMotor;
    public bool IsWarping => isWarping || Time.time < nextWarpTime;
    private bool warpLoading;

    /// <summary>
    /// True while a map is loading behind the black screen (the hourglass cursor shows then):
    /// during a warp's fade and black hold, or whenever the fade overlay is fully black.
    /// </summary>
    internal bool IsLoadingScreen
    {
        get
        {
            if (warpLoading) return true;
            if (fadeImage == null || !fadeImage.enabled) return false;
            Color color = fadeImage.color;
            return color.a > 0.98f && color.r + color.g + color.b < 0.3f;
        }
    }
    internal bool IsShowingCutaway { get; private set; }
    internal bool IsLocalFarmer(CharacterMotor motor) => motor != null && motor == playerMotor;

    /// <summary>
    /// A component on the farmer this client controls. Prefer this to FindFirstObjectByType:
    /// it costs one GetComponent instead of a scene search, and other farmers' avatars are
    /// clones a scene search can return instead. Null until the farmer exists.
    /// </summary>
    internal static T LocalFarmerComponent<T>() where T : Component
    {
        CharacterMotor farmer = Instance != null ? Instance.playerMotor : null;
        return farmer != null ? farmer.GetComponent<T>() : null;
    }

    // NPC schedules use the map warp graph.
    // Read prefab warp metadata without activating another map in the player's scene.
    public List<LocationWarp> FindNpcWarpRoute(string start, string destination)
    {
        if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(destination)) return null;
        if (string.Equals(start, destination, System.StringComparison.OrdinalIgnoreCase))
            return new List<LocationWarp>();
        var queue = new Queue<string>();
        var previous = new Dictionary<string, (string From, LocationWarp Warp)>(
            System.StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string location = queue.Dequeue();
            foreach (LocationWarp warp in NpcWarpsFor(location))
            {
                if (warp == null || string.IsNullOrEmpty(warp.targetName) ||
                    !visited.Add(warp.targetName)) continue;
                previous[warp.targetName] = (location, warp);
                if (string.Equals(warp.targetName, destination,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    var route = new List<LocationWarp>();
                    string cursor = destination;
                    while (!string.Equals(cursor, start,
                               System.StringComparison.OrdinalIgnoreCase))
                    {
                        var step = previous[cursor];
                        route.Add(step.Warp);
                        cursor = step.From;
                    }
                    route.Reverse();
                    return route;
                }
                queue.Enqueue(warp.targetName);
            }
        }
        return null;
    }

    private IEnumerable<LocationWarp> NpcWarpsFor(string locationName)
    {
        if (loadedLocations.TryGetValue(locationName, out GameLocation loaded) && loaded != null)
            foreach (LocationWarp warp in loaded.Warps) yield return warp;
        else
            foreach (GameObject prefab in preloadedMapPrefabs)
            {
                if (prefab == null) continue;
                GameLocation location = prefab.GetComponent<GameLocation>();
                if (location == null || !string.Equals(location.LocationName, locationName,
                        System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (LocationWarp warp in location.Warps) yield return warp;
                break;
            }
        MapDoorCatalog doors = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
        if (doors != null)
            foreach (LocationWarp warp in doors.DoorsFor(locationName)) yield return warp;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        FindDependencies();
        SetupFadeCanvas();
        RegisterExistingLocations();
    }

    private void Start()
    {
        FindDependencies();

        // If no currentLocation assigned, find one in scene
        if (currentLocation == null)
        {
            currentLocation = FindFirstObjectByType<GameLocation>();
        }

        UseRiverlandStartingFarm();
        FarmShippingBin.Attach(currentLocation);

        // CameraFollow.Awake can run before this manager has selected the active map.
        cameraFollow?.RefreshMapBounds();
        cameraFollow?.SnapToTarget();

        if (currentLocation != null)
        {
            loadedLocations[currentLocation.LocationName] = currentLocation;
        }
        if (!StartupSession.IsOpening) GameAudio.RefreshLocationMusic();
    }

    private void UseRiverlandStartingFarm()
    {
        if (currentLocation == null || currentLocation.name != "Map_Farm_FourCorners") return;

        GameObject riverlandFarm = preloadedMapPrefabs.Find(prefab =>
            prefab != null && prefab.name.Equals("Farm_Fishing", System.StringComparison.OrdinalIgnoreCase));
        if (riverlandFarm == null)
        {
            Debug.LogError("[LocationManager] Riverland Farm prefab is missing from preloaded maps.");
            return;
        }

        GameLocation formerFarm = currentLocation;
        var farmObject = Instantiate(riverlandFarm);
        farmObject.name = "Map_Farm";
        var riverlandLocation = farmObject.GetComponent<GameLocation>();
        if (riverlandLocation == null) riverlandLocation = farmObject.AddComponent<GameLocation>();
        riverlandLocation.LocationName = "Farm";

        formerFarm.GetComponent<FarmHouseExterior>()?.CopyTo(farmObject);
        formerFarm.gameObject.SetActive(false);
        currentLocation = riverlandLocation;
        loadedLocations["Farm"] = riverlandLocation;

        var spawner = formerFarm.GetComponent<FarmResourceSpawner>();
        if (spawner != null)
        {
            spawner.ClearExistingResources();
            spawner.SpawnResourcesForLocation(riverlandLocation);
        }

    }

    private void FindDependencies()
    {
        if (playerMotor == null) playerMotor = FindFirstObjectByType<CharacterMotor>();
        if (playerTransform == null && playerMotor != null) playerTransform = playerMotor.transform;
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (worldResources == null)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == "WorldResources")
                {
                    worldResources = candidate.gameObject;
                    break;
                }
            }
        }
    }

    private void RegisterExistingLocations()
    {
        foreach (var loc in FindObjectsByType<GameLocation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string locName = loc.LocationName;
            if (!string.IsNullOrEmpty(locName))
            {
                loadedLocations[locName] = loc;
            }
        }
    }

    private void SetupFadeCanvas()
    {
        if (fadeCanvas != null) return;

        var canvasObj = new GameObject("ScreenFadeCanvas");
        canvasObj.transform.SetParent(transform, false);

        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // Render on top of everything

        canvasObj.AddComponent<CanvasScaler>();

        var imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        var rect = imageObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = false;
    }

    /// <summary>
    /// Warps the player using a LocationWarp entry.
    /// </summary>
    public void WarpFarmer(LocationWarp warp)
    {
        if (warp == null || IsWarping || IsCutscenePlaying || MineManager.IsMineFloor(currentLocation?.LocationName)) return;
        if (!CanEnterStoryLocation(warp.targetName)) return;

        // Preserves the current facing unless flipFarmer is set.
        Vector2 facing = playerMotor != null ? playerMotor.Facing : Vector2.down;
        if (warp.flipFarmer) facing = -facing;

        nextWarpTime = Time.time + 0.5f;
        // Door actions (building entrances) close behind the farmer; edge and stair warps are silent.
        if (warp.fromMapAction || warp.isDoor && currentLocation != null && currentLocation.IsOutdoors)
            GameAudio.Play("doorClose");
        StartCoroutine(PerformWarpRoutine(warp.targetName, warp.targetX, warp.targetY, facing));
    }

    /// <summary>
    /// Warps the player to the specified location and tile position.
    /// </summary>
    public void WarpFarmer(string targetLocation, int tileX, int tileY, Vector2 facingDirection)
    {
        if (IsWarping || IsCutscenePlaying || !CanEnterStoryLocation(targetLocation)) return;
        nextWarpTime = Time.time + 0.5f;
        StartCoroutine(PerformWarpRoutine(targetLocation, tileX, tileY, facingDirection));
    }

    /// <summary>
    /// Warp between two halves of a cutscene: the screen is already black and stays black on
    /// arrival so the next cutscene can fade in. Returns false when the warp cannot start.
    /// </summary>
    internal bool WarpFarmerFromBlack(string targetLocation, int tileX, int tileY, Vector2 facingDirection)
    {
        if (IsWarping || IsCutscenePlaying || !CanEnterStoryLocation(targetLocation)) return false;
        nextWarpTime = Time.time + 0.5f;
        StartCoroutine(PerformWarpRoutine(targetLocation, tileX, tileY, facingDirection, stayBlack: true));
        return true;
    }

    /// <summary>A map prefab by name, for reading another version of a map (never activated).</summary>
    internal GameObject FindMapPrefab(string prefabName)
    {
        foreach (GameObject prefab in preloadedMapPrefabs)
            if (prefab != null && (prefab.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase) ||
                                   prefab.name.Equals("Map_" + prefabName, System.StringComparison.OrdinalIgnoreCase)))
                return prefab;
#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { "Assets/Prefabs/Maps" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        return null;
    }

    public void WarpFarmerToMineFloor(int floor, bool byElevator)
    {
        if (IsWarping || IsCutscenePlaying || floor < 0 || (floor > 120 && floor != 77377) ||
            !CanEnterStoryLocation("Mine")) return;
        var online = playerMotor?.GetComponent<OnlineGameState>();
        if (byElevator)
        {
            if (!MineManager.IsInMine || MineManager.CurrentFloor > 120 || floor == MineManager.CurrentFloor ||
                (floor != 0 && (floor % 5 != 0 || floor > MineManager.LastElevatorStop))) return;
        }
        else if (floor > 0)
        {
            if (!MineManager.IsInMine ||
                (floor == 77377 ? MineManager.CurrentFloor != 0 : floor != MineManager.CurrentFloor + 1)) return;
            if (floor <= 120 && online?.Snapshot is OnlineGameSnapshot snapshot &&
                MineManager.CurrentFloor > snapshot.deepestMineFloor)
            {
                online.RecordMineArrival(MineManager.CurrentFloor);
                NpcConversation.ShowNotice("Đang lưu tầng hiện tại. Hãy thử lại sau giây lát.");
                return;
            }
        }
        nextWarpTime = Time.time + 0.5f;
        StartCoroutine(PerformWarpRoutine(floor == 0 ? "Mine" : floor.ToString(), 0, 0,
            Vector2.down, true, byElevator));
    }

    private bool IsCutscenePlaying =>
        StartupSession.IsOpening || playerMotor?.GetComponent<CutsceneDirector>()?.Service.IsBusy == true;

    private bool CanEnterStoryLocation(string target)
    {
        var clock = FarmDayClock.Instance;
        var online = playerMotor?.GetComponent<OnlineGameState>();
        bool preview = false;
        bool centerSeen = online?.Snapshot?.dailyEvents?.CompletedOn("community_center") > 0;
        bool centerOpened = online?.Snapshot?.milestones?.HasSeen("community_opening") == true;
#if UNITY_EDITOR
        var qa = EventQaPreview.For(playerMotor);
        preview = qa != null;
        if (preview)
        {
            centerOpened = qa.HasMilestoneFlag("community_opening");
            centerSeen = centerOpened || qa.HasDailyFlag("community_center");
        }
#endif
        if (clock == null || online == null && !preview) return true;

        var calendar = DailyEventAssets.Calendar;
        string message = target switch
        {
            _ when (target == "Mine" || MineManager.IsMineFloor(target)) &&
                (preview ? clock.Day : online?.Snapshot?.gameDay ?? clock.Day) < calendar.Find("mine_open").day =>
                "Lối vào mỏ mở vào ngày 5 mùa xuân.",
            "Railroad" or "BathHouse_Entry" when clock.Day < calendar.Find("earthquake").day =>
                "Đá đang chắn đường lên Spa.",
            "CommunityCenter" or "CommunityCenter_Ruins" when !centerSeen =>
                "Hãy gặp Lewis tại Town vào buổi sáng trời quang, từ ngày 5 mùa xuân.",
            // After Morris's ceremony the old center is a Joja warehouse (no interior map yet).
            "CommunityCenter" or "CommunityCenter_Ruins" when online?.Snapshot?.milestones?.HasSeen("joja_opening") == true =>
                "Đây giờ là nhà kho của Joja. Cửa đã khóa.",
            "JojaMart" when centerOpened =>
                "JojaMart đã đóng cửa sau lễ khánh thành Trung tâm Cộng đồng.",
            _ => null
        };
        if (message == null) return true;
        if (Time.unscaledTime - lastLockedDoorNoticeTime >= 1.5f && !NpcConversation.IsOpen)
        {
            lastLockedDoorNoticeTime = Time.unscaledTime;
            NpcConversation.ShowNotice(message);
        }
        return false;
    }

    public bool TryUseDoor(Vector2? pointer, Vector3 standingPosition, Vector2 facing)
    {
        if (IsWarping || currentLocation == null) return false;
        if (!IsCutscenePlaying && currentLocation.InteriorDoors?.TryUse(pointer, standingPosition, facing,
            playerMotor?.GetComponent<OnlineGameState>()) == true) return true;
        foreach (LocationWarp door in currentLocation.Warps)
        {
            if (door.npcOnly) continue;
            if (!door.fromMapAction)
            {
                if (currentLocation.LocationName == "Farm" && door.targetName == "FarmHouse")
                {
                    if (!door.IsWithinDoorInteractionRange(standingPosition, pointer.HasValue)) continue;
                    if (pointer.HasValue ? !door.ContainsDoorPointer(pointer.Value) : facing.y < 0.5f) continue;
                    WarpFarmer(door);
                    return true;
                }
                if (currentLocation.IsOutdoors ||
                    (door.targetName != "Town" && currentLocation.LocationName != "FarmHouse")) continue;
                Vector2 toExit = door.WorldPosition - standingPosition;
                if (Mathf.Abs(toExit.x) > 0.65f || toExit.y < -1.3f || toExit.y > 0.65f) continue;
                if (pointer.HasValue)
                {
                    if ((pointer.Value - (Vector2)door.WorldPosition).sqrMagnitude > 1f) continue;
                }
                else if (facing.y > -0.5f) continue;
                WarpFarmer(door);
                return true;
            }
            if (!door.IsWithinDoorInteractionRange(standingPosition, pointer.HasValue)) continue;
            if (pointer.HasValue)
            {
                if (!door.ContainsDoorPointer(pointer.Value)) continue;
            }
            else if (facing.y < 0.5f) continue;

            if (!door.IsOpenAtCurrentTime())
            {
                if (Time.unscaledTime - lastLockedDoorNoticeTime >= 1.5f && !NpcConversation.IsOpen)
                {
                    lastLockedDoorNoticeTime = Time.unscaledTime;
                    GameAudio.Play("cancel");
                    NpcConversation.ShowNotice(door.GetLockedMessage());
                }
                return true;
            }

            WarpFarmer(door);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Coroutine executing the warp transition flow:
    /// 1. Halt player
    /// 2. Fade to black
    /// 3. Hide old location, activate target location
    /// 4. Reposition player
    /// 5. Snap camera & refresh bounds
    /// 6. Fade back
    /// 7. Restore player movement
    /// </summary>
    private IEnumerator PerformWarpRoutine(string targetName, int targetX, int targetY, Vector2 facing,
        bool mineArrival = false, bool byElevator = false, System.Func<IEnumerator> morning = null,
        bool opening = false, System.Func<IEnumerator> afterMorning = null, bool stayBlack = false)
    {
        isWarping = true;
        warpLoading = true;
        FindDependencies();
        GameLocation sourceLocation = currentLocation;
        int sourceMineFloor = MineManager.CurrentFloor;

        // 1. Halt Player
        if (playerMotor != null)
        {
            playerMotor.IsMovementEnabled = false;
            playerMotor.SetMovement(Vector2.zero);
        }

        // 2. Fade Screen To Black
        if (opening || stayBlack) SetCutsceneFade(1f);
        else yield return FadeScreen(0, 1);

        // Small hold at full black
        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Switch Maps
        if (morning != null)
        {
            // The day summary is interactive, so it keeps the normal cursor.
            warpLoading = false;
            if (fadeImage != null) fadeImage.enabled = false;
            yield return morning();
            if (fadeImage != null) fadeImage.enabled = true;
            warpLoading = true;
        }
        GameLocation nextLocation = GetOrCreateLocation(targetName);
        bool mineLobbyArrival = mineArrival && targetName == "Mine";
        Vector3 minePosition = default;
        if (nextLocation == null || (mineArrival && !MineManager.TryGetArrivalPosition(nextLocation,
            mineLobbyArrival, sourceMineFloor, byElevator, out minePosition)))
        {
            Debug.LogError($"[LocationManager] Could not find a destination in {targetName}.");
            if (nextLocation != null && nextLocation != sourceLocation) nextLocation.gameObject.SetActive(false);
            if (fadeImage != null) fadeImage.color = Color.clear;
            if (playerMotor != null) playerMotor.IsMovementEnabled = true;
            isWarping = false;
            warpLoading = false;
            yield break;
        }

        if (!mineArrival)
        {
            ResolveFarmReturnPosition(sourceLocation, nextLocation, ref targetX, ref targetY);
            if (nextLocation.LocationName == "FarmHouse")
            {
                Vector2Int entry = morning != null || opening ? FarmHouseFurnishings.BedSpot(nextLocation) :
                    new Vector2Int(nextLocation.ContentMapName == "FarmHouse1" ? 9 : 3, 11);
                targetX = entry.x;
                targetY = entry.y;
            }
            // Moves arrivals at the rightmost map edge
            // one tile inward before placing the farmer.
            var back = nextLocation.transform.Find("Back");
            if (back != null && back.TryGetComponent(out UnityEngine.Tilemaps.Tilemap backMap) &&
                targetX >= backMap.cellBounds.xMax - 1)
                targetX--;
        }

        // The map owns its resources and drops, including when the map is inactive.
        if (sourceLocation != null && sourceLocation != nextLocation) sourceLocation.gameObject.SetActive(false);
        nextLocation.gameObject.SetActive(true);
        currentLocation = nextLocation;
        FarmRanchView.Refresh(nextLocation, playerMotor?.GetComponent<OnlineGameState>()?.Snapshot);
        PlacedObjectsView.Refresh(nextLocation, playerMotor?.GetComponent<OnlineGameState>()?.Snapshot);
        FarmShippingBin.Attach(nextLocation);
        CommunityCenterRooms.Attach(nextLocation);
        TownCommunityCenterFacade.Attach(nextLocation);
        ForageView.Attach(nextLocation);
        if (!opening) GameAudio.RefreshLocationMusic();
        if (int.TryParse(nextLocation.LocationName, out int activeMineFloor))
        {
            MineManager.CurrentFloor = activeMineFloor;
            MineManager.FloorVisit = UnityEngine.Random.Range(1, int.MaxValue);
        }
        else if (nextLocation.LocationName.Equals("Mine", System.StringComparison.OrdinalIgnoreCase))
            MineManager.CurrentFloor = 0;

        MineMonsterSpawner mineMonsterSpawner = null;
        if (nextLocation.IsOutdoors && FarmResourceSpawner.Instance != null)
            FarmResourceSpawner.Instance.SpawnResourcesForLocation(nextLocation);
        if (MineResourceSpawner.IsMineOrCave(nextLocation))
        {
            mineMonsterSpawner = nextLocation.GetComponent<MineMonsterSpawner>();
            if (mineMonsterSpawner == null) mineMonsterSpawner = nextLocation.gameObject.AddComponent<MineMonsterSpawner>();
            mineMonsterSpawner.PrepareFloor(MineManager.CurrentFloor, minePosition);
            MineResourceSpawner.SpawnForLocation(nextLocation, MineManager.CurrentFloor,
                mineMonsterSpawner.IsInfestedFloor);
            MineTreasure.Attach(nextLocation, MineManager.CurrentFloor, playerMotor?.GetComponent<OnlineGameState>());
        }

        if (worldResources != null)
            worldResources.SetActive(nextLocation.LocationName.Equals("Farm", System.StringComparison.OrdinalIgnoreCase));

        // 4. Reposition Player
        Vector3 targetPos = mineArrival ? minePosition : nextLocation.TileCenter(targetX, targetY);
        if (playerTransform != null)
        {
            playerMotor?.ResetAfterWarp();
            Physics2D.SyncTransforms();
            if (!TryFindSafeArrival(nextLocation, targetPos, facing, mineLobbyArrival ? 1 : 4, out Vector3 safePosition))
            {
                Debug.LogError($"[LocationManager] No clear arrival near {targetName} {targetPos}. Warp cancelled.");
                nextLocation.gameObject.SetActive(false);
                if (sourceLocation != null) sourceLocation.gameObject.SetActive(true);
                currentLocation = sourceLocation;
                MineManager.CurrentFloor = sourceMineFloor;
                if (!opening) GameAudio.RefreshLocationMusic();
                if (worldResources != null) worldResources.SetActive(sourceLocation != null && sourceLocation.LocationName == "Farm");
                if (fadeImage != null) fadeImage.color = Color.clear;
                if (playerMotor != null) playerMotor.IsMovementEnabled = true;
                isWarping = false;
                warpLoading = false;
                yield break;
            }
            targetPos = safePosition;
            // The farmer pivot is above the standing collider. Align that collider
            // with the destination tile instead of aligning the sprite pivot.
            var standingCollider = playerTransform.GetComponent<Collider2D>();
            if (standingCollider != null)
                targetPos -= standingCollider.bounds.center - playerTransform.position;
            playerTransform.position = targetPos;
            if (playerMotor != null)
            {
                if (playerMotor.body != null)
                {
                    playerMotor.body.position = targetPos;
                    playerMotor.body.linearVelocity = Vector2.zero;
                }
                playerMotor.FaceTowards(facing);
                playerMotor.ResetAfterWarp();
            }
        }

        mineMonsterSpawner?.PopulateFloor();
        if (mineArrival && !byElevator && MineManager.CurrentFloor is >= 1 and <= 120)
            playerMotor?.GetComponent<OnlineGameState>()?.RecordMineArrival(MineManager.CurrentFloor);

        // 5. Update Camera
        if (cameraFollow != null)
        {
            cameraFollow.RefreshMapBounds();
            cameraFollow.SnapToTarget();
        }

        // Wait a frame for physics and tilemaps to settle
        Physics2D.SyncTransforms();
        yield return null;

        if (afterMorning != null)
        {
            warpLoading = false;
            yield return afterMorning();
        }

        // 6. Fade Screen Back.
        // opens out of this same darkness instead of showing the map and then cutting to black.
        bool arrivalCutscene = !opening && !stayBlack && morning == null &&
            playerMotor != null && playerMotor.TryGetComponent(out CutsceneDirector director) &&
            director.HasArrivalEvent();
        warpLoading = false;
        if (!opening && !stayBlack && !arrivalCutscene) yield return FadeScreen(1, 0);

        // 7. Restore Player Movement
        if (playerMotor != null && !opening)
        {
            playerMotor.IsMovementEnabled = true;
        }

        nextWarpTime = Time.time + 0.35f;
        isWarping = false;
        LocationChanged?.Invoke();
        if (arrivalCutscene) StartCoroutine(RevealUnlessCutscene());
    }

    /// <summary>Lifts the held black if the expected arrival cutscene did not start after all.</summary>
    private IEnumerator RevealUnlessCutscene()
    {
        for (float waited = 0f; waited < 2.5f; waited += Time.unscaledDeltaTime)
        {
            if (IsCutscenePlaying || isWarping) yield break;
            yield return null;
        }
        if (!IsCutscenePlaying && !isWarping) yield return FadeScreen(1, 0);
    }

    internal IEnumerator WakeAtFarmHouse(System.Func<IEnumerator> morning, System.Func<IEnumerator> afterMorning) =>
        PerformWarpRoutine("FarmHouse", 0, 0, Vector2.down, morning: morning, afterMorning: afterMorning);

    internal System.Action ShowMountainCutaway() => ShowCutaway("Mountain");

    /// <summary>Location the farmer is actually standing in, even while another map is shown.</summary>
    internal GameLocation FarmerLocation => cutawayHome != null ? cutawayHome : currentLocation;
    private GameLocation cutawayHome;

    /// <summary>Shows another map without moving the farmer; the returned action restores the view.</summary>
    internal System.Action ShowCutaway(string locationName, bool hideFarmer = false)
    {
        var home = currentLocation;
        var shown = GetOrCreateLocation(locationName);
        if (shown == null || home == null || IsShowingCutaway) return null;
        IsShowingCutaway = true;
        cutawayHome = home;
        home.gameObject.SetActive(false);
        currentLocation = shown;
        shown.gameObject.SetActive(true);
        bool farm = shown.LocationName == "Farm";
        FarmRanchView.Refresh(shown, playerMotor?.GetComponent<OnlineGameState>()?.Snapshot);
        PlacedObjectsView.Refresh(shown, playerMotor?.GetComponent<OnlineGameState>()?.Snapshot);
        if (shown.IsOutdoors && FarmResourceSpawner.Instance != null)
            FarmResourceSpawner.Instance.SpawnResourcesForLocation(shown);
        if (worldResources != null) worldResources.SetActive(farm);
        var hidden = new List<Renderer>();
        if (hideFarmer && playerTransform != null)
            foreach (Renderer renderer in playerTransform.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
        return () =>
        {
            if (this == null) return;
            foreach (Renderer renderer in hidden) if (renderer != null) renderer.enabled = true;
            if (shown != null) shown.gameObject.SetActive(false);
            currentLocation = home;
            cutawayHome = null;
            if (home != null) home.gameObject.SetActive(true);
            if (worldResources != null) worldResources.SetActive(home != null && home.LocationName == "Farm");
            if (hideFarmer) cameraFollow?.SetTarget(playerTransform);
            cameraFollow?.RefreshMapBounds();
            cameraFollow?.SnapToTarget();
            IsShowingCutaway = false;
        };
    }

    internal IEnumerator WarpForOpening(string targetName, Vector2Int tile) =>
        PerformWarpRoutine(targetName, tile.x, tile.y, Vector2.down, opening: true);

    internal IEnumerator RevealOpening() => FadeScreen(1, 0);

    private IEnumerator FadeScreen(float from, float to)
    {
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            SetCutsceneFade(Mathf.Lerp(from, to, elapsed / fadeDuration));
            yield return null;
        }
        SetCutsceneFade(to);
    }

    internal bool TryGetCutsceneStandingPosition(GameLocation map, Vector3 desired, out Vector3 result) =>
        TryFindSafeArrival(map, desired, Vector2.down, 4, out result);

    private bool TryFindSafeArrival(GameLocation location, Vector3 desired, Vector2 facing, int searchRadius, out Vector3 result)
    {
        var back = location.transform.Find("Back")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var playerCollider = playerTransform.GetComponent<Collider2D>();
        Vector2 size = playerCollider == null ? new Vector2(0.6f, 0.4f) : (Vector2)playerCollider.bounds.size * 0.95f;
        int mask = 0;
        for (int layer = 0; layer < 32; layer++)
            if (!Physics2D.GetIgnoreLayerCollision(playerTransform.gameObject.layer, layer)) mask |= 1 << layer;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(mask);
        var hits = new Collider2D[64];
        bool Clear(Vector3 position)
        {
            if (back != null && !back.HasTile(back.WorldToCell(position))) return false;
            int count = Physics2D.OverlapBox(position, size, 0f, filter, hits);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(playerTransform)) return false;
            return true;
        }
        result = desired;
        if (Clear(desired)) return true;
        for (int radius = 1; radius <= searchRadius; radius++)
        {
            Vector3 forward = desired + (Vector3)(facing.normalized * radius);
            if (Clear(forward)) { result = forward; return true; }
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                Vector3 candidate = desired + new Vector3(x, y, 0f);
                if (Clear(candidate)) { result = candidate; return true; }
            }
        }
        return false;
    }

    // Remaps these default map warps to the selected farm layout.
    private void ResolveFarmReturnPosition(GameLocation source, GameLocation destination, ref int x, ref int y)
    {
        if (source == null || destination == null || destination.LocationName != "Farm") return;

        string sourceName = source.LocationName;
        var snapshot = playerMotor?.GetComponent<OnlineGameState>()?.Snapshot;
        string ranchKind = FarmRanchAssets.KindFor(sourceName);
        OnlineRanchHome ranch = snapshot?.RanchHomeById(FarmRanchAssets.BuildingId(sourceName)) ??
            (ranchKind == null ? null : snapshot?.ranchHomes?.FirstOrDefault(home => home.kind == ranchKind));
        if (ranch != null)
        {
            x = ranch.DoorX;
            y = ranch.DoorY;
            return;
        }
        if (sourceName == "FarmHouse" && x == 64 && y == 15)
        {
            // Leaving the house puts the farmer on the porch tile in front of the door, the same
            // tile whose contact triggers the door when walking up (the farm's entry point).
            foreach (LocationWarp entry in destination.Warps)
            {
                if (entry.targetName == "FarmHouse")
                {
                    x = entry.x;
                    y = entry.y;
                    break;
                }
            }
        }
        else if (sourceName == "FarmCave" && x == 34 && y == 6)
        {
            foreach (LocationWarp entry in destination.Warps)
            {
                if (entry.targetName == "FarmCave")
                {
                    x = entry.x;
                    y = entry.y + 1;
                    return;
                }
            }
        }
        else if (sourceName == "Forest" && x == 41 && y == 64 &&
                 destination.gameObject.name.Contains("FourCorners"))
        {
            // whichFarm == 5 returns at (40, 64).
            x = 40;
        }
    }

    private GameLocation GetOrCreateLocation(string targetName)
    {
        string prefabName = targetName.Equals("CommunityCenter", System.StringComparison.OrdinalIgnoreCase)
            ? "CommunityCenter_Ruins" : targetName;
        bool restoredCenter = targetName == "CommunityCenter" &&
            playerMotor?.GetComponent<OnlineGameState>()?.Snapshot?.milestones?.HasSeen("community_opening") == true;
#if UNITY_EDITOR
        if (targetName == "CommunityCenter" && EventQaPreview.For(playerMotor)?.HasMilestoneFlag("community_opening") == true)
            restoredCenter = true;
#endif
        if (restoredCenter) prefabName = "CommunityCenter_Refurbished";
        bool upgradedHouse = false;
        if (targetName == "FarmHouse")
        {
            var state = playerMotor?.GetComponent<OnlineGameState>()?.Snapshot;
            int day = FarmDayClock.Instance?.Day ?? state?.gameDay ?? 0;
            upgradedHouse = state != null && state.houseUpgradeDay > 0 && day >= state.houseUpgradeDay;
            if (upgradedHouse) prefabName = "FarmHouse1";
        }
        bool useVariant = upgradedHouse || restoredCenter;
        string cacheKey = useVariant ? prefabName : targetName;
        // Numbered mine assets are room templates; logical floors continue beyond the available templates.
        bool numberedMine = MineManager.IsMineFloor(targetName);
        if (numberedMine && int.TryParse(targetName, out int mineFloor))
            prefabName = MineManager.RoomTemplate(mineFloor).ToString();
        // Check loaded dictionary
        if (loadedLocations.TryGetValue(cacheKey, out var loc) && loc != null)
        {
            return loc;
        }

        GameObject ranchInterior = FarmRanchAssets.Interior(prefabName);
        if (ranchInterior != null)
        {
            var room = Instantiate(ranchInterior).GetComponent<GameLocation>();
            room.name = targetName;
            room.ContentMapName = ranchInterior.name;
            room.LocationName = targetName;
            loadedLocations[cacheKey] = room;
            return room;
        }

        // Check if map exists in scene with "Map_" prefix or direct name
        foreach (var sceneLoc in FindObjectsByType<GameLocation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (useVariant ? sceneLoc.ContentMapName == prefabName :
                sceneLoc.LocationName.Equals(targetName, System.StringComparison.OrdinalIgnoreCase) ||
                sceneLoc.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase) ||
                sceneLoc.name.Equals("Map_" + targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                loadedLocations[cacheKey] = sceneLoc;
                return sceneLoc;
            }
        }

        // Check preloaded prefabs
        for (int i = 0; i < preloadedMapPrefabs.Count; i++)
        {
            var p = preloadedMapPrefabs[i];
            if (p != null && (p.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase) ||
                              p.name.Equals("Map_" + prefabName, System.StringComparison.OrdinalIgnoreCase)))
            {
                var instance = Instantiate(p);
                instance.name = targetName;
                var gLoc = instance.GetComponent<GameLocation>() ?? instance.AddComponent<GameLocation>();
                gLoc.LocationName = targetName;
                loadedLocations[cacheKey] = gLoc;
                return gLoc;
            }
        }

#if UNITY_EDITOR
        // In Editor, dynamically search recursively under Assets/Prefabs/Maps
        string cleanTarget = prefabName.Replace("Map_", "");
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{cleanTarget} t:Prefab", new[] { "Assets/Prefabs/Maps" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (fileName.Equals(cleanTarget, System.StringComparison.OrdinalIgnoreCase) ||
                (!numberedMine && !useVariant && fileName.Equals(targetName, System.StringComparison.OrdinalIgnoreCase)) ||
                fileName.Equals("Farm_" + cleanTarget, System.StringComparison.OrdinalIgnoreCase))
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var instance = Instantiate(prefab);
                    instance.name = targetName;
                    var gLoc = instance.GetComponent<GameLocation>() ?? instance.AddComponent<GameLocation>();
                    gLoc.LocationName = targetName;
                    loadedLocations[cacheKey] = gLoc;
                    return gLoc;
                }
            }
        }
#endif

        return null;
    }
}
