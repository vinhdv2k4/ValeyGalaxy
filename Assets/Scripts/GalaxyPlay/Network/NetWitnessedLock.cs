#if false
using Netcode;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource.Network
{
  public class NetWitnessedLock : INetObject<NetFields>
  {
    private readonly NetBool requested = new NetBool().Interpolated(false, false);
    private readonly NetFarmerCollection witnesses = new NetFarmerCollection();
    private Action acquired;

    [XmlIgnore]
    public NetFields NetFields { get; } = new NetFields();

    public NetWitnessedLock() => this.NetFields.AddFields((INetSerializable) this.requested, (INetSerializable) this.witnesses.NetFields);

    public void RequestLock(Action acquired, Action failed)
    {
      if (!Game1.IsMasterGame)
        throw new InvalidOperationException();
      if (acquired == null)
        throw new ArgumentException();
      if ((bool) (NetFieldBase<bool, NetBool>) this.requested)
      {
        failed();
      }
      else
      {
        this.requested.Value = true;
        this.acquired = acquired;
      }
    }

    public bool IsLocked() => (bool) (NetFieldBase<bool, NetBool>) this.requested;

    public void Update()
    {
      this.witnesses.RetainOnlinePlayers();
      if (!(bool) (NetFieldBase<bool, NetBool>) this.requested)
        return;
      if (!this.witnesses.Contains(Game1.player))
        this.witnesses.Add(Game1.player);
      if (!Game1.IsMasterGame)
        return;
      foreach (Farmer farmer in (IEnumerable<Farmer>) Game1.otherFarmers.Values)
      {
        if (!this.witnesses.Contains(farmer))
          return;
      }
      this.acquired();
      this.acquired = (Action) null;
      this.requested.Value = false;
      this.witnesses.Clear();
    }
  }
}
#endif
