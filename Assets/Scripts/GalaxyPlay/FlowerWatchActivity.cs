#if false
using Microsoft.Xna.Framework;
using GameSource.TerrainFeatures;
using System;

namespace GameSource
{
  public class FlowerWatchActivity : FarmActivity
  {
    protected override bool _AttemptActivity(Farm farm)
    {
      HoeDirt randomCrop = this.GetRandomCrop(farm, (Func<Crop, bool>) (crop => crop.currentPhase.Value >= crop.phaseDays.Count - 1 && new Object(Vector2.Zero, crop.indexOfHarvest.Value, 1).category.Value == -80));
      if (randomCrop == null)
        return false;
      this.activityPosition = randomCrop.currentTileLocation;
      return true;
    }
  }
}
#endif
