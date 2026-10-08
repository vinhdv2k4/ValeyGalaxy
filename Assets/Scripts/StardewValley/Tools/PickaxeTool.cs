public sealed class PickaxeTool : Tool
{
    public PickaxeTool() : base(PlayerItemKind.Pickaxe, PickaxeSpriteIndex,
        FarmerAnimationAction.Tool, new GenericToolVisualStrategy()) { }

    public override bool Apply(ItemUseContext context)
        => context.Farming != null && context.Farming.TryHitResource(context.Facing, context);
}
