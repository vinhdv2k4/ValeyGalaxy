using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Recreates the FarmerSprite animations from the 16x32 frame
/// indices used by the decompiled Stardew code. The body frames occupy the
/// first 96 pixels of each row; the normal arm frames occupy the next 96.
/// Pants are cut from the first 96 pixels of the pants sheet.
/// </summary>
public sealed class FarmerAnimationGenerator : EditorWindow
{
    private const int FrameWidth = 16;
    private const float FramePixelsPerUnit = 16f;
    private const int FrameHeight = 32;
    private const int Columns = 6;
    private const int ArmColumnOffset = 96;
    private const string DefaultTexturePath = "Assets/Content/Characters/Farmer/farmer_base.png";
    private const string PantsTexturePath = "Assets/Content/Characters/Farmer/pants.png";

    private sealed class FrameStep
    {
        public readonly int index;
        public readonly int milliseconds;
        public readonly int positionOffset;

        public FrameStep(int index, int milliseconds, int positionOffset = 0)
        {
            this.index = index;
            this.milliseconds = milliseconds;
            this.positionOffset = positionOffset;
        }
    }

    private sealed class AnimationSpec
    {
        public readonly string name;
        public readonly bool flipX;
        public readonly bool loop;
        public readonly bool secondaryArm;
        public readonly FrameStep[] steps;

        public AnimationSpec(string name, bool flipX, params FrameStep[] steps)
            : this(name, flipX, true, false, steps) { }

        public AnimationSpec(string name, bool flipX, bool loop, bool secondaryArm, params FrameStep[] steps)
        {
            this.name = name;
            this.flipX = flipX;
            this.loop = loop;
            this.secondaryArm = secondaryArm;
            this.steps = steps;
        }
    }

    // These are FarmerSprite.getAnimationFromIndex() frame indices, durations,
    // and vertical offsets. Run has its own poses and timing.
    private static readonly List<AnimationSpec> Specs = BuildSpecs();
    private static readonly int[] FeatureXOffsets = ReadFeatureOffsets("featureXOffsetPerFrame");
    private static readonly int[] FeatureYOffsets = ReadFeatureOffsets("featureYOffsetPerFrame");

    private static int[] ReadFeatureOffsets(string field)
    {
        // FarmerRenderer is #if false in this project, so read its decompiled
        // offset table as source data instead of depending on that assembly.
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Scripts/StardewValley/FarmerRenderer.cs");
        string source = File.ReadAllText(path);
        var match = Regex.Match(source, field + @"\s*=\s*new int\[126\]\s*\{([^}]*)\}");
        if (!match.Success) throw new InvalidOperationException("Missing FarmerRenderer." + field);
        var values = Regex.Matches(match.Groups[1].Value, @"-?\d+");
        if (values.Count != 126) throw new InvalidOperationException("Unexpected FarmerRenderer." + field + " length");
        var result = new int[values.Count];
        for (int i = 0; i < values.Count; i++) result[i] = int.Parse(values[i].Value);
        return result;
    }

