#if false
using Microsoft.Xna.Framework;
using GameSource.TerrainFeatures;
using System;

namespace GameSource
{
  public class TreeActivity : FarmActivity
  {
    protected override bool _AttemptActivity(Farm farm)
    {
      TerrainFeature randomTerrainFeature = this.GetRandomTerrainFeature(farm, (Func<TerrainFeature, bool>) (feature =>
      {
        switch (feature)
        {
          case Tree _ when (feature as Tree).growthStage.Value >= 5:
            return true;
          case FruitTree _:
            return (feature as FruitTree).growthStage.Value >= 4;
          default:
            return false;
        }
      }));
      if (randomTerrainFeature == null)
        return false;
      Vector2 v = randomTerrainFeature.currentTileLocation + new Vector2(0.0f, 1f);
      Rectangle rectangle = new Rectangle((int) randomTerrainFeature.currentTileLocation.X, (int) randomTerrainFeature.currentTileLocation.Y, 1, 1);
      if (!farm.isTileLocationTotallyClearAndPlaceableIgnoreFloors(v))
        return false;
      rectangle.Inflate(2, 2);
      for (int left = rectangle.Left; left < rectangle.Right; ++left)
      {
        for (int top = rectangle.Top; top < rectangle.Bottom; ++top)
        {
          if ((double) left != (double) randomTerrainFeature.currentTileLocation.X || (double) top != (double) randomTerrainFeature.currentTileLocation.Y)
          {
            Object objectAtTile = farm.getObjectAtTile(left, top);
            if (objectAtTile != null && (objectAtTile.Name.Equals("Weeds") || objectAtTile.Name.Equals("Stone")))
              return false;
            if (farm.terrainFeatures.ContainsKey(new Vector2((float) left, (float) top)))
            {
              switch (farm.terrainFeatures[new Vector2((float) left, (float) top)])
              {
                case Tree _:
                case FruitTree _:
                  return false;
                default:
                  continue;
              }
            }
          }
        }
      }
      this.activityPosition = v;
      this.activityDirection = 2;
      return true;
    }

    protected override void _BeginActivity()
    {
      if (this._character.Name == "Haley")
        this._character.StartActivityRouteEndBehavior("haley_photo", "");
      else if (this._character.Name == "Penny")
      {
        this._character.StartActivityRouteEndBehavior("penny_read", "");
      }
      else
      {
        if (!(this._character.Name == "Leah"))
          return;
        this._character.StartActivityRouteEndBehavior("leah_draw", "");
      }
    }

    protected override void _EndActivity() => this._character.EndActivityRouteEndBehavior();
  }
}
#endif
