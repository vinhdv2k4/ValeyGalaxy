#if false
namespace GameSource
{
  public class AutoHookEnchantment : FishingRodEnchantment
  {
    public override string GetName() => "Auto-Hook";

    protected override void _ApplyTo(Item item) => base._ApplyTo(item);

    protected override void _UnapplyTo(Item item) => base._UnapplyTo(item);
  }
}
#endif
