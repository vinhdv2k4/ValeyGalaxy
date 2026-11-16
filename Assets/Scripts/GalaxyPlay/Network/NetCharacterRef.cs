#if false
using Netcode;
using System;

namespace GameSource.Network
{
  public class NetCharacterRef : INetObject<NetFields>
  {
    private readonly NetNPCRef npc = new NetNPCRef();
    private readonly NetFarmerRef farmer = new NetFarmerRef();

    public NetFields NetFields { get; } = new NetFields();

    public NetCharacterRef() => this.NetFields.AddFields((INetSerializable) this.npc.NetFields, (INetSerializable) this.farmer.NetFields);

    public Character Get(GameLocation location) => (Character) this.npc.Get(location) ?? (Character) this.farmer.Value;

    public void Set(GameLocation location, Character character)
    {
      switch (character)
      {
        case NPC _:
          this.npc.Set(location, character as NPC);
          this.farmer.Value = (Farmer) null;
          break;
        case Farmer _:
          this.npc.Clear();
          this.farmer.Value = character as Farmer;
          break;
        default:
          throw new ArgumentException();
      }
    }

    public void Clear()
    {
      this.npc.Clear();
      this.farmer.Value = (Farmer) null;
    }
  }
}
#endif
