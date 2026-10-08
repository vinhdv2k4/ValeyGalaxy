using UnityEngine;

/// <summary>
/// Weapon types matching decompiled Stardew Valley MeleeWeapon:
/// 0 = Sword, 1 = Dagger, 2 = Club/Hammer, 3 = Defensive Sword / Scythe.
/// </summary>
public enum MeleeWeaponType
{
    Sword = 0,
    Dagger = 1,
    Club = 2,
    DefenseSword = 3
}

/// <summary>
/// OOP & SOLID: Represents melee weapons in Stardew Valley (Swords, Scythes, Daggers, Clubs).
/// Inherits from ToolData, strictly following decompiled Stardew Valley 'public class MeleeWeapon : Tool'.
/// Encapsulates weapon types, damage ranges, attack speeds, knockback, and critical strike attributes.
/// </summary>
[CreateAssetMenu(fileName = "NewMeleeWeapon", menuName = "Stardew/Inventory/Melee Weapon Data")]
public class MeleeWeaponData : ToolData
{
    [Header("Melee Weapon Properties (Stardew Valley MeleeWeapon : Tool)")]
    public MeleeWeaponType weaponType = MeleeWeaponType.Sword;
    public int minDamage = 2;
    public int maxDamage = 5;
    public float knockback = 1f;
    public float speed = 0f;
    public float critChance = 0.02f;
    public float critMultiplier = 3f;
    public int addedDefense = 0;

    protected override void Reset()
    {
        base.Reset();
        kind = PlayerItemKind.Sword;
        useAnimation = FarmerAnimationAction.SwordSwipe;
        staminaCost = 0f; // Swords in Stardew Valley do not consume stamina
    }

    public bool IsScythe()
    {
        return itemId.ToLower().Contains("scythe") || displayName.ToLower().Contains("liềm");
    }
}
