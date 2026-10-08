public sealed class SeedUseBehavior : IItemUseBehavior
{
    public FarmerAnimationAction UseAnimation => FarmerAnimationAction.SeedThrow;
    public float ImpactFraction => 0.5f;

    public bool Apply(ItemUseContext context)
    {
        if (context.Farming == null || !context.Farming.TryPlant(context.Facing)) return false;
        return context.Inventory != null && context.Inventory.ConsumeSelected(PlayerItemKind.Seeds);
    }
}
