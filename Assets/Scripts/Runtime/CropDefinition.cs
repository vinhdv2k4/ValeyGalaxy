using System.Collections.Generic;
using UnityEngine;

/// <summary>Crop data corresponding to a row in the Crop sprite sheet.</summary>
[CreateAssetMenu(fileName = "Crop", menuName = "Stardew/Farming/Crop Definition")]
public sealed class CropDefinition : ScriptableObject
{
    public ItemData harvestItem;
    [Tooltip("Days in each growth phase before the final, harvestable phase.")]
    public int[] phaseDays = { 1, 1, 1, 1 };
    [Min(0)] public int regrowDays;
    [Min(1)] public int minHarvest = 1;
    [Min(1)] public int maxHarvest = 1;
    [Range(0f, 0.9f)] public float extraCropChance;
    public bool requiresScythe;

    [Header("Sprites from Content/TileSheets/crops.png")]
    public Texture2D cropAtlas;
    [Min(0)] public int spriteRow;

    [Header("Harvest icon from Content/Maps/springobjects.png")]
    public Texture2D objectAtlas;
    [Min(0)] public int harvestObjectIndex = -1;
    private readonly Dictionary<int, Sprite> phaseSprites = new();

    public int FinalPhase => phaseDays == null ? 0 : phaseDays.Length;
    public int TotalGrowthDays
    {
        get
        {
            int total = 0;
            if (phaseDays != null)
                foreach (int days in phaseDays) total += Mathf.Max(0, days);
            return total;
        }
    }

    public Sprite GetPhaseSprite(int phase, bool regrowing, bool harvestedBefore,
                                 int tileX = 0, int tileY = 0)
    {
        if (cropAtlas == null) return harvestItem != null ? harvestItem.icon : null;
        // Crop.getSourceRect alternates the two seed sprites by tile position.
        int column = harvestedBefore ? (regrowing ? 7 : 6) : phase == 0
            ? ((tileX * 7 + tileY * 11) & 1) : Mathf.Clamp(phase + 1, 1, 6);
        if (phaseSprites.TryGetValue(column, out var cached)) return cached;
        int x = column * 16 + (spriteRow % 2) * 128;
        int y = cropAtlas.height - (spriteRow / 2 + 1) * 32;
        if (x + 16 > cropAtlas.width || y < 0) return null;
        var sprite = Sprite.Create(cropAtlas, new Rect(x, y, 16, 32),
            new Vector2(0.5f, 0.25f), 16f);
        phaseSprites[column] = sprite;
        return sprite;
    }

    public void EnsureHarvestIcon()
    {
        if (harvestItem == null || harvestItem.icon != null || objectAtlas == null || harvestObjectIndex < 0) return;
        int columns = objectAtlas.width / 16;
        int x = harvestObjectIndex % columns * 16;
        int y = objectAtlas.height - (harvestObjectIndex / columns + 1) * 16;
        if (y >= 0) harvestItem.icon = Sprite.Create(objectAtlas, new Rect(x, y, 16, 16),
            new Vector2(0.5f, 0.5f), 16f);
    }
}
