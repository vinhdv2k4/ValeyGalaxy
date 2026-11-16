#if false
using Netcode;
using System.Xml.Serialization;

namespace GameSource.Quests
{
  public class GoSomewhereQuest : Quest
  {
    [XmlElement("whereToGo")]
    public readonly NetString whereToGo = new NetString();

    public GoSomewhereQuest()
    {
    }

    public GoSomewhereQuest(string where) => this.whereToGo.Value = where;

    protected override void initNetFields()
    {
      base.initNetFields();
      this.NetFields.AddFields((INetSerializable) this.whereToGo);
    }

    public override void adjustGameLocation(GameLocation location) => this.checkIfComplete((NPC) null, -1, -2, (Item) null, (string) (NetFieldBase<string, NetString>) location.name);

    public override bool checkIfComplete(NPC n = null, int number1 = -1, int number2 = -2, Item item = null, string str = null)
    {
      if (str == null || !str.Equals(this.whereToGo.Value))
        return false;
      this.questComplete();
      return true;
    }
  }
}
#endif
