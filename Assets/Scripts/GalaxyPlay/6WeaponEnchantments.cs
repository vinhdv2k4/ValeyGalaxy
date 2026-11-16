#if false
using GameSource.Tools;

namespace GameSource
{
  public class JadeEnchantment : BaseWeaponEnchantment
  {
    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.critMultiplier.Value += 0.1f * (float) this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.critMultiplier.Value -= 0.1f * (float) this.GetLevel();
    }

    public override bool ShouldBeDisplayed() => false;

    public override bool IsForge() => true;
  }
}
#endif
