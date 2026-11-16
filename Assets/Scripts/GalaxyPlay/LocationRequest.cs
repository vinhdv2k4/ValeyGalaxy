#if false
using Netcode;

namespace GameSource
{
  public class LocationRequest
  {
    public string Name;
    public bool IsStructure;
    public GameLocation Location;

    public event LocationRequest.Callback OnLoad;

    public event LocationRequest.Callback OnWarp;

    public LocationRequest(string name, bool isStructure, GameLocation location)
    {
      this.Name = name;
      this.IsStructure = isStructure;
      this.Location = location;
    }

    public void Loaded(GameLocation location)
    {
      if (this.OnLoad == null)
        return;
      this.OnLoad();
    }

    public void Warped(GameLocation location)
    {
      if (this.OnWarp != null)
        this.OnWarp();
      Game1.player.ridingMineElevator = false;
      Game1.forceSnapOnNextViewportUpdate = true;
    }

    public bool IsRequestFor(GameLocation location)
    {
      if (!this.IsStructure && location.Name == this.Name)
        return true;
      return location.uniqueName.Value == this.Name && (bool) (NetFieldBase<bool, NetBool>) location.isStructure;
    }

    public bool IsRequestFor(string name, bool isStructure) => !this.IsStructure ? name == this.Name : name == this.Name & isStructure;

    public override string ToString() => "LocationRequest(" + this.Name + ", " + this.IsStructure.ToString() + ")";

    public delegate void Callback();
  }
}
#endif
