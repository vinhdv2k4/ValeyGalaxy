#if false
using Netcode;
using System;
using System.Collections.Generic;

namespace GameSource
{
  public class ReachMineFloorObjective : OrderObjective
  {
    public NetBool skullCave = new NetBool(false);

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.skullCave);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      base.Load(order, data);
      if (!data.ContainsKey("SkullCave") || !(data["SkullCave"].ToLowerInvariant() == "true"))
        return;
      this.skullCave.Value = true;
    }

    protected override void _Register()
    {
      base._Register();
      this._order.onMineFloorReached += new Action<Farmer, int>(this.OnNewValue);
    }

    protected override void _Unregister()
    {
      base._Unregister();
      this._order.onMineFloorReached -= new Action<Farmer, int>(this.OnNewValue);
    }

    public virtual void OnNewValue(Farmer who, int new_value)
    {
      if (this.skullCave.Value)
        new_value -= 120;
      else if (new_value > 120)
        return;
      if (new_value <= 0)
        return;
      this.SetCount(Math.Min(Math.Max(new_value, this.currentCount.Value), this.GetMaxCount()));
    }
  }
}
#endif
