using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class CharacterMotor : MonoBehaviour
{
    private const float CutsceneWalkInput = 0.5f;
    [Min(0.01f)] public float speed = 4f;
    public Rigidbody2D body;
    [SerializeField] private Animator animator;
    private NetworkCharacterStats stats;

    public Vector2 Facing { get; private set; } = Vector2.down;
    public Vector2 Movement => movement;
    public Vector3 StandingPosition => standingCollider == null ? transform.position :
        body != null && !body.simulated ? transform.TransformPoint(standingCollider.offset) : standingCollider.bounds.center;
    public bool IsMovementEnabled { get; set; } = true;

    private Vector2 movement;
    private Collider2D standingCollider;
    private float nextFootstepTime;
    private Vector3 lastStepPosition;
    private int defaultLayer;
    private int defaultSortingLayer;
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int FacingX = Animator.StringToHash("FacingX");
    private static readonly int FacingY = Animator.StringToHash("FacingY");

    private void Awake()
    {
        body = body == null ? GetComponent<Rigidbody2D>() : body;
        standingCollider = GetComponent<Collider2D>();
        animator = animator == null ? GetComponentInChildren<Animator>() : animator;
        stats = GetComponent<NetworkCharacterStats>();
        defaultLayer = gameObject.layer;
        var group = GetComponent<UnityEngine.Rendering.SortingGroup>();
        defaultSortingLayer = group == null ? 0 : group.sortingLayerID;
        lastStepPosition = StandingPosition;
        if (GetComponent<PlayerWarpDetector>() == null)
        {
            gameObject.AddComponent<PlayerWarpDetector>();
        }
    }

    public void SetMovement(Vector2 input)
    {
        movement = IsMovementEnabled ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
        if (movement.sqrMagnitude > 0.001f)
        {
            // Sprite swaps in a 2D blend tree are discrete. Pick one direction
            // so diagonal input never blends two different sprite frames.
            Facing = Mathf.Abs(movement.x) > Mathf.Abs(movement.y)
                ? new Vector2(Mathf.Sign(movement.x), 0f)
                : new Vector2(0f, Mathf.Sign(movement.y));
        }

        UpdatePresentation();
    }

    public void FaceTowards(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) return;
        Facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
        UpdatePresentation();
    }

    internal void SetCutscenePose(Vector3 pivot, Vector2 direction, bool walking)
    {
        transform.position = pivot;
        if (body != null) body.position = pivot;
        FaceTowards(direction);
        movement = walking ? Facing * CutsceneWalkInput : Vector2.zero;
        UpdatePresentation();
    }

    /// <summary>
    /// Farmer.xVelocity / yVelocity: source pixels per 60 Hz tick (Y up), losing 1/16 each tick
    /// and stopping at 0.05. Weapons use it to lunge the farmer forward and settle back.
    /// </summary>
    private Vector2 nudge;
    private const float SourceTicksPerSecond = 60f;
    private const float SourcePixelsPerUnit = 64f;

    internal void Nudge(Vector2 sourceVelocity) => nudge = sourceVelocity;

    private void FixedUpdate()
    {
        Vector2 velocity = movement * Mathf.Max(0.1f, speed + (stats == null ? 0 : stats.SpeedBonus));
        if (nudge != Vector2.zero)
        {
            // The physics body stops the lunge at walls, like isCollidingPosition.
            velocity += nudge * (SourceTicksPerSecond / SourcePixelsPerUnit);
            float ticks = Time.fixedDeltaTime * SourceTicksPerSecond;
            nudge *= Mathf.Pow(15f / 16f, ticks);
            if (Mathf.Abs(nudge.x) <= 0.05f) nudge.x = 0f;
            if (Mathf.Abs(nudge.y) <= 0.05f) nudge.y = 0f;
        }
        if (body != null)
            body.linearVelocity = velocity;
        float moved = (StandingPosition - lastStepPosition).sqrMagnitude;
        lastStepPosition = StandingPosition;
        if (movement.sqrMagnitude > 0.01f && moved > 0.00001f && moved < 4f && Time.time >= nextFootstepTime)
        {
            nextFootstepTime = Time.time + FootstepInterval();
            GameLocation location = LocationManager.Instance?.CurrentLocation;
            string step = location == null ? "thudStep" : location.GetFootstepCue(StandingPosition);
            GameAudio.PlayFootstep(step);
            FootstepEffects.Spawn(step, StandingPosition, Facing);
        }
    }

    // A step lands every half of the 8-frame run cycle (330 ms), and the cycle speeds up with
    // movement speed: about 0.26 s at the base speed of 5, a little faster with speed buffs.
    private float FootstepInterval()
    {
        float runSpeed = 5f + (stats == null ? 0 : stats.SpeedBonus);
        float modifier = 1f - 0.0375f * Mathf.Max(1f, runSpeed * 0.066f * 16.667f);
        return 0.33f * Mathf.Max(0.5f, modifier);
    }

    public void ResetAfterWarp()
    {
        movement = Vector2.zero;
        nudge = Vector2.zero;
        gameObject.layer = defaultLayer;
        if (body != null)
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        var group = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (group != null)
        {
            group.sortingLayerID = defaultSortingLayer;
            group.sortingOrder = WorldSortOrder.GetOrder(StandingPosition.y);
        }
        lastStepPosition = StandingPosition;
        nextFootstepTime = Time.time + 0.15f;
    }

    private void UpdatePresentation()
    {
        if (animator != null)
        {
            animator.SetFloat(MoveX, movement.sqrMagnitude > 0.001f ? Facing.x : 0f);
            animator.SetFloat(MoveY, movement.sqrMagnitude > 0.001f ? Facing.y : 0f);
            animator.SetFloat(Speed, movement.sqrMagnitude);
            animator.SetFloat(FacingX, Facing.x);
            animator.SetFloat(FacingY, Facing.y);
        }

    }
}
