#if false
using Microsoft.Xna.Framework;

namespace GameSource
{
  public class ShrineActivity : FarmActivity
  {
    public override bool AttemptActivity(Farm farm)
    {
      this.activityPosition = new Vector2(8f, 8f);
      this.activityDirection = 0;
      return true;
    }
  }
}
#endif
