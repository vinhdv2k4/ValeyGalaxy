public sealed class HoeTool : Tool
{
    public HoeTool() : base(PlayerItemKind.Hoe, HoeSpriteIndex,
        FarmerAnimationAction.Tool, new HoeVisualStrategy()) { }

    // Every swing thuds; each newly tilled tile also plays hoeHit.
    public override bool Apply(ItemUseContext context)
    {
        GameAudio.Play("woodyHit");
        return context.Farming != null && context.Farming.TryHoe(context.Facing);
    }
}
