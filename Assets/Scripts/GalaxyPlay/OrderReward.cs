#if false
using Netcode;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource
{
  public class OrderReward : INetObject<NetFields>
  {
    [XmlIgnore]
    public NetFields NetFields { get; } = new NetFields();

    public OrderReward() => this.InitializeNetFields();

    public virtual void InitializeNetFields()
    {
    }

    public virtual void Grant()
    {
    }

    public virtual void Load(SpecialOrder order, Dictionary<string, string> data)
    {
    }
  }
}
#endif
