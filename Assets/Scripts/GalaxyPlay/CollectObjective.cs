#if false
using Netcode;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource
{
  public class CollectObjective : OrderObjective
  {
    [XmlElement("acceptableContextTagSets")]
    public NetStringList acceptableContextTagSets = new NetStringList();

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      if (!data.ContainsKey("AcceptedContextTags"))
        return;
      this.acceptableContextTagSets.Add(order.Parse(data["AcceptedContextTags"]));
    }

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.acceptableContextTagSets);
    }

    protected override void _Register()
    {
      base._Register();
      this._order.onItemCollected += new Action<Farmer, Item>(this.OnItemShipped);
    }

    protected override void _Unregister()
    {
      base._Unregister();
      this._order.onItemCollected -= new Action<Farmer, Item>(this.OnItemShipped);
    }

    public virtual void OnItemShipped(Farmer farmer, Item item)
    {
      foreach (string acceptableContextTagSet in (NetList<string, NetString>) this.acceptableContextTagSets)
      {
        bool flag1 = false;
        foreach (string str1 in acceptableContextTagSet.Split(','))
        {
          bool flag2 = false;
          foreach (string str2 in str1.Split('/'))
          {
            if (item.HasContextTag(str2.Trim()))
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
          this.IncrementCount(item.Stack);
          break;
        }
      }
    }
  }
}
#endif
