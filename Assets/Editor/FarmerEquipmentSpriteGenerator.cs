using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Cuts equipped sprites and inventory icons from their distinct source atlases.</summary>
public static class FarmerEquipmentSpriteGenerator
{
    private const string Output = "Assets/Animations/Farmer/Appearance";
    private const string PrefabPath = "Assets/Prefabs/Character/Farrmer/Player.prefab";

    [MenuItem("Tools/Generate Farmer Appearance And Inventory Icons")]
    public static void Generate()
    {
        try
        {
            // Uses 16x32 hair and 8x8 shirt direction tiles.
            using (var hair = new Source("Assets/Content/Characters/Farmer/hairstyles.png"))
            {
                Write("Hair_Down", hair, 0, 0, 16, 32);
                Write("Hair_Right", hair, 0, 32, 16, 32);
                Write("Hair_Up", hair, 0, 64, 16, 32);
            }
            using (var shirts = new Source("Assets/Content/Characters/Farmer/shirts.png"))
            {
                Write("Shirt_Down", shirts, 0, 0, 8, 8);
                Write("Shirt_Right", shirts, 0, 8, 8, 8);
                Write("Shirt_Up", shirts, 0, 24, 8, 8);
                Write("Shirt_Left", shirts, 0, 16, 8, 8);
            }
            using (var tools = new Source("Assets/Content/TileSheets/tools.png"))
            {
                // Hoe:
                Write("Hoe_Held", tools, 21 % 21 * 16, 21 / 21 * 16, 16, 32, true);
                for (int index = 21; index <= 25; index++)
                    Write("Hoe_Index_" + index, tools, index % 21 * 16, index / 21 * 16, 16, 32, true);
                Write("Hoe_Inventory", tools, 47 % 21 * 16, 47 / 21 * 16, 16, 16);

                // Pickaxe (105..109):
                Write("Pickaxe_Held", tools, 105 % 21 * 16, 105 / 21 * 16, 16, 32, true);
                for (int index = 105; index <= 109; index++)
                    Write("Pickaxe_Index_" + index, tools, index % 21 * 16, index / 21 * 16, 16, 32, true);

                // Axe (189..193):
                Write("Axe_Held", tools, 189 % 21 * 16, 189 / 21 * 16, 16, 32, true);
                for (int index = 189; index <= 193; index++)
                    Write("Axe_Index_" + index, tools, index % 21 * 16, index / 21 * 16, 16, 32, true);

                // Watering Can (273..277):
                Write("WateringCan_Held", tools, 273 % 21 * 16, 273 / 21 * 16, 16, 32, true);
                for (int index = 273; index <= 277; index++)
                    Write("WateringCan_Index_" + index, tools, index % 21 * 16, index / 21 * 16, 16, 32, true);

                // Fishing strip at y=288/336 (48x48).
                for (int frame = 0; frame < 5; frame++)
                {
                    Write("FishingRod_Cast_Side_" + frame, tools, frame * 48, 288, 48, 48,
                        false, 16f, new Vector2(0f, 1f));
                    Write("FishingRod_Cast_Vertical_" + frame, tools, frame * 48, 336, 48, 48,
                        false, 16f, new Vector2(0f, 1f));
                }
            }
            using (var weapons = new Source("Assets/Content/TileSheets/weapons.png"))
            {
                Write("Scythe_Held", weapons, 112, 80, 16, 16, false, 16f, new Vector2(1f / 16f, 1f / 16f));
                Write("Sword_Held", weapons, 0, 0, 16, 16, false, 16f, new Vector2(1f / 16f, 1f / 16f));
            }
            using (var animations = new Source("Assets/Content/TileSheets/animations.png"))
            {
                // Farmer.showToolSwipeEffect: rows 18 (Down), 15 (sides), 19 (Up).
                for (int frame = 0; frame < 4; frame++)
                {
                    Write("ToolSwipe_Down_" + frame, animations, frame * 64, 18 * 64, 64, 64, false, 64f);
                    Write("ToolSwipe_Side_" + frame, animations, frame * 128, 15 * 64, 128, 128, false, 64f);
                    Write("ToolSwipe_Up_" + frame, animations, frame * 128, 19 * 64, 128, 128, false, 64f);
                }
            }
            using (var objects = new Source("Assets/Content/Maps/springobjects.png"))
            {
                // Object indexes: Parsnip Seeds=472, Wood=388.
                WriteObject("Seeds_Inventory", objects, 472);
                WriteObject("Wood_Inventory", objects, 388);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AttachFishingRodFrames();
            AttachToPlayerPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[FarmerEquipmentSpriteGenerator] Regenerated hair, shirt, Hoe source indices 21-25, tool swipe frames and inventory icons.");
        }
        catch (Exception error)
        {
            Debug.LogError("[FarmerEquipmentSpriteGenerator] " + error);
        }
    }

    private static void WriteObject(string name, Source source, int index)
    {
        int columns = source.texture.width / 16;
        Write(name, source, index % columns * 16, index / columns * 16, 16, 16);
    }

    private static void Write(string name, Source source, int x, int top, int width, int height, bool heldTool = false, float pixelsPerUnit = 16f, Vector2? customPivot = null)
    {
        if (x < 0 || top < 0 || x + width > source.texture.width || top + height > source.texture.height)
            throw new InvalidOperationException(name + " falls outside " + source.path);
        var pixels = source.texture.GetPixels32();
        var resultPixels = new Color32[width * height];
        int bottom = source.texture.height - top - height;
        for (int row = 0; row < height; row++)
            Array.Copy(pixels, (bottom + row) * source.texture.width + x, resultPixels, row * width, width);

        var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            output.SetPixels32(resultPixels);
            output.Apply(false, false);
            string path = Output + "/" + name + ".png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), output.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (heldTool || customPivot.HasValue) ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
            textureSettings.spritePivot = customPivot ?? (heldTool ? new Vector2(0.5f, 0.25f) : new Vector2(0.5f, 0.5f));
            importer.SetTextureSettings(textureSettings);
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        finally { UnityEngine.Object.DestroyImmediate(output); }
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Output + "/" + name + ".png");

    private static void AttachFishingRodFrames()
    {
        var rod = AssetDatabase.LoadAssetAtPath<FishingRodData>("Assets/Data/Items/Item_FishingRod.asset");
        if (rod == null) return;
        var serialized = new SerializedObject(rod);
        SetSpriteArray(serialized.FindProperty("castSideFrames"), "FishingRod_Cast_Side_", 5);
        SetSpriteArray(serialized.FindProperty("castVerticalFrames"), "FishingRod_Cast_Vertical_", 5);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rod);
    }

