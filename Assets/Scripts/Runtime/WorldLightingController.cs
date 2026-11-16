using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(100)]
public sealed class WorldLightingController : MonoBehaviour
{
    private static readonly Color Sunset = new(1f, 0.68f, 0.43f, 1f);
    // Night subtracts yellow from the scene. Suppress red and green much more than blue
    // to get that look with the URP Multiply light.
    private static readonly Color Night = new(0.10f, 0.16f, 1f, 1f);
    private static readonly Color Rain = new(0.68f, 0.70f, 0.73f, 1f);
    private static readonly Color MineStone = new(80 / 255f, 80 / 255f, 40 / 255f, 1f);
    // These mine colors describe darkness in a subtractive lightmap.
    // URP's Multiply style needs the light that remains on the stone instead.
    private static readonly Color MineStoneAmbient = new(0.54f, 0.43f, 0.60f, 1f);
    private static readonly Color MineLavaAmbient = new(0.47f, 0.37f, 0.54f, 1f);
    private static readonly Color MineLampGlow = new(1f, 0.76f, 0.43f, 1f);
    private static readonly Color YellowTorch = Color.yellow;
    private static readonly Color PurpleTorch = new(70 / 255f, 0f, 150 / 255f, 1f);
    private static readonly Color RingGlow = new(1f, 0.88f, 0.65f, 1f);
    private static readonly Color IridiumGlow = new(1f, 0.69f, 1f, 1f);
    private static readonly int[] DefaultSortingLayer = { 0 };

    [SerializeField, Range(0.15f, 0.2f)] private float nightIntensity = 0.18f;
    [SerializeField, Range(0f, 1f)] private float indoorIntensity = 0.9f;
    [SerializeField, Range(0f, 1f)] private float caveAmbientIntensity = 0.12f;
    [SerializeField, Min(0.01f)] private float transitionSpeed = 6f;

    // Indoors after dark: a bluish (75,75,255) ambient.
    private static readonly Color IndoorNight = new(75 / 255f, 75 / 255f, 1f, 1f);
    private readonly Dictionary<GameLocation, List<MapLightingData.TileSwap>> tileSwaps = new();
    private readonly List<bool> mapLightIsWindow = new();
    // Lights drawn with a lighting mask, which remove darkness.
    private readonly List<bool> mapLightIsMask = new();
    private Color darknessColor;
    private List<MapLightingData.TileSwap> activeSwaps;
    private MapLightingData.MapEntry mapLighting;
    private bool? mapNightApplied;
    private Color? mapAmbient;
    private readonly List<Light2D> mapLights = new();
    private readonly List<MapLightFlicker> mapLightFlickers = new();
    private readonly List<Renderer> renderers = new();
    private static WorldLightingController instance;
    private LocationManager manager;
    private GameLocation location;
    private TownLightingData townLighting;
    private bool? townNightApplied;
    private FarmDayClock clock;
    private CharacterMotor farmer;
    private PlayerInventory inventory;
    private Light2D globalLight, playerLight;
    private Material worldMaterial;
    private int floor, mineDay, worldId, usedMapLights;
    private int? cutsceneOutdoorTime;
    private bool snapAmbient;
    private float mineIntensity, playerRadius;
    private Color mineColor, playerColor;
    private bool outdoors, cave, mine, initializeAmbient, playerLightVisible;

