using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NpcCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/NpcCatalog.asset";

    [MenuItem("Tools/NPC/Rebuild Catalog")]
    public static void Rebuild()
    {
        NpcCatalog catalog = AssetDatabase.LoadAssetAtPath<NpcCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<NpcCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var characters = new List<NpcDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Content/Characters/schedules" }))
        {
            string schedulePath = AssetDatabase.GUIDToAssetPath(guid);
            TextAsset schedule = AssetDatabase.LoadAssetAtPath<TextAsset>(schedulePath);
            if (schedule == null || schedule.name == "template") continue;

            string spritePath = $"Assets/Content/Characters/{schedule.name}.png";
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(spritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameIndex(sprite.name))
                .ToArray();
            if (frames.Length == 0) continue;

            string portraitPath = $"Assets/Content/Portraits/{schedule.name}.png";
            Sprite[] portraits = AssetDatabase.LoadAllAssetsAtPath(portraitPath)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameIndex(sprite.name))
                .ToArray();
            characters.Add(new NpcDefinition
            {
                name = schedule.name,
                schedule = schedule,
                dialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"Assets/Content/Characters/Dialogue/{schedule.name}.json"),
                portrait = portraits.FirstOrDefault(),
                portraits = portraits,
                animationFrames = frames
            });
        }

        catalog.characters = characters.OrderBy(npc => npc.name).ToList();
        catalog.dialogueFrame = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Maps/MenuTiles.png");
        catalog.dialogueFont = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/font_bold.png");
        catalog.buffsIcons = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/BuffsIcons.png");
        catalog.toolsAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/tools.png");
        catalog.animationsAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/animations.png");
        catalog.animationDescriptions = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Content/Data/animationDescriptions.json");
        catalog.marlon = new NpcDefinition
        {
            name = "Marlon",
            portrait = AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Portraits/Marlon.png").OfType<Sprite>().FirstOrDefault(),
            animationFrames = AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Characters/Marlon.png").OfType<Sprite>().Take(1).ToArray()
        };
        catalog.gilPortrait = AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Portraits/Gil.png")
            .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Gil_0");
        catalog.weaponsAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/weapons.png");
        catalog.adventureGuildStock = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/AdventureGuildStock.json");
        catalog.furnitureAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/furniture.png");
        catalog.craftablesAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/TileSheets/Craftables.png");
        catalog.furnitureDefinitions = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/Furniture.json");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NPC] Catalog rebuilt with {catalog.characters.Count} characters.");
    }

    private static int FrameIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out int index)
            ? index : int.MaxValue;
    }
}
