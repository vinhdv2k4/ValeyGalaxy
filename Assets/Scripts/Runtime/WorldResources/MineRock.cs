using UnityEngine;

/// <summary>A breakable mine stone. Ore nodes and ordinary stones have different drops.</summary>
public sealed class MineRock : WorldResource
{
    private ItemData oreItem;
    private bool isOreNode;
    private int oreQuantityMax;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Pickaxe;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    public void Setup(Sprite sprite, ItemData stone, ItemData ore, bool oreNode, int maxOre = 3)
    {
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 22;
        dropItem = stone;
        oreItem = ore;
        isOreNode = oreNode;
        oreQuantityMax = maxOre;
        maxHealth = oreNode ? 2 : 1;
        health = maxHealth;
    }

    protected override void OnBreak(Vector2 hitDirection)
    {
        if (isOreNode)
        {
            Drop(oreItem, Random.Range(1, oreQuantityMax + 1), hitDirection);
        }
        else
        {
            // GameLocation.breakStone always produces one Stone for ordinary
            // outdoor rocks. MineShaft.checkStoneForItems then rolls extra ore.
            // Luck/profession bonuses need player stats not present here yet.
            Drop(dropItem, 1, hitDirection);
            if (Random.value < 0.04f) Drop(oreItem, 1, hitDirection);
        }
        Destroy(gameObject);
    }

    private void Drop(ItemData item, int count, Vector2 direction)
    {
        if (item != null) WorldItemDrop.Spawn(item, count, transform.position, direction);
    }
}
