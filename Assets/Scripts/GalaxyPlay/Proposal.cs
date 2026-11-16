#if false
using Netcode;
using GameSource.Network;

namespace GameSource
{
  public class Proposal : INetObject<NetFields>
  {
    public readonly NetFarmerRef sender = new NetFarmerRef();
    public readonly NetFarmerRef receiver = new NetFarmerRef();
    public readonly NetEnum<ProposalType> proposalType = new NetEnum<ProposalType>(ProposalType.Gift);
    public readonly NetEnum<ProposalResponse> response = new NetEnum<ProposalResponse>(ProposalResponse.None);
    public readonly NetString responseMessageKey = new NetString();
    public readonly NetRef<Item> gift = new NetRef<Item>();
    public readonly NetBool canceled = new NetBool();
    public readonly NetBool cancelConfirmed = new NetBool();

    public NetFields NetFields { get; } = new NetFields();

    public Proposal() => this.NetFields.AddFields((INetSerializable) this.sender.NetFields, (INetSerializable) this.receiver.NetFields, (INetSerializable) this.proposalType, (INetSerializable) this.response, (INetSerializable) this.responseMessageKey, (INetSerializable) this.gift, (INetSerializable) this.canceled, (INetSerializable) this.cancelConfirmed);
  }
}
#endif
