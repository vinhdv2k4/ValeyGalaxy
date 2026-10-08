public sealed class WateringCanTool : Tool
{
    public WateringCanTool() : base(PlayerItemKind.WateringCan, WateringCanSpriteIndex,
        FarmerAnimationAction.Watering, new WateringCanVisualStrategy()) { }

    public override float ImpactFraction => 175f / 675f;

    public override bool Apply(ItemUseContext context)
        => context.Farming != null && context.Farming.TryWater(context.Facing);

    public override int GetAtlasIndex(int unityDirection, int motionFrame)
    {
        int direction = unityDirection == 0 ? 2 : unityDirection == 2 ? 0 : unityDirection;
        int offset = direction == 0 ? 4 : direction == 1 || direction == 3 ? 2 : 0;
        return InitialAtlasIndex + offset + (motionFrame >= 5 && direction != 0 ? 1 : 0);
    }
}
