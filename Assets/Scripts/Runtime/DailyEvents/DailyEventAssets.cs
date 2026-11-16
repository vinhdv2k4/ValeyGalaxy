using System;
using DailyEvents;
using Newtonsoft.Json;
using UnityEngine;

internal static class DailyEventAssets
{
    private static readonly Lazy<DailyEventCalendar> Source = new(() =>
    {
        var asset = Resources.Load<TextAsset>("DailyEvents/Calendar");
        if (asset == null) throw new InvalidOperationException("Missing DailyEvents/Calendar.");
        return new DailyEventCalendar(JsonConvert.DeserializeObject<DailyEventFile>(asset.text));
    });

    internal static DailyEventCalendar Calendar => Source.Value;

    internal static ItemData CreateSeed(string id, Func<string, ItemData> resolve)
    {
        var crop = Calendar.FindCrop(id);
        if (crop == null) return null;
        var objectAtlas = Resources.Load<Texture2D>("Maps/springobjects");
        var template = Resources.Load<ItemData>("OnlineItems/Item_CauliflowerSeeds");
        var harvest = resolve(crop.harvest);
        harvest.displayName = crop.name;
        var definition = ScriptableObject.CreateInstance<CropDefinition>();
        definition.harvestItem = harvest;
        definition.cropAtlas = template?.cropDefinition?.cropAtlas;
        definition.objectAtlas = objectAtlas;
        definition.harvestObjectIndex = crop.harvestIcon;
        definition.spriteRow = crop.row;
        definition.phaseDays = crop.phases;
        definition.regrowDays = crop.regrow;

        var seed = ScriptableObject.CreateInstance<ItemData>();
        seed.itemId = id;
        seed.displayName = crop.seedName;
        seed.kind = PlayerItemKind.Seeds;
        seed.icon = ItemData.CreateAtlasSprite(objectAtlas, crop.seedIcon);
        seed.cropDefinition = definition;
        return seed;
    }
}
