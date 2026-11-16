using UnityEngine;

/// <summary>
/// Mining rock on the farm and in the mines.
/// A Paths stone: one basic pickaxe hit yields one Stone.
/// </summary>
public sealed class FarmRock : WorldResource
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Pickaxe;
        maxHealth = 1;
        health = maxHealth;
        minDropAmount = 1;
        maxDropAmount = 1;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    public void Setup(Sprite rockSprite, ItemData stoneItem, int hp = 1)
    {
        EnsureCollider();
        requiredToolKind = PlayerItemKind.Pickaxe;
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = rockSprite;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
        dropItem = stoneItem;
        maxHealth = hp;
        health = hp;
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        GameAudio.Play("hammer");
        base.OnBreak(hitDirection);
    }
}
