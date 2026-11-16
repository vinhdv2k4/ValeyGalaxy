#if false
using GameSource.Tools;

namespace GameSource
{
  public class BaseWeaponEnchantment : BaseEnchantment
  {
    public override bool CanApplyTo(Item item) => item is MeleeWeapon && !(item as MeleeWeapon).isScythe();

    public void OnSwing(MeleeWeapon weapon, Farmer farmer) => this._OnSwing(weapon, farmer);

    protected virtual void _OnSwing(MeleeWeapon weapon, Farmer farmer)
    {
    }
  }
}
#endif
