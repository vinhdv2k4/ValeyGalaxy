#if false
using Microsoft.Xna.Framework;

namespace GameSource.Menus
{
  public class InviteCodeDialog : ConfirmationDialog
  {
    private string code;

    public InviteCodeDialog(string code, ConfirmationDialog.behavior onClose)
      : base(Game1.content.LoadString("Strings\\UI:Server_InviteCode", (object) code), onClose, onClose)
    {
      this.code = code;
      if (!DesktopClipboard.IsAvailable)
      {
        this.cancelButton.visible = false;
      }
      else
      {
        this.onCancel = new ConfirmationDialog.behavior(this.copyCode);
        ClickableTextureComponent textureComponent = new ClickableTextureComponent("OK", new Rectangle(this.xPositionOnScreen + this.width - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder - 64, this.yPositionOnScreen + this.height - IClickableMenu.borderWidth - IClickableMenu.spaceToClearTopBorder + 21, 64, 64), (string) null, (string) null, Game1.mouseCursors, new Rectangle(274, 284, 16, 16), 4f);
        textureComponent.myID = 102;
        textureComponent.leftNeighborID = 101;
        this.cancelButton = textureComponent;
      }
      if (!Game1.options.SnappyMenus)
        return;
      this.populateClickableComponentList();
      this.currentlySnappedComponent = this.getComponentWithID(101);
      this.snapCursorToCurrentSnappedComponent();
    }

    protected void copyCode(Farmer who)
    {
      if (DesktopClipboard.SetText(this.code))
        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Server_InviteCode_Copied")));
      else
        Game1.showRedMessageUsingLoadString("Strings\\UI:Server_InviteCode_CopyFailed");
    }
  }
}
#endif
