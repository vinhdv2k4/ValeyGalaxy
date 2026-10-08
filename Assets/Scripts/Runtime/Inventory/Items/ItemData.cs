using System;
using UnityEngine;

/// <summary>
/// OCP & LSP: Base ScriptableObject for any item in Stardew Valley.
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
    public int maxStackSize = 999;

    [Header("Player Interaction")]
    public PlayerItemKind kind = PlayerItemKind.Material;
    public bool hasUseAnimation;
    public FarmerAnimationAction useAnimation = FarmerAnimationAction.Tool;

    [Header("In-Hand Directional Sprites")]
    public Sprite heldSprite;
    public Sprite heldSpriteDown;
    public Sprite heldSpriteRight;
    public Sprite heldSpriteUp;
    public Sprite[] heldAnimationSprites;

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
