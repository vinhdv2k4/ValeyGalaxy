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

        public static GameObject BuildMapToScene(string tbinPath, string season, int ppu, bool addColliders, bool hidePaths = false, bool separatePathsObjects = true, bool silent = false)
        {
            if (!File.Exists(tbinPath))
            {
                if (!silent) EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file map: {tbinPath}", "OK");
                return null;
            }

            if (!silent) EditorUtility.DisplayProgressBar("Đang xây dựng Map", "Đang đọc dữ liệu .tbin...", 0.1f);
            try
            {
                TbinData mapData;
                using (var fs = File.OpenRead(tbinPath))
                {
                    mapData = TbinParser.Read(fs);
                }

                string mapName = Path.GetFileNameWithoutExtension(tbinPath);

                // Root Grid GameObject
                GameObject gridGo = new GameObject($"Map_{mapName}");
                Grid grid = gridGo.AddComponent<Grid>();
                grid.cellSize = new Vector3(1f, 1f, 0f);

                // Load all tilesheet textures and sprites
                if (!silent) EditorUtility.DisplayProgressBar("Đang xây dựng Map", "Đang xử lý Tilesheets...", 0.3f);
                var sheetSprites = LoadTileSheetSprites(mapData.TileSheets, season, ppu);

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
                        obsRenderer.sortingOrder = -4;

                        GameObject weedsGo = new GameObject("Paths_Weeds");
                        weedsGo.transform.SetParent(gridGo.transform, false);
                        Tilemap weedsTilemap = weedsGo.AddComponent<Tilemap>();
                        TilemapRenderer weedsRenderer = weedsGo.AddComponent<TilemapRenderer>();
                        weedsRenderer.sortingOrder = -5;

                        for (int y = 0; y < layer.Height; y++)
                        {
                            for (int x = 0; x < layer.Width; x++)
                            {
                                var tileInfo = layer.Tiles[x, y];
                                if (tileInfo == null) continue;

                                if (sheetSprites.TryGetValue(tileInfo.TileSheetId, out var sprites) &&
                                    tileInfo.TileIndex >= 0 && tileInfo.TileIndex < sprites.Length &&
                                    sprites[tileInfo.TileIndex] != null)
                                {
                                    Tile tile = ScriptableObject.CreateInstance<Tile>();
                                    tile.sprite = sprites[tileInfo.TileIndex];
                                    Vector3Int tilePos = new Vector3Int(x, -y, 0);

                                    // Stardew Valley tile index logic:
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

                    // Configure Sorting Order
                    ConfigureLayerRenderer(layer.Id, renderer);

                    // Add Collider if Buildings layer
                    if (addColliders && layer.Id.StartsWith("Buildings", StringComparison.OrdinalIgnoreCase))
                    {
                        var collider = layerGo.AddComponent<TilemapCollider2D>();
                        var rb = layerGo.AddComponent<Rigidbody2D>();
                        rb.bodyType = RigidbodyType2D.Static;
                        var composite = layerGo.AddComponent<CompositeCollider2D>();
                        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
#if UNITY_2023_1_OR_NEWER
                        collider.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
                        collider.usedByComposite = true;
#endif
                    }

                    // Populate Tiles
                    for (int y = 0; y < layer.Height; y++)
                    {
                        for (int x = 0; x < layer.Width; x++)
                        {
                            var tileInfo = layer.Tiles[x, y];
                            if (tileInfo == null) continue;

                            if (sheetSprites.TryGetValue(tileInfo.TileSheetId, out var sprites) &&
                                tileInfo.TileIndex >= 0 && tileInfo.TileIndex < sprites.Length &&
                                sprites[tileInfo.TileIndex] != null)
                            {
                                Tile tile = ScriptableObject.CreateInstance<Tile>();
                                tile.sprite = sprites[tileInfo.TileIndex];

                                // Stardew Valley: (0,0) is top-left, Unity Tilemap: y increases upwards
                                Vector3Int tilePos = new Vector3Int(x, -y, 0);
                                tilemap.SetTile(tilePos, tile);
                            }
                        }
                    }

                    layerIndex++;
                }

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
            var go = BuildMapToScene(tbinPath, season, ppu, addColliders, hidePaths, separatePathsObjects, silent);
            if (go != null)
            {
                try
                {
                    string mapName = Path.GetFileNameWithoutExtension(tbinPath);
                    // Giữ nguyên subfolder nếu có (ví dụ: Mines/1.tbin -> Assets/Prefabs/Maps/Mines/1.prefab)
                    string mapsRoot = Path.Combine(Application.dataPath, "Content/Maps").Replace('\\', '/');
                    string normalizedTbin = tbinPath.Replace('\\', '/');
                    string subDir = "";
                    if (normalizedTbin.StartsWith(mapsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = normalizedTbin.Substring(mapsRoot.Length).TrimStart('/');
                        subDir = Path.GetDirectoryName(rel);
                    }
                    else if (normalizedTbin.Contains("Content/Maps/"))
                    {
                        int idx = normalizedTbin.IndexOf("Content/Maps/", StringComparison.OrdinalIgnoreCase);
                        string rel = normalizedTbin.Substring(idx + "Content/Maps/".Length);
                        subDir = Path.GetDirectoryName(rel);
                    }

                    string outDir = "Assets/Prefabs/Maps";
                    if (!string.IsNullOrEmpty(subDir)) outDir = $"{outDir}/{subDir}".Replace('\\', '/');
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                    string prefabPath = $"{outDir}/{mapName}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
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

        private static void ConfigureLayerRenderer(string layerId, TilemapRenderer renderer)
        {
            switch (layerId.ToLower())
            {
                case "back":
                case "back2":
                case "back3":
                    renderer.sortingOrder = -10;
                    break;
                case "paths":
                    renderer.sortingOrder = -5;
                    break;
                case "buildings":
                case "buildings2":
                    renderer.sortingOrder = 0;
                    break;
                case "front":
                case "front2":
                    renderer.sortingOrder = 10;
                    break;
                case "alwaysfront":
                case "alwaysfront2":
                case "alwaysfront3":
                    renderer.sortingOrder = 20;
                    break;
                default:
                    renderer.sortingOrder = 0;
                    break;
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

                EnsureTextureReadable(pngPath, ppu);

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (tex == null) continue;

                int tileW = sheet.TileWidth > 0 ? sheet.TileWidth : 16;
                int tileH = sheet.TileHeight > 0 ? sheet.TileHeight : 16;
                int cols = tex.width / tileW;
                int rows = tex.height / tileH;
                int totalTiles = cols * rows;

                Sprite[] sprites = new Sprite[totalTiles];

                for (int ty = 0; ty < rows; ty++)
                {
                    for (int tx = 0; tx < cols; tx++)
                    {
                        int index = ty * cols + tx;
                        float pixelY = tex.height - (ty + 1) * tileH;
                        Rect rect = new Rect(tx * tileW, pixelY, tileW, tileH);
                        sprites[index] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu);
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

        private static void EnsureTextureReadable(string assetPath, int ppu)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool needReimport = false;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needReimport = true;
                }
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    needReimport = true;
                }
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    needReimport = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    needReimport = true;
                }
                if (importer.spritePixelsPerUnit != ppu)
                {
                    importer.spritePixelsPerUnit = ppu;
                    needReimport = true;
                }

                if (needReimport)
                {
                    importer.SaveAndReimport();
                }
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
                            long frameInterval = LoadInt32(stream);
                            int frameCount = LoadInt32(stream);
                            TileData firstFrame = null;
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
                                    if (firstFrame == null) firstFrame = frameTile;
                                    frameCount--;
                                }
                            }
                            var animProps = LoadProperties(stream);
                            if (firstFrame != null)
                            {
                                foreach (var kv in animProps) firstFrame.Properties[kv.Key] = kv.Value;
                            }
                            layer.Tiles[curX, curY] = firstFrame;
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
