using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class CharacterMotor : MonoBehaviour
{
    [Min(0.01f)] public float speed = 4f;
    public Rigidbody2D body;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public Vector2 Facing { get; private set; } = Vector2.down;
    public bool IsMovementEnabled { get; set; } = true;

    private Vector2 movement;
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int FacingX = Animator.StringToHash("FacingX");
    private static readonly int FacingY = Animator.StringToHash("FacingY");
    private static readonly int UseTool = Animator.StringToHash("UseTool");

    private void Awake()
    {
        body = body == null ? GetComponent<Rigidbody2D>() : body;
        animator = animator == null ? GetComponentInChildren<Animator>() : animator;
        spriteRenderer = spriteRenderer == null ? GetComponentInChildren<SpriteRenderer>() : spriteRenderer;
    }

    public void SetMovement(Vector2 input)
    {
        movement = IsMovementEnabled ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
        if (movement.sqrMagnitude > 0.001f)
            Facing = movement.normalized;

        UpdatePresentation();
    }

    public void PlayToolAnimation() => animator?.SetTrigger(UseTool);

    private void FixedUpdate()
    {
        if (body != null)
            body.linearVelocity = movement * speed;
    }

    private void UpdatePresentation()
    {
        if (animator != null)
        {
            animator.SetFloat(MoveX, movement.x);
            animator.SetFloat(MoveY, movement.y);
            animator.SetFloat(Speed, movement.sqrMagnitude);
            animator.SetFloat(FacingX, Facing.x);
            animator.SetFloat(FacingY, Facing.y);
        }

        if (spriteRenderer != null && Mathf.Abs(Facing.x) > 0.01f)
            spriteRenderer.flipX = Facing.x < 0f;
    }
}
