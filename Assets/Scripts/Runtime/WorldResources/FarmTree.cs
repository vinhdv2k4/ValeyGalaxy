using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interactive world tree (Oak, Maple, Pine).
/// Supports growth stages, axe chopping, shaking physics, tree tipping/falling animation,
/// stump persistence, and wood log drops.
/// </summary>
public sealed class FarmTree : WorldResource
{
    public enum TreeType { Maple = 1, Oak = 2, Pine = 3 }

    [Header("Tree Components")]
    [SerializeField] private TreeType treeType = TreeType.Maple;
    [SerializeField] private SpriteRenderer trunkRenderer;
    [SerializeField] private SpriteRenderer canopyRenderer;
    [SerializeField] private Transform canopyPivot;
    [SerializeField] private Sprite stumpSprite;
    [SerializeField] private bool isStump;

    [Header("Tree Drop Settings")]
    [SerializeField] private ItemData seedDropItem;
    [SerializeField] private int fallenWoodCount = 12;
    [SerializeField] private int stumpWoodCount = 5;

    private bool isFalling;
    private bool isSapling;
    private bool breakingStump;
    private float fallDirection = 1f;
    private Sprite[] leafSprites;

    public TreeType Type => treeType;
    public bool IsStump => isStump;
    public bool IsMature => !isStump && !isSapling && !isFalling;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Axe;
        maxHealth = isStump ? 5 : 10;
        health = maxHealth;
        minDropAmount = 1;
        maxDropAmount = 2;
    }

    protected override void EnsureCollider()
    {
        if (GetComponent<Collider2D>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.6f);
            box.offset = new Vector2(0f, 0f);
        }
    }

    public void Setup(TreeType type, Sprite fullTree, Sprite stump, ItemData wood, ItemData seed,
        bool stumpOnly = false)
    {
        EnsureCollider();
        requiredToolKind = PlayerItemKind.Axe;
        treeType = type;
        isStump = stumpOnly;
        stumpSprite = stump;
        if (fullTree != null)
        {
            var sheet = fullTree.texture;
            leafSprites = new Sprite[4];
            for (int i = 0; i < leafSprites.Length; i++)
            {
                int x = 16 + (i % 2) * 8;
                int y = sheet.height - 112 - (i / 2 + 1) * 8;
                leafSprites[i] = AtlasSprites.Get(sheet, new Rect(x, y, 8, 8),
                    new Vector2(0.5f, 0.5f), 16f);
            }
        }
        dropItem = wood;
        seedDropItem = seed;
        maxHealth = isStump ? 5 : 10;
        health = maxHealth;

        int baseOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);

        if (trunkRenderer == null)
        {
            var trunkObj = new GameObject("Trunk");
            trunkObj.transform.SetParent(transform, false);
            trunkObj.transform.localPosition = Vector3.zero;
            trunkRenderer = trunkObj.AddComponent<SpriteRenderer>();
        }
        trunkRenderer.sprite = stumpSprite;
        trunkRenderer.sortingOrder = baseOrder;
        trunkRenderer.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        // The stump sprite draws the root base touching the ground.
        trunkRenderer.enabled = true;

        if (canopyRenderer == null && fullTree != null)
        {
            var pivotObj = new GameObject("CanopyPivot");
            pivotObj.transform.SetParent(transform, false);
            pivotObj.transform.localPosition = Vector3.zero;
            canopyPivot = pivotObj.transform;

            var canopyObj = new GameObject("Canopy");
            canopyObj.transform.SetParent(canopyPivot, false);
            canopyObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            canopyRenderer = canopyObj.AddComponent<SpriteRenderer>();
        }

        if (canopyRenderer != null)
        {
            canopyRenderer.sprite = fullTree;
            canopyRenderer.sortingOrder = baseOrder + 1;
            canopyRenderer.gameObject.SetActive(!isStump);
        }
    }

    internal void SetupSapling(TreeType type, Sprite sprite)
    {
        treeType = type;
        isSapling = true;
        health = maxHealth = 2;
        var visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
    }

    private Transform playerTransform;

    private void LateUpdate()
    {
        if (isStump || canopyRenderer == null) return;

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform != null)
        {
            Vector3 pPos = playerTransform.position;
            Vector3 tPos = transform.position;
            // When player walks behind canopy (north of trunk) and under the foliage
            bool behindCanopy = pPos.y > tPos.y - 0.2f && pPos.y < tPos.y + 3.2f
                && Mathf.Abs(pPos.x - tPos.x) < 1.4f;
            Color cur = canopyRenderer.color;
            float targetAlpha = behindCanopy ? 0.45f : 1f;
            cur.a = Mathf.MoveTowards(cur.a, targetAlpha, Time.deltaTime * 5f);
            canopyRenderer.color = cur;
        }
    }

    // Axe damage per level: Basic 1, Copper 1.25, Steel 1.67, Gold 2.5, Iridium 5.
    protected override float ToolDamage(ItemData tool) => UpgradeLevel(tool) switch
    {
        1 => 1.25f,
        2 => 1.67f,
        3 => 2.5f,
        4 => 5f,
        _ => 1f
    };

    public override bool Hit(ItemUseContext context)
    {
        if (isFalling || isBroken) return false;

        ItemData tool = context.Item?.itemData;
        PlayerItemKind kind = context.Item != null ? context.Item.kind : PlayerItemKind.Material;
        if (!CanHarvest(tool, kind)) return false;
        if (!isStump && PlacedObjectsView.HasTapper(transform.position))
        {
            Shake(0.03f, 0.15f);
            NpcConversation.ShowNotice("Hãy gỡ máy chiết nhựa khỏi cây trước khi chặt.");
            return true;
        }

        int previousHealth = health;
        int damage = CalculateDamage(tool);
        health -= damage;

        // Tree shake
        if (!isStump && !isSapling)
        {
            Shake(0.055f, 0.25f);
            SpawnFallingLeaves();
            GameAudio.Play("axchop");
            GameAudio.Play("leafrustle");
        }
        else
        {
            Shake(0.045f, 0.18f);
            GameAudio.Play("axchop");
        }

        // Fall direction away from player
        fallDirection = context.Facing.x >= 0 ? 1f : -1f;

        if (health <= 0)
        {
            if (isSapling)
            {
                FinishRemoval(context.User, false, previousHealth, () => base.OnBreak(context.Facing));
            }
            else if (isStump)
            {
                FinishRemoval(context.User, false, previousHealth, () =>
                {
                    if (!isActiveAndEnabled) { OnBreak(context.Facing); return; }
                    breakingStump = true;
                    StartCoroutine(BreakStumpAfterShake(context.Facing));
                });
            }
            else
            {
                FinishRemoval(context.User, true, previousHealth, () =>
                {
                    if (isActiveAndEnabled) StartCoroutine(FallAndDrop(context.Facing));
                    else FinishFall(context.Facing);
                });
            }
        }

        return true;
    }

    protected override IEnumerator DoShake(float intensity, float duration)
    {
        Transform shakenPart = isStump ? trunkRenderer?.transform : canopyPivot;
        if (shakenPart == null)
        {
            shakeCoroutine = null;
            yield break;
        }

        Vector3 startPosition = shakenPart.localPosition;
        float elapsed = 0f;
        while (elapsed < duration && shakenPart != null && !isFalling)
        {
            elapsed += Time.deltaTime;
            float wave = Mathf.Sin(elapsed * 35f) * (1f - elapsed / duration);
            if (isStump)
                shakenPart.localPosition = startPosition + new Vector3(wave * intensity, 0f, 0f);
            else
                shakenPart.localRotation = Quaternion.Euler(0f, 0f, wave * intensity * Mathf.Rad2Deg);
            yield return null;
        }
        if (shakenPart != null && !isFalling)
        {
            if (isStump) shakenPart.localPosition = startPosition;
            else shakenPart.localRotation = Quaternion.identity;
        }
        shakeCoroutine = null;
    }

    private IEnumerator BreakStumpAfterShake(Vector2 hitDirection)
    {
        yield return new WaitForSeconds(0.18f);
        OnBreak(hitDirection);
    }

    private void SpawnFallingLeaves()
    {
        if (leafSprites == null || canopyRenderer == null) return;
        int count = Random.Range(1, 5);
        for (int i = 0; i < count; i++)
        {
            FallingTreeLeaf.Spawn(
                leafSprites[Random.Range(0, leafSprites.Length)],
                canopyRenderer.sortingOrder + 2,
                transform.position + new Vector3(
                    Random.Range(-1.2f, 1.2f), Random.Range(2.4f, 4.2f), 0f),
                LocationManager.Instance?.CurrentLocation?.transform);
        }
    }

    private IEnumerator FallAndDrop(Vector2 hitDirection)
    {
        isFalling = true;
        GameAudio.Play("treecrack");
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = null;
        if (canopyPivot != null) canopyPivot.localRotation = Quaternion.identity;
        if (trunkRenderer != null) trunkRenderer.enabled = true;

        if (canopyPivot != null)
        {
            float targetAngle = -fallDirection * 90f;
            float elapsed = 0f;
            float duration = 0.85f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeInT = t * t; // Accelerating fall physics
                float angle = Mathf.Lerp(0f, targetAngle, easeInT);
                canopyPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            GameAudio.Play("treethud");
        }

        FinishFall(hitDirection);
    }

    private void FinishFall(Vector2 hitDirection)
    {
        Vector3 impactPos = transform.position + new Vector3(fallDirection * 2f, 0f, 0f);
        if (dropItem != null)
            for (int i = 0; i < fallenWoodCount; i++)
                SpawnResourceDrop(dropItem, impactPos, hitDirection + Random.insideUnitCircle * 0.8f);
        if (seedDropItem != null && Random.value < 0.005f)
            SpawnResourceDrop(seedDropItem, impactPos, hitDirection);
        // A felled tree also leaves five sap.
        ItemData sap = LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>()?.ResolveItem("object_92");
        if (sap != null)
            for (int i = 0; i < 5; i++)
                SpawnResourceDrop(sap, impactPos, hitDirection + Random.insideUnitCircle * 0.8f);
        BecomeStump();
    }

    private void BecomeStump()
    {
        if (canopyPivot != null) Destroy(canopyPivot.gameObject);
        canopyPivot = null;
        canopyRenderer = null;
        isStump = true;
        isFalling = false;
        isBroken = false;
        maxHealth = 5;
        health = maxHealth;

        if (stumpSprite != null && trunkRenderer != null)
        {
            trunkRenderer.sprite = stumpSprite;
        }
    }

    private void OnDisable()
    {
        if (isFalling) BecomeStump();
        else if (breakingStump) Destroy(gameObject);
    }



    protected override void OnBreak(Vector2 hitDirection)
    {
        GameAudio.Play("stumpCrack");
        if (dropItem != null)
        {
            for (int i = 0; i < stumpWoodCount; i++)
            {
                Vector2 pop = hitDirection + Random.insideUnitCircle * 0.5f;
                SpawnResourceDrop(dropItem, transform.position, pop);
            }
        }

        Destroy(gameObject);
    }
}