    private struct MapLightFlicker
    {
        public float baseIntensity;
        public float phase;
        public bool enabled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureController()
    {
        if (instance != null) return;
        var host = new GameObject("World Lighting Controller");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<WorldLightingController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        worldMaterial = Resources.Load<MapDoorCatalog>("MapDoorCatalog")?.litWorldMaterial;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    internal static void SetCutsceneOutdoorTime(int? time)
    {
        if (instance == null) return;
        instance.cutsceneOutdoorTime = time;
        instance.snapAmbient = true;
    }

    private void Update()
    {
        if (globalLight == null || location == null) return;

        GetAmbient(out float intensity, out Color color);
        float blend = snapAmbient ? 1f : 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        snapAmbient = false;
        globalLight.intensity = Mathf.Lerp(globalLight.intensity, intensity, blend);
        globalLight.color = Color.Lerp(globalLight.color, color, blend);
        Color lit = globalLight.color;
        AmbientDarkness = Mathf.Clamp01(1f - (lit.r * 0.3f + lit.g * 0.59f + lit.b * 0.11f) * globalLight.intensity);
        // Ambient light is the darkness subtracted from the scene; a light at full mask strength
        // removes exactly that, back to the art's own colours.
        darknessColor = new Color(Mathf.Clamp01(1f - lit.r * globalLight.intensity),
            Mathf.Clamp01(1f - lit.g * globalLight.intensity), Mathf.Clamp01(1f - lit.b * globalLight.intensity), 1f);
        UpdateMapLightFlicker();
        UpdateTownLighting(intensity);
        UpdateMapLighting(intensity, color);
        UpdatePlayerLight(blend);
    }

    // Map entry can create pooled lights and read assets. The per-frame fade above
    // only works with cached objects and value types.
    private void LateUpdate()
    {
        var activeManager = LocationManager.Instance;
        if (!ReferenceEquals(activeManager, manager))
            BindWorld(activeManager);
        if (manager == null) return;

        CharacterMotor activeFarmer = manager.LocalFarmer;
        if (farmer != activeFarmer)
        {
            farmer = activeFarmer;
            inventory = farmer != null ? farmer.GetComponent<PlayerInventory>() : null;
            ApplyWorldMaterial(farmer);
        }

        GameLocation activeLocation = manager.CurrentLocation;
        int day = clock != null ? clock.Day : 1;
        if (activeLocation != location || (floor > 0 && mineDay != day))
            EnterLocation(activeLocation, day);
        if (playerLight != null && playerLight.enabled != playerLightVisible)
            playerLight.enabled = playerLightVisible;
    }

    private void BindWorld(LocationManager activeManager)
    {
        manager = activeManager;
        location = null;
        townLighting = null;
        farmer = null;
        inventory = null;
        globalLight = null;
        playerLightVisible = false;
        usedMapLights = 0;
        DisableUnusedLights();
        if (playerLight != null)
        {
            playerLight.intensity = 0f;
            playerLight.enabled = false;
        }
        if (manager == null) return;

        clock = FarmDayClock.Instance;
        worldId = StableWorldId(StartupSession.Api?.UserName, StartupSession.Farmer?.farmName);
        foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.gameObject.scene == manager.gameObject.scene &&
                light.lightType == Light2D.LightType.Global && light.blendStyleIndex == 0)
            {
                globalLight = light;
                break;
            }
        }
        if (globalLight == null)
        {
            var host = new GameObject("World Ambient Light");
            host.transform.SetParent(manager.transform, false);
            host.SetActive(false);
            globalLight = host.AddComponent<Light2D>();
            globalLight.targetSortingLayers = DefaultSortingLayer;
            globalLight.lightType = Light2D.LightType.Global;
            host.SetActive(true);
        }
        if (playerLight == null)
        {
            playerLight = CreatePointLight("Farmer Equipment Light");
            playerLight.intensity = 0f;
        }
        initializeAmbient = true;
    }

    private void EnterLocation(GameLocation next, int day)
    {
        location = next;
        townLighting = null;
        mapLighting = null;
        activeSwaps = null;
        mapNightApplied = null;
        mapAmbient = null;
        usedMapLights = 0;
        floor = 0;
        if (next != null)
        {
            ApplyWorldMaterial(next);
            outdoors = next.IsOutdoors;
            string map = next.LocationName;
            if (MineManager.IsMineFloor(map)) int.TryParse(map, out floor);
            mine = floor > 0 || string.Equals(map, "Mine", StringComparison.OrdinalIgnoreCase);
            cave = mine || (!outdoors &&
                (map.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 map.IndexOf("Underground", StringComparison.OrdinalIgnoreCase) >= 0));
            outdoors &= !cave;
            if (mine) SetupMineLighting(floor, day);
            else if (outdoors) SetupTownLighting();
            if (!mine && townLighting == null) SetupMapLighting(next);
            if (initializeAmbient || townLighting != null || mapLighting != null || !outdoors)
            {
                GetAmbient(out float intensity, out Color color);
                if (initializeAmbient || !outdoors)
                {
                    // Indoors the ambient switches on entry, not by fading.
                    globalLight.intensity = intensity;
                    globalLight.color = color;
                    initializeAmbient = false;
                }
                UpdateTownLighting(intensity);
                UpdateMapLighting(intensity, color);
            }
        }
        DisableUnusedLights();
    }

