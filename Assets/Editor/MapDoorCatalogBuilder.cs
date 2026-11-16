using System;
using System.Collections.Generic;
using System.IO;
using StardewValley.EditorTools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MapDoorCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/MapDoorCatalog.asset";
    private const string InteriorDoorsPath = "Assets/Resources/InteriorRoomDoors.json";
    static MapDoorCatalogBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var catalog = AssetDatabase.LoadAssetAtPath<MapDoorCatalog>(CatalogPath);
            if (catalog == null || catalog.surfaceVersion < 1 || !File.Exists(InteriorDoorsPath)) Rebuild();
        };
    }

    [MenuItem("Tools/Maps/Rebuild Door Catalog")]
    public static void Rebuild()
    {
        MapDoorCatalog catalog = AssetDatabase.LoadAssetAtPath<MapDoorCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MapDoorCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.mineFrost = LoadMineTexture("mine_frost");
        catalog.mineFrostDark = LoadMineTexture("mine_frost_dark");
        catalog.mineLava = LoadMineTexture("mine_lava");
        catalog.mineLavaDark = LoadMineTexture("mine_lava_dark");
        catalog.waterRippleAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/Cursors.png");
        catalog.waterRippleShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/MapWaterOverlay.shader");
        catalog.litWorldMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        catalog.seasonalWaterfalls = Array.ConvertAll(new[] { "spring", "summer", "fall", "winter" },
            season => AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Content/Maps/{season}_Waterfalls.png"));
        catalog.townFountain = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Maps/spring_town.png");
        catalog.mineTreasureRewards = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/MineTreasureRewards.json");

        var locations = new List<MapDoorLocation>();
        var interiorDoors = new List<MapInteriorRoomDoor>();
        var surfaces = new List<MapSurfaceRule>();
        var cells = new List<MapSurfaceCell>();
        var waterMasks = new List<MapWaterMask>();
        var waterClips = new List<MapWaterTileClip>();
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] files = Directory.GetFiles("Assets/Content/Maps", "*.tbin", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        int doorCount = 0;
        foreach (string file in files)
        {
            TbinData map;
            using (FileStream stream = File.OpenRead(file)) map = TbinParser.Read(stream);
            var location = new MapDoorLocation { locationName = Path.GetFileNameWithoutExtension(file) };
            var sheets = new Dictionary<string, TileSheetData>(StringComparer.OrdinalIgnoreCase);
            foreach (TileSheetData sheet in map.TileSheets)
            {
                sheets[sheet.Id] = sheet;
                string image = Path.GetFileNameWithoutExtension(sheet.ImageSource.Replace('\\', '/'));
                foreach (var property in sheet.Properties)
                {
                    string[] parts = property.Key.Split('@');
                    if (parts.Length != 4 || parts[1] != "TileIndex" || parts[3] != "Type" ||
                        !int.TryParse(parts[2], out int tile)) continue;
                    string key = image + "|" + tile;
                    if (defaults.TryGetValue(key, out string previous) && previous == property.Value) continue;
                    bool shared = !defaults.ContainsKey(key);
                    if (shared) defaults[key] = property.Value;
                    surfaces.Add(new MapSurfaceRule { map = shared ? "" : location.locationName,
                        sheet = image, tile = tile, type = property.Value });
                }
            }

            foreach (LayerData layer in map.Layers)
            {
                byte[] water = layer.Id == "Back" ? new byte[(layer.Width * layer.Height + 7) / 8] : null;
                byte[] invisibleWater = null;
                bool hasWater = false;
                for (int y = 0; y < layer.Height; y++)
                for (int x = 0; x < layer.Width; x++)
                {
                    TileData tile = layer.Tiles[x, y];
                    if (tile == null) continue;
                    AddWaterClip(waterClips, location.locationName, layer.Id, x, y, tile, sheets);
                    if (water != null && (tile.Properties.TryGetValue("Water", out string waterValue) ||
                        (sheets.TryGetValue(tile.TileSheetId, out var waterSheet) &&
                        waterSheet.Properties.TryGetValue($"@TileIndex@{tile.TileIndex}@Water", out waterValue))))
                    {
                        int bit = y * layer.Width + x;
                        byte flag = (byte)(1 << (bit % 8));
                        water[bit / 8] |= flag;
                        if (waterValue == "I")
                        {
                            invisibleWater ??= new byte[water.Length];
                            invisibleWater[bit / 8] |= flag;
                        }
                        hasWater = true;
                    }
                    if ((layer.Id == "Back" || layer.Id == "Buildings") && tile.Properties.TryGetValue("Type", out string surface))
                        cells.Add(new MapSurfaceCell { map = location.locationName, layer = layer.Id,
                            x = x, y = y, type = surface });
                    string action = GetAction(tile, sheets);
                    if (string.IsNullOrEmpty(action)) continue;
                    bool buildingAction = layer.Id == "Buildings";
                    // Store the farmer's standing tile for Buildings actions.
                    // Runtime door contact must use this coordinate as-is.
                    if (!TryMakeDoor(action, x, y + (buildingAction ? 1 : 0),
                        buildingAction, out LocationWarp door))
                        continue;
                    location.doors.Add(door);
                    doorCount++;
                }
                if (hasWater)
                    waterMasks.Add(new MapWaterMask { map = location.locationName, width = layer.Width,
                        height = layer.Height, cellsBase64 = Convert.ToBase64String(water),
                        invisibleBase64 = invisibleWater == null ? "" : Convert.ToBase64String(invisibleWater) });
            }

            AddInteriorDoors(interiorDoors, location.locationName, map, sheets);
            if (location.doors.Count > 0) locations.Add(location);
        }

        catalog.locations = locations;
        catalog.surfaces = surfaces;
        catalog.surfaceCells = cells;
        WriteResourceJson(InteriorDoorsPath, new MapInteriorRoomDoorData { doors = interiorDoors.ToArray() });
        WriteResourceJson("Assets/Resources/MapWaterMasks.json",
            new MapWaterMaskData { masks = waterMasks.ToArray() });
        const string animationPath = "Assets/Resources/WaterTileAnimations.json";
        WriteResourceJson(animationPath, new MapWaterTileClips { clips = waterClips.ToArray() });
        catalog.surfaceVersion = 1;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Doors] Indexed {doorCount} tile actions in {locations.Count} maps.");
    }

    private static void AddInteriorDoors(List<MapInteriorRoomDoor> doors, string mapName, TbinData map,
        Dictionary<string, TileSheetData> sheets)
    {
        if (!map.Properties.TryGetValue("Doors", out string raw)) return;
        LayerData buildings = map.Layers.Find(layer => layer.Id == "Buildings");
        if (buildings == null) return;
        string[] cells = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 3 < cells.Length; i += 4)
        {
            if (!int.TryParse(cells[i], out int x) || !int.TryParse(cells[i + 1], out int y) ||
                x < 0 || x >= buildings.Width || y < 0 || y >= buildings.Height) continue;
            TileData tile = buildings.Tiles[x, y];
            string[] action = tile == null ? null : GetAction(tile, sheets)?.Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (action == null || action.Length == 0 || action[0] != "Door") continue;
            var owners = new string[action.Length - 1];
            Array.Copy(action, 1, owners, 0, owners.Length);
            doors.Add(new MapInteriorRoomDoor { map = mapName, x = x, y = y,
                tileIndex = tile.TileIndex, owners = owners });
        }
    }

    private static void AddWaterClip(List<MapWaterTileClip> clips, string map, string layer,
        int x, int y, TileData tile, Dictionary<string, TileSheetData> sheets)
    {
        if (!sheets.TryGetValue(tile.TileSheetId, out var sheet)) return;
        bool waterfall = sheet.ImageSource.EndsWith("_Waterfalls", StringComparison.OrdinalIgnoreCase);
        bool fountain = map.StartsWith("Town", StringComparison.OrdinalIgnoreCase) &&
            x >= 25 && x <= 27 && y >= 24 && y <= 26 && tile.FrameInterval > 0 &&
            sheet.ImageSource.EndsWith("_town", StringComparison.OrdinalIgnoreCase);
        if (!waterfall && !fountain) return;
        if (tile.Frames != null && tile.Frames.Exists(frame => frame.TileSheetId != tile.TileSheetId)) return;

        clips.Add(new MapWaterTileClip { map = map, layer = layer, x = x, y = y,
            waterfall = waterfall, interval = tile.FrameInterval,
            frames = tile.Frames?.ConvertAll(frame => frame.TileIndex).ToArray() ?? new[] { tile.TileIndex } });
    }

    private static void WriteResourceJson(string path, object data)
    {
        string json = JsonUtility.ToJson(data);
        if (File.Exists(path) && File.ReadAllText(path) == json) return;
        File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
    }

    private static string GetAction(TileData tile, Dictionary<string, TileSheetData> sheets)
    {
        if (tile.Properties.TryGetValue("Action", out string action) ||
            tile.Properties.TryGetValue("TouchAction", out action)) return action;
        if (!sheets.TryGetValue(tile.TileSheetId, out TileSheetData sheet)) return null;
        string prefix = $"@TileIndex@{tile.TileIndex}@";
        if (sheet.Properties.TryGetValue(prefix + "Action", out action) ||
            sheet.Properties.TryGetValue(prefix + "TouchAction", out action)) return action;
        return null;
    }

    private static Texture2D LoadMineTexture(string name) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Content/Maps/Mines/{name}.png");

    private static bool TryMakeDoor(string action, int x, int y, bool requiresApproach,
        out LocationWarp door)
    {
        door = null;
        string[] parts = action.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (parts[0] == "WarpCommunityCenter")
        {
            door = new LocationWarp(x, y, "CommunityCenter", 32, 23);
        }
        else if ((parts[0] == "LockedDoorWarp" || parts[0] == "Warp" ||
                  parts[0] == "WarpMensLocker" || parts[0] == "WarpWomensLocker") &&
                 parts.Length >= 4 && int.TryParse(parts[1], out int targetX) &&
                 int.TryParse(parts[2], out int targetY))
        {
            door = new LocationWarp(x, y, parts[3], targetX, targetY);
            if (parts[0] == "LockedDoorWarp" && parts.Length >= 6 &&
                int.TryParse(parts[4], out int opensAt) && int.TryParse(parts[5], out int closesAt))
            {
                door.hasOpeningHours = true;
                door.opensAt = opensAt;
                door.closesAt = closesAt;
            }
        }

        if (door == null) return false;
        door.isDoor = requiresApproach;
        door.fromMapAction = true;
        return true;
    }
}
