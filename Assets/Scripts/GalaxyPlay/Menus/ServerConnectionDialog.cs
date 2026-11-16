#if false
using Microsoft.Xna.Framework;

namespace GameSource.Menus
{
  public class ServerConnectionDialog : ConfirmationDialog
  {
    public ServerConnectionDialog(
      ConfirmationDialog.behavior onConfirm = null,
      ConfirmationDialog.behavior onCancel = null)
      : base(Game1.content.LoadString("Strings\\UI:CoopMenu_Connecting"), onConfirm, onCancel)
    {
      this.okButton.visible = false;
      if (!Game1.options.SnappyMenus)
        return;
      this.populateClickableComponentList();
      this.snapToDefaultClickableComponent();
    }

    public override void update(GameTime time)
    {
      base.update(time);
      if (Game1.server == null || !Game1.server.connected())
        return;
      this.confirm();
    }
  }
}
#endif
