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
    protected NetworkCharacterStats lastHitStats;
    private System.Action<Transform, bool, System.Action<bool>> saveNaturalRemoval;
    private GameLocation resourceLocation;
    protected virtual Transform ShakeTarget => transform;

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
        lastHitStats = context.User == null ? null : context.User.GetComponent<NetworkCharacterStats>();

        int damage = CalculateDamage(tool) + (kind == PlayerItemKind.Sword && lastHitStats != null
            ? Mathf.Max(0, lastHitStats.AttackBonus) : 0);
        int previousHealth = health;
        health -= damage;

        Shake(0.08f, 0.15f);

        if (health <= 0)
        {
            FinishRemoval(context.User, false, previousHealth, () => OnBreak(context.Facing));
        }
        else
        {
            if (requiredToolKind == PlayerItemKind.Pickaxe)
                GameAudio.Play("hammer");
            else if (requiredToolKind == PlayerItemKind.Axe)
                GameAudio.Play("axchop");
        }

        return true;
    }

    /// <summary>Bomb blast: breaks stones, ore and boulders outright.</summary>
    internal bool Explode(Transform user, Vector2 direction)
    {
        if (isBroken || requiredToolKind != PlayerItemKind.Pickaxe) return false;
        lastHitStats = user == null ? null : user.GetComponent<NetworkCharacterStats>();
        int previousHealth = health;
        health = 0;
        FinishRemoval(user, false, previousHealth, () => OnBreak(direction));
        return true;
    }

    internal void BindNaturalRemoval(GameLocation location, System.Action<Transform, bool, System.Action<bool>> save)
    {
        resourceLocation = location;
        saveNaturalRemoval = save;
    }

    protected void FinishRemoval(Transform user, bool stump, int previousHealth, System.Action finish)
    {
        isBroken = true;
        if (saveNaturalRemoval == null) { finish(); return; }
        saveNaturalRemoval(user, stump, saved =>
        {
            if (this == null) return;
            if (saved) finish();
            else
            {
                health = previousHealth;
                isBroken = false;
            }
        });
    }

    private float damageCarry;

    /// <summary>Damage of one strike. Stones lose upgradeLevel + 1 per pickaxe hit.</summary>
    protected virtual float ToolDamage(ItemData tool) =>
        tool is ToolData toolData ? 1 + toolData.upgradeLevel : 1;

    // Health is stored as whole points; keep the fraction so a Copper Axe (1.25) fells
    // a 10-health tree in exactly eight swings.
    protected int CalculateDamage(ItemData tool)
    {
        float total = ToolDamage(tool) + damageCarry;
        int whole = Mathf.FloorToInt(total);
        damageCarry = total - whole;
        return whole;
    }

    /// <summary>Upgrade level of the tool in hand, or -1 when it is not a tool.</summary>
    protected static int UpgradeLevel(ItemData tool) => tool is ToolData data ? data.upgradeLevel : -1;

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
        Transform target = ShakeTarget;
        Vector3 restPosition = target == transform ? initialLocalPosition : Vector3.zero;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 50f) * intensity * (1f - (elapsed / duration));
            target.localPosition = restPosition + new Vector3(offset, 0f, 0f);
            yield return null;
        }
        target.localPosition = restPosition;
        shakeCoroutine = null;
    }

    /// <summary>
    /// Spawns drop items and plays destruction behavior.
    /// </summary>
    protected virtual void OnBreak(Vector2 hitDirection)
    {
        GameAudio.Play(requiredToolKind switch
        {
            PlayerItemKind.Axe => "stumpCrack",
            PlayerItemKind.Pickaxe => "stoneCrack",
            PlayerItemKind.Sword => "cut",
            _ => "harvest"
        });
        SpawnDrops(hitDirection);
        Destroy(gameObject);
    }

    protected virtual void SpawnDrops(Vector2 hitDirection)
    {
        if (dropItem != null)
        {
            int count = Random.Range(minDropAmount, maxDropAmount + 1);
            if (lastHitStats != null && Random.value < Mathf.Clamp01(lastHitStats.ForagingBonus * 0.05f))
                count++;
            for (int i = 0; i < count; i++)
            {
                Vector2 randomPop = hitDirection + Random.insideUnitCircle * 0.5f;
                SpawnResourceDrop(dropItem, transform.position, randomPop);
            }
        }
    }

    protected void SpawnResourceDrop(ItemData item, Vector3 position, Vector2 direction) =>
        WorldItemDrop.Spawn(item, 1, position, direction,
            location: resourceLocation != null ? resourceLocation : GetComponentInParent<GameLocation>(true));
}
