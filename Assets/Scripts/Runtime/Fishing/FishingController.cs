using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Complete Fishing Controller.
/// Controls casting charge, bobber trajectory, waiting for bites, "!" alert,
/// BobberBar slider minigame, stamina bar, and catching celebration rewards.
/// Uses clean Prefabs for world visuals and pixel-perfect UI rendering.
/// </summary>
public sealed class FishingController : MonoBehaviour
{
    public enum FishingState
    {
        Inactive,
        Charging,
        Casting,
        Waiting,
        Biting,
        Minigame,
        Celebration,
        Reeling,
        LandingCatch
    }

    [Header("Component References")]
    [SerializeField] private CharacterMotor motor;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerAnimationController playerAnimation;
    [SerializeField] private Texture2D cursorsTexture;
    [SerializeField] private WaterDetector waterDetector;
    [SerializeField] private NetworkCharacterStats characterStats;
    private PlayerAppearance appearance;

    [Header("Prefab References")]
    [SerializeField] private GameObject bobberPrefab;
    [SerializeField] private GameObject splashPrefab;
    [SerializeField] private GameObject linePrefab;

    [Header("Fishing Settings")]
    [SerializeField] private float minCastDistance = 2f;
    [SerializeField] private float maxCastDistance = 3.5f;
    [SerializeField] private float biteWaitMin = 0.5f;
    [SerializeField] private float biteWaitMax = 12f;
    [SerializeField] private float biteReactionWindow = 1.0f;

    public FishingState State { get; private set; } = FishingState.Inactive;
    public bool IsFishing => State != FishingState.Inactive;
    internal bool IsShowingCatch => State is FishingState.LandingCatch or FishingState.Celebration;

    // Casting
    private float chargeTimer;
    private float castingPower;
    private Vector2 originalFacing;

    // Bobber & Line instances (from Prefabs)
    private GameObject bobberObj;
    private SpriteRenderer bobberRenderer;
    private Sprite bobberSprite;
    private FishingSplashEffect splashEffect;
    private LineRenderer fishingLine;
    private Vector3 bobberTargetPos;
    private float nextBobTime;
    private bool pendingAction;

    // Bite & Hook
    private float biteTimer;
    private float biteReactionTimer;
    private bool biteAlertActive;

    // Minigame
    private BobberBarMinigame minigame;
    private FishData targetFish;
    private string fishAttemptId;
    private int caughtFishSize;
    private bool wasPerfectCatch;

    // Celebration
    private ItemData caughtItem;
    private float celebrationStartedAt;
    private bool wasNewRecord;
    private bool catchSaving;
    private string catchSaveMessage;

    // Stamina UI
    private float staminaShakeTimer;

    // Sprite rects in Cursors.png
    private static readonly Rect BubbleRect = new(652, 1685, 52, 157);
    private static readonly Rect BgRect = new(644, 1999, 37, 150);
    private static readonly Rect BarTopRect = new(682, 2078, 9, 2);
    private static readonly Rect BarMidRect = new(682, 2081, 9, 1);
    private static readonly Rect BarBotRect = new(682, 2085, 9, 2);
    private static readonly Rect ReelRect = new(257, 1990, 5, 10);
    private static readonly Rect FishRect = new(614, 1840, 20, 20);
    private static readonly Rect BossFishRect = new(634, 1840, 20, 20);
    private static readonly Rect BiteAlertRect = new(395, 497, 3, 8);
    private static readonly Rect CastBarRect = new(193, 1868, 47, 12);
    private static readonly Rect StaminaTopRect = new(256, 408, 12, 16);
    private static readonly Rect StaminaMidRect = new(256, 424, 12, 16);
    private static readonly Rect StaminaBotRect = new(256, 448, 12, 16);
    private static readonly Rect CatchDialogRect = new(31, 1870, 73, 49);

    private void Awake()
    {
        if (motor == null) motor = GetComponent<CharacterMotor>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimationController>();
        if (characterStats == null) characterStats = GetComponent<NetworkCharacterStats>();
        appearance = GetComponent<PlayerAppearance>();

        if (waterDetector == null)
            waterDetector = GetComponent<WaterDetector>() ?? gameObject.AddComponent<WaterDetector>();

        ResolvePrefabs();
        InitializeInstances();
        FishDatabase.Initialize();
    }

    public void Configure(Texture2D cursors, Texture2D animations = null)
    {
        if (cursors != null) cursorsTexture = cursors;
        ResolvePrefabs();
        InitializeInstances();
    }

    private void ResolvePrefabs()
    {
        if (cursorsTexture == null) cursorsTexture = StardewUiAtlas.Cursors;
#if UNITY_EDITOR
        if (bobberPrefab == null)
            bobberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fishing/FishingBobber.prefab");
        if (splashPrefab == null)
            splashPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fishing/FishingSplash.prefab");
        if (linePrefab == null)
            linePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fishing/FishingLine.prefab");
#endif
    }

    private void InitializeInstances()
    {
        // 1. Bobber
        if (bobberObj == null && bobberPrefab != null)
        {
            bobberObj = Instantiate(bobberPrefab);
            bobberObj.name = "FishingBobber";
            bobberRenderer = bobberObj.GetComponent<SpriteRenderer>();
            bobberSprite = bobberRenderer != null ? bobberRenderer.sprite : null;
            bobberObj.SetActive(false);
        }

        // 2. Splash (delicate, properly sized)
        if (splashEffect == null && splashPrefab != null)
        {
            var spGo = Instantiate(splashPrefab);
            spGo.name = "FishingSplash";
            splashEffect = spGo.GetComponent<FishingSplashEffect>();
            spGo.SetActive(false);
        }

        // 3. Line
        if (fishingLine == null && linePrefab != null)
        {
            var lineGo = Instantiate(linePrefab);
            lineGo.name = "FishingLine";
            fishingLine = lineGo.GetComponent<LineRenderer>();
            fishingLine.enabled = false;
        }
    }

