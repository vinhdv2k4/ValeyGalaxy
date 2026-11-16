using System;
using UnityEngine;

internal sealed class MilestoneWorldView : IDisposable
{
    private readonly MilestoneSprites sprites;
    private readonly Func<string, ItemData> resolveItem;
    private GameLocation location;
    private GameObject root;
    private Sprite candle;
    private Sprite starterGift;
    private string pet, cave;
    private int candles, caveHarvestDay, day;
    private bool showStarterGift;
    internal const string StarterGiftId = "starter_gift";
    internal const int StarterGiftX = 4, StarterGiftY = 7;

    internal static bool HasStarterGift(OnlineMilestoneStatus state) =>
        state?.HasSeen(StartupSession.ArrivalEventId) == true && !state.HasSeen(StarterGiftId);

    internal MilestoneWorldView(MilestoneSprites sprites, Func<string, ItemData> resolveItem)
    {
        this.sprites = sprites;
        this.resolveItem = resolveItem;
    }

    internal void Sync(GameLocation map, OnlineMilestoneStatus state, int currentDay)
    {
        if (map == null || state == null) return;
        bool giftPending = map.LocationName == "FarmHouse" && HasStarterGift(state);
        if (location == map && pet == state.pet && cave == state.cave && candles == state.candles &&
            caveHarvestDay == state.caveHarvestDay && day == currentDay && showStarterGift == giftPending) return;
        Dispose();
        location = map;
        pet = state.pet;
        cave = state.cave;
        candles = state.candles;
        caveHarvestDay = state.caveHarvestDay;
        day = currentDay;
        showStarterGift = giftPending;
        root = new GameObject("Milestone props");
        root.transform.SetParent(map.transform, false);

        switch (map.LocationName)
        {
            case "Farm":
                if (!string.IsNullOrEmpty(pet))
                    Place(state.petName, sprites?.SpriteFor(pet), map.TileCenter(68, 19));
                PlaceCandles(map);
                break;
            case "FarmCave" when !string.IsNullOrEmpty(cave):
                if (caveHarvestDay != day)
                    Place(cave == "bats" ? "Fruit from bats" : "Mushroom tray",
                        resolveItem(cave == "bats" ? "object_410" : "object_420")?.GetIcon(),
                        map.TileCenter(5, 6));
                break;
            case "CommunityCenter":
                Place("Bundles", resolveItem("object_770")?.GetIcon(), map.TileCenter(30, 18));
                break;
            case "FarmHouse" when showStarterGift:
                PlaceStarterGift(map);
                break;
        }
    }

    private void PlaceStarterGift(GameLocation map)
    {
        var atlas = Resources.Load<Texture2D>("Startup/Giftbox");
        if (atlas == null) return;
        starterGift = Sprite.Create(atlas, new Rect(0, atlas.height - 32, 16, 32),
            new Vector2(0.5f, 0.35f), 16f, 0, SpriteMeshType.FullRect);
        GameObject prop = Place("Starter gift", starterGift, map.TileCenter(StarterGiftX, StarterGiftY));
        var footprint = prop.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(0.8f, 0.65f);
    }

    private void PlaceCandles(GameLocation map)
    {
        Texture2D atlas = sprites?.cursors;
        if (candles <= 0 || atlas == null || atlas.width < 579 || atlas.height < 1990) return;
        candle = Sprite.Create(atlas, new Rect(577, atlas.height - 1990, 2, 5), Vector2.one * 0.5f, 16f);
        Vector3[] offsets = { new(-0.8f, 0.6f), new(-0.45f, 1.15f), new(0.65f, 1.15f), new(0.95f, 0.6f) };
        for (int i = 0; i < Mathf.Min(candles, offsets.Length); i++)
            Place("Grandpa candle " + (i + 1), candle, map.TileCenter(8, 7) + offsets[i], new Color(1f, 0.87f, 0.52f));
    }

    private GameObject Place(string name, Sprite sprite, Vector3 position, Color? tint = null)
    {
        if (sprite == null) return null;
        var prop = new GameObject(name);
        prop.transform.SetParent(root.transform, false);
        prop.transform.position = position;
        var visual = prop.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.color = tint ?? Color.white;
        visual.sortingOrder = WorldSortOrder.GetOrder(position.y, 0f, 20);
        return prop;
    }

    public void Dispose()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        if (candle != null) UnityEngine.Object.Destroy(candle);
        if (starterGift != null) UnityEngine.Object.Destroy(starterGift);
        root = null;
        candle = null;
        starterGift = null;
    }
}
