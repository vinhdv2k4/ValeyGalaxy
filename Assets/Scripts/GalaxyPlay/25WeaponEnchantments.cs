#if false
using GameSource.Tools;

namespace GameSource
{
  public class EfficientToolEnchantment : BaseEnchantment
  {
    public override string GetName() => "Efficient";

    public override bool CanApplyTo(Item item) => item is Tool && !(item is MilkPail) && !(item is MeleeWeapon) && !(item is Shears) && !(item is Pan) && !(item is Wand) && !(item is Slingshot);

    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is Tool tool))
        return;
      tool.IsEfficient = true;
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is Tool tool))
        return;
      tool.IsEfficient = false;
    }
  }
}
#endif
