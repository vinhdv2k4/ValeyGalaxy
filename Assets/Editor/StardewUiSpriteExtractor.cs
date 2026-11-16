using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Slices UI elements from source textures
/// into Assets/Resources/Startup for the Character Customization screen.
/// </summary>
[InitializeOnLoad]
public static class StardewUiSpriteExtractor
{
    private const string StartupFolder = "Assets/Resources/Startup";

    static StardewUiSpriteExtractor()
    {
        EditorApplication.delayCall += ExtractIfMissing;
    }

    private static void ExtractIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ExtractIfMissing;
            return;
        }
        if (!File.Exists(StartupFolder + "/MenuBox.png") ||
            !File.Exists(StartupFolder + "/daybg_0.png") ||
            !File.Exists(StartupFolder + "/ArrowLeft.png") ||
            !File.Exists(StartupFolder + "/ButtonOK.png") ||
            !File.Exists(StartupFolder + "/Farm0_Standard.png"))
        {
            ExtractAll();
        }
    }

    [MenuItem("Tools/Startup UI/Extract Authentic Stardew UI Sprites")]
    public static void ExtractAll()
    {
        Directory.CreateDirectory(StartupFolder);

        // 1. MenuBox 9-slice (Rect 0, 256, 60, 60)
        ExtractSprite("Assets/Content/Maps/MenuTiles.png", "MenuBox", 0, 256, 60, 60, new Vector4(20, 20, 20, 20));

        // 2. Use the complete 128x192 daybg_0 portrait sprite.
        CopyOrExtract("Assets/Content/LooseSprites/daybg.png", "daybg_0", 0, 0, 128, 192);

        // 3. TextBox 9-slice (192x48)
        ExtractSprite("Assets/Content/LooseSprites/textBox.png", "TextBox", 0, 0, 192, 48, new Vector4(16, 16, 16, 16));

        // 4. Cursors.png elements
        string cursors = "Assets/Content/LooseSprites/Cursors.png";
        
        // Direction arrows: Tile 44 (Left) and Tile 33 (Right)
        // In Cursors (704 wide):
        // Tile 44: x = (44*64)%704 = 0, y = (44*64)/704*64 = 256
        // Tile 33: x = (33*64)%704 = 0, y = (33*64)/704*64 = 192
        ExtractSprite(cursors, "ArrowLeft", 0, 256, 64, 64);
        ExtractSprite(cursors, "ArrowRight", 0, 192, 64, 64);

        // OK Button: Tile 46
        // Tile 46: x = (46*64)%704 = 128, y = (46*64)/704*64 = 256
        ExtractSprite(cursors, "ButtonOK", 128, 256, 64, 64);

        // Checkmark indicator: Tile 34
        // Tile 34: x = (34*64)%704 = 64, y = (34*64)/704*64 = 192
        ExtractSprite(cursors, "Checkmark", 64, 192, 64, 64);

        // Randomize dice button: rect (381, 361, 10, 10)
        ExtractSprite(cursors, "ButtonRandom", 381, 361, 10, 10);

        // Gender icons: Male (128, 192, 16, 16), Female (144, 192, 16, 16)
        ExtractSprite(cursors, "GenderMale", 128, 192, 16, 16);
        ExtractSprite(cursors, "GenderFemale", 144, 192, 16, 16);

        // Skip intro checkbox: Unchecked (227, 425, 9, 9), Checked (236, 425, 9, 9)
        ExtractSprite(cursors, "CheckboxUnchecked", 227, 425, 9, 9);
        ExtractSprite(cursors, "CheckboxChecked", 236, 425, 9, 9);

        // Slider thumb: (64, 256, 32, 32)
        ExtractSprite(cursors, "SliderThumb", 64, 256, 32, 32);

        // Farm Selection Border: (375, 357, 3, 3) 9-slice border (1, 1, 1, 1)
        ExtractSprite(cursors, "FarmBorder", 375, 357, 3, 3, new Vector4(1, 1, 1, 1));

        // 7 Farm Type Icons:
        ExtractSprite(cursors, "Farm0_Standard", 0, 324, 22, 20);
        ExtractSprite(cursors, "Farm1_Riverland", 22, 324, 22, 20);
        ExtractSprite(cursors, "Farm2_Forest", 44, 324, 22, 20);
        ExtractSprite(cursors, "Farm3_Hills", 66, 324, 22, 20);
        ExtractSprite(cursors, "Farm4_Wilderness", 88, 324, 22, 20);
        ExtractSprite(cursors, "Farm5_FourCorners", 0, 345, 22, 20);
        ExtractSprite(cursors, "Farm6_Beach", 22, 345, 22, 20);

        // Pet portraits:
        // Cat breeds: 160 + 0..2 * 16, 208, 16, 16
        // Dog breeds: 160 + 48 + 0..2 * 16, 208, 16, 16
        for (int i = 0; i < 3; i++)
        {
            ExtractSprite(cursors, "PetCat" + i, 160 + i * 16, 208, 16, 16);
            ExtractSprite(cursors, "PetDog" + i, 208 + i * 16, 208, 16, 16);
        }

        // Generate Slider Track Texture (Hue rainbow and clean track)
        GenerateSliderTrackTextures();

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[StardewUiSpriteExtractor] Successfully extracted all authentic Stardew UI sprites into " + StartupFolder);
    }

    private static void CopyOrExtract(string sourcePath, string targetName, int x, int top, int width, int height)
    {
        ExtractSprite(sourcePath, targetName, x, top, width, height);
    }

    private static void ExtractSprite(string sourcePath, string targetName, int x, int top, int width, int height, Vector4? border = null)
    {
        var sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (sourceTex == null)
        {
            Debug.LogError("[StardewUiSpriteExtractor] Could not load texture at: " + sourcePath);
            return;
        }

        // Ensure source texture is readable
        string assetPath = AssetDatabase.GetAssetPath(sourceTex);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        int bottom = sourceTex.height - top - height;
        var pixels = sourceTex.GetPixels(x, bottom, width, height);

        var output = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        output.SetPixels(pixels);
        output.Apply(false, false);

        string targetPath = StartupFolder + "/" + targetName + ".png";
        File.WriteAllBytes(targetPath, output.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(output);

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        var outImporter = AssetImporter.GetAtPath(targetPath) as TextureImporter;
        if (outImporter != null)
        {
            outImporter.textureType = TextureImporterType.Sprite;
            outImporter.spriteImportMode = SpriteImportMode.Single;
            outImporter.spritePixelsPerUnit = 16;
            outImporter.filterMode = FilterMode.Point;
            outImporter.textureCompression = TextureImporterCompression.Uncompressed;
            outImporter.mipmapEnabled = false;
            if (border.HasValue)
            {
                outImporter.spriteBorder = border.Value;
            }
            outImporter.SaveAndReimport();
        }
    }

    private static void GenerateSliderTrackTextures()
    {
        // 1. Rainbow Hue track (128x8)
        var hueTex = new Texture2D(128, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int x = 0; x < 128; x++)
        {
            float h = (float)x / 128f;
            Color c = Color.HSVToRGB(h, 0.9f, 0.9f);
            for (int y = 0; y < 8; y++)
            {
                // Draw 1px top & bottom border with shadow
                if (y == 0 || y == 7) hueTex.SetPixel(x, y, new Color(0.15f, 0.1f, 0.05f, 1f));
                else hueTex.SetPixel(x, y, c);
            }
        }
        hueTex.Apply(false, false);
        string huePath = StartupFolder + "/SliderTrackHue.png";
        File.WriteAllBytes(huePath, hueTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(hueTex);
        AssetDatabase.ImportAsset(huePath, ImportAssetOptions.ForceSynchronousImport);
        var hueImp = AssetImporter.GetAtPath(huePath) as TextureImporter;
        if (hueImp != null)
        {
            hueImp.textureType = TextureImporterType.Sprite;
            hueImp.spritePixelsPerUnit = 16;
            hueImp.filterMode = FilterMode.Point;
            hueImp.textureCompression = TextureImporterCompression.Uncompressed;
            hueImp.SaveAndReimport();
        }

        // 2. Generic slider track background (128x8)
        var trackTex = new Texture2D(128, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int x = 0; x < 128; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (y == 0 || y == 7) trackTex.SetPixel(x, y, new Color(0.2f, 0.12f, 0.06f, 1f));
                else trackTex.SetPixel(x, y, new Color(0.45f, 0.28f, 0.14f, 1f));
            }
        }
        trackTex.Apply(false, false);
        string trackPath = StartupFolder + "/SliderTrackBase.png";
        File.WriteAllBytes(trackPath, trackTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(trackTex);
        AssetDatabase.ImportAsset(trackPath, ImportAssetOptions.ForceSynchronousImport);
        var trackImp = AssetImporter.GetAtPath(trackPath) as TextureImporter;
        if (trackImp != null)
        {
            trackImp.textureType = TextureImporterType.Sprite;
            trackImp.spritePixelsPerUnit = 16;
            trackImp.filterMode = FilterMode.Point;
            trackImp.textureCompression = TextureImporterCompression.Uncompressed;
            trackImp.SaveAndReimport();
        }
    }
}
