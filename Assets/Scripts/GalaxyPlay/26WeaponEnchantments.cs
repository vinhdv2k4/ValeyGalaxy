#if false
using GameSource.Tools;

namespace GameSource
{
  public class SwiftToolEnchantment : BaseEnchantment
  {
    public override string GetName() => "Swift";

    public override bool CanApplyTo(Item item) => item is Tool && !(item is MilkPail) && !(item is MeleeWeapon) && !(item is Shears) && !(item is FishingRod) && !(item is Pan) && !(item is WateringCan) && !(item is Wand) && !(item is Slingshot);

    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is Tool tool))
        return;
      tool.AnimationSpeedModifier = 0.66f;
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is Tool tool))
        return;
      tool.AnimationSpeedModifier = 1f;
    }
  }
}
#endif
