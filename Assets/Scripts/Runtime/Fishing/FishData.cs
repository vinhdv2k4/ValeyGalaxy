using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Data container for a single fish type.
/// </summary>
public sealed class FishData
{
    public int id;
    public string name;
    public float difficulty;
    public int motionType; // 0=Mixed, 1=Dart, 2=Smooth, 3=Sinker, 4=Floater
    public int minSize;
    public int maxSize;
    public string seasons;
    public string weather;
    public bool isBossFish;
    public Sprite icon;

    public int GetRandomSize()
    {
        return UnityEngine.Random.Range(minSize, maxSize + 1);
    }
}

/// <summary>
/// Central registry and loader for fish data.
/// </summary>
public static class FishDatabase
{
    private static readonly List<FishData> AllFish = new();
    private static readonly Dictionary<int, FishData> FishById = new();
    private static readonly Dictionary<int, ItemData> CachedFishItems = new();
    private static Texture2D springObjectsTexture;
    private static bool isInitialized;

    public static void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        LoadTexture();
        LoadFishJson();
    }

    private static void LoadTexture()
    {
        springObjectsTexture = Resources.Load<Texture2D>("Maps/springobjects");
        if (springObjectsTexture != null) return;
        string texturePath = Path.Combine(Application.dataPath, "Content", "Maps", "springobjects.png");
        if (File.Exists(texturePath))
        {
            byte[] bytes = File.ReadAllBytes(texturePath);
            springObjectsTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            springObjectsTexture.filterMode = FilterMode.Point;
            springObjectsTexture.LoadImage(bytes);
        }
    }

    private static void LoadFishJson()
    {
        TextAsset fishAsset = Resources.Load<TextAsset>("FishData");
        string json = fishAsset == null ? null : fishAsset.text;
        if (string.IsNullOrEmpty(json))
        {
            string jsonPath = Path.Combine(Application.dataPath, "Content", "Data", "Fish.json");
            if (!File.Exists(jsonPath)) return;
            json = File.ReadAllText(jsonPath);
        }
        // Format: { "128": "Pufferfish/80/floater/1/36/...", ... }
        // Simple manual parser for fish dictionary JSON:
        int cur = 0;
        while (cur < json.Length)
        {
            int quoteStart = json.IndexOf('\"', cur);
            if (quoteStart < 0) break;
            int quoteEnd = json.IndexOf('\"', quoteStart + 1);
            if (quoteEnd < 0) break;
            string key = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

            int colon = json.IndexOf(':', quoteEnd);
            if (colon < 0) break;

            int valStart = json.IndexOf('\"', colon);
            if (valStart < 0) break;
            int valEnd = json.IndexOf('\"', valStart + 1);
            if (valEnd < 0) break;
            string val = json.Substring(valStart + 1, valEnd - valStart - 1);

            cur = valEnd + 1;

            if (int.TryParse(key, out int fishId))
            {
                string[] parts = val.Split('/');
                if (parts.Length >= 5)
                {
                    var data = new FishData
                    {
                        id = fishId,
                        name = parts[0],
                        difficulty = float.TryParse(parts[1], out float diff) ? diff : 50f,
                        minSize = int.TryParse(parts[3], out int minS) ? minS : 5,
                        maxSize = int.TryParse(parts[4], out int maxS) ? maxS : 20,
                        seasons = parts.Length > 6 ? parts[6] : "all",
                        weather = parts.Length > 7 ? parts[7] : "both",
                        isBossFish = fishId == 159 || fishId == 160 || fishId == 163 || fishId == 682 || fishId == 775
                    };

                    string motion = parts[2].ToLowerInvariant();
                    data.motionType = motion switch
                    {
                        "dart" => 1,
                        "smooth" => 2,
                        "sinker" => 3,
                        "floater" => 4,
                        _ => 0 // mixed
                    };

                    data.icon = GetFishSprite(fishId);

                    AllFish.Add(data);
                    FishById[fishId] = data;
                }
            }
        }
    }

    public static Sprite GetFishSprite(int id)
    {
        if (springObjectsTexture == null) return null;
        int columns = springObjectsTexture.width / 16;
        int col = id % columns;
        int row = id / columns;
        int pxX = col * 16;
        int pxY = springObjectsTexture.height - (row + 1) * 16;

        return AtlasSprites.Get(springObjectsTexture,
            new Rect(pxX, pxY, 16, 16),
            new Vector2(0.5f, 0.5f), 16f);
    }

    public static FishData GetRandomFish(bool allowBoss = false)
    {
        Initialize();
        if (AllFish.Count == 0) return GetFallbackFish();

        // Filter out boss fish by default
        var candidates = AllFish.FindAll(f => allowBoss || !f.isBossFish);
        if (candidates.Count == 0) return AllFish[0];

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    public static FishData GetFish(int id)
    {
        Initialize();
        return FishById.TryGetValue(id, out var data) ? data : GetFallbackFish();
    }

    private static FishData GetFallbackFish()
    {
        return new FishData
        {
            id = 142,
            name = "Carp",
            difficulty = 25f,
            motionType = 0,
            minSize = 15,
            maxSize = 40,
            isBossFish = false
        };
    }

    public static ItemData CreateFishItem(FishData fish, int size, bool perfect)
    {
        if (CachedFishItems.TryGetValue(fish.id, out var existing))
            return existing;

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = "fish_" + fish.id;
        item.displayName = fish.name;
        item.description = $"Một con {fish.name} vừa được câu lên.";
        item.icon = fish.icon;
        item.maxStackSize = 999;
        item.kind = PlayerItemKind.Material;
        item.edibility = 5;

        CachedFishItems[fish.id] = item;
        return item;
    }
}
