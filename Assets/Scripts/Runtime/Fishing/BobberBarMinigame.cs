using System;
using UnityEngine;

/// <summary>
/// Core simulation logic for the fishing slider minigame.
/// Faithful port of BobberBar.cs with bar physics, fish motion AI, and catch progress.
/// </summary>
public sealed class BobberBarMinigame
{
    public const float TrackHeight = 548f;
    public const float MaxFishPos = 532f;

    public FishData Fish { get; }
    public float BarHeight { get; private set; }
    public float BarPos { get; private set; } // 0 = top, TrackHeight - BarHeight = bottom
    public float BarSpeed { get; private set; }

    public float FishPos { get; private set; }
    public float FishTargetPos { get; private set; }
    public float FishSpeed { get; private set; }
    public float FishAcceleration { get; private set; }
    public float FloaterSinkerAcceleration { get; private set; }

    public float CatchProgress { get; private set; } // 0.0 to 1.0
    public bool IsFishInBar { get; private set; }
    public bool IsPerfect { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsSuccess { get; private set; }

    public float ShakeAmount { get; private set; }
    public float ReelRotation { get; private set; }

    private float nextFishMoveTimer;

    public BobberBarMinigame(FishData fish, int fishingLevel = 0)
    {
        Fish = fish ?? throw new ArgumentNullException(nameof(fish));
        BarHeight = Mathf.Clamp(96f + fishingLevel * 8f, 70f, 220f);
        BarPos = TrackHeight - BarHeight; // Start at the bottom
        BarSpeed = 0f;

        FishPos = 480f; // Start near the bottom
        FishTargetPos = 400f;
        FishSpeed = 0f;

        CatchProgress = 0.30f; // Default start at 30%
        IsPerfect = true;
        IsGameOver = false;
        IsSuccess = false;
        nextFishMoveTimer = 0.5f;
    }

    public void Update(float dt, bool isPulling)
    {
        if (IsGameOver) return;

        UpdateBarPhysics(dt, isPulling);
        UpdateFishAI(dt);
        UpdateCatchProgress(dt);
    }

    private void UpdateBarPhysics(float dt, bool isPulling)
    {
        // Upward pull vs downward gravity
        float accel = isPulling ? -800f : 700f;
        BarSpeed += accel * dt;

        // Dampen velocity to prevent infinite acceleration
        BarSpeed = Mathf.Clamp(BarSpeed, -550f, 550f);
        BarPos += BarSpeed * dt;

        float maxBarPos = TrackHeight - BarHeight;
        if (BarPos > maxBarPos)
        {
            BarPos = maxBarPos;
            BarSpeed = -BarSpeed * 0.5f; // Elastic bounce at bottom
        }
        else if (BarPos < 0f)
        {
            BarPos = 0f;
            BarSpeed = -BarSpeed * 0.4f; // Elastic bounce at top
        }
    }

    private void UpdateFishAI(float dt)
    {
        nextFishMoveTimer -= dt;
        if (nextFishMoveTimer <= 0f)
        {
            ChooseNextFishTarget();
            // Faster retarget interval for harder fish
            float baseInterval = Mathf.Lerp(1.6f, 0.5f, Fish.difficulty / 100f);
            nextFishMoveTimer = UnityEngine.Random.Range(baseInterval * 0.6f, baseInterval * 1.3f);
        }

        // Floater / Sinker bias
        if (Fish.motionType == 4) // Floater
            FloaterSinkerAcceleration = Mathf.Max(FloaterSinkerAcceleration - 60f * dt, -120f);
        else if (Fish.motionType == 3) // Sinker
            FloaterSinkerAcceleration = Mathf.Min(FloaterSinkerAcceleration + 60f * dt, 120f);
        else
            FloaterSinkerAcceleration = 0f;

        // Smooth vs Dart vs Mixed movement
        float diff = FishTargetPos - FishPos;
        float speedMultiplier = Mathf.Lerp(150f, 380f, Fish.difficulty / 100f);
        if (Fish.motionType == 1) speedMultiplier *= 1.4f; // Darting fish move faster

        FishAcceleration = Mathf.Sign(diff) * speedMultiplier;
        FishSpeed = Mathf.MoveTowards(FishSpeed, FishAcceleration, 800f * dt);
        FishPos += (FishSpeed + FloaterSinkerAcceleration) * dt;
        FishPos = Mathf.Clamp(FishPos, 0f, MaxFishPos);
    }

    private void ChooseNextFishTarget()
    {
        float diff = Fish.difficulty;
        float target = FishPos;

        switch (Fish.motionType)
        {
            case 1: // Dart: sudden large leaps
                float jump = UnityEngine.Random.Range(100f, 300f);
                target += UnityEngine.Random.value < 0.5f ? -jump : jump;
                break;
            case 2: // Smooth: gentle glide
                float glide = UnityEngine.Random.Range(40f, 130f);
                target += UnityEngine.Random.value < 0.5f ? -glide : glide;
                break;
            case 3: // Sinker: favors the bottom half
                target = UnityEngine.Random.Range(TrackHeight * 0.4f, MaxFishPos);
                break;
            case 4: // Floater: favors the top half
                target = UnityEngine.Random.Range(10f, TrackHeight * 0.5f);
                break;
            default: // Mixed: standard behavior
                float range = Mathf.Lerp(70f, 220f, diff / 100f);
                target += UnityEngine.Random.Range(-range, range);
                break;
        }

        FishTargetPos = Mathf.Clamp(target, 0f, MaxFishPos);
    }

    private void UpdateCatchProgress(float dt)
    {
        // Is fish inside the green bar?
        // Note: Fish icon center is around FishPos + 10px.
        float fishCenter = FishPos + 10f;
        IsFishInBar = fishCenter >= BarPos && fishCenter <= BarPos + BarHeight;

        if (IsFishInBar)
        {
            CatchProgress += 0.17f * dt; // ~5-6 seconds to catch
            ReelRotation += 12f * dt;
            ShakeAmount = UnityEngine.Random.Range(-1.5f, 1.5f);
        }
        else
        {
            CatchProgress -= 0.13f * dt;
            IsPerfect = false;
            ShakeAmount = 0f;
        }

        CatchProgress = Mathf.Clamp01(CatchProgress);

        if (CatchProgress >= 1f)
        {
            IsGameOver = true;
            IsSuccess = true;
        }
        else if (CatchProgress <= 0f)
        {
            IsGameOver = true;
            IsSuccess = false;
        }
    }
}
