using UnityEngine;

/// <summary>
/// World item drop (debris) in Stardew Valley.
/// Pops out when trees/rocks/weeds are broken, bounces on the ground,
/// magnetizes towards the player, and collects into PlayerInventory.
/// </summary>
public sealed class WorldItemDrop : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Vector2 velocity;
    private float groundY;
    private bool isGrounded;
    private float spawnTime;
    private float magnetRadius = 2.0f;
    private float magnetSpeed = 4.0f;
    private Transform targetPlayer;
    private PlayerInventory playerInventory;

    public ItemData Item => itemData;
    public int Amount => amount;

    public void Initialize(ItemData data, int count, Vector2 popDirection)
    {
        itemData = data;
        amount = count;
        spawnTime = Time.time;
        groundY = transform.position.y - 0.15f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (itemData != null && itemData.icon != null)
        {
            spriteRenderer.sprite = itemData.icon;
            spriteRenderer.sortingOrder = 25; // In front of ground, below player hands
        }

        // Pop velocity
        velocity = popDirection.normalized * Random.Range(1.0f, 2.0f) + Vector2.up * Random.Range(1.5f, 2.5f);
        isGrounded = false;
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            var p = Object.FindAnyObjectByType<PlayerInventory>();
            if (p != null) playerObj = p.gameObject;
        }

        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
            playerInventory = playerObj.GetComponent<PlayerInventory>();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 1. Arc bounce physics
        if (!isGrounded)
        {
            velocity.y -= 9.8f * dt; // Gravity
            transform.position += (Vector3)(velocity * dt);

            if (transform.position.y <= groundY && velocity.y < 0f)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                if (Mathf.Abs(velocity.y) > 1.0f)
                {
                    velocity.y = -velocity.y * 0.4f; // Bounce
                    velocity.x *= 0.6f;
                }
                else
                {
                    isGrounded = true;
                    velocity = Vector2.zero;
                }
            }
        }

        // 2. Magnetism towards player (after brief 0.25s delay so pop arc is visible)
        if (Time.time - spawnTime > 0.25f)
        {
            if (targetPlayer == null || playerInventory == null)
            {
                FindPlayer();
            }

            if (targetPlayer != null)
            {
                Vector3 playerCenter = targetPlayer.position + new Vector3(0f, 0.4f, 0f);
                float dist = Vector3.Distance(transform.position, playerCenter);

                if (dist <= magnetRadius)
                {
                    magnetSpeed += 6.0f * dt; // Accelerate towards player
                    transform.position = Vector3.MoveTowards(transform.position, playerCenter, magnetSpeed * dt);

                    if (dist <= 0.35f && playerInventory != null)
                    {
                        Collect();
                    }
                }
            }
        }
    }

    private void Collect()
    {
        if (playerInventory != null && itemData != null)
        {
            bool added = playerInventory.AddItem(itemData, amount);
            if (added)
            {
                Destroy(gameObject);
            }
        }
    }

    public static WorldItemDrop Spawn(ItemData data, int count, Vector3 position, Vector2 popDirection)
    {
        var go = new GameObject($"Drop_{data?.itemId ?? "Item"}");
        go.transform.position = position;
        var drop = go.AddComponent<WorldItemDrop>();
        drop.Initialize(data, count, popDirection);
        return drop;
    }
}
