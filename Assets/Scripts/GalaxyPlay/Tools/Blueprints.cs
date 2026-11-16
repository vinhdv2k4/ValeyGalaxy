#if false
using Microsoft.Xna.Framework;
using GameSource.Menus;

namespace GameSource.Tools
{
  public class Blueprints : Tool
  {
    public Blueprints()
      : base("Farmer's Catalogue", 0, 75, 75, false)
    {
      this.UpgradeLevel = 0;
      this.CurrentParentTileIndex = this.IndexOfMenuItemView;
      this.InstantUse = true;
    }

    public override Item getOne()
    {
      Blueprints one = new Blueprints();
      one._GetOneFrom((Item) this);
      return (Item) one;
    }

    protected override string loadDisplayName() => Game1.content.LoadString("Strings\\StringsFromCSFiles:Blueprints.cs.14039");

    protected override string loadDescription() => Game1.content.LoadString("Strings\\StringsFromCSFiles:Blueprints.cs.14040");

    public override void DoFunction(GameLocation location, int x, int y, int power, Farmer who)
    {
      if (Game1.activeClickableMenu == null)
      {
        Game1.activeClickableMenu = (IClickableMenu) new BlueprintsMenu(Game1.viewport.Width / 2 - (Game1.viewport.Width / 2 + 96) / 2, Game1.viewport.Height / 4);
        ((BlueprintsMenu) Game1.activeClickableMenu).changePosition(Game1.activeClickableMenu.xPositionOnScreen, Game1.viewport.Height / 2 - Game1.activeClickableMenu.height / 2);
      }
      this.CurrentParentTileIndex = this.IndexOfMenuItemView;
    }

    public override void tickUpdate(GameTime time, Farmer who)
    {
    }
  }
}
#endif
