using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base class for interactive world resources on the farm and in mines
/// (Trees, Stumps, Rocks, Twigs, Weeds).
/// Follows Single Responsibility and Open/Closed Principles.
/// </summary>
public abstract class WorldResource : MonoBehaviour
{
    [Header("Resource Settings")]
    [SerializeField] protected int health = 1;
    [SerializeField] protected int maxHealth = 1;
    [SerializeField] protected PlayerItemKind requiredToolKind = PlayerItemKind.Axe;
    [SerializeField] protected ItemData dropItem;
    [SerializeField] protected int minDropAmount = 1;
    [SerializeField] protected int maxDropAmount = 2;

    protected bool isBroken;
    protected Coroutine shakeCoroutine;
    protected Vector3 initialLocalPosition;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public PlayerItemKind RequiredToolKind => requiredToolKind;
    public bool IsBroken => isBroken;

    protected virtual void Awake()
    {
        initialLocalPosition = transform.localPosition;
        EnsureCollider();
    }

    protected virtual void EnsureCollider()
    {
        if (GetComponent<Collider2D>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.8f);
        }
    }

    /// <summary>
    /// Checks if the provided tool is valid to harvest or damage this resource.
    /// </summary>
    public virtual bool CanHarvest(ItemData tool, PlayerItemKind kind)
    {
        if (requiredToolKind == PlayerItemKind.Material) return true; // Any tool/weapon can cut weeds
        if (tool != null && tool.kind == requiredToolKind) return true;
        return kind == requiredToolKind;
    }

    public virtual bool CanHarvest(ItemData tool) => CanHarvest(tool, tool != null ? tool.kind : PlayerItemKind.Material);

    /// <summary>
    /// Called when the player strikes this resource with a tool.
    /// </summary>
    public virtual bool Hit(ItemUseContext context)
    {
        if (isBroken) return false;

        ItemData tool = context.Item?.itemData;
        PlayerItemKind kind = context.Item != null ? context.Item.kind : PlayerItemKind.Material;
        if (!CanHarvest(tool, kind)) return false;

        int damage = CalculateDamage(tool);
        health -= damage;

        Shake(0.08f, 0.15f);

        if (health <= 0)
        {
            isBroken = true;
            OnBreak(context.Facing);
        }

        return true;
    }

    protected virtual int CalculateDamage(ItemData tool)
    {
        if (tool is ToolData toolData)
        {
            return 1 + toolData.upgradeLevel;
        }
        return 1;
    }

    /// <summary>
    /// Shakes the resource back and forth upon being struck.
    /// </summary>
    public virtual void Shake(float intensity, float duration)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(DoShake(intensity, duration));
    }

    protected virtual IEnumerator DoShake(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 50f) * intensity * (1f - (elapsed / duration));
            transform.localPosition = initialLocalPosition + new Vector3(offset, 0f, 0f);
            yield return null;
        }
        transform.localPosition = initialLocalPosition;
        shakeCoroutine = null;
    }

    /// <summary>
    /// Spawns drop items and plays destruction behavior.
    /// </summary>
    protected virtual void OnBreak(Vector2 hitDirection)
    {
        SpawnDrops(hitDirection);
        Destroy(gameObject);
    }

    protected virtual void SpawnDrops(Vector2 hitDirection)
    {
        if (dropItem != null)
        {
            int count = Random.Range(minDropAmount, maxDropAmount + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 randomPop = hitDirection + Random.insideUnitCircle * 0.5f;
                WorldItemDrop.Spawn(dropItem, 1, transform.position, randomPop);
            }
        }
    }
}
