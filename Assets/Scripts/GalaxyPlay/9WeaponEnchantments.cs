#if false
using GameSource.Tools;
using System;
using System.Collections.Generic;

namespace GameSource
{
  public class RubyEnchantment : BaseWeaponEnchantment
  {
    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      string[] strArray = Game1.temporaryContent.Load<Dictionary<int, string>>("Data\\weapons")[meleeWeapon.InitialParentTileIndex].Split('/');
      int int32_1 = Convert.ToInt32(strArray[2]);
      int int32_2 = Convert.ToInt32(strArray[3]);
      meleeWeapon.minDamage.Value += Math.Max(1, (int) ((double) int32_1 * 0.100000001490116)) * this.GetLevel();
      meleeWeapon.maxDamage.Value += Math.Max(1, (int) ((double) int32_2 * 0.100000001490116)) * this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is MeleeWeapon meleeWeapon))
        return;
      string[] strArray = Game1.temporaryContent.Load<Dictionary<int, string>>("Data\\weapons")[meleeWeapon.InitialParentTileIndex].Split('/');
      int int32_1 = Convert.ToInt32(strArray[2]);
      int int32_2 = Convert.ToInt32(strArray[3]);
      meleeWeapon.minDamage.Value -= Math.Max(1, (int) ((double) int32_1 * 0.100000001490116)) * this.GetLevel();
      meleeWeapon.maxDamage.Value -= Math.Max(1, (int) ((double) int32_2 * 0.100000001490116)) * this.GetLevel();
    }

    public override bool ShouldBeDisplayed() => false;

    public override bool IsForge() => true;
  }
}
#endif
