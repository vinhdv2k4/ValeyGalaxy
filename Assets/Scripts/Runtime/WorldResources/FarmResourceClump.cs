using UnityEngine;

/// <summary>Two-by-two stump, log, or boulder from Paths markers 19-21.</summary>
public sealed class FarmResourceClump : WorldResource
{
    private int requiredUpgrade;
    private int dropCount;

    protected override void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(2f, 2f);
    }

    public void Setup(int marker, Sprite sprite, ItemData wood, ItemData stone)
    {
        int sourceIndex;
        switch (marker)
        {
            case 19: // hollow log
                sourceIndex = 602;
                requiredToolKind = PlayerItemKind.Axe;
                requiredUpgrade = 2;
                maxHealth = 20;
                dropItem = wood;
                dropCount = 8;
                break;
            case 20: // boulder
                sourceIndex = 672;
                requiredToolKind = PlayerItemKind.Pickaxe;
                requiredUpgrade = 2;
                maxHealth = 10;
                dropItem = stone;
                dropCount = 15;
                break;
            default: // large stump (21)
                sourceIndex = 600;
                requiredToolKind = PlayerItemKind.Axe;
                requiredUpgrade = 1;
                maxHealth = 10;
                dropItem = wood;
                dropCount = 2;
                break;
        }
        health = maxHealth;
        name = sourceIndex switch
        {
            600 => "Large Stump",
            602 => "Hollow Log",
            _ => "Boulder"
        };
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 22;
    }

    public override bool CanHarvest(ItemData tool, PlayerItemKind kind)
    {
        return base.CanHarvest(tool, kind) && tool is ToolData data && data.upgradeLevel >= requiredUpgrade;
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        if (dropItem != null)
            WorldItemDrop.Spawn(dropItem, dropCount, transform.position, hitDirection);
        Destroy(gameObject);
    }
}
