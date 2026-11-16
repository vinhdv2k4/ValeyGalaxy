using System.Collections;
using UnityEngine;

public enum FarmerAnimationAction
{
    Grab, CarryWalk, CarryRun, Tool, ToolChoose, SeedThrow,
    Eat, Sick, SwordSwipe, Punch, Harvest, Shear, Milk,
    Tired, Tired2, PassOutTired, Drink, Fishing, FishingDone,
    Pan, ShowHoldingEdible, Cheer, Watering, ReceiveReward
}

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterMotor motor;

    private Coroutine returnRoutine;
    private float actionStartedAt;
    private float actionDuration;
    private float? heldPoseProgress;
    private int carryStateHash;
    private bool carryIdle;

    public FarmerAnimationAction? ActiveAction { get; private set; }
    /// <summary>
    /// Counts one-shot actions like Farmer.fireToolEvent / beginUsingToolEvent: world presence
    /// sends the counter, and other clients replay the action once whenever it changes.
    /// </summary>
    public int ActionSequence { get; private set; }
    public FarmerAnimationAction LastAction { get; private set; }
    public Vector2 LastActionFacing { get; private set; } = Vector2.down;
    public float ActionProgress => heldPoseProgress ?? (actionDuration <= 0f ? 0f : Mathf.Clamp01((Time.time - actionStartedAt) / actionDuration));

    private void Awake()
    {
        animator = animator == null ? GetComponent<Animator>() : animator;
        motor = motor == null ? GetComponent<CharacterMotor>() : motor;
    }

    public float Play(FarmerAnimationAction action)
    {
        string state = action.ToString();
        if (IsDirectional(action)) state += "_" + GetDirection();
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning("Missing player animation state: " + state, this);
            return 0f;
        }

        if (returnRoutine != null) StopCoroutine(returnRoutine);
        heldPoseProgress = null;
        animator.speed = 1f;
        animator.Play(hash, 0, 0f);

        float duration = 0f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Farmer_" + state) { duration = clip.length; break; }
        ActiveAction = action;
        carryStateHash = action == FarmerAnimationAction.CarryWalk || action == FarmerAnimationAction.CarryRun ? hash : 0;
        carryIdle = false;
        actionStartedAt = Time.time;
        actionDuration = duration;
        // Carrying is locomotion, not an action others must replay.
        if (carryStateHash == 0)
        {
            ActionSequence++;
            LastAction = action;
            LastActionFacing = motor == null ? Vector2.down : motor.Facing;
        }
        if (action != FarmerAnimationAction.CarryWalk && action != FarmerAnimationAction.CarryRun && action != FarmerAnimationAction.ShowHoldingEdible && action != FarmerAnimationAction.Fishing)
            returnRoutine = StartCoroutine(ReturnToIdle(duration));
        return duration;
    }

    public void UpdateCarryLocomotion(Vector2 movement, bool carrying)
    {
        bool isPlayingCarry = ActiveAction == FarmerAnimationAction.CarryWalk
            || ActiveAction == FarmerAnimationAction.CarryRun;
        if (ActiveAction.HasValue && !isPlayingCarry) return;

        if (!carrying)
        {
            if (isPlayingCarry) StopAction();
            return;
        }

        if (movement.sqrMagnitude <= 0.01f)
        {
            // The original farmer holds the first carry-run frame while standing still.
            int idleHash = Animator.StringToHash("CarryRun_" + GetDirection());
            if (carryIdle && carryStateHash == idleHash) return;
            Play(FarmerAnimationAction.CarryRun);
            animator.Update(0f);
            animator.speed = 0f;
            carryIdle = true;
            return;
        }

        FarmerAnimationAction action = movement.sqrMagnitude > 0.8f
            ? FarmerAnimationAction.CarryRun : FarmerAnimationAction.CarryWalk;
        int stateHash = Animator.StringToHash(action + "_" + GetDirection());
        if (!carryIdle && ActiveAction == action && carryStateHash == stateHash) return;
        Play(action);
    }

    public void HoldFishingCast(bool waiting = false) =>
        HoldPose(FarmerAnimationAction.Fishing, "Fishing_" + GetDirection(), waiting ? 0.98f : 0f);

    // Falls back to the end of the cast until the FishingReel clips are generated.
    internal void HoldFishingReel()
    {
        string state = "FishingReel_" + GetDirection();
        if (animator != null && animator.HasState(0, Animator.StringToHash(state)))
            HoldPose(FarmerAnimationAction.Fishing, state, 0f);
        else HoldFishingCast(true);
    }

    internal void HoldFishingPull() =>
        HoldPose(FarmerAnimationAction.FishingDone, "FishingDone_" + GetDirection(), 0f);

    /// <summary>Farmer.toolPower while charging the Hoe or Watering Can (0 when not charging).</summary>
    public int ToolPower { get; set; }

    /// <summary>
    /// Holds the first frame of a use animation, as FarmerSprite pauses on it while a tool
    /// charges (Game1.toolHold); the swing plays from the start once the button is released.
    /// </summary>
    internal void HoldUseStart(FarmerAnimationAction action) =>
        HoldPose(action, IsDirectional(action) ? action + "_" + GetDirection() : action.ToString(), 0f);

    // Harvest_Down ends on farmer frame 57, used by Farmer.holdUpItemThenMessage.
    internal void HoldReward() => HoldPose(FarmerAnimationAction.ReceiveReward, "Harvest_Down", 1f);

    private void HoldPose(FarmerAnimationAction action, string state, float normalizedTime)
    {
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return;
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = null;
        animator.speed = 1f;
        animator.Play(hash, 0, normalizedTime);
        animator.Update(0f);
        animator.speed = 0f;
        heldPoseProgress = normalizedTime;
        ActiveAction = action;
        actionStartedAt = Time.time;
        actionDuration = 0f;
    }

    public void StopAction()
    {
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = null;
        heldPoseProgress = null;
        ActiveAction = null;
        carryStateHash = 0;
        carryIdle = false;
        animator.speed = 1f;
        animator.Play("Idle", 0, 0f);
    }

    private IEnumerator ReturnToIdle(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(duration, 0.05f));
        returnRoutine = null;
        ActiveAction = null;
        animator.Play("Idle", 0, 0f);
    }

    private string GetDirection()
    {
        Vector2 facing = motor == null ? Vector2.down : motor.Facing;
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            return facing.x > 0f ? "Right" : "Left";
        return facing.y > 0f ? "Up" : "Down";
    }

    private static bool IsDirectional(FarmerAnimationAction action)
    {
        switch (action)
        {
            case FarmerAnimationAction.Grab:
            case FarmerAnimationAction.CarryWalk:
            case FarmerAnimationAction.CarryRun:
            case FarmerAnimationAction.Tool:
            case FarmerAnimationAction.ToolChoose:
            case FarmerAnimationAction.SeedThrow:
            case FarmerAnimationAction.SwordSwipe:
            case FarmerAnimationAction.Punch:
            case FarmerAnimationAction.Harvest:
            case FarmerAnimationAction.Shear:
            case FarmerAnimationAction.Milk:
            case FarmerAnimationAction.Fishing:
            case FarmerAnimationAction.Watering:
            case FarmerAnimationAction.FishingDone:
                return true;
            default:
                return false;
        }
    }
}
