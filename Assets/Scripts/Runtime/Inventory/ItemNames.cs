using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// English display names from the game's Data/Objects, BigCraftables, Weapons, Tools, Boots,
/// hats and Furniture, keyed by our item ids ("object_16" → "Wild Horseradish").
/// Quality ("@2") and preserve ("#…") suffixes fall back to the base item's name.
/// </summary>
internal static class ItemNames
{
    private static Dictionary<string, string> names;

    internal static string Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        names ??= Load();
        if (names.TryGetValue(itemId, out var name)) return name;
        string baseId = ItemQuality.BaseId(itemId);
        int preserve = baseId.IndexOf('#');
        if (preserve > 0) baseId = baseId.Substring(0, preserve);
        return baseId != itemId && names.TryGetValue(baseId, out name) ? name : null;
    }

    private static Dictionary<string, string> Load()
    {
        var asset = Resources.Load<TextAsset>("Data/ItemNames");
        if (asset == null)
        {
            Debug.LogWarning("Resources/Data/ItemNames.json is missing; items show their ids.");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
