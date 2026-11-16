#if false
using Netcode;
using GameSource.Objects;
using System.Xml.Serialization;

namespace GameSource.Quests
{
  public class CraftingQuest : Quest
  {
    [XmlElement("isBigCraftable")]
    public readonly NetBool isBigCraftable = new NetBool();
    [XmlElement("indexToCraft")]
    public readonly NetInt indexToCraft = new NetInt();

    public CraftingQuest()
    {
    }

    public CraftingQuest(int indexToCraft, bool bigCraftable)
    {
      this.indexToCraft.Value = indexToCraft;
      this.isBigCraftable.Value = bigCraftable;
    }

    protected override void initNetFields()
    {
      base.initNetFields();
      this.NetFields.AddFields((INetSerializable) this.isBigCraftable, (INetSerializable) this.indexToCraft);
    }

    public override bool checkIfComplete(NPC n = null, int number1 = -1, int number2 = -2, Item item = null, string str = null)
    {
      switch (item)
      {
        case Clothing _:
          return false;
        case Object _:
          if ((item as Object).bigCraftable.Value == this.isBigCraftable.Value && (item as Object).parentSheetIndex.Value == this.indexToCraft.Value)
          {
            this.questComplete();
            return true;
          }
          break;
      }
      return false;
    }
  }
}
#endif