    private static List<AnimationSpec> BuildSpecs()
    {
        var specs = new List<AnimationSpec>
        {
        new AnimationSpec("Walk_Down", false, new FrameStep(1, 200), new FrameStep(0, 200), new FrameStep(2, 200), new FrameStep(0, 200)),
        new AnimationSpec("Walk_Right", false, new FrameStep(7, 200), new FrameStep(6, 200), new FrameStep(8, 200), new FrameStep(6, 200)),
        new AnimationSpec("Walk_Up", false, new FrameStep(13, 200), new FrameStep(12, 200), new FrameStep(14, 200), new FrameStep(12, 200)),
        new AnimationSpec("Walk_Left", true, new FrameStep(7, 200), new FrameStep(6, 200), new FrameStep(8, 200), new FrameStep(6, 200)),
        new AnimationSpec("Run_Down", false, new FrameStep(0, 90), new FrameStep(1, 60, -2), new FrameStep(18, 120, -4), new FrameStep(1, 60, -2), new FrameStep(0, 90), new FrameStep(2, 60, -2), new FrameStep(19, 120, -4), new FrameStep(2, 60, -2)),
        new AnimationSpec("Run_Right", false, new FrameStep(6, 80), new FrameStep(6, 10, -1), new FrameStep(20, 140, -2), new FrameStep(11, 100), new FrameStep(6, 80), new FrameStep(6, 10, -1), new FrameStep(21, 140, -2), new FrameStep(17, 100)),
        new AnimationSpec("Run_Up", false, new FrameStep(12, 90), new FrameStep(13, 60, -2), new FrameStep(22, 120, -3), new FrameStep(13, 60, -2), new FrameStep(12, 90), new FrameStep(14, 60, -2), new FrameStep(23, 120, -3), new FrameStep(14, 60, -2)),
        new AnimationSpec("Run_Left", true, new FrameStep(6, 80), new FrameStep(6, 10, -1), new FrameStep(20, 140, -2), new FrameStep(11, 100), new FrameStep(6, 80), new FrameStep(6, 10, -1), new FrameStep(21, 140, -2), new FrameStep(17, 100))
        };

        // Carrying uses the alternate arm column at x=192; body and pants
        // follow the same frame indices as ordinary locomotion.
        foreach (var locomotion in specs.ToArray())
            specs.Add(new AnimationSpec("Carry" + locomotion.name, locomotion.flipX, true, true, locomotion.steps));

        AddAction(specs, "Grab_Down", false, false, 0);
        AddAction(specs, "Grab_Right", false, false, 6);
        AddAction(specs, "Grab_Up", false, false, 12);
        AddAction(specs, "Grab_Left", true, false, 6);
        AddTimedAction(specs, "ToolChoose_Down", false, false, 500, 3);
        AddTimedAction(specs, "ToolChoose_Right", false, false, 500, 9);
        AddTimedAction(specs, "ToolChoose_Up", false, false, 500, 15);
        AddTimedAction(specs, "ToolChoose_Left", true, false, 500, 9);

        // Stardew FacingDirection 2 is Down, 0 is Up.
        specs.Add(Action("Tool_Down", false, false, F(66, 150), F(67, 40), F(68, 40), F(69, 170), F(70, 75)));
        specs.Add(Action("Tool_Right", false, false, F(48, 100), F(49, 40), F(50, 40), F(51, 220), F(52, 75)));
        specs.Add(Action("Tool_Up", false, false, F(36, 100), F(37, 40), F(38, 40), F(63, 220), F(62, 75)));
        specs.Add(Action("Tool_Left", true, false, F(48, 100), F(49, 40), F(50, 40), F(51, 220), F(52, 75)));

        // Tool.endUsing: Down uses 164 and Up uses 180.
        specs.Add(Action("Watering_Down", false, false, F(54, 0), F(54, 75), F(55, 100), F(25, 500)));
        specs.Add(Action("Watering_Right", false, false, F(58, 0), F(58, 75), F(59, 100), F(45, 500)));
        specs.Add(Action("Watering_Up", false, false, F(62, 0), F(62, 75), F(63, 100), F(46, 500)));
        specs.Add(Action("Watering_Left", true, false, F(58, 0), F(58, 75), F(59, 100), F(45, 500)));

        // seedThrow codes have no dedicated case in this decompiled version.
        // Use the hand-to-ground frames used for crop interaction.
        AddAction(specs, "SeedThrow_Down", false, false, 54, 55, 56, 57);
        AddAction(specs, "SeedThrow_Right", false, false, 58, 59, 60, 61);
        AddAction(specs, "SeedThrow_Up", false, false, 62, 63, 64, 65);
        AddAction(specs, "SeedThrow_Left", true, false, 58, 59, 60, 61);

        specs.Add(Action("Eat", false, false, F(0, 17), F(84, 250), F(85, 400), F(86, 400), F(87, 250), F(88, 250), F(87, 250), F(88, 250), F(87, 250), F(0, 250)));
        specs.Add(Action("Sick", false, false, F(104, 350), F(105, 350), F(104, 350), F(105, 350), F(104, 350), F(105, 350), F(104, 350), F(105, 350)));
        specs.Add(Action("SwordSwipe_Down", false, true, F(24, 55), F(25, 45), F(26, 25), F(27, 25), F(28, 25), F(29, 100)));
        specs.Add(Action("SwordSwipe_Right", false, true, F(30, 55), F(31, 45), F(32, 25), F(33, 25), F(34, 25), F(35, 100)));
        specs.Add(Action("SwordSwipe_Up", false, true, F(36, 55), F(37, 45), F(38, 25), F(39, 25), F(40, 25), F(41, 100)));
        specs.Add(Action("SwordSwipe_Left", true, true, F(30, 55), F(31, 45), F(32, 25), F(33, 25), F(34, 25), F(35, 100)));
        AddAction(specs, "Punch_Down", false, true, 25, 27);
        AddAction(specs, "Punch_Right", false, false, 34, 33);
        AddAction(specs, "Punch_Up", false, true, 40, 38);
        AddAction(specs, "Punch_Left", true, false, 34, 33);
        AddAction(specs, "Harvest_Down", false, false, 54, 55, 56, 57);
        AddAction(specs, "Harvest_Right", false, false, 58, 59, 60, 61);
        AddAction(specs, "Harvest_Up", false, false, 62, 63, 64, 65);
        AddAction(specs, "Harvest_Left", true, false, 58, 59, 60, 61);
        AddTimedAction(specs, "Shear_Down", false, false, 400, 78, 79, 78, 79);
        AddTimedAction(specs, "Shear_Right", false, false, 400, 80, 81, 80, 81);
        AddTimedAction(specs, "Shear_Up", false, false, 400, 82, 83, 82, 83);
        AddTimedAction(specs, "Shear_Left", true, false, 400, 80, 81, 80, 81);
        AddTimedAction(specs, "Milk_Down", false, false, 400, 54, 55, 54, 55);
        AddTimedAction(specs, "Milk_Right", false, false, 400, 58, 59, 58, 59);
        AddTimedAction(specs, "Milk_Up", false, false, 400, 62, 63, 62, 63);
        AddTimedAction(specs, "Milk_Left", true, false, 400, 58, 59, 58, 59);
        AddTimedAction(specs, "Tired", false, false, 1500, 16);
        AddTimedAction(specs, "Tired2", false, false, 500, 16, 0, 16, 0);
        specs.Add(Action("PassOutTired", false, false, F(16, 1000), F(0, 500), F(16, 1000), F(4, 200), F(5, 2000), F(5, 2000)));
        specs.Add(Action("Drink", false, false, F(0, 17), F(90, 250), F(91, 150), F(92, 250), F(93, 200), F(92, 250), F(93, 200), F(92, 250), F(93, 200), F(91, 250), F(90, 50)));
        specs.Add(Action("Fishing_Down", false, false, F(66, 100), F(67, 40), F(68, 40), F(69, 80), F(70, 200)));
        specs.Add(Action("Fishing_Right", false, false, F(48, 100), F(49, 40), F(50, 40), F(51, 80), F(52, 200)));
        specs.Add(Action("Fishing_Up", false, false, F(76, 100), F(38, 40), F(63, 40), F(62, 80), F(63, 200)));
        specs.Add(Action("Fishing_Left", true, false, F(48, 100), F(49, 40), F(50, 40), F(51, 80), F(52, 200)));
        AddTimedAction(specs, "FishingDone_Down", false, false, 5000, 74);
        AddTimedAction(specs, "FishingDone_Right", false, false, 5000, 72);
        AddTimedAction(specs, "FishingDone_Up", false, false, 5000, 76);
        AddTimedAction(specs, "FishingDone_Left", true, false, 5000, 72);
        specs.Add(Action("Pan", true, false, F(123, 150), F(124, 150), F(123, 150), F(125, 150), F(123, 150), F(124, 150), F(123, 150), F(125, 150), F(123, 150), F(124, 150), F(123, 150), F(125, 150), F(123, 150), F(124, 150), F(123, 500)));
        specs.Add(new AnimationSpec("ShowHoldingEdible", false, true, false, F(84, 1000)));
        AddTimedAction(specs, "Cheer", false, false, 800, 97);
        return specs;
    }

