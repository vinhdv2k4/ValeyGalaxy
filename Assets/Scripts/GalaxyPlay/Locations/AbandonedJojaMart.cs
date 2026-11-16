#if false
using Microsoft.Xna.Framework;
using Netcode;
using GameSource.Menus;
using GameSource.Network;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GameSource.Locations
{
  public class AbandonedJojaMart : GameLocation
  {
    [XmlIgnore]
    private readonly NetEvent0 restoreAreaCutsceneEvent = new NetEvent0();
    [XmlIgnore]
    public NetMutex bundleMutex = new NetMutex();

    public AbandonedJojaMart()
    {
    }

    public AbandonedJojaMart(string mapPath, string name)
      : base(mapPath, name)
    {
    }

    protected override void initNetFields()
    {
      base.initNetFields();
      this.NetFields.AddFields((INetSerializable) this.restoreAreaCutsceneEvent);
      this.restoreAreaCutsceneEvent.onEvent += new NetEvent0.Event(this.doRestoreAreaCutscene);
      this.NetFields.AddField((INetSerializable) this.bundleMutex.NetFields);
    }

    public void checkBundle() => this.bundleMutex.RequestLock((Action) (() => Game1.activeClickableMenu = (IClickableMenu) new JunimoNoteMenu(6, (Game1.getLocationFromName("CommunityCenter") as CommunityCenter).bundlesDict())));

    public override void updateEvenIfFarmerIsntHere(GameTime time, bool ignoreWasUpdatedFlush = false)
    {
      base.updateEvenIfFarmerIsntHere(time, ignoreWasUpdatedFlush);
      this.bundleMutex.Update((GameLocation) this);
      if (this.bundleMutex.IsLockHeld() && Game1.activeClickableMenu == null)
        this.bundleMutex.ReleaseLock();
      this.restoreAreaCutsceneEvent.Poll();
    }

    public void restoreAreaCutscene() => this.restoreAreaCutsceneEvent.Fire();

    private void doRestoreAreaCutscene()
    {
      if (Game1.currentLocation == this)
      {
        Game1.player.freezePause = 1000;
        DelayedAction.removeTileAfterDelay(8, 8, 100, Game1.currentLocation, "Buildings");
        Game1.getLocationFromName(nameof (AbandonedJojaMart)).startEvent(new GameSource.Event(Game1.content.Load<Dictionary<string, string>>("Data\\Events\\AbandonedJojaMart")["missingBundleComplete"], 192393));
      }
      Game1.addMailForTomorrow("ccMovieTheater", true, true);
      if (Game1.player.team.theaterBuildDate.Value >= 0L)
        return;
      Game1.player.team.theaterBuildDate.Set((long) (Game1.Date.TotalDays + 1));
    }

    protected override void resetSharedState()
    {
      if (Game1.netWorldState.Value.Bundles.ContainsKey(36) && !this.bundleMutex.IsLocked() && !Game1.eventUp && !Game1.MasterPlayer.hasOrWillReceiveMail("ccMovieTheater"))
      {
        bool flag1 = false;
        foreach (bool flag2 in Game1.netWorldState.Value.Bundles[36])
        {
          if (!flag2)
            return;
        }
        if (!flag1)
          this.restoreAreaCutscene();
      }
      base.resetSharedState();
    }
  }
}
#endif
