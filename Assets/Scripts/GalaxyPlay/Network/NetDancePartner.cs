#if false
using Netcode;
using System;

namespace GameSource.Network
{
  public class NetDancePartner : INetObject<NetFields>
  {
    private readonly NetFarmerRef farmer = new NetFarmerRef();
    private readonly NetString villager = new NetString();

    public Character Value
    {
      get => this.GetCharacter();
      set => this.SetCharacter(value);
    }

    public NetFields NetFields { get; } = new NetFields();

    public NetDancePartner() => this.NetFields.AddFields((INetSerializable) this.farmer.NetFields, (INetSerializable) this.villager);

    public NetDancePartner(Farmer farmer) => this.farmer.Value = farmer;

    public NetDancePartner(string villagerName) => this.villager.Value = villagerName;

    public Character GetCharacter()
    {
      if (this.farmer.Value != null)
        return (Character) this.farmer.Value;
      return Game1.CurrentEvent != null && this.villager.Value != null ? (Character) Game1.CurrentEvent.getActorByName(this.villager.Value) : (Character) null;
    }

    public void SetCharacter(Character value)
    {
      switch (value)
      {
        case null:
          this.farmer.Value = (Farmer) null;
          this.villager.Value = (string) null;
          return;
        case Farmer _:
          this.farmer.Value = value as Farmer;
          this.villager.Value = (string) null;
          return;
        case NPC _:
          if ((value as NPC).isVillager())
          {
            this.farmer.Value = (Farmer) null;
            this.villager.Value = (value as NPC).Name;
            return;
          }
          break;
      }
      throw new ArgumentException(value.ToString());
    }

    public NPC TryGetVillager()
    {
      if (this.farmer.Value != null)
        return (NPC) null;
      return Game1.CurrentEvent != null && this.villager.Value != null ? Game1.CurrentEvent.getActorByName(this.villager.Value) : (NPC) null;
    }

    public Farmer TryGetFarmer() => this.farmer.Value;

    public bool IsFarmer() => this.TryGetFarmer() != null;

    public bool IsVillager() => this.TryGetVillager() != null;

    public int GetGender() => this.IsFarmer() ? (!this.TryGetFarmer().IsMale ? 1 : 0) : (this.IsVillager() ? this.TryGetVillager().Gender : 2);
  }
}
#endif
