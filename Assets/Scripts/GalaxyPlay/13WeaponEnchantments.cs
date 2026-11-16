#if false
using GameSource.Monsters;

namespace GameSource
{
  public class BugKillerEnchantment : BaseWeaponEnchantment
  {
    protected override void _OnDealDamage(
      Monster monster,
      GameLocation location,
      Farmer who,
      ref int amount)
    {
      switch (monster)
      {
        case Grub _:
        case Fly _:
        case Bug _:
        case Leaper _:
        case LavaCrab _:
        case RockCrab _:
          amount = (int) ((double) amount * 2.0);
          break;
      }
    }

    public override string GetName() => "Bug Killer";
  }
}
#endif
