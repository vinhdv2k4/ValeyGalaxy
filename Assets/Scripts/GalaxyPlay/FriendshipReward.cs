#if false
using Netcode;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource
{
  public class FriendshipReward : OrderReward
  {
    [XmlElement("targetName")]
    public NetString targetName = new NetString();
    [XmlElement("amount")]
    public NetInt amount = new NetInt();

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.targetName, (INetSerializable) this.amount);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      string requester = (string) (NetFieldBase<string, NetString>) order.requester;
      if (data.ContainsKey("TargetName"))
        requester = data["TargetName"];
      this.targetName.Value = order.Parse(requester);
      string data1 = "250";
      if (data.ContainsKey("Amount"))
        data1 = data["Amount"];
      this.amount.Value = int.Parse(order.Parse(data1));
    }

    public override void Grant()
    {
      NPC characterFromName = Game1.getCharacterFromName(this.targetName.Value);
      if (characterFromName == null)
        return;
      Game1.player.changeFriendship(this.amount.Value, characterFromName);
    }
  }
}
#endif
