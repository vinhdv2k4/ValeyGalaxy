public sealed class PickaxeTool : Tool
{
    public PickaxeTool() : base(PlayerItemKind.Pickaxe, PickaxeSpriteIndex,
        FarmerAnimationAction.Tool, new GenericToolVisualStrategy()) { }

    // A swing that hits nothing thuds against the ground.
    public override bool Apply(ItemUseContext context)
    {
        bool hit = context.Farming != null && (context.Farming.TryHitResource(context.Facing, context) ||
            context.Farming.TryClearSoil(context.Facing));
        if (!hit) GameAudio.Play("woodyHit");
        return hit;
    }
}
