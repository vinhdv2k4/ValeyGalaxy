public sealed class HoeTool : Tool
{
    public HoeTool() : base(PlayerItemKind.Hoe, HoeSpriteIndex,
        FarmerAnimationAction.Tool, new HoeVisualStrategy()) { }

    public override bool Apply(ItemUseContext context)
        => context.Farming != null && context.Farming.TryHoe(context.Facing);
}
