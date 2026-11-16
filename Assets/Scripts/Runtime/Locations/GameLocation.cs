using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Runtime representation of a map location.
/// Holds location metadata, warp points, and spatial queries.
/// </summary>
public sealed class GameLocation : MonoBehaviour
{
    [SerializeField] private string locationName;
    [SerializeField] private bool isOutdoors = true;
    [SerializeField] private List<LocationWarp> warps = new();

    public string LocationName
    {
        get => string.IsNullOrEmpty(locationName) ? gameObject.name : locationName;
        set => locationName = value;
    }

    public bool IsOutdoors
    {
        get => isOutdoors;
        set => isOutdoors = value;
    }

    public IReadOnlyList<LocationWarp> Warps => warps;
    public string ContentMapName { get; internal set; }
    internal string MineSurfaceSheet { get; set; }
    private MapDoorCatalog tileCatalog;
    internal MapWaterMask WaterMask { get; private set; }
    internal FarmMailbox Mailbox { get; private set; }
    internal InteriorRoomDoors InteriorDoors { get; private set; }

    internal Vector3 TileCenter(int x, int y)
    {
        var ground = transform.Find("Back")?.GetComponent<Tilemap>();
        return ground != null ? ground.GetCellCenterWorld(new Vector3Int(x, -y, 0)) :
            transform.TransformPoint(new Vector3(x + 0.5f, 0.5f - y, 0f));
    }

    // Last surface sound; a tile without a Type keeps the previous step. Entering a map starts
    // with sand outdoors and a thud indoors.
    private string currentStep;

    private void OnEnable() => currentStep = null;

    public string GetFootstepCue(Vector3 standingPosition)
    {
        bool caveLike = int.TryParse(LocationName, out _) ||
            LocationName.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0 ||
            LocationName.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0 ||
            LocationName.Equals("Greenhouse", StringComparison.OrdinalIgnoreCase);
        // Houses, shops and other interiors always thud, whatever the floor.
        if (!isOutdoors && !caveLike) return "thudStep";
        currentStep ??= isOutdoors ? "sandyStep" : "thudStep";

        if (tileCatalog == null) tileCatalog = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
        string type = tileCatalog?.SurfaceAt(ContentMapName ?? LocationName,
            transform.Find("Buildings")?.GetComponent<UnityEngine.Tilemaps.Tilemap>(), standingPosition, MineSurfaceSheet);
        if (string.IsNullOrEmpty(type)) type = tileCatalog?.SurfaceAt(ContentMapName ?? LocationName,
            transform.Find("Back")?.GetComponent<UnityEngine.Tilemaps.Tilemap>(), standingPosition, MineSurfaceSheet);

        // Dirt (paths, sand and tilled soil alike) sounds sandy.
        currentStep = type switch
        {
            "Dirt" or "Sand" => "sandyStep",
            "Stone" => "stoneStep",
            "Grass" => FarmDayClock.Instance?.CurrentSeason == "winter" ? "snowyStep" : "grassyStep",
            "Wood" => "woodyStep",
            _ => currentStep
        };
        return currentStep;
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(locationName))
        {
            locationName = gameObject.name.Replace("Map_", "").Trim();
        }
        ContentMapName = LocationName;

        if (Application.isPlaying)
        {
            HidePathMarkers();
            if (!IsOutdoors) InteriorDoors = InteriorRoomDoors.Attach(this);
            BusStopBus.Attach(this);
            BeachBridge.Attach(this);
            WaterMask = MapWaterMask.ForMap(ContentMapName);
            tileCatalog = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
            if (tileCatalog != null)
            {
                foreach (LocationWarp door in tileCatalog.DoorsFor(LocationName))
                {
                    if (IsCollidingWithWarp(door.x, door.y) == null) warps.Add(door);
                }
            }
        }

        // Auto create trigger colliders if none exist under Warps child
        EnsureWarpTriggers();

