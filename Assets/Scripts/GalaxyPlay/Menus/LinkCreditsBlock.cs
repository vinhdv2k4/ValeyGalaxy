#if false
using Microsoft.Xna.Framework.Graphics;
using GameSource.BellsAndWhistles;
using System;
using System.Diagnostics;

namespace GameSource.Menus
{
  internal class LinkCreditsBlock : ICreditsBlock
  {
    private string text;
    private string url;
    private bool currentlyHovered;

    public LinkCreditsBlock(string text, string url)
    {
      this.text = text;
      this.url = url;
    }

    public override void draw(int topLeftX, int topLeftY, int widthToOccupy, SpriteBatch b)
    {
      SpriteText.drawString(b, this.text, topLeftX, topLeftY, width: widthToOccupy, height: 99999, color: (this.currentlyHovered ? 6 : 7));
      this.currentlyHovered = false;
    }

    public override int getHeight(int maxWidth) => !(this.text == "") ? SpriteText.getHeightOfString(this.text, maxWidth) : 64;

    public override void hovered() => this.currentlyHovered = true;

    public override void clicked()
    {
      Game1.playSound("bigSelect");
      try
      {
        Process.Start(this.url);
      }
      catch (Exception ex)
      {
      }
    }
  }
}
#endif
