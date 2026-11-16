#if false
using Netcode;
using System.Collections.Generic;

namespace GameSource.Quests
{
  public class NetDescriptionElementList : NetList<DescriptionElement, NetDescriptionElementRef>
  {
    public NetDescriptionElementList()
    {
    }

    public NetDescriptionElementList(IEnumerable<DescriptionElement> values)
      : base(values)
    {
    }

    public NetDescriptionElementList(int capacity)
      : base(capacity)
    {
    }
  }
}
#endif
