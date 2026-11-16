#if false
using Microsoft.Xna.Framework;
using Netcode;
using GameSource.Network;
using System;
using System.Xml.Serialization;

namespace GameSource
{
  public class Chunk : INetObject<NetFields>
  {
    [XmlElement("position")]
    public NetPosition position = new NetPosition();
    [XmlIgnore]
    public readonly NetFloat xVelocity = new NetFloat().Interpolated(true, true);
    [XmlIgnore]
    public readonly NetFloat yVelocity = new NetFloat().Interpolated(true, true);
    [XmlIgnore]
    public readonly NetBool hasPassedRestingLineOnce = new NetBool(false);
    [XmlIgnore]
    public int bounces;
    private readonly NetInt netDebrisType = new NetInt();
    [XmlIgnore]
    public bool hitWall;
    [XmlElement("xSpriteSheet")]
    public readonly NetInt xSpriteSheet = new NetInt();
    [XmlElement("ySpriteSheet")]
    public readonly NetInt ySpriteSheet = new NetInt();
    [XmlIgnore]
    public float rotation;
    [XmlIgnore]
    public float rotationVelocity;
    private readonly NetFloat netScale = new NetFloat().Interpolated(true, true);
    private readonly NetFloat netAlpha = new NetFloat();

    public int debrisType
    {
      get => (int) (NetFieldBase<int, NetInt>) this.netDebrisType;
      set => this.netDebrisType.Value = value;
    }

    public float scale
    {
      get => (float) (NetFieldBase<float, NetFloat>) this.netScale;
      set => this.netScale.Value = value;
    }

    public float alpha
    {
      get => (float) (NetFieldBase<float, NetFloat>) this.netAlpha;
      set => this.netAlpha.Value = value;
    }

    [XmlIgnore]
    public NetFields NetFields { get; } = new NetFields();

    public Chunk()
    {
      this.NetFields.AddFields((INetSerializable) this.position.NetFields, (INetSerializable) this.xVelocity, (INetSerializable) this.yVelocity, (INetSerializable) this.netDebrisType, (INetSerializable) this.xSpriteSheet, (INetSerializable) this.ySpriteSheet, (INetSerializable) this.netScale, (INetSerializable) this.netAlpha, (INetSerializable) this.hasPassedRestingLineOnce);
      if (LocalMultiplayer.IsLocalMultiplayer(true))
        this.NetFields.DeltaAggregateTicks = (ushort) 10;
      else
        this.NetFields.DeltaAggregateTicks = (ushort) 30;
    }

    public Chunk(Vector2 position, float xVelocity, float yVelocity, int debrisType)
      : this()
    {
      this.position.Value = position;
      this.xVelocity.Value = xVelocity;
      this.yVelocity.Value = yVelocity;
      this.debrisType = debrisType;
      this.alpha = 1f;
    }

    public float getSpeed() => (float) Math.Sqrt((double) (float) (NetFieldBase<float, NetFloat>) this.xVelocity * (double) (float) (NetFieldBase<float, NetFloat>) this.xVelocity + (double) (float) (NetFieldBase<float, NetFloat>) this.yVelocity * (double) (float) (NetFieldBase<float, NetFloat>) this.yVelocity);
  }
}
#endif
