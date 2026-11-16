/// <summary>
/// Unity runtime profile for a tool. The XNA/Netcode reference
/// source is preserved in Tool.Source.txt for frame and atlas reference.
/// Unity directions are Down=0, Right=1, Up=2, Left=3.
/// </summary>
public class Tool : IItemUseBehavior
{
    public const int HoeSpriteIndex = 21;
    public const int PickaxeSpriteIndex = 105;
    public const int AxeSpriteIndex = 189;
    public const int WateringCanSpriteIndex = 273;

    public Tool(PlayerItemKind kind, int initialAtlasIndex, FarmerAnimationAction useAnimation, IToolVisualStrategy visualStrategy)
    {
        Kind = kind;
        InitialAtlasIndex = initialAtlasIndex;
        UseAnimation = useAnimation;
        VisualStrategy = visualStrategy;
    }

    public PlayerItemKind Kind { get; }
    public int InitialAtlasIndex { get; }
    public FarmerAnimationAction UseAnimation { get; }
    public IToolVisualStrategy VisualStrategy { get; }

    // Each concrete tool chooses when its gameplay effect happens. The caller
    // only runs the lifecycle and never branches over concrete tool types.
    public virtual float ImpactFraction => 0.5f;
    public virtual bool Apply(ItemUseContext context) => false;

    /// <summary>
    /// Port of Tool.Update's atlas index selection for ordinary tools.
    /// motionFrame is the zero-based frame within the current use animation.
    /// </summary>
    public virtual int GetAtlasIndex(int unityDirection, int motionFrame)
    {
        if (InitialAtlasIndex < 0) return -1;
        int direction = unityDirection == 0 ? 2 : unityDirection == 2 ? 0 : unityDirection;
        int directionOffset = direction == 0 ? 3 : direction == 1 || direction == 3 ? 2 : 0;
        int frameOffset = motionFrame >= 1 && (direction == 0 || direction == 2 && motionFrame >= 2) ? 1 : 0;
        return InitialAtlasIndex + directionOffset + frameOffset;
    }
}
