using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Indexes the user's Assets/Audio clips without copying them into Resources.</summary>
[InitializeOnLoad]
public static class GameAudioLibraryBuilder
{
    private const string AssetPath = "Assets/Resources/GameAudioLibrary.asset";

    static GameAudioLibraryBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    private static void BuildIfMissing()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(AssetPath) == null)
            Rebuild();
    }

    [MenuItem("Tools/Audio/Rebuild Cue Library")]
    public static void Rebuild()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
        var entries = new List<GameAudioLibrary.Entry>(guids.Length);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
        foreach (string guid in guids)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && names.Add(clip.name))
                entries.Add(new GameAudioLibrary.Entry { cue = clip.name, clip = clip });
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var library = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(AssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<GameAudioLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);
        }
        library.entries = entries;
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameAudio] Indexed {entries.Count} audio cues from Assets/Audio.");
    }
}
