#if false
using Netcode;

namespace GameSource.Network
{
  public class NetFarmerRoot : NetRoot<Farmer>
  {
    public NetFarmerRoot() => this.Serializer = SaveGame.farmerSerializer;

    public NetFarmerRoot(Farmer value)
      : base(value)
    {
      this.Serializer = SaveGame.farmerSerializer;
    }

    public override NetRoot<Farmer> Clone()
    {
      NetRoot<Farmer> netRoot = base.Clone();
      if ((NetFieldBase<Farmer, NetRef<Farmer>>) Game1.serverHost != (NetRef<Farmer>) null && netRoot.Value != null)
        netRoot.Value.teamRoot = Game1.serverHost.Value.teamRoot;
      return netRoot;
    }
  }
}
#endif
