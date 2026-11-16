using UnityEngine;

internal sealed class MineEntranceBlock : MonoBehaviour
{
    // Landslide bounds and source rect.
    internal static readonly RectInt RubbleSource = new(646, 1218, 48, 80);

    private SpriteRenderer rocks;
    private BoxCollider2D footprint;
    private OnlineGameState online;
    private int openingDay;

    internal static Vector3 RubbleTopLeft(GameLocation mountain) =>
        mountain.TileCenter(50, 4) + new Vector3(-0.5f, 0.5f);

    internal static void Attach(GameLocation mountain)
    {
        var atlas = StardewUiAtlas.Cursors;
        if (atlas == null || atlas.width < RubbleSource.xMax || atlas.height < RubbleSource.yMax) return;

        var root = new GameObject("Mine landslide");
        root.transform.SetParent(mountain.transform, false);
        root.transform.position = RubbleTopLeft(mountain);
        var block = root.AddComponent<MineEntranceBlock>();
        block.openingDay = DailyEventAssets.Calendar.Find("mine_open").day;
        block.rocks = root.AddComponent<SpriteRenderer>();
        block.rocks.sprite = AtlasSprites.FromTop(atlas, RubbleSource, Vector2.up);
        block.rocks.sortingOrder = WorldSortOrder.GetOrder(root.transform.position.y - 5f);
        block.footprint = root.AddComponent<BoxCollider2D>();
        block.footprint.size = new Vector2(3f, 5f);
        block.footprint.offset = new Vector2(1.5f, -2.5f);
        block.Update();
    }

    private void Update()
    {
        if (online == null) online = LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>();
        var clock = FarmDayClock.Instance;
        int day = online?.Snapshot?.gameDay ?? clock?.Day ?? 1;
#if UNITY_EDITOR
        if (EventQaPreview.For(LocationManager.Instance?.LocalFarmer) != null) day = clock?.Day ?? day;
#endif
        bool blocked = day < openingDay;
        if (rocks.enabled != blocked) rocks.enabled = footprint.enabled = blocked;
    }
}