    public void NotifyActionPressed() => pendingAction = true;

    public void StartCharging(Vector2? pointer, Vector2 facingBeforeCast)
    {
        if (State != FishingState.Inactive) return;
        // FishingRod.beginUsing: no cast at 1 energy or less.
        if (characterStats != null && characterStats.ExactStamina <= 1f)
        {
            NpcConversation.ShowNotice("Bạn quá mệt để câu cá.");
            return;
        }

        State = FishingState.Charging;
        originalFacing = facingBeforeCast;
        chargeTimer = 0f;
        castingPower = 0f;
        if (motor != null) motor.IsMovementEnabled = false;
        if (playerAnimation != null) playerAnimation.HoldFishingCast();
    }

    private void Update()
    {
        if (staminaShakeTimer > 0f)
            staminaShakeTimer -= Time.deltaTime;

        switch (State)
        {
            case FishingState.Charging:
                UpdateCharging();
                break;
            case FishingState.Waiting:
                UpdateWaiting();
                break;
            case FishingState.Biting:
                UpdateBiting();
                break;
            case FishingState.Minigame:
                UpdateMinigame();
                break;
            case FishingState.Celebration:
                UpdateCelebration();
                break;
        }
    }

    // Cast power sweeps 0 -> 1 in one second and back, with a rising SinWave tone.
    private void UpdateCharging()
    {
        chargeTimer += Time.deltaTime;
        castingPower = Mathf.PingPong(chargeTimer, 1f);
        UpdateChargeTone(true);

        bool buttonHeld = Mouse.current != null &&
                (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)
            || Keyboard.current != null && Keyboard.current.spaceKey.isPressed
            || Gamepad.current != null && Gamepad.current.buttonWest.isPressed;

        if (!buttonHeld)
        {
            StartCoroutine(ExecuteCast(castingPower));
        }
    }

    private AudioSource chargeTone;
    private float chosenBarPower, chosenBarUntil, maxPopupStart = -1f;
    private const float ChosenBarSeconds = 0.35f;

    private void UpdateChargeTone(bool charging)
    {
        if (!charging)
        {
            if (chargeTone != null && chargeTone.isPlaying) chargeTone.Stop();
            return;
        }
        if (chargeTone == null)
        {
            AudioClip clip = GameAudio.Clip("SinWave");
            if (clip == null) return;
            chargeTone = gameObject.AddComponent<AudioSource>();
            chargeTone.clip = clip;
            chargeTone.loop = true;
            chargeTone.playOnAwake = false;
            chargeTone.spatialBlend = 0f;
        }
        chargeTone.volume = GameAudio.SoundVolume * 0.5f;
        // Pitch rises up to 2400 cents (two octaves) at full power.
        chargeTone.pitch = Mathf.Min(3f, Mathf.Pow(2f, 2400f * castingPower / 1200f));
        if (!chargeTone.isPlaying) chargeTone.Play();
    }

    private IEnumerator ExecuteCast(float power)
    {
        State = FishingState.Casting;
        UpdateChargeTone(false);
        // The bar stays on screen for 350 ms with the chosen power; a full cast shows "MAX".
        chosenBarPower = power;
        chosenBarUntil = Time.time + ChosenBarSeconds;
        if (power > 0.99f)
        {
            maxPopupStart = Time.time + 0.2f;
            StartCoroutine(PlayCritAfterDelay());
        }
        GameAudio.Play("cast");
        float chosenPower = power;
        castingPower = 0f;

        if (playerAnimation != null)
            playerAnimation.Play(FarmerAnimationAction.Fishing);

        yield return new WaitForSeconds(0.22f);

        Vector2 facing = motor != null ? motor.Facing : Vector2.down;
        int fishingLevel = characterStats?.FishingBonus ?? 0;
        int addedDistance = fishingLevel >= 15 ? 4 : fishingLevel >= 8 ? 3 : fishingLevel >= 4 ? 2 : fishingLevel >= 1 ? 1 : 0;
        float distance = Mathf.Lerp(minCastDistance, maxCastDistance + addedDistance, chosenPower);

        Vector3 standing = motor != null ? motor.StandingPosition : transform.position;
        facing = Mathf.Abs(facing.x) > Mathf.Abs(facing.y)
            ? new Vector2(Mathf.Sign(facing.x), 0f)
            : new Vector2(0f, Mathf.Sign(facing.y));

        Vector3 start = GetRodTipWorldPosition();

        if (characterStats != null)
        {
            // FishingRod: 8 energy less 0.1 per Fishing level.
            if (!characterStats.TrySpendStamina(NetworkCharacterStats.ToolStaminaCost(8f, characterStats.FishingBonus)))
            {
                EndFishingSession();
                yield break;
            }
            staminaShakeTimer = 0.5f;
        }

        bool landsInWater = TryFindWaterLanding(standing, facing, distance, out bobberTargetPos);
        if (!landsInWater)
        {
            bobberTargetPos = standing + (Vector3)(facing * distance);
        }

        // Show bobber and fishing line immediately during cast flight
        if (bobberObj != null)
        {
            bobberObj.SetActive(true);
            bobberObj.transform.position = start;
            if (bobberRenderer != null)
                bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 10);
        }
        if (fishingLine != null) fishingLine.enabled = true;