    private static FrameStep F(int index, int milliseconds) => new FrameStep(index, milliseconds);

    private static AnimationSpec Action(string name, bool flip, bool secondaryArm, params FrameStep[] steps)
        => new AnimationSpec(name, flip, false, secondaryArm, steps);

    private static void AddAction(List<AnimationSpec> specs, string name, bool flip, bool secondaryArm, params int[] indices)
        => AddTimedAction(specs, name, flip, secondaryArm, 100, indices);

    private static void AddTimedAction(List<AnimationSpec> specs, string name, bool flip, bool secondaryArm, int milliseconds, params int[] indices)
    {
        var steps = new FrameStep[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            steps[i] = F(indices[i], milliseconds);
        specs.Add(Action(name, flip, secondaryArm, steps));
    }

    [SerializeField] private Texture2D sourceTexture;
    [SerializeField] private string outputFolder = "Assets/Animations/Farmer";
    [SerializeField, Min(0)] private int pantsStyleIndex;
    [SerializeField] private bool femalePants;

    [MenuItem("Tools/Generate Farmer Animations")]
    private static void Open()
    {
        GetWindow<FarmerAnimationGenerator>("Farmer Animations").Show();
    }

    [MenuItem("Tools/Regenerate Farmer Tool Up Down Clips")]
    public static void RegenerateVerticalToolClips()
    {
        const string folder = "Assets/Animations/Farmer";
        foreach (var spec in Specs)
            if (spec.name == "Tool_Down" || spec.name == "Tool_Up")
                WriteClip(spec, folder, folder + "/Frames");
        AssetDatabase.SaveAssets();
    }

    public static void RegenerateFromCommandLine()
    {
        var window = CreateInstance<FarmerAnimationGenerator>();
        window.sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultTexturePath);
        window.Generate();
        DestroyImmediate(window);
    }

