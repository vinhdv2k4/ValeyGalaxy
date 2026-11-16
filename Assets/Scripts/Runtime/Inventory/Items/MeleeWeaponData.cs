using UnityEngine;

/// <summary>
/// Melee weapon types:
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
/// OOP & SOLID: Represents melee weapons (Swords, Scythes, Daggers, Clubs).
/// Inherits from ToolData ('public class MeleeWeapon : Tool').
/// Encapsulates weapon types, damage ranges, attack speeds, knockback, and critical strike attributes.
/// </summary>
[CreateAssetMenu(fileName = "NewMeleeWeapon", menuName = "Stardew/Inventory/Melee Weapon Data")]
public class MeleeWeaponData : ToolData
{
    [Header("Melee Weapon Properties (MeleeWeapon : Tool)")]
    public MeleeWeaponType weaponType = MeleeWeaponType.Sword;
    public int minDamage = 2;
    public int maxDamage = 5;
    public float knockback = 1f;
    public float speed = 0f;
    public float critChance = 0.02f;
    public float critMultiplier = 3f;
    public int addedDefense = 0;

    internal static MeleeWeaponData Create(string id, string name, Sprite icon, MeleeWeaponType type)
    {
        var weapon = CreateInstance<MeleeWeaponData>();
        weapon.itemId = id;
        weapon.displayName = name;
        weapon.icon = weapon.heldSprite = icon;
        weapon.maxStackSize = 1;
        weapon.kind = PlayerItemKind.Sword;
        weapon.weaponType = type;
        weapon.hasUseAnimation = true;
        weapon.useAnimation = FarmerAnimationAction.SwordSwipe;
        weapon.staminaCost = 0;
        return weapon;
    }

    protected override void Reset()
    {
        base.Reset();
        kind = PlayerItemKind.Sword;
        useAnimation = FarmerAnimationAction.SwordSwipe;
        staminaCost = 0f; // Swords do not consume stamina
    }

    public bool IsScythe()
    {
        return itemId.ToLower().Contains("scythe") || displayName.ToLower().Contains("liềm");
    }
}
