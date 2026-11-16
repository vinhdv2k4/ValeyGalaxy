#if false
using Microsoft.Xna.Framework;
using System.Collections.Generic;

public class TilePositionComparer : IEqualityComparer<Vector2>
{
  public bool Equals(Vector2 a, Vector2 b) => a.Equals(b);

  public int GetHashCode(Vector2 a) => (int) (ushort) a.X | (int) (ushort) a.Y << 16;
}
#endif
