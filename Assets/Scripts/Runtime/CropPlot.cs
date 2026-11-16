using UnityEngine;
using UnityEngine.Events;

public sealed class CropPlot : MonoBehaviour
{
    public Transform cropAnchor;
    [SerializeField] private bool tilled;
    [SerializeField] private bool planted;
    [SerializeField] private bool watered;
    [SerializeField] private ItemData plantedSeed;
    [SerializeField] private CropDefinition cropDefinition;
    [SerializeField] private int currentPhase;
    [SerializeField] private int dayOfCurrentPhase;
    [SerializeField] private int regrowDaysRemaining;
    [SerializeField] private bool harvestedBefore;
    [SerializeField] private SpriteRenderer soilRenderer;
    [SerializeField] private SpriteRenderer waterRenderer;
    [SerializeField] private SpriteRenderer cropRenderer;
    [SerializeField] private UnityEvent onTilled = new();
    [SerializeField] private UnityEvent onPlanted = new();
    [SerializeField] private UnityEvent onWatered = new();

    public bool IsTilled => tilled;
    public bool IsPlanted => planted;
    public bool IsWatered => watered;
    public ItemData PlantedSeed => plantedSeed;
    internal bool MatchesPlant(ItemData seed, CropDefinition definition) =>
        plantedSeed?.itemId == seed.itemId && cropDefinition == definition;
    public bool IsReadyToHarvest => planted && cropDefinition != null &&
        currentPhase >= cropDefinition.FinalPhase && regrowDaysRemaining <= 0;
    public ItemData HarvestItem => IsReadyToHarvest ? cropDefinition.harvestItem : null;
    public bool HarvestRequiresScythe => cropDefinition != null && cropDefinition.requiresScythe;

    private Texture2D soilAtlas;
    private static readonly int[] DrawGuide = { 0, 12, 13, 9, 4, 8, 1, 5, 15, 11, 14, 10, 3, 7, 2, 6 };
    private int neighborMask;
    private int wateredNeighborMask;
    public void InitializeVisuals(Vector2 size, Texture2D atlas)
    {
        soilAtlas = atlas;
        if (soilRenderer == null)
        {
            var soil = new GameObject("Tilled Soil");
            soil.transform.SetParent(transform, false);
            soilRenderer = soil.AddComponent<SpriteRenderer>();
        }
        soilRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        soilRenderer.sortingOrder = WorldSortOrder.LayerPaths + 1;
        if (waterRenderer == null)
        {
            var water = new GameObject("Watered Soil");
            water.transform.SetParent(transform, false);
            waterRenderer = water.AddComponent<SpriteRenderer>();
        }
        waterRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        waterRenderer.sortingOrder = soilRenderer.sortingOrder + 1;
        RefreshVisuals();
    }

