public sealed class AxeTool : Tool
{
    public AxeTool() : base(PlayerItemKind.Axe, AxeSpriteIndex,
        FarmerAnimationAction.Tool, new GenericToolVisualStrategy()) { }

    public override bool Apply(ItemUseContext context)
        => context.Farming != null && context.Farming.TryHitResource(context.Facing, context);
}
