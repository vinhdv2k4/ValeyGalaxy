#if false
using GameSource.Monsters;

namespace GameSource
{
  public class CrusaderEnchantment : BaseWeaponEnchantment
  {
    protected override void _OnDealDamage(
      Monster monster,
      GameLocation location,
      Farmer who,
      ref int amount)
    {
      switch (monster)
      {
        case Ghost _:
        case Skeleton _:
        case Mummy _:
        case ShadowBrute _:
        case ShadowShaman _:
        case ShadowGirl _:
        case ShadowGuy _:
        case Shooter _:
          amount = (int) ((double) amount * 1.5);
          break;
      }
    }

    public override string GetName() => "Crusader";
  }
}
#endif