    public void SetNeighbors(int mask, int waterMask)
    {
        neighborMask = mask & 15;
        wateredNeighborMask = waterMask & 15;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (soilRenderer != null && soilAtlas != null)
        {
            int index = DrawGuide[neighborMask];
            int x = (index % 4) * 16;
            int y = soilAtlas.height - (index / 4 + 1) * 16;
            soilRenderer.sprite = AtlasSprites.Get(soilAtlas, new Rect(x, y, 16, 16),
                new Vector2(0.5f, 0.5f), 16f);
            soilRenderer.color = Color.white;
            if (waterRenderer != null)
            {
                int waterIndex = DrawGuide[wateredNeighborMask];
                int waterX = 64 + (waterIndex % 4) * 16;
                int waterY = soilAtlas.height - (waterIndex / 4 + 1) * 16;
                waterRenderer.sprite = AtlasSprites.Get(soilAtlas,
                    new Rect(waterX, waterY, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                waterRenderer.enabled = watered;
            }
        }
        if (cropRenderer != null)
            cropRenderer.sprite = planted && cropDefinition != null
                ? cropDefinition.GetPhaseSprite(currentPhase, regrowDaysRemaining > 0,
                    harvestedBefore, Mathf.FloorToInt(transform.position.x),
                    Mathf.FloorToInt(-transform.position.y))
                : null;
    }

    public bool TryHoe()
    {
        if (tilled) return false;
        tilled = true;
        RefreshVisuals();
        onTilled?.Invoke();
        return true;
    }

    public bool TryPlant(ItemData seed, CropDefinition resolvedDefinition = null)
    {
        if (!tilled || planted || seed == null || seed.kind != PlayerItemKind.Seeds) return false;
        CropDefinition definition = resolvedDefinition ?? seed.cropDefinition;
        if (definition == null && seed.mixedSeedCrops != null && seed.mixedSeedCrops.Length > 0)
        {
            definition = seed.mixedSeedCrops[Random.Range(0, seed.mixedSeedCrops.Length)];
        }
        if (definition == null || definition.harvestItem == null) return false;
        planted = true;
        plantedSeed = seed;
        cropDefinition = definition;
        currentPhase = 0;
        dayOfCurrentPhase = 0;
        regrowDaysRemaining = 0;
        harvestedBefore = false;
        if (cropRenderer == null)
        {
            var crop = new GameObject("Planted Seed");
            crop.transform.SetParent(cropAnchor != null ? cropAnchor : transform, false);
            cropRenderer = crop.AddComponent<SpriteRenderer>();
            cropRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, 0f, 2);
        }
        RefreshVisuals();
        onPlanted?.Invoke();
        return true;
    }

    public void RestoreServerGrowth(int growthDays, int daysToGrow,
                                    int regrowDaysLeft, bool serverHasHarvested,
                                    bool isWatered)
    {
        if (!planted || cropDefinition == null) return;
        int elapsedDays = Mathf.Clamp(growthDays, 0,
            Mathf.Min(Mathf.Max(0, daysToGrow), cropDefinition.TotalGrowthDays));
        currentPhase = 0;
        dayOfCurrentPhase = 0;
        while (currentPhase < cropDefinition.FinalPhase)
        {
            int phaseLength = Mathf.Max(0, cropDefinition.phaseDays[currentPhase]);
            if (phaseLength > elapsedDays)
            {
                dayOfCurrentPhase = elapsedDays;
                break;
            }
            elapsedDays -= phaseLength;
            currentPhase++;
        }
        harvestedBefore = serverHasHarvested;
        if (serverHasHarvested) currentPhase = cropDefinition.FinalPhase;
        regrowDaysRemaining = Mathf.Max(0, regrowDaysLeft);
        watered = isWatered;
        RefreshVisuals();
    }

    public void ClearPlantedCrop()
    {
        planted = false;
        plantedSeed = null;
        cropDefinition = null;
        currentPhase = 0;
        dayOfCurrentPhase = 0;
        regrowDaysRemaining = 0;
        harvestedBefore = false;
        RefreshVisuals();
    }

    public bool TryWater()
    {
        if (!tilled || watered) return false;
        watered = true;
        RefreshVisuals();
        onWatered?.Invoke();
        return true;
    }

    private SpriteRenderer fertilizerRenderer;

    /// <summary>Fertilizer, retaining soil and speed-gro show as a coloured layer on the soil.</summary>
    internal void SetFertilizer(string itemId)
    {
        int index = itemId switch
        {
            "object_368" => 0, "object_369" => 1, "object_919" => 2, "object_370" => 3, "object_371" => 4,
            "object_920" => 5, "object_465" => 6, "object_466" => 7, "object_918" => 8, _ => -1
        };
        if (index < 0)
        {
            if (fertilizerRenderer != null) fertilizerRenderer.enabled = false;
            return;
        }
        if (fertilizerRenderer == null)
        {
            var go = new GameObject("Fertilizer");
            go.transform.SetParent(transform, false);
            fertilizerRenderer = go.AddComponent<SpriteRenderer>();
            if (soilRenderer != null)
            {
                go.transform.position = soilRenderer.transform.position;
                fertilizerRenderer.sortingLayerID = soilRenderer.sortingLayerID;
                fertilizerRenderer.sortingOrder = soilRenderer.sortingOrder + 1;
            }
        }
        fertilizerRenderer.sprite = AtlasSprites.FromTop(StardewUiAtlas.Cursors, 173 + index / 3 * 16, 462 + index % 3 * 16,
            16, 16, new Vector2(0.5f, 0.5f), 16f);
        fertilizerRenderer.enabled = true;
    }

    internal void RestoreSoilWater(bool isWatered)
    {
        if (watered == isWatered) return;
        watered = isWatered;
        RefreshVisuals();
    }

    public bool TryHarvestVisual(bool usingScythe = false)
    {
        if (!IsReadyToHarvest ||
            (cropDefinition.requiresScythe && !usingScythe)) return false;

        if (cropDefinition.regrowDays > 0)
        {
            regrowDaysRemaining = cropDefinition.regrowDays;
            harvestedBefore = true;
        }
        else
        {
            planted = false;
            plantedSeed = null;
            cropDefinition = null;
            currentPhase = 0;
            dayOfCurrentPhase = 0;
            harvestedBefore = false;
        }
        RefreshVisuals();
        return true;
    }
}
