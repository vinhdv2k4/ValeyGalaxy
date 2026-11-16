#if false
using Netcode;
using System.Xml.Serialization;

namespace GameSource
{
  public class Warp : INetObject<NetFields>
  {
    [XmlElement("x")]
    private readonly NetInt x = new NetInt();
    [XmlElement("y")]
    private readonly NetInt y = new NetInt();
    [XmlElement("targetX")]
    private readonly NetInt targetX = new NetInt();
    [XmlElement("targetY")]
    private readonly NetInt targetY = new NetInt();
    [XmlElement("flipFarmer")]
    public readonly NetBool flipFarmer = new NetBool();
    [XmlElement("targetName")]
    private readonly NetString targetName = new NetString();
    [XmlElement("npcOnly")]
    public readonly NetBool npcOnly = new NetBool();

    [XmlIgnore]
    public NetFields NetFields { get; } = new NetFields();

    public int X => (int) (NetFieldBase<int, NetInt>) this.x;

    public int Y => (int) (NetFieldBase<int, NetInt>) this.y;

    public int TargetX
    {
      get => (int) (NetFieldBase<int, NetInt>) this.targetX;
      set => this.targetX.Value = value;
    }

    public int TargetY
    {
      get => (int) (NetFieldBase<int, NetInt>) this.targetY;
      set => this.targetY.Value = value;
    }

    public string TargetName
    {
      get => (string) (NetFieldBase<string, NetString>) this.targetName;
      set => this.targetName.Value = value;
    }

    public Warp() => this.NetFields.AddFields((INetSerializable) this.x, (INetSerializable) this.y, (INetSerializable) this.targetX, (INetSerializable) this.targetY, (INetSerializable) this.targetName, (INetSerializable) this.flipFarmer, (INetSerializable) this.npcOnly);

    public Warp(
      int x,
      int y,
      string targetName,
      int targetX,
      int targetY,
      bool flipFarmer,
      bool npcOnly = false)
      : this()
    {
      this.x.Value = x;
      this.y.Value = y;
      this.targetX.Value = targetX;
      this.targetY.Value = targetY;
      this.targetName.Value = targetName;
      this.flipFarmer.Value = flipFarmer;
      this.npcOnly.Value = npcOnly;
    }
  }
}
#endif