    private static void AttachToPlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var appearance = new SerializedObject(root.GetComponent<PlayerAppearance>());
            var hair = appearance.FindProperty("hairSprites");
            hair.arraySize = 4;
            hair.GetArrayElementAtIndex(0).objectReferenceValue = Sprite("Hair_Down");
            hair.GetArrayElementAtIndex(1).objectReferenceValue = Sprite("Hair_Right");
            hair.GetArrayElementAtIndex(2).objectReferenceValue = Sprite("Hair_Up");
            hair.GetArrayElementAtIndex(3).objectReferenceValue = Sprite("Hair_Right");
            var shirt = appearance.FindProperty("shirtSprites");
            shirt.arraySize = 4;
            shirt.GetArrayElementAtIndex(0).objectReferenceValue = Sprite("Shirt_Down");
            shirt.GetArrayElementAtIndex(1).objectReferenceValue = Sprite("Shirt_Right");
            shirt.GetArrayElementAtIndex(2).objectReferenceValue = Sprite("Shirt_Up");
            shirt.GetArrayElementAtIndex(3).objectReferenceValue = Sprite("Shirt_Left");
            appearance.FindProperty("body").objectReferenceValue = root.transform.Find("Body").GetComponent<SpriteRenderer>();
            appearance.FindProperty("pants").objectReferenceValue = root.transform.Find("Pants").GetComponent<SpriteRenderer>();
            appearance.FindProperty("shirtSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Characters/Farmer/shirts.png");
            appearance.FindProperty("pantsSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Characters/Farmer/pants.png");
            var swipeTransform = root.transform.Find("ToolSwipe");
            if (swipeTransform == null)
            {
                var swipeObject = new GameObject("ToolSwipe");
                swipeObject.transform.SetParent(root.transform, false);
                swipeTransform = swipeObject.transform;
            }
            var swipeRenderer = swipeTransform.GetComponent<SpriteRenderer>();
            if (swipeRenderer == null) swipeRenderer = swipeTransform.gameObject.AddComponent<SpriteRenderer>();
            swipeRenderer.enabled = false;
            appearance.FindProperty("toolSwipe").objectReferenceValue = swipeRenderer;
            SetSpriteArray(appearance.FindProperty("swipeDown"), "ToolSwipe_Down_");
            SetSpriteArray(appearance.FindProperty("swipeSide"), "ToolSwipe_Side_");
            SetSpriteArray(appearance.FindProperty("swipeUp"), "ToolSwipe_Up_");
            appearance.ApplyModifiedPropertiesWithoutUndo();

