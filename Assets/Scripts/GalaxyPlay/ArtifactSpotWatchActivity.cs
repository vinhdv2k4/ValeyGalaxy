#if false
using System;

namespace GameSource
{
  public class ArtifactSpotWatchActivity : FarmActivity
  {
    protected override bool _AttemptActivity(Farm farm)
    {
      Object randomObject = this.GetRandomObject(farm, (Func<Object, bool>) (o => Utility.IsNormalObjectAtParentSheetIndex((Item) o, 595)));
      if (randomObject == null)
        return false;
      this.activityPosition = this.GetNearbyTile(farm, randomObject.TileLocation);
      return true;
    }
  }
}
#endif
