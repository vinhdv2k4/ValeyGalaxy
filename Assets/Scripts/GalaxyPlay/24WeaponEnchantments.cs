#if false
using GameSource.Tools;

namespace GameSource
{
  public class PowerfulEnchantment : BaseEnchantment
  {
    public override string GetName() => "Powerful";

    public override bool CanApplyTo(Item item)
    {
      if (!(item is Tool))
        return false;
      return item is Pickaxe || item is Axe;
    }

    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is Tool tool))
        return;
      if (tool is Pickaxe)
        (tool as Pickaxe).additionalPower.Value += this.GetLevel();
      if (!(tool is Axe))
        return;
      (tool as Axe).additionalPower.Value += 2 * this.GetLevel();
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is Tool tool))
        return;
      if (tool is Pickaxe)
        (tool as Pickaxe).additionalPower.Value -= this.GetLevel();
      if (!(tool is Axe))
        return;
      (tool as Axe).additionalPower.Value -= 2 * this.GetLevel();
    }
  }
}
#endif
