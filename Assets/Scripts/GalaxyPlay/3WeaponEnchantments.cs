#if false
using GameSource.Tools;

namespace GameSource
{
  public class AmethystEnchantment : BaseWeaponEnchantment
  {
    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.knockback.Value += (float) this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.knockback.Value -= (float) this.GetLevel();
    }

    public override bool ShouldBeDisplayed() => false;

    public override bool IsForge() => true;
  }
}
#endif
