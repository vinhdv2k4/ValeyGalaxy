using UnityEngine;

/// <summary>Two-by-two stump, log, or boulder from map markers.</summary>
public sealed class FarmResourceClump : WorldResource
{
    private int requiredUpgrade;
    private int sourceIndex;
    private int dropCount;

    protected override void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(2f, 2f);
    }

    public void Setup(int marker, Sprite sprite, ItemData hardwood, ItemData stone)
    {
        switch (marker)
        {
            case 19: // hollow log
                sourceIndex = 602;
                requiredToolKind = PlayerItemKind.Axe;
                requiredUpgrade = 2;
                maxHealth = 20;
                dropItem = hardwood;
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
                dropItem = hardwood;
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
        renderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -1.0f);
    }

    // The right tool kind always connects; when it is not upgraded enough the swing
    // bounces off with a sound and a message instead of doing damage.
    public override bool Hit(ItemUseContext context)
    {
        if (isBroken) return false;
        ItemData tool = context.Item?.itemData;
        PlayerItemKind kind = context.Item != null ? context.Item.kind : PlayerItemKind.Material;
        if (CanHarvest(tool, kind) && UpgradeLevel(tool) < requiredUpgrade)
        {
            if (requiredToolKind == PlayerItemKind.Axe) GameAudio.Play("axe");
            else
            {
                GameAudio.Play("clubhit");
                GameAudio.Play("clank");
            }
            Shake(0.04f, 0.12f);
            NpcConversation.ShowNotice(TooWeakMessage());
            return true;
        }
        return base.Hit(context);
    }

    private string TooWeakMessage()
    {
        string tier = requiredUpgrade switch { 1 => "Đồng", 2 => "Thép", 3 => "Vàng", _ => "Iridium" };
        return sourceIndex switch
        {
            600 => $"Rìu của bạn chưa đủ mạnh để chặt gốc cây này. Cần Rìu {tier} trở lên.",
            602 => $"Rìu của bạn chưa đủ mạnh để chặt khúc gỗ này. Cần Rìu {tier} trở lên.",
            672 => $"Cuốc chim của bạn chưa đủ mạnh để đập tảng đá này. Cần Cuốc chim {tier} trở lên.",
            _ => $"Cuốc chim của bạn chưa đủ mạnh để đập vật này. Cần Cuốc chim {tier} trở lên."
        };
    }

    // ResourceClump: health -= Math.Max(1, (upgradeLevel + 1) * 0.75).
    protected override float ToolDamage(ItemData tool) => Mathf.Max(1f, (UpgradeLevel(tool) + 1) * 0.75f);

    protected override void OnBreak(Vector2 hitDirection)
    {
        GameAudio.Play(requiredToolKind == PlayerItemKind.Axe ? "stumpCrack" : "boulderBreak");
        if (dropItem != null)
            WorldItemDrop.Spawn(dropItem, dropCount, transform.position, hitDirection);
        Destroy(gameObject);
    }
}
