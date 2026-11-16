#if false
using Microsoft.Xna.Framework;

namespace GameSource
{
  public struct RainDrop
  {
    public int frame;
    public int accumulator;
    public Vector2 position;

    public RainDrop(int x, int y, int frame, int accumulator)
    {
      this.position = new Vector2((float) x, (float) y);
      this.frame = frame;
      this.accumulator = accumulator;
    }
  }
}
#endif
