#if false
using Netcode;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource
{
  public class FishObjective : OrderObjective
  {
    [XmlElement("acceptableContextTagSets")]
    public NetStringList acceptableContextTagSets = new NetStringList();

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.acceptableContextTagSets);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      if (!data.ContainsKey("AcceptedContextTags"))
        return;
      this.acceptableContextTagSets.Add(order.Parse(data["AcceptedContextTags"]));
    }

    protected override void _Register()
    {
      base._Register();
      this._order.onFishCaught += new Action<Farmer, Item>(this.OnFishCaught);
    }

    protected override void _Unregister()
    {
      base._Unregister();
      this._order.onFishCaught -= new Action<Farmer, Item>(this.OnFishCaught);
    }

    public virtual void OnFishCaught(Farmer farmer, Item fish_item)
    {
      foreach (string acceptableContextTagSet in (NetList<string, NetString>) this.acceptableContextTagSets)
      {
        bool flag1 = false;
        foreach (string str1 in acceptableContextTagSet.Split(','))
        {
          bool flag2 = false;
          foreach (string str2 in str1.Split('/'))
          {
            if (fish_item.HasContextTag(str2.Trim()))
            {
              flag2 = true;
              break;
            }
          }
          if (!flag2)
            flag1 = true;
        }
        if (!flag1)
        {
          this.IncrementCount(fish_item.Stack);
          break;
        }
      }
    }
  }
}
#endif
