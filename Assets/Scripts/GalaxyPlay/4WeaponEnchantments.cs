#if false
using GameSource.Tools;

namespace GameSource
{
  public class TopazEnchantment : BaseWeaponEnchantment
  {
    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.addedDefense.Value += this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      meleeWeapon.addedDefense.Value -= this.GetLevel();
    }

    public override bool ShouldBeDisplayed() => false;

    public override bool IsForge() => true;
  }
}
#endif
