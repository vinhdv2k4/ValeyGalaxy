#if false
using GameSource.Tools;

namespace GameSource
{
  public class ReachingToolEnchantment : BaseEnchantment
  {
    public override string GetName() => "Expansive";

    public override bool CanApplyTo(Item item) => item is Tool && (item is WateringCan || item is Hoe) && (item as Tool).UpgradeLevel == 4;
  }
}
#endif
