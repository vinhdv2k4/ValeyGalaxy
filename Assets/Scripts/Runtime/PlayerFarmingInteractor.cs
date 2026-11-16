using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PlayerFarmingInteractor : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CropPlot selectedCrop;
    [Min(0.1f)] public float interactionRadius = 1.2f;
    public LayerMask plotLayer;
    [SerializeField] private Tilemap farmGround;
    [SerializeField] private Tilemap farmBuildings;
    [SerializeField] private Texture2D hoeDirtAtlas;
    [SerializeField] private Texture2D wateringAtlas;
    [SerializeField] private FarmingVfx waterEffectPrefab;
    [SerializeField, Min(1)] private int waterCapacity = 40;
    [SerializeField, Min(0)] private int waterLeft = 40;
    private readonly System.Collections.Generic.Queue<FarmingVfx> waterEffects = new();
    private WaterDetector waterDetector;
    private GameLocation activeMap;
    public int WaterLeft => waterLeft;
    public int WaterCapacity => waterCapacity;
    public event System.Action<int, int> WaterChanged;
    private readonly System.Collections.Generic.Dictionary<Vector3Int, CropPlot> plots = new();
    private readonly System.Collections.Generic.HashSet<CropPlot> pendingHarvests = new();
    private Vector2? pointerTarget;
    private bool soilChangePending;
    private Vector3 StandingPosition
    {
        get
        {
            var bodyCollider = GetComponent<Collider2D>();
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        }
    }

    public void SetPointerTarget(Vector2? target) => pointerTarget = target;

    public bool IsFarmingAllowed()
    {
        GameLocation location = LocationManager.Instance != null ? LocationManager.Instance.CurrentLocation : activeMap;
        if (location == null) return false;
        // Strictly allow tilling, planting, and farming ONLY on the Farm.
        // Disallow in Town, Town-*, Mountain, Beach, Forest, etc.
        return string.Equals(location.LocationName, "Farm", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Charge level for the next Hoe or Watering Can use.</summary>
    internal int ChargePower { get; set; }

    private int TakeChargePower()
    {
        int power = ChargePower;
        ChargePower = 0;
        return Mathf.Clamp(power, 0, 4);
    }

    private CropPlot PlotAt(Vector3Int cell)
    {
        if (plots.TryGetValue(cell, out var existing) && existing != null) return existing;
        var center = farmGround.GetCellCenterWorld(cell);
        foreach (var hit in Physics2D.OverlapPointAll(center,
                     plotLayer.value == 0 ? Physics2D.DefaultRaycastLayers : plotLayer.value))
        {
            var found = hit.GetComponentInParent<CropPlot>();
            if (found != null) { plots[cell] = found; return found; }
        }
        return null;
    }

    private System.Collections.Generic.List<Vector2Int> AreaTiles(Vector3Int targetCell, Vector2 facing, int power) =>
        ToolArea.TilesAffected(new Vector2Int(targetCell.x, -targetCell.y), power, ToolArea.FacingDirection(facing));

    private bool CanTillCell(Vector3Int cell)
    {
        if (!farmGround.HasTile(cell) || farmBuildings != null && farmBuildings.HasTile(cell)) return false;
        if (PlotAt(cell) != null) return false;
        foreach (var hit in Physics2D.OverlapBoxAll(farmGround.GetCellCenterWorld(cell),
                     (Vector2)farmGround.layoutGrid.cellSize * 0.7f, 0f))
            if (hit.GetComponentInParent<WorldResource>() != null) return false;
        return true;
    }

    private bool TryHoeArea(Vector3Int targetCell, Vector2 facing, int power)
    {
        if (soilChangePending || !activeMap.IsOutdoors) return false;
        var online = GetComponent<OnlineGameState>();
        if (online == null) return false;
        var tiles = AreaTiles(targetCell, facing, power)
            .FindAll(tile => tile.x >= 0 && tile.y >= 0 && CanTillCell(new Vector3Int(tile.x, -tile.y, 0)));
        if (tiles.Count == 0) return false;
        soilChangePending = true;
        // The dirt flies as the blade lands; farther tiles of a charged swing follow a moment later.
        GameAudio.Play("hoeHit");
        Vector2Int first = tiles[0];
        foreach (Vector2Int tile in tiles)
            DirtKickEffect.Spawn(farmGround.GetCellCenterWorld(new Vector3Int(tile.x, -tile.y, 0)),
                tiles.Count > 2 ? Vector2Int.Distance(first, tile) * 0.03f : 0f);
        online.ChangeFarmSoilArea(power, tiles, error =>
        {
            soilChangePending = false;
            if (error != null) NpcConversation.ShowNotice(error);
        });
        return true;
    }

    public bool TryHoe(Vector2 facing)
    {
        int power = TakeChargePower();
        if (power > 0)
            return IsFarmingAllowed() && TryGetTargetCell(facing, out var areaCell, out _) &&
                TryHoeArea(areaCell, facing, power);
        if (!IsFarmingAllowed() || !TryGetTargetCell(facing, out var cell, out var center) ||
            !activeMap.IsOutdoors || !farmGround.HasTile(cell) ||
            (farmBuildings != null && farmBuildings.HasTile(cell)) || FindPlot(facing) != null)
            return false;
        foreach (var hit in Physics2D.OverlapBoxAll(center, (Vector2)farmGround.layoutGrid.cellSize * 0.7f, 0f))
            if (hit.GetComponentInParent<WorldResource>() != null) return false;
        return ChangeSoil(center, clear: false);
    }

    internal bool TryClearSoil(Vector2 facing)
    {
        if (!IsFarmingAllowed()) return false;
        var plot = FindPlot(facing);
        return plot != null && plot.IsTilled && !plot.IsPlanted && ChangeSoil(plot.transform.position, clear: true);
    }

    private bool ChangeSoil(Vector3 center, bool clear)
    {
        if (soilChangePending) return false;
        var online = GetComponent<OnlineGameState>();
        if (online == null) return false;
        soilChangePending = true;
        if (!clear)
        {
            GameAudio.Play("hoeHit");
            DirtKickEffect.Spawn(center);
        }
        online.ChangeFarmSoil(Mathf.FloorToInt(center.x), Mathf.FloorToInt(-center.y), clear, error =>
        {
            soilChangePending = false;
            if (error == null) { if (clear) GameAudio.Play("hammer"); }
            else NpcConversation.ShowNotice(error);
        });
        return true;
    }
    public bool TryPlant(Vector2 facing, ItemData seed)
    {
        if (!IsFarmingAllowed()) return false;
        var plot = FindPlot(facing);
        if (plot == null || !plot.TryPlant(seed)) return false;
        return true;
    }
    public ItemData HarvestItemAt(Vector2 facing) => FindPlot(facing)?.HarvestItem;
    public CropPlot PlantTarget(Vector2 facing) => IsFarmingAllowed() ? FindPlot(facing) : null;

    public void RestoreServerCrops(StartupApiClient.FarmCropsResponse response,
                                   System.Func<string, ItemData> resolveItem)
    {
        activeMap = LocationManager.Instance?.CurrentLocation;
        if (activeMap == null || activeMap.LocationName != "Farm") return;
        farmGround = activeMap.transform.Find("Back")?.GetComponent<Tilemap>();
        farmBuildings = activeMap.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (farmGround == null || response?.crops == null || resolveItem == null) return;
        plots.Clear();
        var existingPlots = activeMap.GetComponentsInChildren<CropPlot>(true);
        foreach (CropPlot existing in existingPlots)
            plots[farmGround.WorldToCell(existing.transform.position)] = existing;
        var restoredCells = new System.Collections.Generic.HashSet<Vector3Int>();
        foreach (var record in response.crops)
        {
            if (record == null || record.tileX is < 0 or > 512 || record.tileY is < 0 or > 512) continue;
            var seed = resolveItem(record.seedItemId);
            var definition = seed?.cropDefinition;
            if (seed?.mixedSeedCrops != null && seed.mixedSeedCrops.Length > 0)
                definition = System.Array.Find(seed.mixedSeedCrops,
                    crop => crop != null && crop.harvestItem?.itemId == record.harvestItemId);
            if (seed == null || definition == null) continue;
            var plot = RestorePlot(record.tileX, record.tileY);
            if (plot == null) continue;
            if (plot.IsPlanted && !plot.MatchesPlant(seed, definition))
                plot.ClearPlantedCrop();
            if (!plot.IsPlanted) plot.TryPlant(seed, definition);
            if (record.daysToGrow > 0)
            {
                int gameDay = GetComponent<OnlineGameState>()?.Snapshot?.gameDay ??
                    FarmDayClock.Instance?.Day ?? 1;
                bool legacyUtcGrowth = record.readyGameDay == 0;
                int growthDays = legacyUtcGrowth
                    ? Mathf.Min(record.growthDays, Mathf.Max(0, gameDay - 1))
                    : record.growthDays;
                int regrowDaysLeft = legacyUtcGrowth && record.hasHarvested
                    ? record.regrowDays : record.regrowDaysLeft;
                plot.RestoreServerGrowth(growthDays, record.daysToGrow,
                    regrowDaysLeft, record.hasHarvested, record.isWatered);
            }
            else
                // Legacy records have no count of watered game days. Wall-clock
                // readyUtc and readyGameDay can falsely mark them as ripe.
                plot.RestoreServerGrowth(0, definition.TotalGrowthDays,
                    0, false, record.isWatered);
        }
        var fertilizers = new System.Collections.Generic.Dictionary<Vector3Int, string>();
        foreach (var soil in response.soil ?? System.Array.Empty<StartupApiClient.FarmSoilRecord>())
            if (soil != null && !string.IsNullOrEmpty(soil.fertilizer))
                fertilizers[farmGround.WorldToCell(new Vector3(soil.tileX + 0.5f, -soil.tileY - 0.5f, 0f))] = soil.fertilizer;
        foreach (var soil in response.soil ?? System.Array.Empty<StartupApiClient.FarmSoilRecord>())
        {
            if (soil == null) continue;
            var plot = RestorePlot(soil.tileX, soil.tileY);
            if (plot == null) continue;
            if (plot.IsPlanted) plot.ClearPlantedCrop();
            plot.RestoreSoilWater(soil.isWatered);
        }
        foreach (CropPlot existing in existingPlots)
        {
            Vector3Int cell = farmGround.WorldToCell(existing.transform.position);
            if (restoredCells.Contains(cell)) continue;
            plots.Remove(cell);
            existing.gameObject.SetActive(false);
            Destroy(existing.gameObject);
        }
        foreach (Vector3Int cell in plots.Keys) UpdateNeighborSprites(cell);
        foreach (var pair in plots)
            if (pair.Value != null) pair.Value.SetFertilizer(fertilizers.TryGetValue(pair.Key, out string fertilizer) ? fertilizer : null);

        CropPlot RestorePlot(int tileX, int tileY)
        {
            Vector3 world = new(tileX + 0.5f, -tileY - 0.5f, 0f);
            Vector3Int cell = farmGround.WorldToCell(world);
            if (!farmGround.HasTile(cell) || !restoredCells.Add(cell)) return null;
            if (!plots.TryGetValue(cell, out var plot)) plots[cell] = plot = SpawnPlot(cell);
            if (!plot.IsTilled) plot.TryHoe();
            return plot;
        }
    }
    public bool TryHarvest(Vector2 facing, PlayerInventory inventory, bool usingScythe = false)
        => RequestHarvest(facing, usingScythe, null);

    public bool RequestHarvest(Vector2 facing, bool usingScythe, System.Action<bool> done)
    {
        if (!IsFarmingAllowed()) return false;
        var plot = FindPlot(facing);
        if (plot == null || !plot.IsReadyToHarvest ||
            (plot.HarvestRequiresScythe && !usingScythe) || pendingHarvests.Contains(plot)) return false;
        var online = GetComponent<OnlineGameState>();
        if (online == null) { NpcConversation.ShowNotice("Đăng nhập để thu hoạch."); return false; }
        pendingHarvests.Add(plot);
        int tileX = Mathf.FloorToInt(plot.transform.position.x);
        int tileY = Mathf.FloorToInt(-plot.transform.position.y);
        online.HarvestFarmCrop(tileX, tileY, error =>
        {
            pendingHarvests.Remove(plot);
            bool success = string.IsNullOrEmpty(error) && plot != null &&
                plot.TryHarvestVisual(usingScythe);
            if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
            done?.Invoke(success);
        });
        return true;
    }
    /// <summary>Refills to 40/55/70/85/100 by upgrade level.</summary>
    internal void SyncWateringCan(ToolData can)
    {
        int capacity = ToolArea.WaterCapacity(can != null ? can.upgradeLevel : 0);
        if (capacity == waterCapacity) return;
        waterCapacity = capacity;
        waterLeft = Mathf.Min(waterLeft, waterCapacity);
        WaterChanged?.Invoke(waterLeft, waterCapacity);
    }

    // Waters the charged area and uses power + 1 water.
    private bool TryWaterArea(Vector2 facing, int power)
    {
        if (waterLeft <= 0 || !IsFarmingAllowed() || !TryGetTargetCell(facing, out var cell, out _)) return false;
        var online = GetComponent<OnlineGameState>();
        if (online == null) return false;
        var tiles = AreaTiles(cell, facing, power).FindAll(tile =>
            tile.x >= 0 && tile.y >= 0 && farmGround.HasTile(new Vector3Int(tile.x, -tile.y, 0)));
        var wet = new System.Collections.Generic.List<CropPlot>();
        foreach (var tile in tiles)
        {
            var plot = PlotAt(new Vector3Int(tile.x, -tile.y, 0));
            if (plot != null && plot.IsTilled && !plot.IsWatered) wet.Add(plot);
        }
        waterLeft = Mathf.Max(0, waterLeft - (power + 1));
        WaterChanged?.Invoke(waterLeft, waterCapacity);
        GameAudio.Play("wateringCan");
        PlayWaterEffect(facing);
        if (wet.Count == 0) return true;
        online.WaterFarmArea(power, tiles, error =>
        {
            if (!string.IsNullOrEmpty(error)) { NpcConversation.ShowNotice(error); return; }
            foreach (var plot in wet)
            {
                if (plot == null || !plot.TryWater()) continue;
                if (farmGround != null) UpdateNeighborSprites(farmGround.WorldToCell(plot.transform.position));
            }
        });
        return true;
    }

    public bool TryUseWateringCan(Vector2 facing)
    {
        int power = TakeChargePower();
        if (!IsRefillTarget(facing) && power > 0) return TryWaterArea(facing, power);
        if (IsRefillTarget(facing))
        {
            waterLeft = Mathf.Max(1, waterCapacity);
            WaterChanged?.Invoke(waterLeft, waterCapacity);
            GameAudio.Play("slosh");
            StartCoroutine(PlayDelayedAudio("glug", 0.25f));
            return true;
        }
        if (waterLeft <= 0 || !IsFarmingAllowed()) return false;
        CropPlot targetPlot = FindPlot(facing);
        if (targetPlot == null || !targetPlot.IsTilled || targetPlot.IsWatered) return false;
        var online = GetComponent<OnlineGameState>();
        if (online == null) return false;
        int tileX = Mathf.FloorToInt(targetPlot.transform.position.x);
        int tileY = Mathf.FloorToInt(-targetPlot.transform.position.y);
        online.WaterFarmCrop(tileX, tileY, error =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                NpcConversation.ShowNotice(error);
                return;
            }
            if (targetPlot == null || !targetPlot.TryWater()) return;
            if (farmGround != null)
                UpdateNeighborSprites(farmGround.WorldToCell(targetPlot.transform.position));
            waterLeft--;
            WaterChanged?.Invoke(waterLeft, waterCapacity);
            GameAudio.Play("wateringCan");
            PlayWaterEffect(facing);
        });
        return true;
    }

    private System.Collections.IEnumerator PlayDelayedAudio(string cue, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameAudio.Play(cue);
    }

    public bool CanUseWateringCan(Vector2 facing) => waterLeft > 0 || IsRefillTarget(facing);

    private bool IsRefillTarget(Vector2 facing)
    {
        if (!TryGetTargetCell(facing, out _, out var center)) return false;
        if (waterDetector == null) waterDetector = GetComponent<WaterDetector>() ?? gameObject.AddComponent<WaterDetector>();
        return waterDetector.IsWater(center);
    }

    public void PlayWaterEffect(Vector2 facing)
    {
        Vector3 destination = TryGetTargetCell(facing, out _, out var center)
            ? center : StandingPosition + (Vector3)(facing.normalized * 0.8f);
        PlayEffect(waterEffectPrefab, waterEffects, destination);
    }

    private void PlayEffect(FarmingVfx prefab, System.Collections.Generic.Queue<FarmingVfx> pool, Vector3 position)
    {
        if (prefab == null || wateringAtlas == null) return;
        FarmingVfx effect = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab);
        effect.transform.position = position;
        effect.gameObject.SetActive(true);
        effect.Play(wateringAtlas, () =>
        {
            effect.gameObject.SetActive(false);
            pool.Enqueue(effect);
        });
    }

    public bool TryHitResource(Vector2 facing, ItemUseContext context)
    {
        var resource = FindResource(facing, context);
        if (resource != null)
        {
            return resource.Hit(context);
        }
        if (context.Item != null && context.Item.kind == PlayerItemKind.Pickaxe)
        {
            if (TryGetTargetCell(facing, out _, out _))
            {
                GameAudio.Play("woodyHit");
            }
        }
        return false;
    }

    public bool HitResourcesInArc(Vector2 facing, ItemUseContext context)
    {
        Vector2 direction = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
        Vector2 origin = StandingPosition;
        Vector2 center = origin + direction * 0.65f;
        bool hitAny = false;
        var visited = new System.Collections.Generic.HashSet<WorldResource>();
        foreach (var collider in Physics2D.OverlapCircleAll(center, 1.1f))
        {
            var resource = collider.GetComponentInParent<WorldResource>();
            if (resource == null || resource.IsBroken || !visited.Add(resource)) continue;
            if (Vector2.Dot((Vector2)resource.transform.position - origin, direction) < -0.1f) continue;
            hitAny |= resource.Hit(context);
        }
        return hitAny;
    }

    public WorldResource FindResource(Vector2 facing)
        => FindResource(facing, null);

    private WorldResource FindResource(Vector2 facing, ItemUseContext? context)
    {
        if (pointerTarget.HasValue &&
            ((Vector2)StandingPosition - pointerTarget.Value).sqrMagnitude <= interactionRadius * interactionRadius * 2f)
        {
            foreach (var hit in Physics2D.OverlapPointAll(pointerTarget.Value))
            {
                var pointed = hit.GetComponentInParent<WorldResource>();
                if (pointed != null && !pointed.IsBroken && (!context.HasValue ||
                    pointed.CanHarvest(context.Value.Item?.itemData, context.Value.Item?.kind ?? PlayerItemKind.Material)))
                    return pointed;
            }
        }
        Vector2 dir = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
        Vector2 center = (Vector2)StandingPosition + dir * (interactionRadius * 0.5f);
        var hits = Physics2D.OverlapCircleAll(center, interactionRadius);
        WorldResource nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            var res = hit.GetComponentInParent<WorldResource>();
            if (res == null || res.IsBroken) continue;
            if (context.HasValue)
            {
                InventorySlot slot = context.Value.Item;
                PlayerItemKind kind = slot != null ? slot.kind : PlayerItemKind.Material;
                if (!res.CanHarvest(slot?.itemData, kind)) continue;
            }
            Vector2 target = hit.ClosestPoint(center);
            if (Vector2.Dot((Vector2)hit.bounds.center - (Vector2)StandingPosition, dir) < -0.1f) continue;
            float distance = (target - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = res;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private CropPlot FindPlot(Vector2 facing)
    {
        if (TryGetTargetCell(facing, out var cell, out _))
        {
            if (plots.TryGetValue(cell, out var existing) && existing != null) return existing;
            var cellCenter = farmGround.GetCellCenterWorld(cell);
            var cellHits = Physics2D.OverlapPointAll(cellCenter, plotLayer.value == 0 ? Physics2D.DefaultRaycastLayers : plotLayer.value);
            foreach (var hit in cellHits)
            {
                var found = hit.GetComponentInParent<CropPlot>();
                if (found != null) { plots[cell] = found; return found; }
            }
            return null;
        }
        if (pointerTarget.HasValue || farmGround != null) return null;
        if (selectedCrop != null && IsInRange(selectedCrop.transform.position)) return selectedCrop;

        var center = (Vector2)transform.position + facing.normalized * (interactionRadius * 0.5f);
        var mask = plotLayer.value == 0 ? Physics2D.DefaultRaycastLayers : plotLayer.value;
        var hits = Physics2D.OverlapCircleAll(center, interactionRadius, mask);
        CropPlot nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            var plot = hit.GetComponent<CropPlot>();
            if (plot == null) continue;
            var distance = ((Vector2)plot.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearest = plot;
            nearestDistance = distance;
        }
        return nearest;
    }

    private CropPlot SpawnPlot(Vector3Int cell)
    {
        var go = new GameObject($"CropPlot_{cell.x}_{cell.y}");
        go.transform.SetParent(activeMap.transform, false);
        go.transform.position = farmGround.GetCellCenterWorld(cell);
        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = (Vector2)farmGround.layoutGrid.cellSize * 0.9f;
        var plot = go.AddComponent<CropPlot>();
        plot.InitializeVisuals(farmGround.layoutGrid.cellSize, hoeDirtAtlas);
        return plot;
    }

    private void UpdateNeighborSprites(Vector3Int cell)
    {
        var offsets = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left, Vector3Int.zero };
        foreach (var offset in offsets)
        {
            var position = cell + offset;
            if (!plots.TryGetValue(position, out var plot) || plot == null) continue;
            int mask = 0;
            int waterMask = 0;
            if (plots.ContainsKey(position + Vector3Int.up)) mask |= 1;
            if (plots.ContainsKey(position + Vector3Int.right)) mask |= 2;
            if (plots.ContainsKey(position + Vector3Int.down)) mask |= 4;
            if (plots.ContainsKey(position + Vector3Int.left)) mask |= 8;
            if (plots.TryGetValue(position + Vector3Int.up, out var north) && north != null && north.IsWatered) waterMask |= 1;
            if (plots.TryGetValue(position + Vector3Int.right, out var east) && east != null && east.IsWatered) waterMask |= 2;
            if (plots.TryGetValue(position + Vector3Int.down, out var south) && south != null && south.IsWatered) waterMask |= 4;
            if (plots.TryGetValue(position + Vector3Int.left, out var west) && west != null && west.IsWatered) waterMask |= 8;
            plot.SetNeighbors(mask, waterMask);
        }
    }

    private bool TryGetTargetCell(Vector2 facing, out Vector3Int cell, out Vector3 center)
    {
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        if (location != activeMap)
        {
            activeMap = location;
            plots.Clear();
            farmGround = location == null ? null : location.transform.Find("Back")?.GetComponent<Tilemap>();
            farmBuildings = location == null ? null : location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        }
        cell = default;
        center = default;
        if (farmGround == null) return false;
        Vector2 direction = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
        Vector3 target = StandingPosition + (Vector3)(direction * farmGround.layoutGrid.cellSize.x);
        if (pointerTarget.HasValue)
        {
            if (!IsInAdjacentCell(pointerTarget.Value)) return false;
            target = pointerTarget.Value;
        }
        cell = farmGround.WorldToCell(target);
        center = farmGround.GetCellCenterWorld(cell);
        return IsInAdjacentCell(center);
    }

    private bool IsInAdjacentCell(Vector2 position)
    {
        if (farmGround == null) return false;
        var from = farmGround.WorldToCell(StandingPosition);
        var to = farmGround.WorldToCell(position);
        return Mathf.Abs(from.x - to.x) <= 1 && Mathf.Abs(from.y - to.y) <= 1;
    }

    private bool IsInRange(Vector3 target) => ((Vector2)(target - transform.position)).sqrMagnitude <= interactionRadius * interactionRadius;
}
