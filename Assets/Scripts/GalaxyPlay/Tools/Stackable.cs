#if false
namespace GameSource.Tools
{
  public abstract class Stackable : Tool
  {
    private int numberInStack;

    public int NumberInStack
    {
      get => this.numberInStack;
      set => this.numberInStack = value;
    }

    public Stackable()
    {
    }

    public Stackable(
      string name,
      int upgradeLevel,
      int initialParentTileIndex,
      int indexOfMenuItemView,
      bool stackable)
      : base(name, upgradeLevel, initialParentTileIndex, indexOfMenuItemView, stackable)
    {
    }
  }
}
#endif
