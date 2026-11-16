#if false
namespace GameSource.Tools
{
  /// <summary>
  /// Used as placeholder tools in certain situations. (e.g. as a "spot" in Clint's shop for the trash can upgrade)
  /// </summary>
  public class GenericTool : Tool
  {
    public string description;

    public GenericTool()
    {
    }

    public GenericTool(
      string name,
      string description,
      int upgradeLevel,
      int parentSheetIndex,
      int menuViewIndex)
      : base(name, upgradeLevel, parentSheetIndex, menuViewIndex, false)
    {
      this.description = description;
    }

    public override Item getOne()
    {
      GenericTool one = new GenericTool(this.BaseName, this.description, this.UpgradeLevel, this.InitialParentTileIndex, this.IndexOfMenuItemView);
      one._GetOneFrom((Item) this);
      return (Item) one;
    }

    protected override string loadDescription() => this.description;

    protected override string loadDisplayName() => this.BaseName;
  }
}
#endif
