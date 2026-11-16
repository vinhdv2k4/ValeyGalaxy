public sealed class SeedUseBehavior : IItemUseBehavior
{
    public FarmerAnimationAction UseAnimation => FarmerAnimationAction.SeedThrow;
    public float ImpactFraction => 0.5f;

    public bool Apply(ItemUseContext context)
    {
        // Planting needs the backend state transaction used by NetworkPlayerController.
        return false;
    }
}
