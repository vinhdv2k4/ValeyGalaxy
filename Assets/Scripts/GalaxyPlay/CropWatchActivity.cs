#if false
using Microsoft.Xna.Framework;
using GameSource.TerrainFeatures;
using System;

namespace GameSource
{
  public class CropWatchActivity : FarmActivity
  {
    protected Object _cropObject;

    protected override bool _AttemptActivity(Farm farm)
    {
      this._cropObject = (Object) null;
      HoeDirt randomCrop = this.GetRandomCrop(farm, (Func<Crop, bool>) (crop => crop.currentPhase.Value <= 0 && new Object(Vector2.Zero, crop.indexOfHarvest.Value, 1).category.Value != -80));
      if (randomCrop == null)
        return false;
      this._cropObject = new Object(Vector2.Zero, randomCrop.crop.indexOfHarvest.Value, 1);
      this.activityPosition = randomCrop.currentTileLocation;
      return true;
    }

    protected override void _EndActivity() => this._cropObject = (Object) null;

    protected override void _BeginActivity()
    {
      if (this._cropObject == null)
        return;
      switch (this._character.getGiftTasteForThisItem((Item) this._cropObject))
      {
        case 2:
          this._character.doEmote(32, false);
          break;
        case 6:
          this._character.doEmote(20, false);
          break;
        case 8:
          break;
        default:
          this._character.doEmote(12, false);
          break;
      }
    }
  }
}
#endif
