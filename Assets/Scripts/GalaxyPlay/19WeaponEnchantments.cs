#if false
using GameSource.Tools;
using System.Xml.Serialization;

namespace GameSource
{
  [XmlInclude(typeof (FishingRodEnchantment))]
  public class FishingRodEnchantment : BaseEnchantment
  {
    public override bool CanApplyTo(Item item) => item is FishingRod;
  }
}
#endif
