#if false
using Netcode;
using GameSource.Network;

namespace GameSource
{
  public class MovieInvitation : INetObject<NetFields>
  {
    private NetFarmerRef _farmer = new NetFarmerRef();
    protected NetString _invitedNPCName = new NetString();
    protected NetBool _fulfilled = new NetBool(false);

    public NetFields NetFields { get; } = new NetFields();

    public Farmer farmer
    {
      get => this._farmer.Value;
      set => this._farmer.Value = value;
    }

    public NPC invitedNPC
    {
      get => Game1.getCharacterFromName(this._invitedNPCName.Value);
      set
      {
        if (value == null)
          this._invitedNPCName.Set((string) null);
        else
          this._invitedNPCName.Set((string) (NetFieldBase<string, NetString>) value.name);
      }
    }

    public bool fulfilled
    {
      get => this._fulfilled.Value;
      set => this._fulfilled.Set(value);
    }

    public MovieInvitation() => this.NetFields.AddFields((INetSerializable) this._farmer.NetFields, (INetSerializable) this._invitedNPCName, (INetSerializable) this._fulfilled);
  }
}
#endif
