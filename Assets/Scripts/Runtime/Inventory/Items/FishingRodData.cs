using UnityEngine;

[CreateAssetMenu(fileName = "NewFishingRod", menuName = "Stardew/Inventory/Fishing Rod Data")]
public sealed class FishingRodData : ToolData
{
    [SerializeField] private Sprite[] castSideFrames;
    [SerializeField] private Sprite[] castVerticalFrames;

    public Sprite GetCastSprite(int direction, int frame)
    {
        Sprite[] frames = direction == 1 || direction == 3 ? castSideFrames : castVerticalFrames;
        return frames != null && frame >= 0 && frame < frames.Length && frames[frame] != null
            ? frames[frame] : GetHeldSprite(direction);
    }

    protected override void Reset()
    {
        base.Reset();
        kind = PlayerItemKind.FishingRod;
        useAnimation = FarmerAnimationAction.Fishing;
    }
}
