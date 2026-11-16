#if false
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameSource
{
  public interface ISalable
  {
    string DisplayName { get; }

    bool ShouldDrawIcon();

    void drawInMenu(
      SpriteBatch spriteBatch,
      Vector2 location,
      float scaleSize,
      float transparency,
      float layerDepth,
      StackDrawType drawStackNumber,
      Color color,
      bool drawShadow);

    string Name { get; }

    string getDescription();

    int maximumStackSize();

    int addToStack(Item stack);

    int Stack { get; set; }

    int salePrice();

    bool actionWhenPurchased();

    bool canStackWith(ISalable other);

    bool CanBuyItem(Farmer farmer);

    bool IsInfiniteStock();

    ISalable GetSalableInstance();
  }
}
#endif
