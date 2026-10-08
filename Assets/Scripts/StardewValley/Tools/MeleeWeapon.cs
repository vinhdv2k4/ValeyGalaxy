using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity runtime profile for a Stardew-style MeleeWeapon (Swords, Scythes, Clubs, Daggers).
/// Strictly inherits from Tool, mirroring decompiled Stardew Valley 'public class MeleeWeapon : Tool'.
/// Original decompiled XNA source is preserved in MeleeWeapon.Decompiled.txt for reference.
/// </summary>
public class MeleeWeapon : Tool
{
    public const int DefenseCooldownTime = 1500;
    public const int AttackSwordCooldownTime = 2000;
    public const int DaggerCooldownTime = 3000;
    public const int ClubCooldownTime = 6000;
    public const int MillisecondsPerSpeedPoint = 40;
    public const int DefaultSpeed = 400;

    public const int StabbingSword = 0;
    public const int Dagger = 1;
    public const int Club = 2;
    public const int DefenseSword = 3;
    public const int Scythe = 47;
    public const int GoldenScythe = 53;

    public int WeaponType { get; set; }
    public int MinDamage { get; set; }
    public int MaxDamage { get; set; }
    public float Knockback { get; set; }
    public float Speed { get; set; }
    public float AddedArea { get; set; }
    public float CritChance { get; set; }
    public float CritMultiplier { get; set; }
    public int AddedPrecision { get; set; }
    public int AddedDefense { get; set; }

    public MeleeWeapon(
        PlayerItemKind kind = PlayerItemKind.Sword,
        int initialAtlasIndex = -1,
        IToolVisualStrategy visualStrategy = null,
        int weaponType = StabbingSword,
        int minDamage = 2,
        int maxDamage = 5)
        : base(kind, initialAtlasIndex, FarmerAnimationAction.SwordSwipe, visualStrategy ?? new MeleeWeaponVisualStrategy())
    {
        WeaponType = weaponType == StabbingSword ? DefenseSword : weaponType;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        Knockback = 1f;
        Speed = 0f;
        CritChance = 0.02f;
        CritMultiplier = 3f;
    }

    public bool IsScythe()
    {
        return InitialAtlasIndex == Scythe || InitialAtlasIndex == GoldenScythe;
    }

    public override float ImpactFraction => 100f / 275f;

    public override bool Apply(ItemUseContext context)
    {
        if (context.User == null) return false;
        var data = context.Item?.itemData as MeleeWeaponData;
        int minimum = data == null ? MinDamage : data.minDamage;
        int maximum = data == null ? MaxDamage : data.maxDamage;
        float force = data == null ? Knockback : data.knockback;
        Vector2 facing = context.Facing.sqrMagnitude > 0f ? context.Facing.normalized : Vector2.down;
        Vector2 center = (Vector2)context.User.position + facing * 0.65f;
        var targets = Physics2D.OverlapBoxAll(center, new Vector2(1.1f, 1.1f), 0f);
        var hit = new HashSet<IMeleeDamageable>();
        foreach (var target in targets)
        {
            if (target.transform.IsChildOf(context.User)) continue;
            foreach (var behaviour in target.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is not IMeleeDamageable receiver || !hit.Add(receiver)) continue;
                receiver.TakeMeleeHit(Random.Range(minimum, maximum + 1), facing * force);
            }
        }
        bool hitResource = context.Farming != null && context.Farming.TryHitResource(facing, context);
        return hit.Count > 0 || hitResource;
    }
}
