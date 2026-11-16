using System;
using UnityEngine;

[Serializable]
public sealed class FoodBuffModifiers
{
    public int farming, fishing, mining, luck, foraging, crafting;
    public int maxStamina, magneticRadius, speed, defense, attack;

    public bool HasAny => farming != 0 || fishing != 0 || mining != 0 || luck != 0 ||
        foraging != 0 || crafting != 0 || maxStamina != 0 || magneticRadius != 0 ||
        speed != 0 || defense != 0 || attack != 0;
}

/// <summary>
/// OCP & LSP: Base ScriptableObject for any item.
/// Can be extended for Tools, Crops, Seeds, Weapons, Furniture, etc.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Stardew/Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemId = "item";
    public string displayName = "Vật phẩm";
    [TextArea(2, 4)]
    public string description = "";
    public Sprite icon;
    [SerializeField] private Texture2D iconAtlas;
    [SerializeField] private int iconAtlasIndex = -1;
    public int maxStackSize = 999;

    [Header("Player Interaction")]
    public PlayerItemKind kind = PlayerItemKind.Material;
    public bool hasUseAnimation;
    public FarmerAnimationAction useAnimation = FarmerAnimationAction.Tool;

    [Header("Food and drink (Object.Edibility / Buff)")]
    [Min(0)] public int edibility;
    public bool isDrink;
    [Min(0)] public int buffDurationMinutes;
    public FoodBuffModifiers foodBuff = new();
    public bool IsEdible => edibility > 0;

    [Header("Farming (seeds only)")]
    public CropDefinition cropDefinition;
    public CropDefinition[] mixedSeedCrops;

    [Header("In-Hand Directional Sprites")]
    public Sprite heldSprite;
    public Sprite heldSpriteDown;
    public Sprite heldSpriteRight;
    public Sprite heldSpriteUp;
    public Sprite[] heldAnimationSprites;

    private void OnEnable() => GetIcon();

    public Sprite GetIcon()
    {
        if (icon == null) icon = CreateAtlasSprite(iconAtlas, iconAtlasIndex);
        return icon;
    }

    internal static Sprite CreateAtlasSprite(Texture2D atlas, int index)
    {
        const int tileSize = 16;
        if (atlas == null || index < 0) return null;
        int columns = atlas.width / tileSize;
        if (columns == 0 || index / columns >= atlas.height / tileSize) return null;

        // Object IDs count cells from the top left. Unity sprite rects start at the bottom left.
        int x = index % columns * tileSize;
        int y = atlas.height - (index / columns + 1) * tileSize;
        return AtlasSprites.Get(atlas, new Rect(x, y, tileSize, tileSize), new Vector2(0.5f, 0.5f), tileSize);
    }

    public virtual bool CanStackWith(ItemData other)
    {
        return other != null && other.itemId == itemId && maxStackSize > 1;
    }

    public virtual Sprite GetHeldSprite(int direction)
    {
        return direction switch
        {
            0 => heldSpriteDown != null ? heldSpriteDown : heldSprite,
            1 or 3 => heldSpriteRight != null ? heldSpriteRight : heldSprite,
            2 => heldSpriteUp != null ? heldSpriteUp : heldSprite,
            _ => heldSprite
        };
    }

    public virtual Sprite GetHeldAnimationSprite(int indexOffset)
    {
        if (heldAnimationSprites != null && indexOffset >= 0 && indexOffset < heldAnimationSprites.Length && heldAnimationSprites[indexOffset] != null)
        {
            return heldAnimationSprites[indexOffset];
        }
        return heldSprite;
    }
}
