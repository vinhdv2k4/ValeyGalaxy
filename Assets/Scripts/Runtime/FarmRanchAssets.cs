using System;
using UnityEngine;

public sealed class FarmRanchAssets : ScriptableObject
{
    public TextAsset catalog;
    public Texture2D[] buildings;
    public Texture2D[] animals;
    public GameObject[] interiors;
    public Texture2D[] paintMasks;
    public Texture2D cursors2;
    public Texture2D[] fences;
    public Texture2D flooring;

    private static FarmRanchAssets instance;
    internal static FarmRanchAssets Instance => instance != null ? instance : instance = Resources.Load<FarmRanchAssets>("FarmRanchAssets");
    private RanchShopData data;
    internal static RanchShopData Data => Instance == null || Instance.catalog == null ? null :
        Instance.data ??= JsonUtility.FromJson<RanchShopData>(Instance.catalog.text);

    internal static GameObject Interior(string name)
    {
        string prefab = RoomPrefab(name);
        return prefab != null && Instance != null
            ? Array.Find(Instance.interiors, item => item != null && item.name == prefab) : null;
    }

    /// <summary>The room template behind a building's location name, e.g. "Coop2_&lt;id&gt;" → "Coop2".</summary>
    internal static string RoomPrefab(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        int split = name.IndexOf('_');
        string prefab = split < 0 ? name : name[..split];
        return prefab is "Coop" or "Coop2" or "Coop3" or "Barn" or "Barn2" or "Barn3" ? prefab : null;
    }

    internal static string BuildingId(string name)
    {
        if (RoomPrefab(name) == null) return null;
        int split = name.IndexOf('_');
        return split < 0 ? null : name[(split + 1)..];
    }

    internal static string RoomName(OnlineRanchHome home) =>
        home.kind + (home.level > 1 ? home.level.ToString() : "") + "_" + home.id;

    internal static bool IsInterior(string name) => RoomPrefab(name) != null;

    internal static string KindFor(string name) => IsInterior(name) ? name[..4] : null;
    internal static RanchFootprintView Footprint(string kind) => Data?.footprints == null ? null :
        Array.Find(Data.footprints, item => item.kind == kind);
    internal static string ItemName(string itemId)
    {
        if (Data?.items == null || string.IsNullOrEmpty(itemId)) return null;
        string baseId = ItemQuality.BaseId(itemId);
        return Array.Find(Data.items, item => item.id == baseId)?.name;
    }

    internal static RanchOfferView Species(string id) => Data?.animals == null ? null :
        Array.Find(Data.animals, offer => offer.id == id);
    internal static Texture2D AnimalArt(string name) => Instance == null || name == null ? null :
        Array.Find(Instance.animals, texture => texture != null && texture.name == name);
    internal static RanchPlanView Plan(string kind, int level) => Data == null ? null :
        Array.Find(Data.plans, plan => plan.kind == kind && plan.level == level);
    internal static Texture2D PaintMask(string planName) => Instance?.paintMasks == null || planName == null ? null :
        Array.Find(Instance.paintMasks, texture => texture != null && texture.name == planName + "_PaintMask");
    internal static RanchPaintableView Paintable(string planName) => Data?.paint == null ? null :
        Array.Find(Data.paint, entry => entry.building == planName);
    internal static Texture2D Art(string planName) => Instance == null || planName == null ? null :
        Array.Find(Instance.buildings, texture => texture != null && texture.name == planName);
}

[Serializable]
internal sealed class RanchShopData
{
    public RanchOfferView[] animals;
    public RanchPlanView[] plans;
    public RanchFootprintView[] footprints;
    public RanchPaintableView[] paint;
    public RanchItemView[] items;
}

[Serializable]
internal sealed class RanchItemView
{
    public string id, name, category;
    public int sellPrice, buyPrice, edibility;
    public bool qualities;
}

[Serializable]
internal sealed class RanchOfferView
{
    public string id, name, kind, produce, deluxe, altProduce, altDeluxe, harvest, description;
    public string art, altArt, baby, altBaby, sheared;
    public int level, price, daysToLay, mature, variants;

    internal string ArtFor(OnlineRanchAnimal animal)
    {
        bool alt = animal.variant == 1;
        if (animal.age < mature) return alt && !string.IsNullOrEmpty(altBaby) ? altBaby : baby;
        if (!string.IsNullOrEmpty(sheared) && string.IsNullOrEmpty(animal.produce)) return sheared;
        return alt && !string.IsNullOrEmpty(altArt) ? altArt : art;
    }
}

[Serializable]
internal sealed class RanchPlanView
{
    public string kind, name;
    public int level, price, wood, stone, days;
    public int[] produceArea;
    public RanchMaterialView[] materials;
}

[Serializable]
internal sealed class RanchMaterialView
{
    public string id;
    public int amount;
}

[Serializable]
internal sealed class RanchPaintableView
{
    public string building;
    public RanchPaintRangeView[] regions;
}

[Serializable]
internal sealed class RanchPaintRangeView
{
    public string name;
    public int min, max;
}

[Serializable]
public sealed class OnlineRanchPaint
{
    public bool custom;
    public int hue, saturation, lightness;
}

[Serializable]
internal sealed class RanchFootprintView
{
    public string kind;
    public int width, height, doorX, animalDoorX, animalDoorY, troughX, troughY, hopperX, hopperY;
    internal bool HasAnimalDoor => animalDoorX >= 0;
}

[Serializable]
public sealed class OnlineRanchHome
{
    public string id, kind;
    public int x, y, width, height, level, targetLevel, readyDay, capacity;
    public OnlineRanchPaint[] paint;
    public bool animalDoorOpen, hasDoor;
    public int[] hay;
    internal bool IsSilo => kind == "Silo";
    internal int DoorX => x + 1;
    internal int DoorY => y + height;
    internal bool Contains(int column, int row) => column >= x && column < x + width && row >= y && row < y + height;
    internal bool Reserves(int column, int row) => Contains(column, row) || hasDoor && column == DoorX && row == DoorY;
}

[Serializable]
public sealed class OnlineRanchAnimal
{
    public string id, species, kind, name, home, produce;
    public int variant, age, friendship, happiness, daysSinceLay, dug;
    public bool petted, hungry, grazed;
}

[Serializable]
public sealed class OnlineRanchProduct
{
    public string id, itemId, location;
    public int x, y;
}
