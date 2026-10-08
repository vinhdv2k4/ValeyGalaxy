using System.Collections;
using UnityEngine;

/// <summary>
/// Full-fledged Stardew Valley tree (Oak, Maple, Pine).
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
    private float fallDirection = 1f;

    public TreeType Type => treeType;
    public bool IsStump => isStump;

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

    public void Setup(TreeType type, Sprite fullTree, Sprite stump, ItemData wood, ItemData seed)
    {
        EnsureCollider();
        requiredToolKind = PlayerItemKind.Axe;
        treeType = type;
        stumpSprite = stump;
        dropItem = wood;
        seedDropItem = seed;
        maxHealth = isStump ? 5 : 10;
        health = maxHealth;

        if (trunkRenderer == null)
        {
            var trunkObj = new GameObject("Trunk");
            trunkObj.transform.SetParent(transform, false);
            trunkObj.transform.localPosition = Vector3.zero;
            trunkRenderer = trunkObj.AddComponent<SpriteRenderer>();
        }
        // The decompiled Tree draws the 48x96 crown and 16x32 stump separately.
        trunkRenderer.sprite = stumpSprite;
        trunkRenderer.sortingOrder = 22;
        trunkRenderer.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        if (canopyRenderer == null && fullTree != null)
        {
            var pivotObj = new GameObject("CanopyPivot");
            pivotObj.transform.SetParent(transform, false);
            pivotObj.transform.localPosition = Vector3.zero;
            canopyPivot = pivotObj.transform;

            var canopyObj = new GameObject("Canopy");
            canopyObj.transform.SetParent(canopyPivot, false);
            // The crown is three tiles wide and six tiles high. Its bottom
            // aligns with the bottom of the marker cell, above the stump.
            canopyObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            canopyRenderer = canopyObj.AddComponent<SpriteRenderer>();
        }

        if (canopyRenderer != null)
        {
            canopyRenderer.sprite = fullTree;
            canopyRenderer.sortingOrder = 30; // High sorting order: tree canopy covers player walking behind
        }
    }

    public override bool Hit(ItemUseContext context)
    {
        if (isFalling || isBroken) return false;

        ItemData tool = context.Item?.itemData;
        PlayerItemKind kind = context.Item != null ? context.Item.kind : PlayerItemKind.Material;
        if (!CanHarvest(tool, kind)) return false;

        int damage = CalculateDamage(tool);
        health -= damage;

        // Tree shake
        float intensity = isStump ? 0.05f : 0.12f;
        Shake(intensity, 0.2f);

        // Fall direction away from player
        fallDirection = context.Facing.x >= 0 ? 1f : -1f;

        if (health <= 0)
        {
            if (!isStump)
            {
                StartCoroutine(FallAndDrop(context.Facing));
            }
            else
            {
                isBroken = true;
                OnBreak(context.Facing);
            }
        }

        return true;
    }

    private IEnumerator FallAndDrop(Vector2 hitDirection)
    {
        isFalling = true;

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

            Vector3 impactPos = transform.position + new Vector3(fallDirection * 2f, 0f, 0f);
            if (dropItem != null)
            {
                for (int i = 0; i < fallenWoodCount; i++)
                {
                    Vector2 pop = hitDirection + Random.insideUnitCircle * 0.8f;
                    WorldItemDrop.Spawn(dropItem, 1, impactPos, pop);
                }
            }

            if (seedDropItem != null && Random.value < 0.005f)
            {
                WorldItemDrop.Spawn(seedDropItem, 1, impactPos, hitDirection);
            }

            Destroy(canopyPivot.gameObject);
            canopyPivot = null;
            canopyRenderer = null;
        }

        // Tree now becomes a stump
        isStump = true;
        isFalling = false;
        maxHealth = 5;
        health = maxHealth;

        if (stumpSprite != null && trunkRenderer != null)
        {
            trunkRenderer.sprite = stumpSprite;
        }
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        if (dropItem != null)
        {
            for (int i = 0; i < stumpWoodCount; i++)
            {
                Vector2 pop = hitDirection + Random.insideUnitCircle * 0.5f;
                WorldItemDrop.Spawn(dropItem, 1, transform.position, pop);
            }
        }

        Destroy(gameObject);
    }
}
