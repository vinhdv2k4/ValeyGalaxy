#if false
using Netcode;
using System.Collections.Generic;

namespace GameSource
{
  public class MoneyReward : OrderReward
  {
    public NetInt amount = new NetInt(0);
    public NetFloat multiplier = new NetFloat(1f);

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.amount, (INetSerializable) this.multiplier);
    }

    public virtual int GetRewardMoneyAmount() => (int) ((double) this.amount.Value * (double) this.multiplier.Value);

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      this.amount.Value = int.Parse(order.Parse(data["Amount"]));
      if (!data.ContainsKey("Multiplier"))
        return;
      this.multiplier.Value = float.Parse(order.Parse(data["Multiplier"]));
    }

    public override void Grant() => base.Grant();
  }
}
#endif
