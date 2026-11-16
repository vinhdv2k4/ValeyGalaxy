#if false
using Netcode;
using GameSource.Buildings;
using System.Collections;
using System.Collections.Generic;

namespace GameSource.Network
{
  public class NetBuildingRef : INetObject<NetFields>, IEnumerable<Building>, IEnumerable
  {
    private readonly NetString nameOfIndoors = new NetString();

    public NetFields NetFields { get; } = new NetFields();

    public Building Value
    {
      get
      {
        string name = this.nameOfIndoors.Get();
        return name == null ? (Building) null : Game1.getFarm().getBuildingByName(name);
      }
      set
      {
        if (value == null)
          this.nameOfIndoors.Value = (string) null;
        else
          this.nameOfIndoors.Value = value.nameOfIndoors;
      }
    }

    public NetBuildingRef() => this.NetFields.AddFields((INetSerializable) this.nameOfIndoors);

    public IEnumerator<Building> GetEnumerator()
    {
      yield return this.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();

    public static implicit operator Building(NetBuildingRef buildingRef) => buildingRef.Value;
  }
}
#endif
