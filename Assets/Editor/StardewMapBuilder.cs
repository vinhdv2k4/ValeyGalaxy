using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StardewValley.EditorTools
{
    public class StardewMapBuilder : EditorWindow
    {
        private string selectedMapPath = "Assets/Content/Maps/Farm.tbin";
        private string season = "spring";
        private int pixelsPerUnit = 16;
        private bool addCollidersToBuildings = true;
        private bool hidePathsLayer = false;
        private Vector2 scrollPos;
        private string searchFilter = "";
        private List<string> availableMaps = new List<string>();

        [MenuItem("Stardew Valley/Map Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<StardewMapBuilder>("SV Map Builder");
            window.minSize = new Vector2(420, 540);
            window.RefreshMapList();
            window.Show();
        }

        [MenuItem("Stardew Valley/Build All Maps To Prefabs")]
        public static void MenuBuildAllMaps()
        {
            BuildAllMapsToPrefabs();
        }

        private void OnEnable()
        {
            RefreshMapList();
        }

        private void RefreshMapList()
        {
            availableMaps.Clear();
            string mapsDir = Path.Combine(Application.dataPath, "Content/Maps");
            if (Directory.Exists(mapsDir))
            {
                foreach (var file in Directory.GetFiles(mapsDir, "*.tbin", SearchOption.AllDirectories))
                {
                    string rel = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                    availableMaps.Add(rel);
                }
            }
            availableMaps.Sort();
        }

        private void OnGUI()
        {
            GUILayout.Label("🌾 Stardew Valley Map Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Tự động đọc dữ liệu .tbin và hình ảnh tileset .png để tạo cấu trúc Grid & Tilemap hoàn chỉnh trong Unity.", MessageType.Info);

            EditorGUILayout.Space(6);

            // Season Selection
            string[] seasons = new[] { "spring", "summer", "fall", "winter" };
            int currentSeasonIndex = Array.IndexOf(seasons, season.ToLower());
            if (currentSeasonIndex < 0) currentSeasonIndex = 0;
            int newSeasonIndex = EditorGUILayout.Popup("Mùa (Season)", currentSeasonIndex, new[] { "Spring (Mùa xuân)", "Summer (Mùa hạ)", "Fall (Mùa thu)", "Winter (Mùa đông)" });
            season = seasons[newSeasonIndex];

            // Settings
            pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit (PPU)", pixelsPerUnit);
            addCollidersToBuildings = EditorGUILayout.Toggle("Add Collider cho Buildings", addCollidersToBuildings);
            hidePathsLayer = EditorGUILayout.Toggle("Ẩn layer Paths (Spawn/Debug Markers)", hidePathsLayer);

            EditorGUILayout.Space(8);
            GUILayout.Label("Chọn bản đồ cần xây dựng:", EditorStyles.boldLabel);

            // Search filter
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("Tìm kiếm map", searchFilter);
            if (GUILayout.Button("Làm mới", GUILayout.Width(70)))
            {
                RefreshMapList();
            }
            EditorGUILayout.EndHorizontal();

            // Map list
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            foreach (var mapPath in availableMaps)
            {
                string mapName = Path.GetFileNameWithoutExtension(mapPath);
                if (!string.IsNullOrEmpty(searchFilter) && !mapName.ToLower().Contains(searchFilter.ToLower()))
                    continue;

                bool isSelected = selectedMapPath == mapPath;
                if (GUILayout.Toggle(isSelected, mapName, EditorStyles.radioButton))
                {
                    selectedMapPath = mapPath;
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Map được chọn:", Path.GetFileNameWithoutExtension(selectedMapPath), EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("🚀 XÂY DỰNG MAP VÀO SCENE HIỆN TẠI", GUILayout.Height(36)))
            {
                BuildMapToScene(selectedMapPath, season, pixelsPerUnit, addCollidersToBuildings, hidePathsLayer);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);
            if (GUILayout.Button("💾 XUẤT THÀNH PREFAB", GUILayout.Height(28)))
            {
                BuildMapToPrefab(selectedMapPath, season, pixelsPerUnit, addCollidersToBuildings, hidePathsLayer, separatePathsObjects: true);
            }

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUILayout.Button("📦 XUẤT TẤT CẢ MAP THÀNH PREFAB (BATCH)", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận", $"Bạn có chắc muốn xuất toàn bộ {availableMaps.Count} bản đồ thành Prefab?", "Đồng ý", "Hủy"))
                {
                    BuildAllMapsToPrefabs(season, pixelsPerUnit, addCollidersToBuildings, separatePathsObjects: true);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        public static GameObject BuildMapToScene(string tbinPath, string season, int ppu, bool addColliders, bool hidePaths = false, bool separatePathsObjects = true, bool silent = false, string tileAssetPath = null)
        {
            if (!File.Exists(tbinPath))
            {
                if (!silent) EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file map: {tbinPath}", "OK");
                return null;
            }

            if (!silent) EditorUtility.DisplayProgressBar("Đang xây dựng Map", "Đang đọc dữ liệu .tbin...", 0.1f);
            GameObject gridGo = null;
            try
            {
                TbinData mapData;
                using (var fs = File.OpenRead(tbinPath))
                {
                    mapData = TbinParser.Read(fs);
                }

                string mapName = Path.GetFileNameWithoutExtension(tbinPath);

                // Root Grid GameObject
                gridGo = new GameObject($"Map_{mapName}");
                Grid grid = gridGo.AddComponent<Grid>();
                grid.cellSize = new Vector3(1f, 1f, 0f);

                // Load all tilesheet textures and sprites
                if (!silent) EditorUtility.DisplayProgressBar("Đang xây dựng Map", "Đang xử lý Tilesheets...", 0.3f);
                var sheetSprites = LoadTileSheetSprites(mapData.TileSheets, season, ppu);

                // Tiles created only in memory disappear when a prefab is reloaded.
                // Keep one persistent tile library beside each exported prefab.
                var savedTiles = new Dictionary<string, Tile>();
                bool hasTileLibrary = !string.IsNullOrEmpty(tileAssetPath) && AssetDatabase.LoadMainAssetAtPath(tileAssetPath) != null;
                if (hasTileLibrary)
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(tileAssetPath))
                    {
                        if (asset is Tile savedTile) savedTiles[savedTile.name] = savedTile;
                    }
                }

                // Cache Tile ScriptableObjects per sheet and tile index to prevent massive duplication
                var sheetTiles = new Dictionary<string, Tile[]>();
                foreach (var kvp in sheetSprites)
                {
                    sheetTiles[kvp.Key] = new Tile[kvp.Value.Length];
                }
                var tileSheetsById = new Dictionary<string, TileSheetData>(StringComparer.OrdinalIgnoreCase);
                foreach (var sheet in mapData.TileSheets) tileSheetsById[sheet.Id] = sheet;

                bool HasTileProperty(TileData tileInfo, string property)
                {
                    if (tileInfo.Properties.ContainsKey(property)) return true;
                    return tileSheetsById.TryGetValue(tileInfo.TileSheetId, out var sheet) &&
                           sheet.Properties.ContainsKey($"@TileIndex@{tileInfo.TileIndex}@{property}");
                }

                Tile GetOrCreateTile(string sheetId, int tileIdx)
                {
                    if (!sheetSprites.TryGetValue(sheetId, out var spArr)) return null;
                    if (tileIdx < 0 || tileIdx >= spArr.Length || spArr[tileIdx] == null) return null;

                    if (!sheetTiles.TryGetValue(sheetId, out var tArr)) return null;
                    if (tArr[tileIdx] == null)
                    {
                        if (!string.IsNullOrEmpty(tileAssetPath) && !AssetDatabase.Contains(spArr[tileIdx]))
                            throw new InvalidOperationException($"Sprite {sheetId}[{tileIdx}] chưa được lưu trong AssetDatabase.");

                        string tileName = $"{sheetId.Length}_{sheetId}_{tileIdx}";
                        Tile t;
                        if (!string.IsNullOrEmpty(tileAssetPath) && savedTiles.TryGetValue(tileName, out var existingTile))
                        {
                            t = existingTile;
                            if (t.sprite != spArr[tileIdx])
                            {
                                t.sprite = spArr[tileIdx];
                                EditorUtility.SetDirty(t);
                            }
                        }
                        else
                        {
                            t = ScriptableObject.CreateInstance<Tile>();
                            t.name = tileName;
                            t.sprite = spArr[tileIdx];
                            if (!string.IsNullOrEmpty(tileAssetPath))
                            {
                                if (!hasTileLibrary)
                                {
                                    AssetDatabase.CreateAsset(t, tileAssetPath);
                                    hasTileLibrary = true;
                                }
                                else AssetDatabase.AddObjectToAsset(t, tileAssetPath);
                                savedTiles[tileName] = t;
                            }
                        }
                        tArr[tileIdx] = t;
                    }
                    return tArr[tileIdx];
                }

                // Keep collision independent of visual layers: a Buildings tile may carry
                // Passable/Shadow and must remain walkable.
                Tilemap collisionMap = null;
                Tile collisionTile = null;
                if (addColliders)
                {
                    collisionTile = GetCollisionTile();
                    var collisionGo = new GameObject("MapCollision");
                    collisionGo.transform.SetParent(gridGo.transform, false);
                    collisionMap = collisionGo.AddComponent<Tilemap>();
                    var collisionBody = collisionGo.AddComponent<Rigidbody2D>();
                    collisionBody.bodyType = RigidbodyType2D.Static;
                    var collisionShape = collisionGo.AddComponent<TilemapCollider2D>();
                    var collisionComposite = collisionGo.AddComponent<CompositeCollider2D>();
                    collisionComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;
#if UNITY_2023_1_OR_NEWER
                    collisionShape.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
                    collisionShape.usedByComposite = true;
#endif
                }

                // Build each Layer
                int layerIndex = 0;
                foreach (var layer in mapData.Layers)
                {
                    bool isPathsLayer = layer.Id.Equals("Paths", StringComparison.OrdinalIgnoreCase);
                    if (hidePaths && isPathsLayer)
                    {
                        continue;
                    }

                    if (!silent)
                    {
                        float progress = 0.4f + (0.5f * layerIndex / Mathf.Max(1, mapData.Layers.Count));
                        EditorUtility.DisplayProgressBar("Đang xây dựng Map", $"Đang tạo layer '{layer.Id}'...", progress);
                    }

                    if (isPathsLayer && separatePathsObjects)
                    {
                        // Method 2: Tách layer Paths thành 2 sub-layers:
                        // Paths only encode spawn markers. Runtime objects supply
                        // their own colliders; the marker tilemaps must not block movement.
                        GameObject obstaclesGo = new GameObject("Paths_Obstacles");
                        obstaclesGo.transform.SetParent(gridGo.transform, false);
                        Tilemap obsTilemap = obstaclesGo.AddComponent<Tilemap>();
                        TilemapRenderer obsRenderer = obstaclesGo.AddComponent<TilemapRenderer>();
                        if (tileAssetPath != null) ConfigureMapMaterial(obsRenderer);
                        obsRenderer.sortingOrder = WorldSortOrder.LayerPaths;
                        // Marker layers are data only; the game never draws them.
                        obsRenderer.enabled = false;

                        GameObject weedsGo = new GameObject("Paths_Weeds");
                        weedsGo.transform.SetParent(gridGo.transform, false);
                        Tilemap weedsTilemap = weedsGo.AddComponent<Tilemap>();
                        TilemapRenderer weedsRenderer = weedsGo.AddComponent<TilemapRenderer>();
                        if (tileAssetPath != null) ConfigureMapMaterial(weedsRenderer);
                        weedsRenderer.sortingOrder = WorldSortOrder.LayerPaths;
                        weedsRenderer.enabled = false;

                        for (int y = 0; y < layer.Height; y++)
                        {
                            for (int x = 0; x < layer.Width; x++)
                            {
                                var tileInfo = layer.Tiles[x, y];
                                if (tileInfo == null) continue;

                                Tile tile = GetOrCreateTile(tileInfo.TileSheetId, tileInfo.TileIndex);
                                if (tile != null)
                                {
                                    Vector3Int tilePos = new Vector3Int(x, -y, 0);

                                    // Tile index logic:
                                    // 16, 17: Stones (Đá) -> Block
                                    // 18: Twigs (Cành củi) -> Block
                                    // 9, 10, 11, 12, 31, 32: Trees (Cây) -> Block
                                    // 19-21: two-by-two log, boulder, stump markers.
                                    // 22: Grass, not a resource clump.
                                    int idx = tileInfo.TileIndex;
                                    bool isObstacle = (idx == 16 || idx == 17 || idx == 18 || 
                                                       idx == 9 || idx == 10 || idx == 11 || idx == 12 || 
                                                       idx == 31 || idx == 32 || idx == 19 || idx == 20 || idx == 21);

                                    if (isObstacle)
                                    {
                                        obsTilemap.SetTile(tilePos, tile);
                                    }
                                    else
                                    {
                                        weedsTilemap.SetTile(tilePos, tile);
                                    }
                                }
                            }
                        }

                        layerIndex++;
                        continue;
                    }

                    GameObject layerGo = new GameObject(layer.Id);
                    layerGo.transform.SetParent(gridGo.transform, false);

                    Tilemap tilemap = layerGo.AddComponent<Tilemap>();
                    TilemapRenderer renderer = layerGo.AddComponent<TilemapRenderer>();
                    if (tileAssetPath != null) ConfigureMapMaterial(renderer);

                    // Configure Sorting Order
                    ConfigureLayerRenderer(layer.Id, renderer);

                    // Populate Tiles
                    for (int y = 0; y < layer.Height; y++)
                    {
                        for (int x = 0; x < layer.Width; x++)
                        {
                            var tileInfo = layer.Tiles[x, y];
                            if (tileInfo == null) continue;

                            Tile tile = GetOrCreateTile(tileInfo.TileSheetId, tileInfo.TileIndex);
                            if (tile != null)
                            {
                                // Map source: (0,0) is top-left, Unity Tilemap: y increases upwards
                                Vector3Int tilePos = new Vector3Int(x, -y, 0);
                                tilemap.SetTile(tilePos, tile);
                                if (collisionMap != null)
                                {
                                    bool building = layer.Id.Equals("Buildings", StringComparison.OrdinalIgnoreCase);
                                    bool back = layer.Id.Equals("Back", StringComparison.OrdinalIgnoreCase);
                                    if ((building && !HasTileProperty(tileInfo, "Passable") &&
                                         !HasTileProperty(tileInfo, "Shadow")) ||
                                        (back && HasTileProperty(tileInfo, "Passable")))
                                        collisionMap.SetTile(tilePos, collisionTile);
                                }
                            }
                        }
                    }

                    layerIndex++;
                }

                if (collisionMap != null)
                {
                    ClearWarpArrivalCorridors(mapName, collisionMap);
                    collisionMap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                }

                // Setup GameLocation & parse Warps
                GameLocation loc = gridGo.AddComponent<GameLocation>();
                loc.LocationName = mapName;
                loc.IsOutdoors = mapData.Properties.TryGetValue("Outdoors", out string outVal) &&
                                 (outVal.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                                  outVal.Equals("True", StringComparison.OrdinalIgnoreCase));

                if (mapData.Properties.TryGetValue("Warp", out string warpProp) && !string.IsNullOrWhiteSpace(warpProp))
                {
                    loc.LoadWarpsFromPropertyString(warpProp);
                }

                if (mapName.Equals("FarmHouse", StringComparison.OrdinalIgnoreCase) ||
                    mapName.Equals("FarmCave", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (LocationWarp warp in loc.Warps) warp.isDoor = true;
                }

                // Special case for Farm maps: add FarmHouseEntry door warp
                if (mapName.StartsWith("Farm", StringComparison.OrdinalIgnoreCase) && !mapName.Contains("House") && !mapName.Contains("Cave"))
                {
                    int entryX = 64;
                    int entryY = 15;
                    if (mapData.Properties.TryGetValue("FarmHouseEntry", out string fhEntry))
                    {
                        var parts = fhEntry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int ex) && int.TryParse(parts[1], out int ey))
                        {
                            entryX = ex;
                            entryY = ey;
                        }
                    }
                    loc.AddWarp(new LocationWarp(entryX, entryY, "FarmHouse", 3, 11) { isDoor = true });
                }

                // Build physical / visual WarpTrigger colliders
                loc.EnsureWarpTriggers();

                if (!silent)
                {
                    Undo.RegisterCreatedObjectUndo(gridGo, $"Build Map {mapName}");
                    Selection.activeGameObject = gridGo;
                    EditorUtility.DisplayDialog("Thành công!", $"Đã xây dựng thành công bản đồ '{mapName}' ({mapData.Layers.Count} layers) vào Scene!", "OK");
                }
                return gridGo;
            }
            catch (Exception ex)
            {
                if (gridGo != null) DestroyImmediate(gridGo);
                Debug.LogError($"[MapBuilder] Lỗi khi dựng map: {ex}");
                if (!silent) EditorUtility.DisplayDialog("Lỗi", $"Lỗi khi dựng map: {ex.Message}", "OK");
                return null;
            }
            finally
            {
                if (!silent) EditorUtility.ClearProgressBar();
            }
        }

        public static string BuildMapToPrefab(string tbinPath, string season, int ppu, bool addColliders, bool hidePaths = false, bool separatePathsObjects = true, bool silent = false)
        {
            string mapName = Path.GetFileNameWithoutExtension(tbinPath);
            string category = GetMapCategory(mapName);
            string outDir = $"Assets/Prefabs/Maps/{category}".Replace('\\', '/');
            EnsureAssetFolder(outDir);
            string tileAssetPath = $"{outDir}/{mapName}_Tiles.asset";
            var go = BuildMapToScene(tbinPath, season, ppu, addColliders, hidePaths, separatePathsObjects, silent, tileAssetPath);
            if (go != null)
            {
                try
                {
                    string prefabPath = $"{outDir}/{mapName}.prefab";
                    AssetDatabase.SaveAssets();
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out bool saved);
                    if (!saved) throw new InvalidOperationException($"Không thể lưu prefab: {prefabPath}");
                    if (!silent) Debug.Log($"Đã lưu Prefab tại: {prefabPath}");
                    return prefabPath;
                }
                finally
                {
                    DestroyImmediate(go);
                }
            }
            return null;
        }

        // Repairs maps embedded directly in older scenes without replacing their
        // GameLocation, warp, resource, or exterior components.
        public static int RebuildCollisionOnExistingMap(GameObject mapRoot, string tbinPath)
        {
            TbinData map;
            using (var stream = File.OpenRead(tbinPath)) map = TbinParser.Read(stream);
            var sheets = new Dictionary<string, TileSheetData>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in map.TileSheets) sheets[sheet.Id] = sheet;

            bool HasProperty(TileData tile, string key)
            {
                return tile.Properties.ContainsKey(key) ||
                       sheets.TryGetValue(tile.TileSheetId, out var sheet) &&
                       sheet.Properties.ContainsKey($"@TileIndex@{tile.TileIndex}@{key}");
            }

            var oldCollision = mapRoot.transform.Find("MapCollision");
            if (oldCollision != null) DestroyImmediate(oldCollision.gameObject);
            foreach (var old in mapRoot.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                var go = old.gameObject;
                DestroyImmediate(old);
                var composite = go.GetComponent<CompositeCollider2D>();
                if (composite != null) DestroyImmediate(composite);
                var body = go.GetComponent<Rigidbody2D>();
                if (body != null) DestroyImmediate(body);
            }

            var collisionGo = new GameObject("MapCollision");
            collisionGo.transform.SetParent(mapRoot.transform, false);
            var collisionMap = collisionGo.AddComponent<Tilemap>();
            var collisionTile = GetCollisionTile();
            var rb = collisionGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var collider = collisionGo.AddComponent<TilemapCollider2D>();
            var compositeCollider = collisionGo.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
#if UNITY_2023_1_OR_NEWER
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
            collider.usedByComposite = true;
#endif

            int solidCount = 0;
            foreach (var layer in map.Layers)
            {
                bool buildings = layer.Id.Equals("Buildings", StringComparison.OrdinalIgnoreCase);
                bool back = layer.Id.Equals("Back", StringComparison.OrdinalIgnoreCase);
                if (!buildings && !back) continue;
                var visual = mapRoot.transform.Find(layer.Id);
                if (visual == null || !visual.TryGetComponent(out Tilemap source)) continue;
                for (int y = 0; y < layer.Height; y++)
                for (int x = 0; x < layer.Width; x++)
                {
                    var tile = layer.Tiles[x, y];
                    if (tile == null) continue;
                    if (!((buildings && !HasProperty(tile, "Passable") && !HasProperty(tile, "Shadow")) ||
                          (back && HasProperty(tile, "Passable")))) continue;
                    var cell = new Vector3Int(x, -y, 0);
                    if (!source.HasTile(cell)) continue;
                    collisionMap.SetTile(cell, collisionTile);
                    solidCount++;
                }
            }
            ClearWarpArrivalCorridors(Path.GetFileNameWithoutExtension(tbinPath), collisionMap);
            collider.ProcessTilemapChanges();
            return solidCount;
        }

        private static Tile GetCollisionTile()
        {
            const string path = "Assets/Prefabs/Maps/MapCollisionTile.asset";
            EnsureAssetFolder("Assets/Prefabs/Maps");
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "MapCollisionTile";
                tile.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(tile, path);
            }
            else if (tile.colliderType != Tile.ColliderType.Grid)
            {
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
            }
            return tile;
        }

        // A few vanilla warp destinations land inside the static Buildings art at
        // the edge of a map. Keep their original coordinates while opening the
        // narrow route from that arrival tile to the walkable map interior.
        private static void ClearWarpArrivalCorridors(string mapName, Tilemap collisionMap)
        {
            void ClearVertical(int x, int firstY, int lastY)
            {
                for (int y = firstY; y <= lastY; y++)
                    collisionMap.SetTile(new Vector3Int(x, -y, 0), null);
            }
            if (mapName.Equals("Mountain", StringComparison.OrdinalIgnoreCase))
            {
                ClearVertical(57, 38, 40);
                ClearVertical(85, 40, 40);
            }
            else if (mapName.Equals("Beach", StringComparison.OrdinalIgnoreCase))
                ClearVertical(67, 0, 5);
            else if (mapName.Equals("Town", StringComparison.OrdinalIgnoreCase))
            {
                ClearVertical(90, 0, 2);
                ClearVertical(98, 0, 4);
            }
            else if (mapName.Equals("Mine", StringComparison.OrdinalIgnoreCase))
                ClearVertical(8, 9, 9);
        }

        public static void BuildAllMapsToPrefabs(string season = "spring", int ppu = 16, bool addColliders = true, bool separatePathsObjects = true)
        {
            string mapsDir = Path.Combine(Application.dataPath, "Content/Maps");
            if (!Directory.Exists(mapsDir))
            {
                Debug.LogError("[MapBuilder] Không tìm thấy thư mục Assets/Content/Maps!");
                return;
            }

            var files = Directory.GetFiles(mapsDir, "*.tbin", SearchOption.AllDirectories);
            Debug.Log($"[MapBuilder] Bắt đầu xuất hàng loạt {files.Length} bản đồ thành Prefab...");
            int success = 0;
            int failed = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string tbinFile = files[i].Replace('\\', '/');
                string mapName = Path.GetFileNameWithoutExtension(tbinFile);
                try
                {
                    string res = BuildMapToPrefab(tbinFile, season, ppu, addColliders, hidePaths: false, separatePathsObjects: separatePathsObjects, silent: true);
                    if (!string.IsNullOrEmpty(res)) success++;
                    else failed++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MapBuilder] Lỗi khi xuất map '{mapName}': {ex.Message}");
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MapBuilder] HOÀN TẤT! Đã xuất thành công: {success}/{files.Length} prefabs. Thất bại: {failed}.");
        }

        public static string GetMapCategory(string mapName)
        {
            if (int.TryParse(mapName, out _) || mapName.StartsWith("Volcano", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("Mine", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("SkullCave", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("SkullCaveAltar", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("BugLand", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("Tunnel", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("Darkroom", StringComparison.OrdinalIgnoreCase))
            {
                return "Mines";
            }

            if (mapName.StartsWith("Island", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("Caldera", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("QiNutRoom", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("LeoTreeHouse", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("BoatTunnel", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("CaptainRoom", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("MermaidHouse", StringComparison.OrdinalIgnoreCase) ||
                mapName.Equals("Submarine", StringComparison.OrdinalIgnoreCase))
            {
                return "Island";
            }

            string lower = mapName.ToLowerInvariant();
            if (lower.Contains("festival") || lower.Contains("fair") || lower.Contains("luau") ||
                lower.Contains("jellies") || lower.Contains("nightmarket") || lower.Contains("halloween") ||
                lower.Contains("christmas") || lower.Contains("eggfestival") || lower.Contains("squidfest") ||
                lower.Contains("fishingderby"))
            {
                return "Festivals";
            }

            if (lower.Contains("dreamscape") || lower.Contains("balloon") || lower.Contains("samshow") ||
                lower.Contains("sebastian") || lower.Contains("nightscene") || lower.Contains("elliottsea") ||
                lower.Contains("bus(people)") || lower.Contains("stadium") || lower.Contains("fishinggame") ||
                lower.Contains("targetgame"))
            {
                return "Events";
            }

            if (lower.Contains("farmhouse") || lower.Contains("farmcave") || lower.Contains("greenhouse") ||
                lower.Contains("cellar") || lower.Contains("cabin") || lower.Contains("barn") ||
                lower.Contains("coop") || lower.Contains("shed") || lower.Contains("slimehutch") ||
                lower.Contains("spouse"))
            {
                return "Farm/Interiors";
            }

            if (mapName.StartsWith("Farm", StringComparison.OrdinalIgnoreCase))
            {
                return "Farm";
            }

            string[] wilderness = new[]
            {
                "AdventureGuild", "AnimalShop", "Club", "ElliottHouse", "FishShop", "LeahHouse",
                "MaruBasement", "MasteryCave", "SandyHouse", "ScienceHouse", "SebastianRoom",
                "Tent", "WitchHut", "WitchSwamp", "WitchWarpCave", "WizardHouse", "WizardHouseBasement",
                "Forest_RaccoonHouse", "Forest_RaccoonStump"
            };
            foreach (var w in wilderness)
            {
                if (mapName.Equals(w, StringComparison.OrdinalIgnoreCase)) return "Interiors/Wilderness";
            }

            string[] townInteriors = new[]
            {
                "AbandonedJojaMart", "ArchaeologyHouse", "Blacksmith", "CommunityCenter",
                "CommunityCenter_Ruins", "CommunityCenter_Joja", "CommunityCenter_Refurbished",
                "HaleyHouse", "HarveyRoom", "Hospital", "JojaMart", "JoshHouse", "LewisBasement",
                "ManorHouse", "MovieTheater", "MovieTheaterScreen", "Saloon", "RefurbishedSaloonRoom",
                "SamHouse", "SeedShop", "Sunroom", "Trailer", "Trailer_Big", "Trailer_big"
            };
            foreach (var t in townInteriors)
            {
                if (mapName.Equals(t, StringComparison.OrdinalIgnoreCase) || mapName.StartsWith("BathHouse", StringComparison.OrdinalIgnoreCase))
                    return "Interiors/Town";
            }

            return "Overworld";
        }

        private static void ConfigureLayerRenderer(string layerId, TilemapRenderer renderer)
        {
            switch (layerId.ToLower())
            {
                case "back":
                case "back2":
                case "back3":
                    renderer.sortingOrder = WorldSortOrder.LayerBack;
                    break;
                case "paths":
                    renderer.sortingOrder = WorldSortOrder.LayerPaths;
                    renderer.enabled = false;
                    break;
                case "buildings":
                case "buildings2":
                case "buildings3":
                    renderer.sortingOrder = WorldSortOrder.LayerBuildings;
                    break;
                case "front":
                case "front2":
                    renderer.sortingOrder = WorldSortOrder.LayerFront;
                    break;
                case "alwaysfront":
                case "alwaysfront2":
                case "alwaysfront3":
                    renderer.sortingOrder = WorldSortOrder.LayerAlwaysFront;
                    break;
                default:
                    renderer.sortingOrder = 0;
                    break;
            }
        }

        private static void ConfigureMapMaterial(TilemapRenderer renderer)
        {
            // Sprite-Lit-Default renders magenta in prefab previews without a 2D light pass.
            // These pixel-art maps do not need lighting, so use URP's persistent unlit material.
            const string materialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null) renderer.sharedMaterial = material;
            else Debug.LogError($"[MapBuilder] Không tìm thấy material: {materialPath}");
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i])))
                    throw new IOException($"Không thể tạo thư mục asset: {next}");
                current = next;
            }
        }

        private static Dictionary<string, Sprite[]> LoadTileSheetSprites(List<TileSheetData> sheets, string season, int ppu)
        {
            var dict = new Dictionary<string, Sprite[]>();

            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                string imgSource = sheet.ImageSource;

                if (imgSource.Contains("spring_") || imgSource.Contains("summer_") || imgSource.Contains("fall_") || imgSource.Contains("winter_"))
                {
                    imgSource = imgSource.Replace("spring_", season + "_")
                                         .Replace("summer_", season + "_")
                                         .Replace("fall_", season + "_")
                                         .Replace("winter_", season + "_");
                }

                string pngPath = FindPngAssetPath(imgSource);
                if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
                {
                    Debug.LogWarning($"[MapBuilder] Không tìm thấy tilesheet: {imgSource} (File: {pngPath})");
                    continue;
                }

                int tileW = sheet.TileWidth > 0 ? sheet.TileWidth : 16;
                int tileH = sheet.TileHeight > 0 ? sheet.TileHeight : 16;

                EnsureTextureSliced(pngPath, tileW, tileH, ppu);

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (tex == null) continue;

                int cols = tex.width / tileW;
                int rows = tex.height / tileH;
                int totalTiles = cols * rows;

                // Load all sub-sprites from AssetDatabase with persistent GUID/fileID
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
                var spriteMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                foreach (var obj in subAssets)
                {
                    if (obj is Sprite sp)
                    {
                        spriteMap[sp.name] = sp;
                    }
                }

                Sprite[] sprites = new Sprite[totalTiles];
                string baseName = Path.GetFileNameWithoutExtension(pngPath);

                for (int idx = 0; idx < totalTiles; idx++)
                {
                    string expectedName = $"{baseName}_{idx}";
                    if (spriteMap.TryGetValue(expectedName, out Sprite sp))
                    {
                        sprites[idx] = sp;
                    }
                    else
                    {
                        float pixelY = tex.height - (idx / cols + 1) * tileH;
                        Rect rect = new Rect((idx % cols) * tileW, pixelY, tileW, tileH);
                        sprites[idx] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu);
                    }
                }

                dict[sheet.Id] = sprites;
            }

            return dict;
        }

        private static string FindPngAssetPath(string imageSource)
        {
            string clean = imageSource.Replace('\\', '/');
            if (clean.StartsWith("../")) clean = clean.Substring(3);
            if (clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 4);
            if (clean.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 4);

            string filename = Path.GetFileName(clean);

            string[] searchPaths = new[]
            {
                $"Assets/Content/Maps/{filename}.png",
                $"Assets/Content/TileSheets/{filename}.png",
                $"Assets/Content/TerrainFeatures/{filename}.png",
                $"Assets/Content/Buildings/{filename}.png",
                $"Assets/Content/LooseSprites/{filename}.png",
                $"Assets/Content/{clean}.png"
            };

            foreach (var p in searchPaths)
            {
                if (File.Exists(p)) return p;
            }

            string[] guids = AssetDatabase.FindAssets($"{filename} t:Texture2D");
            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            return null;
        }

        private static void EnsureTextureSliced(string assetPath, int tileW, int tileH, int ppu)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool needBasicReimport = false;
            if (!importer.isReadable) { importer.isReadable = true; needBasicReimport = true; }
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; needBasicReimport = true; }
            if (importer.spriteImportMode != SpriteImportMode.Multiple) { importer.spriteImportMode = SpriteImportMode.Multiple; needBasicReimport = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; needBasicReimport = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; needBasicReimport = true; }
            if (importer.spritePixelsPerUnit != ppu) { importer.spritePixelsPerUnit = ppu; needBasicReimport = true; }

            if (needBasicReimport)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return;

            int cols = tex.width / tileW;
            int rows = tex.height / tileH;
            int totalTiles = cols * rows;

            if (importer.spritesheet == null || importer.spritesheet.Length != totalTiles)
            {
                var metaList = new List<SpriteMetaData>();
                string baseName = Path.GetFileNameWithoutExtension(assetPath);
                for (int ty = 0; ty < rows; ty++)
                {
                    for (int tx = 0; tx < cols; tx++)
                    {
                        int index = ty * cols + tx;
                        float pixelY = tex.height - (ty + 1) * tileH;
                        var smd = new SpriteMetaData();
                        smd.name = $"{baseName}_{index}";
                        smd.rect = new Rect(tx * tileW, pixelY, tileW, tileH);
                        smd.alignment = (int)SpriteAlignment.Center;
                        smd.pivot = new Vector2(0.5f, 0.5f);
                        metaList.Add(smd);
                    }
                }
                importer.spritesheet = metaList.ToArray();
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }

    #region Tbin Parser Classes
    public class TbinData
    {
        public string Id = "";
        public string Description = "";
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        public List<TileSheetData> TileSheets = new List<TileSheetData>();
        public List<LayerData> Layers = new List<LayerData>();
    }

    public class TileSheetData
    {
        public string Id = "";
        public string Description = "";
        public string ImageSource = "";
        public int SheetWidth, SheetHeight;
        public int TileWidth, TileHeight;
        public int MarginX, MarginY;
        public int SpacingX, SpacingY;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }

    public class LayerData
    {
        public string Id = "";
        public bool Visible;
        public string Description = "";
        public int Width, Height;
        public int TileWidth, TileHeight;
        public TileData[,] Tiles;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }

    public class TileData
    {
        public string TileSheetId = "";
        public int TileIndex;
        public byte BlendMode;
        public int FrameInterval;
        public List<TileData> Frames;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }

    public static class TbinParser
    {
        public static TbinData Read(Stream stream)
        {
            byte[] magic = LoadSequence(stream, 6);
            string magicStr = Encoding.ASCII.GetString(magic);
            if (magicStr != "tBIN10")
                throw new Exception($"Invalid tBIN magic: {magicStr}");

            var map = new TbinData();
            map.Id = LoadString(stream);
            map.Description = LoadString(stream);
            map.Properties = LoadProperties(stream);

            // TileSheets
            int sheetCount = LoadInt32(stream);
            for (int i = 0; i < sheetCount; i++)
            {
                var sheet = new TileSheetData
                {
                    Id = LoadString(stream),
                    Description = LoadString(stream),
                    ImageSource = LoadString(stream),
                    SheetWidth = LoadInt32(stream),
                    SheetHeight = LoadInt32(stream),
                    TileWidth = LoadInt32(stream),
                    TileHeight = LoadInt32(stream),
                    MarginX = LoadInt32(stream),
                    MarginY = LoadInt32(stream),
                    SpacingX = LoadInt32(stream),
                    SpacingY = LoadInt32(stream),
                    Properties = LoadProperties(stream)
                };
                map.TileSheets.Add(sheet);
            }

            // Layers
            int layerCount = LoadInt32(stream);
            for (int i = 0; i < layerCount; i++)
            {
                var layer = new LayerData
                {
                    Id = LoadString(stream),
                    Visible = LoadBool(stream),
                    Description = LoadString(stream),
                    Width = LoadInt32(stream),
                    Height = LoadInt32(stream),
                    TileWidth = LoadInt32(stream),
                    TileHeight = LoadInt32(stream),
                    Properties = LoadProperties(stream)
                };

                layer.Tiles = new TileData[layer.Width, layer.Height];

                int curY = 0;
                string currentTileSheetId = map.TileSheets.Count > 0 ? map.TileSheets[0].Id : "";

                while (curY < layer.Height)
                {
                    int curX = 0;
                    while (curX < layer.Width)
                    {
                        int b = stream.ReadByte();
                        if (b == -1) throw new EndOfStreamException("Unexpected end of stream in layer tiles");
                        char op = (char)b;

                        if (op == 'S')
                        {
                            var tile = new TileData
                            {
                                TileSheetId = currentTileSheetId,
                                TileIndex = LoadInt32(stream),
                                BlendMode = (byte)stream.ReadByte(),
                                Properties = LoadProperties(stream)
                            };
                            layer.Tiles[curX, curY] = tile;
                            curX++;
                        }
                        else if (op == 'T')
                        {
                            currentTileSheetId = LoadString(stream);
                        }
                        else if (op == 'N')
                        {
                            int nullCount = LoadInt32(stream);
                            curX += nullCount;
                        }
                        else if (op == 'A')
                        {
                            int frameInterval = LoadInt32(stream);
                            int frameCount = LoadInt32(stream);
                            var frames = new List<TileData>(frameCount);
                            string animSheetId = currentTileSheetId;
                            while (frameCount > 0)
                            {
                                int ab = stream.ReadByte();
                                if (ab == 'T')
                                {
                                    animSheetId = LoadString(stream);
                                }
                                else if (ab == 'S')
                                {
                                    var frameTile = new TileData
                                    {
                                        TileSheetId = animSheetId,
                                        TileIndex = LoadInt32(stream),
                                        BlendMode = (byte)stream.ReadByte(),
                                        Properties = LoadProperties(stream)
                                    };
                                    frames.Add(frameTile);
                                    frameCount--;
                                }
                                else throw new InvalidDataException($"Invalid animated tile frame: {ab}");
                            }
                            var animProps = LoadProperties(stream);
                            if (frames.Count > 0)
                            {
                                TileData firstFrame = frames[0];
                                firstFrame.FrameInterval = frameInterval;
                                firstFrame.Frames = frames;
                                foreach (var kv in animProps) firstFrame.Properties[kv.Key] = kv.Value;
                                layer.Tiles[curX, curY] = firstFrame;
                            }
                            curX++;
                        }
                        else
                        {
                            throw new Exception($"Unexpected byte '{op}' (0x{b:X2}) at layer {layer.Id} pos ({curX},{curY})");
                        }
                    }
                    curY++;
                }

                map.Layers.Add(layer);
            }

            return map;
        }

        private static byte[] LoadSequence(Stream stream, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int r = stream.Read(buf, read, count - read);
                if (r <= 0) throw new EndOfStreamException();
                read += r;
            }
            return buf;
        }

        private static int LoadInt32(Stream stream)
        {
            byte[] buf = LoadSequence(stream, 4);
            return BitConverter.ToInt32(buf, 0);
        }

        private static float LoadFloat(Stream stream)
        {
            byte[] buf = LoadSequence(stream, 4);
            return BitConverter.ToSingle(buf, 0);
        }

        private static bool LoadBool(Stream stream)
        {
            int b = stream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return b != 0;
        }

        private static string LoadString(Stream stream)
        {
            int len = LoadInt32(stream);
            if (len <= 0) return "";
            byte[] bytes = LoadSequence(stream, len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static Dictionary<string, string> LoadProperties(Stream stream)
        {
            int count = LoadInt32(stream);
            var dict = new Dictionary<string, string>();
            while (count-- > 0)
            {
                string key = LoadString(stream);
                int type = stream.ReadByte();
                if (type == -1) throw new EndOfStreamException();
                string val = "";
                switch (type)
                {
                    case 0: val = LoadBool(stream).ToString(); break;
                    case 1: val = LoadInt32(stream).ToString(); break;
                    case 2: val = LoadFloat(stream).ToString(); break;
                    case 3: val = LoadString(stream); break;
                }
                dict[key] = val;
            }
            return dict;
        }
    }
    #endregion
}
