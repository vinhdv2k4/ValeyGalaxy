using UnityEngine;

/// <summary>
/// Weeds on the farm.
/// Cut down with 1 hit from Scythe, Sword, or tools.
/// Drops mixed seeds or fiber.
/// </summary>
public sealed class FarmWeed : WorldResource
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    private ItemData mixedSeedItem;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Material; // Any tool or weapon can cut weeds
        maxHealth = 1;
        health = 1;
        minDropAmount = 0;
        maxDropAmount = 1;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    protected override void EnsureCollider()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.8f);
        }
        box.isTrigger = false; // Weeds block player movement until cut down
    }

    public override bool CanHarvest(ItemData tool, PlayerItemKind kind) => true;

    public void Setup(Sprite weedSprite, ItemData fiber, ItemData mixedSeeds)
    {
        EnsureCollider();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = weedSprite;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
        dropItem = fiber;
        mixedSeedItem = mixedSeeds;
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        GameAudio.Play("cut");
        // Object.cutWeed: 50% Fiber, otherwise 5% Mixed Seeds.
        ItemData item = Random.value < 0.5f ? dropItem : Random.value < 0.05f ? mixedSeedItem : null;
        if (item != null) SpawnResourceDrop(item, transform.position, hitDirection);
        Destroy(gameObject);
    }
}