public sealed class FallingTreeLeaf : MonoBehaviour
{
    private const int InitialPoolSize = 8;
    private const int MaxRetainedLeaves = 64;
    private static readonly System.Collections.Generic.Stack<FallingTreeLeaf> Available = new();
    private static GameObject poolRoot;

    private float lifetime;
    private float drift;
    private float spin;
    private SpriteRenderer spriteRenderer;
    private bool isPooled;

    public static FallingTreeLeaf Spawn(Sprite sprite, int sortingOrder, Vector3 position, Transform owner)
    {
        EnsurePool();
        FallingTreeLeaf leaf = null;
        while (Available.Count > 0 && leaf == null) leaf = Available.Pop();
        leaf ??= CreateLeaf();
        leaf.transform.SetParent(owner, false);
        leaf.transform.position = position;
        leaf.transform.rotation = Quaternion.identity;
        leaf.transform.localScale = Vector3.one;
        leaf.spriteRenderer.sprite = sprite;
        leaf.spriteRenderer.sortingOrder = sortingOrder;
        leaf.lifetime = Random.Range(0.8f, 1.4f);
        leaf.drift = Random.Range(-0.65f, 0.65f);
        leaf.spin = Random.Range(-180f, 180f);
        leaf.isPooled = false;
        leaf.gameObject.SetActive(true);
        return leaf;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        transform.position += new Vector3(drift, -1.8f, 0f) * Time.deltaTime;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (lifetime <= 0f) Recycle();
    }

    private void Recycle()
    {
        if (isPooled) return;
        isPooled = true;
        gameObject.SetActive(false);
        spriteRenderer.sprite = null;
        EnsurePool();
        transform.SetParent(poolRoot.transform, false);
        if (Available.Count < MaxRetainedLeaves) Available.Push(this);
        else Destroy(gameObject);
    }

    private static void EnsurePool()
    {
        if (poolRoot != null) return;
        Available.Clear();
        poolRoot = new GameObject("Falling Tree Leaf Pool");
        for (int i = 0; i < InitialPoolSize; i++) Available.Push(CreateLeaf());
    }

    private static FallingTreeLeaf CreateLeaf()
    {
        var go = new GameObject("Pooled Falling Leaf");
        go.SetActive(false);
        go.transform.SetParent(poolRoot.transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        FallingTreeLeaf leaf = go.AddComponent<FallingTreeLeaf>();
        leaf.spriteRenderer = renderer;
        leaf.isPooled = true;
        return leaf;
    }
}