    private void ApplyWorldMaterial(Component root)
    {
        if (root == null || worldMaterial == null) return;
        root.GetComponentsInChildren(true, renderers);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is not (TilemapRenderer or SpriteRenderer)) continue;
            Material material = renderer.sharedMaterial;
            if (material != null && (material.shader.name == "Universal Render Pipeline/2D/Sprite-Unlit-Default" ||
                                     material.shader.name == "Sprites/Default"))
                renderer.sharedMaterial = worldMaterial;
        }
        renderers.Clear();
    }

    private void GetAmbient(out float intensity, out Color color)
    {
        if (outdoors)
        {
            float clockTicks = clock != null ? clock.DayProgress * 120f : 0f;
            float intervalMs = (clockTicks - Mathf.Floor(clockTicks)) * 7000f;
            EvaluateOutdoorLight(cutsceneOutdoorTime ?? clock?.TimeOfDay ?? 600, clock?.SeasonIndex ?? 0,
                cutsceneOutdoorTime.HasValue ? 0f : intervalMs,
                !cutsceneOutdoorTime.HasValue && clock != null && clock.IsRainingOutdoors(location),
                nightIntensity, out intensity, out color);
        }
        else if (cave)
        {
            intensity = mine ? mineIntensity : caveAmbientIntensity;
            color = mine ? mineColor : MineStone;
        }
        else if (mapAmbient.HasValue)
        {
            // A map's AmbientLight property replaces the time-of-day ambient.
            intensity = 1f;
            color = mapAmbient.Value;
        }
        else if (IsDarkOut(clock))
        {
            intensity = 1f;
            color = IndoorNight;
        }
        else
        {
            // Before dark, rooms follow the outdoor light: they dim with the sunset and with rain.
            float clockTicks = clock != null ? clock.DayProgress * 120f : 0f;
            float intervalMs = (clockTicks - Mathf.Floor(clockTicks)) * 7000f;
            EvaluateOutdoorLight(clock?.TimeOfDay ?? 600, clock?.SeasonIndex ?? 0, intervalMs,
                clock != null && clock.IsRainingToday, nightIntensity, out intensity, out color);
        }
    }

    /// <summary>True from the season's truly-dark time.</summary>
    internal static bool IsDarkOut(FarmDayClock clock) =>
        clock != null && clock.TimeOfDay >= StartingDarkTime(clock.SeasonIndex) + 200;

    private void SetupMapLighting(GameLocation next)
    {
        string map = next.ContentMapName ?? next.LocationName;
        mapLighting = MapLightingData.For(map);
        if (mapLighting == null) return;
        if (mapLighting.ambient != null && mapLighting.ambient.Length >= 3 && !outdoors)
            mapAmbient = new Color(1f - mapLighting.ambient[0] / 255f, 1f - mapLighting.ambient[1] / 255f,
                1f - mapLighting.ambient[2] / 255f, 1f);
        if (!tileSwaps.TryGetValue(next, out activeSwaps))
            tileSwaps[next] = activeSwaps = MapLightingData.BuildSwaps(next, mapLighting);
        // "Light" and "WindowLight" become LightSources of radius 1 at the
        // tile centre, drawn with the mask for their type.
        if (mapLighting.lights != null)
            foreach (var light in mapLighting.lights)
                AddMaskLight(next.TileCenter(light.x, light.y), light.type, window: false);
        if (mapLighting.windowLights != null)
            foreach (var light in mapLighting.windowLights)
                AddMaskLight(next.TileCenter(light.x, light.y), light.type, window: true);
    }

    private void UpdateMapLighting(float ambientIntensity, Color ambientColor)
    {
        if (mapLighting == null || clock == null) return;
        bool raining = clock.IsRainingToday;
        bool dark = IsDarkOut(clock);
        // Night tiles after dark or in rain (SandyHouse keeps its day tiles).
        bool night = dark || raining && !string.Equals(location.ContentMapName, "SandyHouse",
            StringComparison.OrdinalIgnoreCase);
        if (mapNightApplied != night && activeSwaps != null)
        {
            foreach (var swap in activeSwaps)
                if (swap.Layer != null) swap.Layer.SetTile(swap.Cell, night ? swap.Night : swap.Day);
            mapNightApplied = night;
        }

        // Window light is off once it is dark or raining (LightContext.WindowLight).
        UpdateMaskLights(raining, dark);
    }

    internal static void EvaluateOutdoorLight(int timeOfDay, int season, float gameTimeInterval,
        bool raining, float minimumNightIntensity, out float intensity, out Color color)
    {
        int startDark = StartingDarkTime(season);
        int trulyDark = startDark + 200;
        if (timeOfDay < startDark)
        {
            intensity = 1f;
            color = Color.white;
        }
        else
        {
            float time = timeOfDay - timeOfDay % 100 + (timeOfDay % 100 / 10) * 16.66f
                + gameTimeInterval / 7000f * 16.6f;
            bool night = timeOfDay >= trulyDark;
            float num = Mathf.Min(0.93f, night
                ? 0.75f + (time - trulyDark) * 0.000625f
                : 0.30f + (time - startDark) * 0.00225f);
            intensity = Mathf.Max(minimumNightIntensity, 1f - num);
            color = night ? Night : Color.Lerp(Sunset, Night, (num - 0.30f) / 0.45f);
        }

        if (raining)
        {
            intensity *= 0.8f;
            color = Color.Lerp(color, Rain, 0.25f);
        }
    }

    private static int StartingDarkTime(int season) => season == 3 ? 1600 : season == 2 ? 1700 : 1800;

    private void SetupTownLighting()
    {
        string map = location.ContentMapName ?? location.LocationName;
        if (!map.Equals("Town", StringComparison.OrdinalIgnoreCase)) return;
        townLighting = Resources.Load<TownLightingData>("TownLighting");
        if (townLighting == null) return;
        townNightApplied = null;

        // Town's lamps use the 512px sconce mask at scale 1.
        foreach (TownLightingData.Lamp lamp in townLighting.Lamps)
            AddMaskLight(location.TileCenter(lamp.x, lamp.y), 4, window: false);
    }

    private void UpdateTownLighting(float ambientIntensity)
    {
        if (townLighting == null) return;

        bool raining = clock != null && clock.IsRainingOutdoors(location);
        int trulyDark = StartingDarkTime(clock != null ? clock.SeasonIndex : 0) + 200;
        bool night = raining || (clock != null && clock.TimeOfDay >= trulyDark);
        if (townNightApplied != night)
        {
            townLighting.ApplyTiles(location, night);
            townNightApplied = night;
        }

        // Each lamp removes the darkness under its mask, so it shows as dusk darkens the world.
        UpdateMaskLights(raining, night);
    }

    private static int StableWorldId(string account, string farm)
    {
        string key = string.IsNullOrEmpty(account) ? farm : account;
        int id = 17;
        if (key != null)
            foreach (char letter in key) id = unchecked(id * 31 + letter);
        return id;
    }

    internal static bool IsDarkFloor(int floor, double roll) =>
        floor % 40 > 30 || (floor > 5 && roll < 0.15);

    private void SetupMineLighting(int level, int day)
    {
        mineDay = day;
        var random = new System.Random(unchecked(day * level + 4 * level + worldId / 2));
        bool lava = level is >= 80 and <= 120;
        mineColor = lava ? MineLavaAmbient : MineStoneAmbient;
        mineIntensity = IsDarkFloor(level, random.NextDouble()) ? 0.54f : 1f;

        var buildings = location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (buildings != null)
        {
            foreach (Vector3Int cell in buildings.cellBounds.allPositionsWithin)
            {
                int tile = TileSpriteIndex.Get(buildings.GetSprite(cell));
                if (tile is 48 or 66 or 97 or 113)
                {
                    // Tiles 65, 66, 81 and 82 form one lantern. The flame sits
                    // at the bottom-left corner of its top-right tile (66).
                    Vector3 flame = tile == 66
                        ? buildings.CellToWorld(cell)
                        : buildings.GetCellCenterWorld(cell);
                    AddMapLight(flame, 2.5f, MineLampGlow, 0.36f, flicker: true);
                }
                else if (tile == 115)
                {
                    Vector3 center = buildings.GetCellCenterWorld(cell);
                    Vector3 tileUp = buildings.GetCellCenterWorld(cell + Vector3Int.up) - center;
                    Vector3 edge = center + tileUp * 0.5f;
                    for (int i = 0; i < 4; i++)
                        AddMapLight(edge + tileUp * (2 - i), (i + 1) * 0.25f, MineLampGlow, 0.08f);
                }
            }
        }

        var water = location.WaterMask;
        var back = lava && water != null ? location.transform.Find("Back")?.GetComponent<Tilemap>() : null;
        if (back == null) return;
        var lavaGlow = new Color(1f, 0.24f, 0.14f, 1f);
        for (int y = 0; y < water.height; y++)
        for (int x = 0; x < water.width; x++)
            if (water.Contains(x, y) && random.NextDouble() < 0.10)
                AddMapLight(back.GetCellCenterWorld(new Vector3Int(x, -y, 0)), 2f, lavaGlow, 0.30f);
    }

    /// <summary>How dark the current ambient is (0 in daylight); lights fade in with it.</summary>
    internal static float AmbientDarkness { get; private set; }

    /// <summary>A point light for a furniture glow (fireplace, TV), set up like the map lights.</summary>
    internal static Light2D CreateFurnitureLight(Transform parent, string label, Color color, float radius)
    {
        var host = new GameObject(label);
        host.transform.SetParent(parent, false);
        host.SetActive(false);
        var light = host.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 0;
        light.targetSortingLayers = DefaultSortingLayer;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.falloffIntensity = 0.75f;
        light.color = color;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.08f;
        light.intensity = 0f;
        host.SetActive(true);
        return light;
    }

    private Light2D CreatePointLight(string label, int blendStyleIndex = 1)
    {
        var host = new GameObject(label);
        host.transform.SetParent(transform, false);
        host.SetActive(false);
        var light = host.AddComponent<Light2D>();
        light.enabled = false;
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = blendStyleIndex;
        light.targetSortingLayers = DefaultSortingLayer;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.falloffIntensity = 0.75f;
        host.SetActive(true);
        return light;
    }

    /// <summary>A LightSource drawn with its original mask (see LightMasks), at one world pixel per mask pixel.</summary>
    private void AddMaskLight(Vector3 position, int type, bool window)
    {
        Sprite mask = LightMasks.For(type);
        if (mask == null)
        {
            // No mask asset: fall back to a round light of the same size.
            AddMapLight(position, MapLightingData.LightRadius(type), Color.white, 0f, window: window);
            mapLightIsMask[usedMapLights - 1] = true;
            return;
        }
        AddMapLight(position, 1f, Color.white, 0f, window: window);
        Light2D light = mapLights[usedMapLights - 1];
        light.lightType = Light2D.LightType.Sprite;
        light.lightCookieSprite = mask;
        light.falloffIntensity = 0f;
        mapLightIsMask[usedMapLights - 1] = true;
    }

    /// <summary>Mask lights take the colour of the darkness they remove; windows switch off at night or in rain.</summary>
    private void UpdateMaskLights(bool raining, bool dark)
    {
        for (int i = 0; i < usedMapLights && i < mapLightIsMask.Count; i++)
        {
            if (!mapLightIsMask[i]) continue;
            Light2D light = mapLights[i];
            bool on = !(mapLightIsWindow[i] && (dark || raining)) && darknessColor.maxColorComponent > 0.01f;
            if (on)
            {
                if (light.color != darknessColor) light.color = darknessColor;
                if (light.intensity != 1f) light.intensity = 1f;
            }
            if (light.enabled != on) light.enabled = on;
        }
    }

    private void AddMapLight(Vector3 position, float radius, Color color, float intensity, bool flicker = false,
        bool window = false)
    {
        if (usedMapLights == mapLights.Count)
        {
            // Modulate the sprite color to preserve detail, including on dark rock.
            mapLights.Add(CreatePointLight("Map Tile Light", blendStyleIndex: 0));
            mapLightFlickers.Add(default);
        }
        while (mapLightIsWindow.Count < mapLights.Count) mapLightIsWindow.Add(false);
        while (mapLightIsMask.Count < mapLights.Count) mapLightIsMask.Add(false);
        mapLightIsWindow[usedMapLights] = window;
        mapLightIsMask[usedMapLights] = false;
        Light2D light = mapLights[usedMapLights++];
        // A pooled light may have been a mask light on the last map.
        light.lightType = Light2D.LightType.Point;
        light.falloffIntensity = 0.75f;
        light.transform.position = position;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.08f;
        light.color = color;
        light.intensity = intensity;
        light.enabled = true;
        mapLightFlickers[usedMapLights - 1] = new MapLightFlicker
        {
            baseIntensity = intensity,
            phase = (usedMapLights - 1) * 0.6180339f,
            enabled = flicker
        };
    }

    private void UpdateMapLightFlicker()
    {
        for (int i = 0; i < usedMapLights && i < mapLightFlickers.Count; i++)
        {
            MapLightFlicker state = mapLightFlickers[i];
            if (!state.enabled || mapLights[i] == null || !mapLights[i].enabled) continue;
            float noise = Mathf.PerlinNoise(Time.time * 6f + state.phase, state.phase * 0.37f);
            mapLights[i].intensity = state.baseIntensity + (noise - 0.5f) * 0.04f;
        }
    }

    private void DisableUnusedLights()
    {
        for (int i = usedMapLights; i < mapLights.Count; i++) mapLights[i].enabled = false;
    }

    private static void SetPointRadius(Light2D light, float radius)
    {
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.2f;
    }

    internal static int LightRingId(string itemId) => itemId switch
    {
        "ring_516" or "object_516" => 516,
        "ring_517" or "object_517" => 517,
        "ring_527" or "object_527" => 527,
        "ring_888" or "object_888" => 888,
        _ => 0
    };

    private static float RingRadius(int id) => id == 516 ? 5f : id != 0 ? 10f : 0f;

    private void UpdatePlayerLight(float blend)
    {
        if (playerLight == null) return;
        if (farmer == null || inventory == null)
        {
            playerLightVisible = false;
            playerLight.intensity = 0f;
            return;
        }

        var selected = inventory.SelectedSlot;
        int torchId = selected != null && !selected.IsEmpty ? HeldTorchId(selected.Data?.itemId) : 0;
        bool holdingTorch = torchId != 0;
        int left = LightRingId(inventory.LeftLightRing?.itemId);
        int right = LightRingId(inventory.RightLightRing?.itemId);
        float ringRadius = Mathf.Max(RingRadius(left), RingRadius(right));
        float torchRadius = mine ? 1.5f : 1.25f;
        float flicker = holdingTorch ? Mathf.PerlinNoise(Time.time * 6f, torchId * 0.173f) : 0.5f;
        float radius = Mathf.Max(ringRadius, holdingTorch ? torchRadius + (flicker - 0.5f) * 0.2f : 0f);
        Color torchColor = torchId switch { 94 => YellowTorch, 95 => PurpleTorch, _ => Color.white * 0.9f };
        Color color = left is 527 or 888 || right is 527 or 888 ? IridiumGlow : ringRadius > 0f ? RingGlow : torchColor;
        float strength = outdoors ? Mathf.Clamp01((1f - globalLight.intensity) * 2f) : 1f;
        float target = radius > 0f ? (holdingTorch && ringRadius <= 0f
            ? (0.9f + (flicker - 0.5f) * 0.25f) * strength
            : 0.9f * strength) : 0f;
        if (radius > 0f && (radius != playerRadius || color != playerColor))
        {
            SetPointRadius(playerLight, radius);
            playerLight.color = color;
            playerRadius = radius;
            playerColor = color;
        }
        else if (holdingTorch && ringRadius <= 0f)
            SetPointRadius(playerLight, radius);

        float intensity = Mathf.Lerp(playerLight.intensity, target, blend);
        playerLight.intensity = intensity;
        playerLightVisible = target > 0f || intensity > 0.005f;
        if (playerLightVisible)
            playerLight.transform.position = farmer.transform.position + new Vector3(0f, 0.15f, 0f);
    }

    private static int HeldTorchId(string itemId) => itemId switch
    {
        "object_93" or "torch" => 93,
        "object_94" => 94,
        "object_95" => 95,
        _ => 0
    };
}
