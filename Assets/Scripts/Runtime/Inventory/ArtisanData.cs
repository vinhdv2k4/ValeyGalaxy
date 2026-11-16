using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Machine rules and readable item names shared with the server (Resources/Data/Artisan.json).</summary>
internal static class ArtisanData
{
    [Serializable] internal sealed class NamedItem { public string id, name, category; }
    [Serializable] internal sealed class Rule { public string input, output, preserve; public int count, minutes, quality; }
    [Serializable] internal sealed class Fuel { public string id; public int count; }
    [Serializable] internal sealed class Machine { public string id; public Rule[] rules; public Fuel fuel; }
    [Serializable] internal sealed class Preserve { public string name; public float multiplier; public int add; }
    [Serializable] internal sealed class PreserveSet { public Preserve wine, juice, jelly, pickles; }
    [Serializable] internal sealed class Producer { public string id, output; public bool onTree, farmOnly; public int minutes; }
    [Serializable] internal sealed class Sprinkler { public string id, range; }
    [Serializable] internal sealed class FenceInfo { public string id, texture; public bool gate; }
    [Serializable] internal sealed class Floor { public string id; public int floor; }
    [Serializable] internal sealed class LightInfo { public string id; public float radius; }

    [Serializable]
    private sealed class File
    {
        public NamedItem[] items;
        public Machine[] machines;
        public PreserveSet preserves;
        public string[] fruits, vegetables;
        public Producer[] producers;
        public Sprinkler[] sprinklers;
        public FenceInfo[] fences;
        public Floor[] floors;
        public LightInfo[] lights;
    }

    private static File data;
    private static Dictionary<string, string> names;
    private static HashSet<string> fruits, vegetables;

    private static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/Artisan");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (NamedItem item in data.items ?? Array.Empty<NamedItem>())
                if (item?.id != null) names[item.id] = item.name;
            fruits = new HashSet<string>(data.fruits ?? Array.Empty<string>());
            vegetables = new HashSet<string>(data.vegetables ?? Array.Empty<string>());
            return data;
        }
    }

    internal static string Name(string itemId)
    {
        _ = Data;
        return itemId != null && names.TryGetValue(itemId, out string name) ? name : null;
    }

    internal static Machine MachineFor(string itemId) =>
        Array.Find(Data.machines ?? Array.Empty<Machine>(), machine => machine.id == itemId);

    internal static bool IsMachine(string itemId) => MachineFor(itemId) != null;

    private static Producer ProducerFor(string itemId) =>
        Array.Find(Data.producers ?? Array.Empty<Producer>(), item => item.id == itemId);

    internal static bool IsProducer(string itemId) => ProducerFor(itemId) != null;
    internal static bool IsTapper(string itemId) => ProducerFor(itemId)?.onTree == true;
    internal static bool IsSprinkler(string itemId) =>
        Array.Exists(Data.sprinklers ?? Array.Empty<Sprinkler>(), item => item.id == itemId);
    internal static FenceInfo Fence(string itemId) =>
        itemId == null ? null : Array.Find(Data.fences ?? Array.Empty<FenceInfo>(), item => item.id == itemId);
    internal static int FloorIndex(string itemId) =>
        itemId == null ? -1 : Array.Find(Data.floors ?? Array.Empty<Floor>(), item => item.id == itemId)?.floor ?? -1;
    internal static float LightRadius(string itemId) =>
        Array.Find(Data.lights ?? Array.Empty<LightInfo>(), item => item.id == itemId)?.radius ?? 0f;

    internal static Rule RuleFor(Machine machine, string heldItemId)
    {
        if (machine?.rules == null || string.IsNullOrEmpty(heldItemId)) return null;
        _ = Data;
        string baseId = ItemQuality.BaseId(heldItemId);
        foreach (Rule rule in machine.rules)
        {
            bool match = rule.input switch
            {
                "#fruit" => fruits.Contains(baseId),
                "#vegetable" => vegetables.Contains(baseId),
                _ => rule.input == baseId
            };
            if (match) return rule;
        }
        return null;
    }

    /// <summary>"Rượu {0}" for wine made from a source item, or null when the id is not a preserved good.</summary>
    internal static string PreservedName(string itemId, Func<string, string> nameOf)
    {
        int hash = itemId?.IndexOf('#') ?? -1;
        if (hash <= 0) return null;
        string output = itemId.Substring(0, hash), source = itemId.Substring(hash + 1);
        Preserve rule = output switch
        {
            "object_348" => Data.preserves?.wine,
            "object_350" => Data.preserves?.juice,
            "object_344" => Data.preserves?.jelly,
            "object_342" => Data.preserves?.pickles,
            _ => null
        };
        return rule == null ? null : string.Format(rule.name, nameOf(source) ?? source);
    }
}