            var inventory = new SerializedObject(root.GetComponent<PlayerInventory>());
            var slots = inventory.FindProperty("slots");

            SetIcons(slots, PlayerItemKind.Hoe, Sprite("Hoe_Held"), Sprite("Hoe_Inventory"));
            SetToolFrames(slots, PlayerItemKind.Hoe, "Hoe_Index_", 21, 5);

            SetIcons(slots, PlayerItemKind.Pickaxe, Sprite("Pickaxe_Held"), Sprite("Icon_Pickaxe"));
            SetToolFrames(slots, PlayerItemKind.Pickaxe, "Pickaxe_Index_", 105, 5);

            SetIcons(slots, PlayerItemKind.Axe, Sprite("Axe_Held"), Sprite("Icon_Axe"));
            SetToolFrames(slots, PlayerItemKind.Axe, "Axe_Index_", 189, 5);

            SetIcons(slots, PlayerItemKind.WateringCan, Sprite("WateringCan_Held"), Sprite("Icon_WateringCan"));
            SetToolFrames(slots, PlayerItemKind.WateringCan, "WateringCan_Index_", 273, 5);

            SetIcons(slots, PlayerItemKind.Sword, Sprite("Scythe_Held"), Sprite("Icon_Scythe"));
            SetIcons(slots, PlayerItemKind.Seeds, Sprite("Seeds_Inventory"), Sprite("Seeds_Inventory"));
            SetIcons(slots, PlayerItemKind.Material, Sprite("Wood_Inventory"), Sprite("Wood_Inventory"));
            inventory.ApplyModifiedPropertiesWithoutUndo();

            root.transform.Find("Hair").GetComponent<SpriteRenderer>().sprite = Sprite("Hair_Down");
            root.transform.Find("Shirt").GetComponent<SpriteRenderer>().sprite = Sprite("Shirt_Down");
            var tool = root.transform.Find("Tool").GetComponent<SpriteRenderer>();
            tool.sprite = Sprite("Hoe_Held");
            tool.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SetSpriteArray(SerializedProperty property, string prefix, int count = 4)
    {
        property.arraySize = count;
        for (int i = 0; i < count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(prefix + i);
    }

    private static void SetToolFrames(SerializedProperty slots, PlayerItemKind kind, string prefix, int startIdx, int count)
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            if (slot.FindPropertyRelative("kind").enumValueIndex != (int)kind) continue;
            var frames = slot.FindPropertyRelative("heldAnimationSprites");
            frames.arraySize = count;
            for (int index = 0; index < count; index++)
                frames.GetArrayElementAtIndex(index).objectReferenceValue = Sprite(prefix + (startIdx + index));
        }
    }

    private static void SetIcons(SerializedProperty slots, PlayerItemKind kind, Sprite held, Sprite inventory, Sprite heldDown = null, Sprite heldRight = null, Sprite heldUp = null)
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            if (slot.FindPropertyRelative("kind").enumValueIndex != (int)kind) continue;
            slot.FindPropertyRelative("heldSprite").objectReferenceValue = held;
            var downProp = slot.FindPropertyRelative("heldSpriteDown");
            if (downProp != null) downProp.objectReferenceValue = heldDown != null ? heldDown : held;
            var rightProp = slot.FindPropertyRelative("heldSpriteRight");
            if (rightProp != null) rightProp.objectReferenceValue = heldRight != null ? heldRight : held;
            var upProp = slot.FindPropertyRelative("heldSpriteUp");
            if (upProp != null) upProp.objectReferenceValue = heldUp != null ? heldUp : held;
            slot.FindPropertyRelative("inventoryIcon").objectReferenceValue = inventory;
        }
    }

    private sealed class Source : IDisposable
    {
        public readonly string path;
        public readonly Texture2D texture;

        public Source(string path)
        {
            this.path = path;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(fullPath), false))
                throw new InvalidOperationException("Cannot decode " + path);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(texture);
    }
}
