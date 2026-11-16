using UnityEngine;

/// <summary>
/// Fallen twig on the farm.
/// Broken with 1 hit from an Axe, drops 1 Wood.
/// </summary>
public sealed class FarmTwig : WorldResource
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Axe;
        maxHealth = 1;
        health = 1;
        minDropAmount = 1;
        maxDropAmount = 1;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    public void Setup(Sprite twigSprite, ItemData woodItem)
    {
        EnsureCollider();
        requiredToolKind = PlayerItemKind.Axe;
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = twigSprite;
        spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
        dropItem = woodItem;
    }
}
