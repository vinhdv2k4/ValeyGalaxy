#if false
using GameSource.Tools;

namespace GameSource
{
  public class BottomlessEnchantment : WateringCanEnchantment
  {
    public override string GetName() => "Bottomless";

    protected override void _ApplyTo(Item item)
    {
      base._ApplyTo(item);
      if (!(item is WateringCan wateringCan))
        return;
      wateringCan.IsBottomless = true;
      wateringCan.WaterLeft = wateringCan.waterCanMax;
    }

    protected override void _UnapplyTo(Item item)
    {
      base._UnapplyTo(item);
      if (!(item is WateringCan wateringCan))
        return;
      wateringCan.IsBottomless = false;
    }
  }
}
#endif
