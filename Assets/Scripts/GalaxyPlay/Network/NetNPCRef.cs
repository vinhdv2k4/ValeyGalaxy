#if false
using Netcode;
using System;

namespace GameSource.Network
{
  public class NetNPCRef : INetObject<NetFields>
  {
    private readonly NetGuid guid = new NetGuid();

    public NetFields NetFields { get; } = new NetFields();

    public NetNPCRef() => this.NetFields.AddFields((INetSerializable) this.guid);

    public NPC Get(GameLocation location)
    {
      if (this.guid.Value == Guid.Empty || location == null)
        return (NPC) null;
      return !location.characters.ContainsGuid(this.guid.Value) ? (NPC) null : location.characters[this.guid.Value];
    }

    public void Set(GameLocation location, NPC npc)
    {
      if (npc == null)
      {
        this.guid.Value = Guid.Empty;
      }
      else
      {
        Guid guid = location.characters.GuidOf(npc);
        this.guid.Value = !(guid == Guid.Empty) ? guid : throw new ArgumentException();
      }
    }

    public void Clear() => this.guid.Value = Guid.Empty;
  }
}
#endif
