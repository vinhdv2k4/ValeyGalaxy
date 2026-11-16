#if false
using Microsoft.Xna.Framework.Graphics;

namespace GameSource.Menus
{
  public abstract class ICreditsBlock
  {
    public virtual void draw(int topLeftX, int topLeftY, int widthToOccupy, SpriteBatch b)
    {
    }

    public virtual int getHeight(int maxWidth) => 0;

    public virtual void hovered()
    {
    }

    public virtual void clicked()
    {
    }
  }
}
#endif
