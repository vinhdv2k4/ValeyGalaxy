public interface IItemUseBehavior
{
    FarmerAnimationAction UseAnimation { get; }
    float ImpactFraction { get; }
    bool Apply(ItemUseContext context);
}
