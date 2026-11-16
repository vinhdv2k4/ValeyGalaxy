#if false
using Netcode;

namespace GameSource.Quests
{
  public class NetDescriptionElementRef : 
    NetExtendableRef<DescriptionElement, NetDescriptionElementRef>
  {
    public NetDescriptionElementRef() => this.Serializer = DescriptionElement.serializer;

    public NetDescriptionElementRef(DescriptionElement value)
      : base(value)
    {
      this.Serializer = DescriptionElement.serializer;
    }
  }
}
#endif
