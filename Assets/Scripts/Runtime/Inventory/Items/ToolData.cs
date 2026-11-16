using UnityEngine;

/// <summary>
/// OOP & SOLID: Represents tools (Hoe, Axe, Pickaxe, WateringCan, etc.).
/// Inherits from ItemData ('Tool : Item').
/// Encapsulates upgrade levels, energy costs, and tool charging.
/// </summary>
[CreateAssetMenu(fileName = "NewTool", menuName = "Stardew/Inventory/Tool Data")]
public class ToolData : ItemData
{
    [Header("Tool Properties (Tool : Item)")]
    [Range(0, 4)]
    public int upgradeLevel = 0; // 0=Basic, 1=Copper, 2=Steel, 3=Gold, 4=Iridium
    public int initialParentTileIndex;
    public int currentParentTileIndex;
    public float staminaCost = 2f;
    public float animationSpeedModifier = 1f;

    protected virtual void Reset()
    {
        maxStackSize = 1;
        hasUseAnimation = true;
        useAnimation = FarmerAnimationAction.Tool;
    }

    public virtual bool IsHeavyHitter()
    {
        return kind == PlayerItemKind.Axe || kind == PlayerItemKind.Pickaxe || kind == PlayerItemKind.Hoe;
    }
}
