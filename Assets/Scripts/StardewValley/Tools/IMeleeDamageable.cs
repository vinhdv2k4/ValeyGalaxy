using UnityEngine;

/// <summary>Combat targets opt in without coupling MeleeWeapon to enemy classes.</summary>
public interface IMeleeDamageable
{
    void TakeMeleeHit(int damage, Vector2 knockback);
}