        if (Application.isPlaying && string.Equals(LocationName, "SeedShop", StringComparison.OrdinalIgnoreCase))
        {
            if (GetComponent<SeedShopController>() == null)
            {
                gameObject.AddComponent<SeedShopController>();
            }
        }
    }

    private void Start()
    {
        if (LocationName == "Farm") Mailbox = FarmMailbox.Attach(this);
        if (LocationName == "Mountain") MineEntranceBlock.Attach(this);
        if (LocationName == "FarmHouse" && GetComponent<FarmHouseFurnishings>() == null)
            gameObject.AddComponent<FarmHouseFurnishings>();
        var back = transform.Find("Back")?.GetComponent<Tilemap>();
        if (back != null)
        {
            if (tileCatalog == null) tileCatalog = Resources.Load<MapDoorCatalog>("MapDoorCatalog");
            WaterMask ??= MapWaterMask.ForMap(ContentMapName);
            if (WaterMask != null)
                gameObject.AddComponent<MapWaterView>().Initialize(back, WaterMask, LocationName == "Farm", tileCatalog);
        }
        MapWaterTileView.Attach(this);
    }

    // The Paths layer only holds spawn and light markers and is never drawn. It used to be
    // hidden only on outdoor maps, so interiors showed the light markers on the floor.
    private void HidePathMarkers()
    {
        foreach (string layer in new[] { "Paths", "Paths_Obstacles", "Paths_Weeds" })
        {
            var renderer = transform.Find(layer)?.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    public void AddWarp(LocationWarp warp)
    {
        if (warp == null) return;
        warps.Add(warp);
    }

    internal void RemoveWarp(LocationWarp warp) => warps.Remove(warp);

    public void ClearWarps()
    {
        warps.Clear();
    }

    /// <summary>
    /// Updates warps from map properties:
    /// Parses the raw 'Warp' string from map properties.
    /// Each warp is 5 space-delimited tokens: [x, y, targetName, targetX, targetY].
    /// </summary>
    public void LoadWarpsFromPropertyString(string rawWarpString)
    {
        if (string.IsNullOrWhiteSpace(rawWarpString)) return;

        string[] tokens = rawWarpString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 4 < tokens.Length; i += 5)
        {
            if (int.TryParse(tokens[i], out int x) &&
                int.TryParse(tokens[i + 1], out int y) &&
                int.TryParse(tokens[i + 3], out int tx) &&
                int.TryParse(tokens[i + 4], out int ty))
            {
                string target = tokens[i + 2];
                warps.Add(new LocationWarp(x, y, target, tx, ty));
            }
        }
    }

    /// <summary>
    /// Checks warp collision at the given tile position:
    /// Checks whether the specified tile position contains a warp point.
    /// </summary>
    public LocationWarp IsCollidingWithWarp(int tileX, int tileY)
    {
        for (int i = 0; i < warps.Count; i++)
        {
            LocationWarp w = warps[i];
            if (w.x == tileX && w.y == tileY)
            {
                return w;
            }
        }
        return null;
    }

    /// <summary>
    /// Instantiates WarpTrigger children with 2D colliders for all registered warps.
    /// </summary>
    public void EnsureWarpTriggers()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
#endif
        Transform warpsRoot = transform.Find("Warps");
        if (warpsRoot == null)
        {
            var go = new GameObject("Warps");
            go.transform.SetParent(transform, false);
            warpsRoot = go.transform;
        }

        for (int i = 0; i < warps.Count; i++)
        {
            LocationWarp w = warps[i];
            // Buildings-layer actions use the interaction button, not a warp collider.
            if (w.fromMapAction) continue;
            string triggerName = $"Warp_{w.targetName}_{w.x}_{w.y}";
            Transform existing = warpsRoot.Find(triggerName);
            if (existing != null)
            {
                existing.position = TileCenter(w.x, w.y);
                continue;
            }
            var warpObj = new GameObject(triggerName);
            warpObj.transform.SetParent(warpsRoot, false);
            warpObj.transform.position = TileCenter(w.x, w.y);

            var col = warpObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.9f);

            var trigger = warpObj.AddComponent<WarpTrigger>();
            trigger.warp = w;
        }
    }
}
