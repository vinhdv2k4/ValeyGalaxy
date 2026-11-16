using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collectible world item drop (debris).
/// Pops out when trees/rocks/weeds are broken, bounces on the ground,
/// magnetizes towards the player, and collects into PlayerInventory.
/// </summary>
public sealed class WorldItemDrop : MonoBehaviour
{
    private const int InitialPoolSize = 24;
    private const int MaxRetainedDrops = 256;
    private static readonly Stack<WorldItemDrop> Available = new();
    private static GameObject poolRoot;

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
    private NetworkCharacterStats playerStats;
    private bool isPooled;
    private int fishCatchSize;
    private string fishAttemptId;
    private string spawnLocation;
    private int spawnTileX, spawnTileY;
    private bool collecting;
    // The server already handed this item over (bush berries): picking it up is only the animation.
    private bool alreadyGranted;
    private float nextCollectAttempt;

    public ItemData Item => itemData;
    public int Amount => amount;

    public void Initialize(ItemData data, int count, Vector2 popDirection, int caughtFishSize = 0,
                           string attemptId = null)
    {
        isPooled = false;
        itemData = data;
        amount = count;
        spawnTime = Time.time;
        groundY = transform.position.y - 0.15f;
        magnetSpeed = 4f;
        targetPlayer = null;
        playerInventory = null;
        playerStats = null;
        fishCatchSize = caughtFishSize;
        fishAttemptId = attemptId;
        collecting = false;
        nextCollectAttempt = 0f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = itemData != null ? itemData.GetIcon() : null;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(groundY, 0f, 1);

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
        // The local farmer first: other farmers' avatars are clones that carry the Player tag too.
        CharacterMotor local = LocationManager.Instance != null ? LocationManager.Instance.LocalFarmer : null;
        GameObject playerObj = local != null ? local.gameObject : GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            var p = Object.FindAnyObjectByType<PlayerInventory>();
            if (p != null) playerObj = p.gameObject;
        }

        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
            playerInventory = playerObj.GetComponent<PlayerInventory>();
            playerStats = playerObj.GetComponent<NetworkCharacterStats>();
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

            if (!collecting && targetPlayer != null && playerInventory != null &&
                (alreadyGranted || playerInventory.CanAccept(itemData, amount)))
            {
                Vector3 playerCenter = targetPlayer.position + new Vector3(0f, 0.4f, 0f);
                float dist = Vector3.Distance(transform.position, playerCenter);

                float effectiveRadius = magnetRadius + (playerStats == null ? 0 : playerStats.MagneticRadiusBonus) / 16f;
                if (dist <= effectiveRadius)
                {
                    magnetSpeed += 6.0f * dt; // Accelerate towards player
                    transform.position = Vector3.MoveTowards(transform.position, playerCenter, magnetSpeed * dt);

                    if (dist <= 0.35f && playerInventory != null)
                    {
                        Collect();
                    }
                }
            }
            else magnetSpeed = 4.0f;
        }
    }

    private void Collect()
    {
        if (playerInventory == null || itemData == null || Time.time < nextCollectAttempt) return;
        if (alreadyGranted)
        {
            GameAudio.Play("pickUpItem");
            HudToast.ShowItem(itemData, amount);
            Recycle();
            return;
        }
        var online = targetPlayer == null ? null : targetPlayer.GetComponent<OnlineGameState>();
        if (online != null && !string.IsNullOrEmpty(fishAttemptId))
        {
            collecting = true;
            online.SaveFishCatch(itemData, fishAttemptId, error =>
            {
                collecting = false;
                if (string.IsNullOrEmpty(error)) { GameAudio.Play("pickUpItem"); HudToast.ShowItem(itemData, amount); Recycle(); }
                else if (error.IndexOf("expired", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         error.IndexOf("invalid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    NpcConversation.ShowNotice(error);
                    Recycle();
                }
                else nextCollectAttempt = Time.time + 2f;
            });
            return;
        }
        if (online == null)
        {
            if (playerInventory.AddItem(itemData, amount)) { GameAudio.Play("pickUpItem"); HudToast.ShowItem(itemData, amount); Recycle(); }
            return;
        }
        collecting = true;
        ItemData picked = itemData;
        int count = amount;
        online.CollectResource(spawnLocation, spawnTileX, spawnTileY, itemData, amount, error =>
        {
            collecting = false;
            if (string.IsNullOrEmpty(error))
            {
                GameAudio.Play("pickUpItem");
                HudToast.ShowItem(picked, count);
                Recycle();
            }
            else if (error.IndexOf("already been collected", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     error.IndexOf("invalid", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     error.IndexOf("limit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Recycle();
            else nextCollectAttempt = Time.time + 2f;
        });
    }

    public static WorldItemDrop Spawn(ItemData data, int count, Vector3 position, Vector2 popDirection,
        int caughtFishSize = 0, string attemptId = null, GameLocation location = null, bool alreadyGranted = false)
    {
        if (data == null || count <= 0) return null;
        EnsurePool();
        WorldItemDrop drop = null;
        while (Available.Count > 0 && drop == null) drop = Available.Pop();
        drop ??= CreateDrop();

        location ??= LocationManager.Instance?.CurrentLocation;
        drop.transform.SetParent(location != null ? location.transform : null, false);
        drop.transform.position = position;
        drop.transform.rotation = Quaternion.identity;
        drop.transform.localScale = Vector3.one;
        drop.gameObject.name = $"Drop_{data.itemId}";
        drop.Initialize(data, count, popDirection, caughtFishSize, attemptId);
        drop.alreadyGranted = alreadyGranted;
        drop.spawnLocation = location != null ? location.LocationName : "Farm";
        drop.spawnTileX = Mathf.FloorToInt(position.x);
        drop.spawnTileY = Mathf.FloorToInt(-position.y);
        drop.gameObject.SetActive(true);
        return drop;
    }

    private void Recycle()
    {
        if (isPooled) return;
        isPooled = true;
        gameObject.SetActive(false);
        itemData = null;
        amount = 0;
        velocity = Vector2.zero;
        targetPlayer = null;
        playerInventory = null;
        fishCatchSize = 0;
        fishAttemptId = null;
        spawnLocation = null;
        collecting = false;
        if (spriteRenderer != null) spriteRenderer.sprite = null;
        EnsurePool();
        transform.SetParent(poolRoot.transform, false);
        gameObject.name = "Pooled World Item Drop";
        if (Available.Count < MaxRetainedDrops) Available.Push(this);
        else Destroy(gameObject);
    }

    private static void EnsurePool()
    {
        if (poolRoot != null) return;
        Available.Clear();
        poolRoot = new GameObject("World Item Drop Pool");
        for (int i = 0; i < InitialPoolSize; i++)
        {
            WorldItemDrop drop = CreateDrop();
            Available.Push(drop);
        }
    }

    private static WorldItemDrop CreateDrop()
    {
        var go = new GameObject("Pooled World Item Drop");
        go.SetActive(false);
        go.transform.SetParent(poolRoot.transform, false);
        go.AddComponent<SpriteRenderer>();
        WorldItemDrop drop = go.AddComponent<WorldItemDrop>();
        drop.isPooled = true;
        return drop;
    }
}
