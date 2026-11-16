#if false
namespace Microsoft.Xna.Framework.Graphics
{
  public static class ViewportExtensions
  {
    public static Microsoft.Xna.Framework.Rectangle GetTitleSafeArea(this Viewport vp) => vp.TitleSafeArea;

    public static Microsoft.Xna.Framework.Rectangle ToXna(this xTile.Dimensions.Rectangle xrect) => new Microsoft.Xna.Framework.Rectangle(xrect.X, xrect.Y, xrect.Width, xrect.Height);

    public static Vector2 Size(this Viewport vp) => new Vector2((float) vp.Width, (float) vp.Height);
  }
}
#endif
