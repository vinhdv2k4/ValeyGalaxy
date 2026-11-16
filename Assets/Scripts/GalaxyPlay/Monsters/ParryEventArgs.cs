#if false
using Netcode;
using System.IO;

namespace GameSource.Monsters
{
  internal class ParryEventArgs : NetEventArg
  {
    public int damage;
    private long farmerId;

    public Farmer who
    {
      get => Game1.getFarmer(this.farmerId);
      set => this.farmerId = value.UniqueMultiplayerID;
    }

    public ParryEventArgs()
    {
    }

    public ParryEventArgs(int damage, Farmer who)
    {
      this.damage = damage;
      this.who = who;
    }

    public void Read(BinaryReader reader)
    {
      this.damage = reader.ReadInt32();
      this.farmerId = reader.ReadInt64();
    }

    public void Write(BinaryWriter writer)
    {
      writer.Write(this.damage);
      writer.Write(this.farmerId);
    }
  }
}
#endif
