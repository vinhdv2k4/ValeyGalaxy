#if false
using Netcode;
using System;
using System.Collections.Generic;

namespace GameSource
{
  public class ResetEventReward : OrderReward
  {
    public NetIntList resetEvents = new NetIntList();

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.resetEvents);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      foreach (string str in order.Parse(data["ResetEvents"]).Split(' '))
        this.resetEvents.Add(Convert.ToInt32(str));
    }

    public override void Grant()
    {
      foreach (int resetEvent in (NetList<int, NetInt>) this.resetEvents)
        Game1.player.eventsSeen.Remove(resetEvent);
    }
  }
}
#endif
