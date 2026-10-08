using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class CharacterMotor : MonoBehaviour
{
    [Min(0.01f)] public float speed = 4f;
    public Rigidbody2D body;
    [SerializeField] private Animator animator;

    public Vector2 Facing { get; private set; } = Vector2.down;
    public bool IsMovementEnabled { get; set; } = true;

    private Vector2 movement;
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int FacingX = Animator.StringToHash("FacingX");
    private static readonly int FacingY = Animator.StringToHash("FacingY");

    private void Awake()
    {
        body = body == null ? GetComponent<Rigidbody2D>() : body;
        animator = animator == null ? GetComponentInChildren<Animator>() : animator;
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

    private void FixedUpdate()
    {
        if (body != null)
            body.linearVelocity = movement * speed;
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
