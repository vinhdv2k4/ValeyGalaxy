#if false
using Netcode;
using System;
using System.Collections.Generic;

namespace GameSource
{
  public class MailReward : OrderReward
  {
    public NetBool noLetter = new NetBool(true);
    public NetStringList grantedMails = new NetStringList();
    public NetBool host = new NetBool(false);

    public override void InitializeNetFields()
    {
      base.InitializeNetFields();
      this.NetFields.AddFields((INetSerializable) this.noLetter, (INetSerializable) this.grantedMails, (INetSerializable) this.host);
    }

    public override void Load(SpecialOrder order, Dictionary<string, string> data)
    {
      foreach (string str in order.Parse(data["MailReceived"]).Split(' '))
        this.grantedMails.Add(str);
      if (data.ContainsKey("NoLetter"))
        this.noLetter.Value = Convert.ToBoolean(order.Parse(data["NoLetter"]));
      if (!data.ContainsKey("Host"))
        return;
      this.host.Value = Convert.ToBoolean(order.Parse(data["Host"]));
    }

    public override void Grant()
    {
      foreach (string grantedMail in (NetList<string, NetString>) this.grantedMails)
      {
        if (this.host.Value)
        {
          if (Game1.IsMasterGame)
          {
            if (Game1.newDaySync != null)
            {
              Game1.addMail(grantedMail, this.noLetter.Value, true);
            }
            else
            {
              string mailName = grantedMail;
              if (mailName == "ClintReward" && Game1.player.mailReceived.Contains("ClintReward"))
              {
                Game1.player.mailReceived.Remove("ClintReward2");
                mailName = "ClintReward2";
              }
              Game1.addMailForTomorrow(mailName, this.noLetter.Value, true);
            }
          }
        }
        else if (Game1.newDaySync != null)
        {
          Game1.addMail(grantedMail, this.noLetter.Value, true);
        }
        else
        {
          string mailName = grantedMail;
          if (mailName == "ClintReward" && Game1.player.mailReceived.Contains("ClintReward"))
          {
            Game1.player.mailReceived.Remove("ClintReward2");
            mailName = "ClintReward2";
          }
          Game1.addMailForTomorrow(mailName, this.noLetter.Value, true);
        }
      }
    }
  }
}
#endif
