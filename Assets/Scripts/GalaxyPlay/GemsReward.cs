#if false
using Netcode;
using System.Collections.Generic;

namespace GameSource
{
  public class GemsReward : OrderReward
  {
    public NetInt amount = new NetInt(0);

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.amount);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data) => this.amount.Value = int.Parse(order.Parse(data["Amount"]));

    public override void Grant() => Game1.player.QiGems += this.amount.Value;
  }
}
#endif
