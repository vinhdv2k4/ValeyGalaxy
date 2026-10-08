using System.Collections;
using UnityEngine;

public enum FarmerAnimationAction
{
    Grab, CarryWalk, CarryRun, Tool, ToolChoose, SeedThrow,
    Eat, Sick, SwordSwipe, Punch, Harvest, Shear, Milk,
    Tired, Tired2, PassOutTired, Drink, Fishing, FishingDone,
    Pan, ShowHoldingEdible, Cheer, Watering
}

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterMotor motor;

    private Coroutine returnRoutine;
    private float actionStartedAt;
    private float actionDuration;

    public FarmerAnimationAction? ActiveAction { get; private set; }
    public float ActionProgress => actionDuration <= 0f ? 0f : Mathf.Clamp01((Time.time - actionStartedAt) / actionDuration);

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
        animator.Play(hash, 0, 0f);

        float duration = 0f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Farmer_" + state) { duration = clip.length; break; }
        ActiveAction = action;
        actionStartedAt = Time.time;
        actionDuration = duration;
        if (action != FarmerAnimationAction.CarryWalk && action != FarmerAnimationAction.CarryRun && action != FarmerAnimationAction.ShowHoldingEdible)
            returnRoutine = StartCoroutine(ReturnToIdle(duration));
        return duration;
    }

    public void StopAction()
    {
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = null;
        ActiveAction = null;
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