    private void OnEnable()
    {
        if (sourceTexture == null)
            sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultTexturePath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Farmer animation from source frame indices", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source PNG", sourceTexture, typeof(Texture2D), false);
        outputFolder = EditorGUILayout.TextField("Output folder", outputFolder);
        pantsStyleIndex = EditorGUILayout.IntField("Pants style", pantsStyleIndex);
        femalePants = EditorGUILayout.Toggle("Female pants frames", femalePants);
        EditorGUILayout.HelpBox("Each frame is 16 x 32 pixels. Body uses x=0..95, arms x=96..191, and pants come from the pants sheet at x=0..95. Run uses distinct frame indices, timing, and vertical offsets from FarmerSprite.", MessageType.Info);
        EditorGUILayout.LabelField("Outputs", "FarmerSprite frame PNGs and Animator states");

        using (new EditorGUI.DisabledScope(sourceTexture == null))
        {
            if (GUILayout.Button("Generate All Farmer Animations", GUILayout.Height(32)))
                Generate();
        }
    }

    private void Generate()
    {
        try
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (!sourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a PNG inside Assets.");

            string folder = outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
                throw new InvalidOperationException("Output folder must be inside Assets and cannot contain '..'.");

            string fullSource = Path.Combine(Directory.GetCurrentDirectory(), sourcePath);
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(decoded, File.ReadAllBytes(fullSource), false))
                throw new InvalidOperationException("Could not decode the source PNG.");
            var pants = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!ImageConversion.LoadImage(pants, File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), PantsTexturePath)), false))
                    throw new InvalidOperationException("Could not decode the pants PNG.");
                if (decoded.width < ArmColumnOffset * 2 + Columns * FrameWidth || decoded.height < 21 * FrameHeight)
                    throw new InvalidOperationException("PNG is too small for the Farmer frame layout.");
                int pantsX = pantsStyleIndex % 10 * 192 + (femalePants ? 96 : 0);
                int pantsY = pantsStyleIndex / 10 * 688;
                if (pants.width < pantsX + Columns * FrameWidth || pants.height < pantsY + 21 * FrameHeight)
                    throw new InvalidOperationException("Pants PNG is too small for the Farmer frame layout.");

                var indices = new HashSet<int>();
                foreach (var spec in Specs)
                    foreach (var step in spec.steps)
                        indices.Add(step.index);

                foreach (int index in indices)
                    if ((index / Columns + 1) * FrameHeight > decoded.height)
                        throw new InvalidOperationException("Frame " + index + " is outside the source PNG.");

                EnsureFolder(folder);
                string framesFolder = folder + "/Frames";
                EnsureFolder(framesFolder);

                foreach (int index in indices)
                {
                    WriteFrame(decoded, index, 0, "Body", framesFolder);
                    WriteFrame(decoded, index, ArmColumnOffset, "Arms", framesFolder);
                    WriteFrame(decoded, index, ArmColumnOffset * 2, "SecondaryArms", framesFolder);
                    WriteFrame(pants, index, pantsX, "Pants", framesFolder, pantsY);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var spec in Specs)
                    WriteClip(spec, folder, framesFolder);
                AttachAnimatorStates(folder);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[FarmerAnimationGenerator] Generated " + Specs.Count + " clips from " + indices.Count + " frame indices in " + folder);
            }
            finally
            {
                DestroyImmediate(decoded);
                DestroyImmediate(pants);
            }
        }
        catch (Exception error)
        {
            Debug.LogError("[FarmerAnimationGenerator] " + error);
        }
    }

    private static void WriteFrame(Texture2D source, int index, int xOffset, string layer, string folder, int yOffsetFromTop = 0)
    {
        int x = xOffset + index % Columns * FrameWidth;
        int y = source.height - yOffsetFromTop - (index / Columns + 1) * FrameHeight;
        var pixels = source.GetPixels32();
        var framePixels = new Color32[FrameWidth * FrameHeight];
        for (int row = 0; row < FrameHeight; row++)
            Array.Copy(pixels, (y + row) * source.width + x, framePixels, row * FrameWidth, FrameWidth);

        var frameTexture = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
        try
        {
            frameTexture.SetPixels32(framePixels);
            frameTexture.Apply(false, false);
            string assetPath = folder + "/" + layer + "_" + index.ToString("D3") + ".png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), frameTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var frameImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            frameImporter.textureType = TextureImporterType.Sprite;
            frameImporter.spriteImportMode = SpriteImportMode.Single;
            frameImporter.spritePixelsPerUnit = FramePixelsPerUnit;
            frameImporter.filterMode = FilterMode.Point;
            frameImporter.mipmapEnabled = false;
            frameImporter.textureCompression = TextureImporterCompression.Uncompressed;
            frameImporter.SaveAndReimport();
        }
        finally
        {
            DestroyImmediate(frameTexture);
        }
    }

    private static void WriteClip(AnimationSpec spec, string folder, string framesFolder)
    {
        string path = folder + "/Farmer_" + spec.name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        var bodyKeys = new ObjectReferenceKeyframe[spec.steps.Length + 1];
        var armKeys = new ObjectReferenceKeyframe[spec.steps.Length + 1];
        var pantsKeys = new ObjectReferenceKeyframe[spec.steps.Length + 1];
        var offsetKeys = new Keyframe[spec.steps.Length + 1];
        var featureXKeys = new Keyframe[spec.steps.Length + 1];
        var hairYKeys = new Keyframe[spec.steps.Length + 1];
        var shirtYKeys = new Keyframe[spec.steps.Length + 1];
        float time = 0f;
        for (int i = 0; i < spec.steps.Length; i++)
        {
            int index = spec.steps[i].index;
            string suffix = index.ToString("D3") + ".png";
            var bodySprite = AssetDatabase.LoadAssetAtPath<Sprite>(framesFolder + "/Body_" + suffix);
            var armSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framesFolder + "/" + (spec.secondaryArm ? "SecondaryArms_" : "Arms_") + suffix);
            var pantsSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framesFolder + "/Pants_" + suffix);
            if (bodySprite == null || armSprite == null || pantsSprite == null)
                throw new InvalidOperationException("Imported frame " + index + " was not found.");
            bodyKeys[i] = new ObjectReferenceKeyframe { time = time, value = bodySprite };
            armKeys[i] = new ObjectReferenceKeyframe { time = time, value = armSprite };
            pantsKeys[i] = new ObjectReferenceKeyframe { time = time, value = pantsSprite };
            // XNA screen Y grows downward; Unity world Y grows upward.
            offsetKeys[i] = new Keyframe(time, -spec.steps[i].positionOffset / (float)FrameWidth);
            float featureX = FeatureXOffsets[index] / (float)FrameWidth;
            float featureY = FeatureYOffsets[index] / (float)FrameWidth;
            featureXKeys[i] = new Keyframe(time, spec.flipX ? -featureX : featureX);
            // FarmerRenderer draws hair one source pixel below the body top,
            // and the 8x8 shirt at source x=4, y=14 within a 16x32 frame.
            hairYKeys[i] = new Keyframe(time, offsetKeys[i].value - (1f / FrameWidth) - featureY);
            shirtYKeys[i] = new Keyframe(time, offsetKeys[i].value - (2f / FrameWidth) - featureY);
            time += spec.steps[i].milliseconds / 1000f;
        }
        int endIndex = spec.loop ? 0 : spec.steps.Length - 1;
        bodyKeys[spec.steps.Length] = new ObjectReferenceKeyframe { time = time, value = bodyKeys[endIndex].value };
        armKeys[spec.steps.Length] = new ObjectReferenceKeyframe { time = time, value = armKeys[endIndex].value };
        pantsKeys[spec.steps.Length] = new ObjectReferenceKeyframe { time = time, value = pantsKeys[endIndex].value };
        offsetKeys[spec.steps.Length] = new Keyframe(time, offsetKeys[endIndex].value);
        featureXKeys[spec.steps.Length] = new Keyframe(time, featureXKeys[endIndex].value);
        hairYKeys[spec.steps.Length] = new Keyframe(time, hairYKeys[endIndex].value);
        shirtYKeys[spec.steps.Length] = new Keyframe(time, shirtYKeys[endIndex].value);
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Body", typeof(SpriteRenderer), "m_Sprite"), bodyKeys);
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Arms", typeof(SpriteRenderer), "m_Sprite"), armKeys);
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Pants", typeof(SpriteRenderer), "m_Sprite"), pantsKeys);
        var offsetCurve = SteppedCurve(offsetKeys);
        foreach (string layer in new[] { "Body", "Arms", "Pants" })
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(layer, typeof(Transform), "m_LocalPosition.y"), offsetCurve);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(layer, typeof(SpriteRenderer), "m_FlipX"),
                AnimationCurve.Constant(0f, time, spec.flipX ? 1f : 0f));
        }
        foreach (string layer in new[] { "Shirt", "Hair" })
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(layer, typeof(Transform), "m_LocalPosition.x"), SteppedCurve(featureXKeys));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(layer, typeof(Transform), "m_LocalPosition.y"),
                SteppedCurve(layer == "Hair" ? hairYKeys : shirtYKeys));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(layer, typeof(SpriteRenderer), "m_FlipX"),
                AnimationCurve.Constant(0f, time, spec.flipX ? 1f : 0f));
        }
        // Tools use their own 16x32 atlas rectangle and pose, never the body
        // frame's transform curve.
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Tool", typeof(Transform), "m_LocalPosition.y"), null);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Tool", typeof(SpriteRenderer), "m_FlipX"), null);
        // Earlier clips contained bindings for a SpriteRenderer on the Player
        // root. The prefab renders sprites only on Body and Arms.
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), null);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_FlipX"), null);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = spec.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static AnimationCurve SteppedCurve(Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    private static void AttachAnimatorStates(string folder)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animator/Player.controller");
        if (controller == null)
            throw new InvalidOperationException("Player.controller was not found.");

        var stateMachine = controller.layers[0].stateMachine;
        int index = 0;
        foreach (var spec in Specs)
        {
            if (spec.name.StartsWith("Walk_", StringComparison.Ordinal) || spec.name.StartsWith("Run_", StringComparison.Ordinal))
                continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Farmer_" + spec.name + ".anim");
            AnimatorState state = null;
            foreach (var child in stateMachine.states)
                if (child.state.name == spec.name) { state = child.state; break; }
            if (state == null)
                state = stateMachine.AddState(spec.name, new Vector3(550 + (index % 4) * 190, (index / 4) * 75, 0));
            state.motion = clip;
            state.speed = 1f;
            index++;
        }
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureFolder(string folder)
    {
        string current = "Assets";
        foreach (string part in folder.Substring("Assets/".Length).Split('/'))
        {
            if (string.IsNullOrWhiteSpace(part))
                throw new InvalidOperationException("Output folder contains an empty segment.");
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
