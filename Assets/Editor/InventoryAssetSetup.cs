using System.IO;
using UnityEditor;
using UnityEngine;

public static class InventoryAssetSetup
{
    [MenuItem("Tools/Stardew/Extract Inventory Sprites")]
    public static void ExtractInventorySprites()
    {
        string uiDir = "Assets/UI/Inventory";
        if (!Directory.Exists(uiDir))
        {
            Directory.CreateDirectory(uiDir);
        }

        // 1. Copy the composite 800x96 Toolbar_Frame
        string exactBoxPath = $"{uiDir}/stardew_exact_box_800x96.png";
        string targetFramePath = $"{uiDir}/Toolbar_Frame.png";
        if (File.Exists(exactBoxPath))
        {
            File.Copy(exactBoxPath, targetFramePath, true);
            ConfigureSprite(targetFramePath, Vector4.zero);
        }

        // 2. Extract Slot_Normal and Slot_Selected from composite
        string compPath = $"{uiDir}/stardew_toolbar_composite.png";
        if (File.Exists(compPath))
        {
            Texture2D compTex = LoadTextureReadable(compPath);
            if (compTex != null)
            {
                // Slot_Selected: x = 16, topY = 16 (in compTex: height is 96, unityY = 96 - (16 + 64) = 16)
                ExtractAndSaveSprite(compTex, 16, 16, 64, 64, $"{uiDir}/Slot_Selected.png", Vector4.zero);

                // Slot_Normal: x = 80, topY = 16
                ExtractAndSaveSprite(compTex, 80, 16, 64, 64, $"{uiDir}/Slot_Normal.png", Vector4.zero);

                Object.DestroyImmediate(compTex);
            }
        }

        // 3. Extract Tools from tools.png
        string toolsPath = "Assets/Content/TileSheets/tools.png";
        if (File.Exists(toolsPath))
        {
            Texture2D toolsTex = LoadTextureReadable(toolsPath);
            if (toolsTex != null)
            {
                // Axe: topX = 80, topY = 144
                ExtractAndSaveSprite(toolsTex, 80, 144, 16, 16, $"{uiDir}/Icon_Axe.png", Vector4.zero);

                // Hoe: topX = 80, topY = 32
                ExtractAndSaveSprite(toolsTex, 80, 32, 16, 16, $"{uiDir}/Icon_Hoe.png", Vector4.zero);

                // Pickaxe: topX = 80, topY = 96
                ExtractAndSaveSprite(toolsTex, 80, 96, 16, 16, $"{uiDir}/Icon_Pickaxe.png", Vector4.zero);

                // Watering Can: topX = 32, topY = 224
                ExtractAndSaveSprite(toolsTex, 32, 224, 16, 16, $"{uiDir}/Icon_WateringCan.png", Vector4.zero);

                Object.DestroyImmediate(toolsTex);
            }
        }

        // 4. Extract Scythe from weapons.png
        string wepPath = "Assets/Content/TileSheets/weapons.png";
        if (File.Exists(wepPath))
        {
            Texture2D wepTex = LoadTextureReadable(wepPath);
            if (wepTex != null)
            {
                // Scythe: topX = 112, topY = 80
                ExtractAndSaveSprite(wepTex, 112, 80, 16, 16, $"{uiDir}/Icon_Scythe.png", Vector4.zero);

                Object.DestroyImmediate(wepTex);
            }
        }

        // 5. Extract items from springobjects.png
        string springObjectsPath = "Assets/Content/Maps/springobjects.png";
        if (File.Exists(springObjectsPath))
        {
            Texture2D springTex = LoadTextureReadable(springObjectsPath);
            if (springTex != null)
            {
                // Seeds: topX = 256, topY = 304
                ExtractAndSaveSprite(springTex, 256, 304, 16, 16, $"{uiDir}/Icon_Seeds.png", Vector4.zero);

                // Wood: topX = 64, topY = 256
                ExtractAndSaveSprite(springTex, 64, 256, 16, 16, $"{uiDir}/Icon_Wood.png", Vector4.zero);

                // Stone: topX = 96, topY = 256
                ExtractAndSaveSprite(springTex, 96, 256, 16, 16, $"{uiDir}/Icon_Stone.png", Vector4.zero);

                Object.DestroyImmediate(springTex);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("Pixel-perfect inventory sprites extracted and configured!");
    }

    private static Texture2D LoadTextureReadable(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
    }

    private static void ExtractAndSaveSprite(Texture2D source, int topX, int topY, int width, int height, string outputPath, Vector4 border)
    {
        int unityY = source.height - (topY + height);
        Color[] pixels = source.GetPixels(topX, unityY, width, height);

        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels(pixels);
        cropped.Apply();

        byte[] pngBytes = cropped.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngBytes);
        Object.DestroyImmediate(cropped);

        ConfigureSprite(outputPath, border);
    }

    private static void ConfigureSprite(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 16;
            if (border != Vector4.zero)
            {
                importer.spriteBorder = border;
            }
            importer.SaveAndReimport();
        }
    }
}