        // Parabolic arc flight
        float elapsed = 0f;
        const float castDuration = 0.55f;
        while (elapsed < castDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / castDuration);
            float arc = Mathf.Sin(t * Mathf.PI) * 1.6f;
            if (bobberObj != null)
            {
                bobberObj.transform.position = Vector3.Lerp(start, bobberTargetPos, t) + Vector3.up * arc;
                if (bobberRenderer != null)
                    bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberObj.transform.position.y, 0f, 10);
            }
            yield return null;
        }

        if (bobberObj != null)
        {
            bobberObj.transform.position = bobberTargetPos;
            if (bobberRenderer != null)
                bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 10);
        }

        if (!landsInWater)
        {
            // Hit dry ground: pause briefly, then reel in
            yield return new WaitForSeconds(0.25f);
            StartCoroutine(ReelIn());
            yield break;
        }

        // Hit water: Enter Waiting State
        State = FishingState.Waiting;
        if (playerAnimation != null) playerAnimation.HoldFishingCast(true);
        nextBobTime = Time.time + Random.Range(2.0f, 4.0f);
        PlaySplash();
        biteTimer = Random.Range(biteWaitMin, biteWaitMax);
    }

    private bool TryFindWaterLanding(Vector3 origin, Vector2 direction, float preferredDistance, out Vector3 landing)
    {
        landing = default;
        if (waterDetector == null) return false;

        // 1. Direct candidate
        Vector3 candidate = origin + (Vector3)(direction * preferredDistance);
        if (waterDetector.IsWater(candidate))
        {
            landing = candidate;
            return true;
        }

        // 2. Wide search in the forward cone/quadrant to accommodate pond boundaries,
        // shorelines, and vertical cliff offsets when casting left/right or up/down:
        Vector2 perp = new Vector2(-direction.y, direction.x);
        float bestDistDiff = float.MaxValue;
        bool found = false;

        // Scan distances from 0.8f up to preferredDistance + 1.6f
        for (float d = 0.8f; d <= preferredDistance + 1.6f; d += 0.35f)
        {
            // Scan lateral offsets (perpendicular to cast direction)
            for (float lat = -1.5f; lat <= 1.5f; lat += 0.35f)
            {
                Vector3 testPt = origin + (Vector3)(direction * d + perp * lat);
                if (waterDetector.IsWater(testPt))
                {
                    float diff = Mathf.Abs(d - preferredDistance) + Mathf.Abs(lat) * 0.6f;
                    if (diff < bestDistDiff)
                    {
                        bestDistDiff = diff;
                        landing = testPt;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    private void UpdateWaiting()
    {
        // Gentle bobbing on water
        float bobberFloatOffset = Mathf.Sin(Time.time * 3.5f) * 0.06f;
        if (bobberObj != null)
        {
            bobberObj.transform.position = bobberTargetPos + Vector3.up * bobberFloatOffset;
            if (bobberRenderer != null)
                bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 10);
        }

        if (Time.time >= nextBobTime)
        {
            PlaySplash();
            nextBobTime = Time.time + Random.Range(2.5f, 4.5f);
        }

        // Manual early reel back
        if (WasActionPressed())
        {
            StartCoroutine(ReelIn());
            return;
        }

        biteTimer -= Time.deltaTime;
        if (biteTimer <= 0f)
        {
            // Fish bites!
            State = FishingState.Biting;
            GameAudio.Play("fishBite");
            biteReactionTimer = biteReactionWindow;
            biteAlertActive = true;
            PlaySplash();
            nextBobTime = Time.time + 0.65f;
        }
    }

    private void UpdateBiting()
    {
        // Jerking bobber animation
        float jerk = (Mathf.PingPong(Time.time * 15f, 1f) - 0.5f) * 0.14f;
        if (bobberObj != null)
        {
            bobberObj.transform.position = bobberTargetPos + Vector3.up * (-0.08f + jerk);
            if (bobberRenderer != null)
                bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 10);
        }

        if (Time.time >= nextBobTime)
        {
            PlaySplash();
            nextBobTime = Time.time + 0.65f;
        }

        biteReactionTimer -= Time.deltaTime;

        if (WasActionPressed())
        {
            // Hooked successfully! Start minigame!
            biteAlertActive = false;
            StartMinigame();
            return;
        }

        if (biteReactionTimer <= 0f)
        {
            biteAlertActive = false;
            State = FishingState.Waiting;
            biteTimer = Random.Range(biteWaitMin, biteWaitMax);
        }
    }

    private void StartMinigame()
    {
        State = FishingState.Minigame;
        playerAnimation?.HoldFishingReel();
        GameAudio.Play("FishHit");
        var online = GetComponent<OnlineGameState>();
        if (online == null) { CancelFishing("Đăng nhập để câu cá."); return; }
        online.BeginFishAttempt((attempt, error) =>
        {
            if (State != FishingState.Minigame) return;
            if (!string.IsNullOrEmpty(error) || attempt == null ||
                string.IsNullOrEmpty(attempt.fishItemId) ||
                !attempt.fishItemId.StartsWith("fish_", System.StringComparison.Ordinal) ||
                !int.TryParse(attempt.fishItemId.Substring(5), out int fishId))
            {
                CancelFishing(error ?? "Không thể bắt đầu lượt câu cá.");
                return;
            }
            targetFish = FishDatabase.GetFish(fishId);
            if (targetFish == null || targetFish.id != fishId)
            {
                CancelFishing("Loại cá không có trong dữ liệu trò chơi.");
                return;
            }
            fishAttemptId = attempt.attemptId;
            caughtFishSize = attempt.size;
            minigame = new BobberBarMinigame(targetFish,
                characterStats == null ? 0 : characterStats.FishingBonus);
        });

        // Keep bobber & line active in water during minigame!
        if (bobberObj != null) bobberObj.SetActive(true);
        if (fishingLine != null) fishingLine.enabled = true;
    }

    private System.Action<bool> festivalCatch;

    internal bool StartFestivalCatch(System.Action<bool> finished)
    {
        if (State != FishingState.Inactive) return false;
        FishData fish = FishDatabase.GetFish(145); // Sunfish, manageable with the festival's loaned rod.
        if (fish == null) return false;
        if (motor != null) originalFacing = motor.Facing;
        festivalCatch = finished;
        minigame = new BobberBarMinigame(fish, characterStats?.FishingBonus ?? 0);
        State = FishingState.Minigame;
        playerAnimation?.HoldFishingReel();
        GameAudio.Play("FishHit");
        return true;
    }

    private void UpdateMinigame()
    {
        if (minigame == null) return;

        // Bobber tugs in water during struggle
        if (bobberObj != null)
        {
            float tug = Mathf.Sin(Time.time * 12f) * 0.07f * (minigame.IsFishInBar ? 0.6f : 1.2f);
            bobberObj.transform.position = bobberTargetPos + Vector3.up * tug;
            if (bobberRenderer != null)
                bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 10);
        }

        bool isPulling = Mouse.current != null && Mouse.current.leftButton.isPressed
            || Keyboard.current != null && Keyboard.current.spaceKey.isPressed
            || Gamepad.current != null && Gamepad.current.buttonWest.isPressed;

        minigame.Update(Time.deltaTime, isPulling);

        if (minigame.IsGameOver)
        {
            if (festivalCatch != null)
            {
                var finished = festivalCatch;
                bool caught = minigame.IsSuccess;
                festivalCatch = null;
                CancelFishing();
                finished(caught);
                return;
            }
            if (minigame.IsSuccess)
            {
                OnCatchSuccess();
            }
            else
            {
                GameAudio.Play("fishEscape");
                CancelFishing("Cá đã thoát mất!");
            }
        }
    }

    private void OnCatchSuccess()
    {
        State = FishingState.LandingCatch;
        GameAudio.Play("pullItemFromWater");
        wasPerfectCatch = minigame != null && minigame.IsPerfect;
        if (wasPerfectCatch)
            GameAudio.Play("shiny4");
        pendingAction = false;

        caughtItem = FishDatabase.CreateFishItem(targetFish, caughtFishSize, wasPerfectCatch);
        playerAnimation?.HoldFishingPull();
        StartCoroutine(AnimateCatch(caughtItem.GetIcon()));
    }

    private Vector3 CatchPosition => (motor != null ? motor.StandingPosition : transform.position + Vector3.down * 0.75f)
        + PlayerAppearance.CatchOffsetFromFeet;

    private IEnumerator AnimateCatch(Sprite fish)
    {
        Vector3 start = bobberTargetPos;
        Vector3 end = CatchPosition;
        float duration = Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(2f, 4f, Vector2.Distance(start, end)));
        float height = Mathf.Abs(originalFacing.x) > 0.5f ? 1.8f : 2.8f;
        if (bobberRenderer != null && fish != null) bobberRenderer.sprite = fish;

        // The catch follows an arc back to the farmer while the rod stays in the pull pose.
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (State != FishingState.LandingCatch) yield break;
            float t = elapsed / duration;
            if (bobberObj != null)
            {
                Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
                bobberObj.transform.position = position;
                if (bobberRenderer != null)
                    bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(Mathf.Min(start.y, end.y), 0f, 10);
            }
            yield return null;
        }

        if (State != FishingState.LandingCatch) yield break;
        if (bobberObj != null) bobberObj.SetActive(false);
        if (bobberRenderer != null) bobberRenderer.sprite = bobberSprite;
        if (fishingLine != null) fishingLine.enabled = false;
        GameAudio.Play("tinyWhip");
        ShowCatchResult();
    }

    private void ShowCatchResult()
    {
        State = FishingState.Celebration;
        celebrationStartedAt = Time.unscaledTime;
        pendingAction = false;
        var item = caughtItem;
        appearance?.ShowCatchPose(item.GetIcon(), targetFish.id);
        string playerKey = StartupSession.Api == null ? "local" : StartupSession.Api.UserName ?? "local";
        string recordKey = $"FishRecord.{playerKey}.{targetFish.id}";
        int previousSize = PlayerPrefs.GetInt(recordKey, 0);
        int size = caughtFishSize;
        string attempt = fishAttemptId;
        wasNewRecord = previousSize > 0 && size > previousSize;
        var onlineGame = GetComponent<OnlineGameState>();
        if (onlineGame != null)
        {
            catchSaving = true;
            catchSaveMessage = "Đang lưu cá...";
            onlineGame.SaveFishCatch(item, attempt, error =>
            {
                bool inventoryFull = !string.IsNullOrEmpty(error) &&
                    error.IndexOf("full", System.StringComparison.OrdinalIgnoreCase) >= 0;
                FinishCatch(error, inventoryFull);
            });
        }
        else FinishCatch(null, inventory == null || !inventory.AddItem(item));

        void FinishCatch(string error, bool inventoryFull)
        {
            bool received = !inventoryFull && string.IsNullOrEmpty(error);
            if (received && size > previousSize)
            {
                PlayerPrefs.SetInt(recordKey, size);
                PlayerPrefs.Save();
            }

            if (State != FishingState.Celebration || fishAttemptId != attempt) return;
            catchSaving = false;
            catchSaveMessage = inventoryFull ? "Túi đầy. Cá rơi gần bạn." : error;
            if (inventoryFull)
                WorldItemDrop.Spawn(item, 1, transform.position, Vector2.up, size, attempt);
            if (received)
                GameAudio.Play(wasNewRecord ? "newRecord" : "fishSlap");
        }
    }

    private void UpdateCelebration()
    {
        bool actionPressed = WasActionPressed();
        if (catchSaving || Time.unscaledTime < celebrationStartedAt + 0.35f) return;

        var keyboard = Keyboard.current;
        if (!actionPressed && keyboard?.escapeKey.wasPressedThisFrame != true &&
            keyboard?.enterKey.wasPressedThisFrame != true && Gamepad.current?.buttonSouth.wasPressedThisFrame != true)
            return;
        InputController.SuppressWorldUseAfterModal();
        EndFishingSession();
    }

    public void CancelFishing(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) NpcConversation.ShowNotice(message);
        if (bobberObj != null) bobberObj.SetActive(false);
        if (fishingLine != null) fishingLine.enabled = false;
        biteAlertActive = false;
        minigame = null;
        EndFishingSession();
    }

    private void EndFishingSession()
    {
        UpdateChargeTone(false);
        if (State == FishingState.Celebration) appearance?.ClearDisplayedItem();
        State = FishingState.Inactive;
        festivalCatch = null;
        pendingAction = false;
        minigame = null;
        caughtItem = null;
        catchSaving = false;
        catchSaveMessage = null;
        fishAttemptId = null;
        if (bobberObj != null) bobberObj.SetActive(false);
        if (bobberRenderer != null) bobberRenderer.sprite = bobberSprite;
        if (fishingLine != null) fishingLine.enabled = false;
        if (motor != null) motor.IsMovementEnabled = true;
        if (motor != null) motor.FaceTowards(originalFacing);
        if (playerAnimation != null) playerAnimation.StopAction();
    }

    /// <summary>
    /// Calculates the exact world position of the fishing rod tip based directly on the
    /// FishingRod formulas across all animation frames and states
    /// (casting swing frames 0-4, waiting/fishing hold pose, and reeling).
    /// </summary>
    public Vector3 GetRodTipWorldPosition()
    {
        Vector3 standing = motor != null ? motor.StandingPosition : (transform.position + new Vector3(0f, -0.75f, 0f));
        Vector2 facing = motor != null ? motor.Facing : (originalFacing != Vector2.zero ? originalFacing : Vector2.down);

        // Source facing: 0 = Up, 1 = Right, 2 = Down, 3 = Left
        int sdvDir;
        if (facing.y > 0.5f) sdvDir = 0;       // Up
        else if (facing.x > 0.5f) sdvDir = 1;  // Right
        else if (facing.x < -0.5f) sdvDir = 3; // Left
        else sdvDir = 2;                       // Down

        int frame = 4;
        if (playerAnimation != null)
        {
            frame = Mathf.Clamp(ToolAnimationTimeline.FishingFrame(playerAnimation.ActionProgress), 0, 4);
        }

        Vector2 sdvOffset;
        if (State == FishingState.Minigame)
        {
            // Fighting the fish: the rod is held up close to the body and the line is straight.
            switch (sdvDir)
            {
                case 0: sdvOffset = new Vector2(25f, -92f); break;  // Up
                case 1: sdvOffset = new Vector2(28f, -104f); break; // Right
                case 2: sdvOffset = new Vector2(12f, -92f); break;  // Down
                case 3: default: sdvOffset = new Vector2(36f, -104f); break; // Left
            }
        }
        else if (State == FishingState.Reeling)
        {
            // Pulling the bobber out of the water: the rod swings back over the shoulder.
            switch (sdvDir)
            {
                case 0: sdvOffset = new Vector2(22f, -92f); break;   // Up
                case 1: sdvOffset = new Vector2(-48f, -104f); break; // Right
                case 2: sdvOffset = new Vector2(8f, -92f); break;    // Down
                case 3: default: sdvOffset = new Vector2(112f, -104f); break; // Left
            }
        }
        else if (State is FishingState.Casting or FishingState.LandingCatch)
        {
            // Cast swing flight animation frames 0..4
            if (State == FishingState.LandingCatch) frame = 0;
            switch (sdvDir)
            {
                case 0: // UP
                    switch (frame)
                    {
                        case 0: sdvOffset = new Vector2(22f, -92f); break;
                        case 1: sdvOffset = new Vector2(32f, -92f); break;
                        case 2: sdvOffset = new Vector2(36f, -24f); break;
                        case 3: sdvOffset = new Vector2(36f, -16f); break;
                        default: sdvOffset = new Vector2(36f, -32f); break;
                    }
                    break;
                case 1: // RIGHT
                    switch (frame)
                    {
                        case 0: sdvOffset = new Vector2(-48f, -104f); break;
                        case 1: sdvOffset = new Vector2(-16f, -116f); break;
                        case 2: sdvOffset = new Vector2(84f, -116f); break;
                        case 3: sdvOffset = new Vector2(112f, -52f); break;
                        default: sdvOffset = new Vector2(120f, -24f); break;
                    }
                    break;
                case 2: // DOWN
                    switch (frame)
                    {
                        case 0: sdvOffset = new Vector2(8f, -92f); break;
                        case 1: sdvOffset = new Vector2(22f, -92f); break;
                        case 2: sdvOffset = new Vector2(28f, -24f); break;
                        case 3: sdvOffset = new Vector2(28f, -8f); break;
                        default: sdvOffset = new Vector2(28f, 32f); break;
                    }
                    break;
                case 3: // LEFT
                default:
                    switch (frame)
                    {
                        case 0: sdvOffset = new Vector2(112f, -104f); break;
                        case 1: sdvOffset = new Vector2(80f, -116f); break;
                        case 2: sdvOffset = new Vector2(-20f, -116f); break;
                        case 3: sdvOffset = new Vector2(-48f, -52f); break;
                        default: sdvOffset = new Vector2(-56f, -24f); break;
                    }
                    break;
            }
        }
        else
        {
            // Fishing / Waiting / Bite / Minigame hold pose
            switch (sdvDir)
            {
                case 0: sdvOffset = new Vector2(28f, -76f); break;  // Up
                case 1: sdvOffset = new Vector2(120f, -48f); break; // Right
                case 2: sdvOffset = new Vector2(28f, 52f); break;   // Down
                case 3: default: sdvOffset = new Vector2(-56f, -48f); break; // Left
            }
        }

        // Offsets are 4x screen pixels (y down) from the farmer's Position; the standing point
        // (bounding box centre) is Position + (32, 16). 64 pixels make one world unit.
        float unityX = (sdvOffset.x - 32f) / 64f;
        float unityY = -(sdvOffset.y - 16f) / 64f;

        return standing + new Vector3(unityX, unityY, 0f);
    }

    private void LateUpdate()
    {
        UpdateFishingLine();
    }

    private void UpdateFishingLine()
    {
        if (fishingLine == null || !fishingLine.enabled || bobberObj == null || !bobberObj.activeSelf) return;

        Vector3 rodTip = GetRodTipWorldPosition();
        Vector3 bobberPoint = bobberObj.transform.position + Vector3.up * 0.08f;
        if (State == FishingState.Minigame)
        {
            // Taut while the fish fights.
            fishingLine.positionCount = 2;
            fishingLine.SetPosition(0, rodTip);
            fishingLine.SetPosition(1, bobberPoint);
        }
        else
        {
            // Slack line: a cubic curve that sags well below the bobber while waiting in the
            // water and only a little while the bobber flies or is pulled back.
            Vector3 d = bobberPoint - rodTip;
            float sag = State is FishingState.Waiting or FishingState.Biting ? 6f / 5f : 2f / 5f;
            Vector3 p1 = rodTip + new Vector3(d.x / 3f, d.y * 2f / 3f, 0f);
            Vector3 p2 = rodTip + new Vector3(d.x * 2f / 3f, d.y * sag, 0f);
            const int segments = 40;
            fishingLine.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1f - t;
                fishingLine.SetPosition(i, u * u * u * rodTip + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * bobberPoint);
            }
        }

        // The line is drawn in front of the farmer, except when casting upward (behind).
        int playerOrder = WorldSortOrder.GetOrder(motor != null ? motor.StandingPosition.y : transform.position.y);
        bool facingUp = motor != null && motor.Facing.y > 0.5f;
        fishingLine.sortingOrder = facingUp ? playerOrder - 1
            : Mathf.Max(playerOrder, WorldSortOrder.GetOrder(bobberObj.transform.position.y)) + 15;
    }

    private bool WasActionPressed()
    {
        bool pressed = pendingAction;
        pendingAction = false;
        return pressed || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            || Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame
            || Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
    }

    private IEnumerator ReelIn()
    {
        State = FishingState.Reeling;
        if (playerAnimation != null) playerAnimation.Play(FarmerAnimationAction.FishingDone);
        Vector3 start = bobberObj != null ? bobberObj.transform.position : bobberTargetPos;
        Vector3 rodTip = GetRodTipWorldPosition();

        float elapsed = 0f;
        const float reelDuration = 0.35f;
        while (elapsed < reelDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reelDuration);
            if (bobberObj != null)
            {
                bobberObj.transform.position = Vector3.Lerp(start, rodTip, t);
                if (bobberRenderer != null)
                    bobberRenderer.sortingOrder = WorldSortOrder.GetOrder(bobberObj.transform.position.y, 0f, 10);
            }
            yield return null;
        }

        EndFishingSession();
    }

    private void PlaySplash()
    {
        GameAudio.Play("dropItemInWater");
        if (splashEffect != null)
        {
            splashEffect.PlayAt(bobberTargetPos, WorldSortOrder.GetOrder(bobberTargetPos.y, 0f, 12));
        }
    }

    private void OnDestroy()
    {
        if (bobberObj != null) Destroy(bobberObj);
        if (fishingLine != null) Destroy(fishingLine.gameObject);
        if (splashEffect != null) Destroy(splashEffect.gameObject);
    }

    private IEnumerator PlayCritAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);
        GameAudio.Play("crit");
    }

    private void OnGUI()
    {
        if (cursorsTexture == null) return;
        if (State != FishingState.Charging && Time.time < chosenBarUntil)
        {
            // Bounce up by a few pixels and fade during the last 100 ms.
            float left = chosenBarUntil - Time.time;
            float alpha = left < 0.1f ? left / 0.1f : 1f;
            DrawCastingPowerBar(chosenBarPower, -Mathf.Floor(left * 1000f / 100f), alpha);
        }
        DrawMaxPopup();

        switch (State)
        {
            case FishingState.Charging:
                DrawCastingPowerBar(castingPower, 0f, 1f);
                break;
            case FishingState.Biting:
                DrawBiteAlert();
                break;
            case FishingState.Minigame:
                DrawBobberBarMinigame();
                break;
            case FishingState.Celebration:
                DrawCelebrationBanner();
                break;
        }
    }

    private static readonly Rect MaxTextRect = new(545, 1921, 53, 19);

    // Converts an offset in the art's 64-pixels-per-tile space, measured from the farmer's
    // feet (x right, y down), to GUI coordinates; also returns GUI pixels per art pixel.
    private bool FeetToGui(float rightPx, float downPx, out Vector2 gui, out float pixel)
    {
        gui = default;
        pixel = 0f;
        Camera view = Camera.main;
        if (view == null) return false;
        Vector3 feet = motor != null ? motor.StandingPosition : transform.position;
        Vector3 a = view.WorldToScreenPoint(feet);
        Vector3 b = view.WorldToScreenPoint(feet + Vector3.right);
        pixel = Mathf.Abs(b.x - a.x) / 64f;
        gui = new Vector2(a.x + rightPx * pixel, Screen.height - a.y + downPx * pixel);
        return true;
    }

    // 47x12 frame at 4x, 80 px left of and 176 px above the feet; the fill sits 12 px inside,
    // 164 px wide at full power and 25 px tall.
    private void DrawCastingPowerBar(float power, float bouncePx, float alpha)
    {
        if (!FeetToGui(-80f, -176f + bouncePx, out Vector2 frame, out float px)) return;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(new Rect(frame.x, frame.y, 188f * px, 48f * px), cursorsTexture, ToUv(CastBarRect));
        Color fill = GetRedToGreenLerpColor(power);
        fill.a = alpha;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(frame.x + 12f * px, frame.y + 12f * px, 164f * Mathf.Clamp01(power) * px, 25f * px),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // "MAX" rises from above the farmer's head for 800 ms, slowing as it climbs and fading out.
    private void DrawMaxPopup()
    {
        if (maxPopupStart < 0f || Time.time < maxPopupStart) return;
        float t = Time.time - maxPopupStart;
        if (t > 0.8f) { maxPopupStart = -1f; return; }
        float ticks = t * 60f;
        float rise = 4f * ticks - 0.1f * ticks * ticks;  // motion -4 px/tick, acceleration 0.2
        if (!FeetToGui(-32f, -208f - rise, out Vector2 pos, out float px)) return;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - ticks * 0.01f));
        GUI.DrawTextureWithTexCoords(new Rect(pos.x, pos.y, 106f * px, 38f * px), cursorsTexture, ToUv(MaxTextRect));
        GUI.color = Color.white;
    }

    private void DrawBiteAlert()
    {
        if (!biteAlertActive || Camera.main == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.55f);
        float pulse = 1f + Mathf.PingPong(Time.time * 6f, 0.25f);
        float w = 15f * pulse;
        float h = 40f * pulse;

        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h * 0.5f, w, h),
            cursorsTexture, ToUv(BiteAlertRect));
    }

    private void DrawBobberBarMinigame()
    {
        if (minigame == null) return;

        // Scale matching 4x pixel art
        float scale = 4f;
        float trackW = 37f * scale;  // 148px
        float trackH = 150f * scale; // 600px
        float bubbleW = 52f * scale; // 208px
        float bubbleH = 157f * scale;// 628px

        Vector3 playerScreen = Camera.main != null
            ? Camera.main.WorldToScreenPoint(transform.position)
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f);

        bool facesLeft = motor != null && motor.Facing.x < -0.5f;
        float startX = facesLeft
            ? playerScreen.x + 80f
            : playerScreen.x - trackW - 120f;
        startX = Mathf.Clamp(startX, 16f, Screen.width - bubbleW - 20f);
        float startY = Mathf.Clamp(Screen.height - playerScreen.y - trackH * 0.6f, 16f, Screen.height - bubbleH - 16f);

        // 1. Semi-transparent Speech Bubble Backdrop (pointing towards farmer)
        float bubbleX = facesLeft ? startX - 20f : startX - 44f + 20f;
        float bubbleY = startY - 14f;
        Rect bubbleUv = ToUv(BubbleRect);
        if (!facesLeft)
        {
            // Flip bubble horizontally to point to the left/right correctly
            bubbleUv = new Rect(bubbleUv.x + bubbleUv.width, bubbleUv.y, -bubbleUv.width, bubbleUv.height);
        }
        GUI.color = new Color(1f, 1f, 1f, 0.78f);
        GUI.DrawTextureWithTexCoords(new Rect(bubbleX, bubbleY, bubbleW, bubbleH), cursorsTexture, bubbleUv);

        // 2. Track & Fishing Rod Frame
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(new Rect(startX, startY, trackW, trackH), cursorsTexture, ToUv(BgRect));

        // 3. Water Channel inside track: left=17, top=4, width=9, height=138
        float waterX = startX + 17f * scale;
        float waterTopY = startY + 4f * scale;
        float waterW = 9f * scale;
        float waterH = 138f * scale;

        // Green Bobber Catch Bar
        float maxBarTravel = BobberBarMinigame.TrackHeight - minigame.BarHeight;
        float normBarPos = maxBarTravel > 0 ? minigame.BarPos / maxBarTravel : 0f;
        float barUiH = (minigame.BarHeight / BobberBarMinigame.TrackHeight) * waterH;
        float barUiY = waterTopY + (waterH - barUiH) * normBarPos;

        float alpha = minigame.IsFishInBar ? 1f : (0.45f + Mathf.PingPong(Time.time * 4f, 0.45f));
        GUI.color = new Color(1f, 1f, 1f, alpha);
        float capH = 2f * scale;
        GUI.DrawTextureWithTexCoords(new Rect(waterX, barUiY, waterW, capH), cursorsTexture, ToUv(BarTopRect));
        GUI.DrawTextureWithTexCoords(new Rect(waterX, barUiY + capH, waterW, Mathf.Max(0f, barUiH - capH * 2f)), cursorsTexture, ToUv(BarMidRect));
        GUI.DrawTextureWithTexCoords(new Rect(waterX, barUiY + barUiH - capH, waterW, capH), cursorsTexture, ToUv(BarBotRect));

        // 4. Fish Icon (40x40 px)
        float normFishY = minigame.FishPos / BobberBarMinigame.MaxFishPos;
        float fishSize = 40f;
        float fishUiY = waterTopY + (waterH - fishSize) * normFishY;
        float fishShake = minigame.IsFishInBar ? minigame.ShakeAmount * 3f : 0f;
        float fishUiX = waterX + (waterW - fishSize) * 0.5f + fishShake;

        GUI.color = Color.white;
        Rect fishRect = minigame.Fish.isBossFish ? BossFishRect : FishRect;
        GUI.DrawTextureWithTexCoords(new Rect(fishUiX, fishUiY, fishSize, fishSize), cursorsTexture, ToUv(fishRect));

        // 5. Reel Handle on the fishing rod (rotates when pulling/reeling)
        Vector2 reelPivot = new Vector2(startX + 9f * scale, startY + 129f * scale);
        GUIUtility.RotateAroundPivot(minigame.ReelRotation * Mathf.Rad2Deg, reelPivot);
        GUI.DrawTextureWithTexCoords(new Rect(reelPivot.x - 2f * scale, reelPivot.y - 5f * scale, 5f * scale, 10f * scale),
            cursorsTexture, ToUv(ReelRect));
        GUIUtility.RotateAroundPivot(-minigame.ReelRotation * Mathf.Rad2Deg, reelPivot);

        // 6. Catch Progress Meter in the built-in groove on the right (x=32, w=4, h=146)
        // FLUSH AT THE BOTTOM:
        // In the 37x150 track frame, the groove bottom ends at Y = startY + 148 * scale!
        float meterX = startX + 32f * scale;
        float meterW = 4f * scale;
        float grooveBottomY = startY + 148f * scale; // EXACT BOTTOM OF GROOVE!
        float maxGrooveH = 146f * scale;
        float progressH = maxGrooveH * Mathf.Clamp01(minigame.CatchProgress);
        float progressTopY = grooveBottomY - progressH; // SITS COMPLETELY FLUSH AT THE BOTTOM!

        Color meterColor = GetRedToGreenLerpColor(minigame.CatchProgress);
        GUI.color = meterColor;
        GUI.DrawTexture(new Rect(meterX, progressTopY, meterW, progressH), Texture2D.whiteTexture);

        // Darker 4px top edge on progress meter
        if (progressH > 4f)
        {
            GUI.color = new Color(Mathf.Max(0f, meterColor.r - 0.25f), Mathf.Max(0f, meterColor.g - 0.25f), 0f, 1f);
            GUI.DrawTexture(new Rect(meterX, progressTopY, meterW, 4f), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }

    private void DrawCelebrationBanner()
    {
        if (Event.current.type != EventType.Repaint) return;
        float floatOffset = Mathf.Sin(Time.unscaledTime * 4f) / 16f;
        Vector3 anchor = CatchPosition + new Vector3(0.5f, 36f / 64f + floatOffset, 0f);
        Vector3 playerScreen = Camera.main == null
            ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)
            : Camera.main.WorldToScreenPoint(anchor);

        const float width = 292f, height = 196f, margin = 12f, statusGap = 8f, statusScale = 1.55f;
        Texture2D font = StardewUiAtlas.FontBold;
        float statusHeight = font != null && !string.IsNullOrEmpty(catchSaveMessage)
            ? Mathf.Ceil(NpcBitmapText.MeasureWrappedHeight(catchSaveMessage, width - 32f, statusScale)) + 22f
            : 0f;
        float above = statusHeight > 0f ? statusHeight + statusGap : 0f;
        float scale = Mathf.Min(1f, Mathf.Min((Screen.width - 2f * margin) / width,
            (Screen.height - 2f * margin) / (height + above)));
        if (scale <= 0f) return;
        float x = Mathf.Clamp(playerScreen.x - width * scale * .5f, margin, Screen.width - width * scale - margin);
        float y = Mathf.Clamp(Screen.height - playerScreen.y - height * scale,
            margin + above * scale, Screen.height - height * scale - margin);
        GUI.depth = -45;
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3(Mathf.Round(x), Mathf.Round(y), 0f),
            Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.color = Color.white;
        try
        {
            GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, width, height), cursorsTexture, ToUv(CatchDialogRect));
            Sprite icon = caughtItem?.GetIcon();
            if (icon != null && icon.texture != null)
            {
                Rect source = icon.rect;
                Texture2D texture = icon.texture;
                // FishingRod.draw places the 64px fish inside the wooden frame built into Cursors.
                GUI.DrawTextureWithTexCoords(new Rect(40f, 72f, 64f, 64f), texture,
                    new Rect(source.x / texture.width, source.y / texture.height,
                        source.width / texture.width, source.height / texture.height));
            }
            if (font == null) return;
            DrawCatchLine(font, new Rect(14f, 14f, 264f, 34f), caughtItem?.displayName, 2.1f);
            int lengthCm = Mathf.RoundToInt(caughtFishSize * 2.54f);
            DrawCatchLine(font, new Rect(139f, 66f, 137f, 32f), "Chiều dài", 1.8f);
            DrawCatchLine(font, new Rect(139f, 102f, 137f, 32f), $"{lengthCm} cm", 2.15f);
            string result = targetFish != null && targetFish.isBossFish ? "Cá huyền thoại!" :
                wasNewRecord ? "Kỷ lục mới!" : wasPerfectCatch ? "Hoàn hảo!" : null;
            DrawCatchLine(font, new Rect(139f, 149f, 137f, 31f), result, 1.65f);

            if (statusHeight > 0f)
            {
                StardewUiAtlas.DrawTextureBox(new Rect(0f, -above, width, statusHeight),
                    cursorsTexture, new Rect(384, 373, 18, 18), Color.white, 2f, false);
                NpcBitmapText.Draw(font, new Rect(16f, -above + 12f, width - 32f, statusHeight - 16f),
                    catchSaveMessage, statusScale);
            }
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
    }

    private static void DrawCatchLine(Texture2D font, Rect bounds, string value, float maxScale)
    {
        if (string.IsNullOrEmpty(value)) return;
        float scale = Mathf.Min(maxScale, (bounds.width - 8f) / Mathf.Max(1f, NpcBitmapText.GetStringWidth(value, 1f)));
        NpcBitmapText.Draw(font, bounds, value, scale, centered: true);
    }

    private static Color GetRedToGreenLerpColor(float power)
    {
        power = Mathf.Clamp01(power);
        float r = power <= 0.5f ? 1f : (1f - power) * 2f;
        float g = Mathf.Min(1f, power * 2f);
        return new Color(r, g, 0f, 1f);
    }

    private Rect ToUv(Rect pixelRect)
    {
        const float atlasW = 704f;
        const float atlasH = 2256f;
        return new Rect(
            pixelRect.x / atlasW,
            (atlasH - pixelRect.y - pixelRect.height) / atlasH,
            pixelRect.width / atlasW,
            pixelRect.height / atlasH
        );
    }
}
