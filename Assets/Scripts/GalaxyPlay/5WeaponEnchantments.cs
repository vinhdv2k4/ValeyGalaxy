#if false
using GameSource.Tools;

namespace GameSource
{
  public class AquamarineEnchantment : BaseWeaponEnchantment
  {
    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.critChance.Value += 23f / 500f * (float) this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.critChance.Value -= 23f / 500f * (float) this.GetLevel();
    }

    public override bool ShouldBeDisplayed() => false;

    public override bool IsForge() => true;
  }
}
#endif
