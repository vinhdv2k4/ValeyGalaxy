#if false
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace GameSource.Util
{
  public class BoundingBoxGroup
  {
    private List<Rectangle> rectangles = new List<Rectangle>();

    public bool Intersects(Rectangle rect)
    {
      foreach (Rectangle rectangle in this.rectangles)
      {
        if (rectangle.Intersects(rect))
          return true;
      }
      return false;
    }

    public bool Contains(int x, int y)
    {
      foreach (Rectangle rectangle in this.rectangles)
      {
        if (rectangle.Contains(x, y))
          return true;
      }
      return false;
    }

    public void Add(Rectangle rect)
    {
      if (this.rectangles.Contains(rect))
        return;
      this.rectangles.Add(rect);
    }

    public void ClearNonIntersecting(Rectangle rect)
    {
      for (int index = this.rectangles.Count - 1; index >= 0; --index)
      {
        if (!this.rectangles[index].Intersects(rect))
          this.rectangles.RemoveAt(index);
      }
    }

    public void Clear() => this.rectangles.Clear();

    public void Draw(SpriteBatch b)
    {
      foreach (Rectangle rectangle in this.rectangles)
      {
        rectangle.Offset(-Game1.viewport.X, -Game1.viewport.Y);
        b.Draw(Game1.fadeToBlackRect, rectangle, Color.Green * 0.5f);
      }
    }

    public bool IsEmpty() => this.rectangles.Count == 0;
  }
}
#endif
