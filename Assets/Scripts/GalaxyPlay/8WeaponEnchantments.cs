#if false
namespace GameSource
{
  public class GalaxySoulEnchantment : BaseWeaponEnchantment
  {
    public override bool IsSecondaryEnchantment() => true;

    public override bool IsForge() => false;

    public override int GetMaximumLevel() => 3;

    public override bool ShouldBeDisplayed() => false;
  }
}
#endif
