using System.Collections.Generic;
using DailyEvents;
using UnityEngine;

/// <summary>
/// The day's forage lying on a map: the spots come from the shared
/// ForageSpawns generator and the server's list of what was already picked today. Picking one asks
/// the server, which grants the item, its quality and the Foraging experience.
/// </summary>
internal sealed class ForageView : MonoBehaviour
{
    private static Dictionary<string, (int X, int Y)[]> tiles;
    private static readonly List<ForageView> Views = new();

    private GameLocation location;
    private readonly Dictionary<Vector2Int, (GameObject Visual, string ItemId)> shown = new();
    private int shownDay = -1, shownVersion = -1;
    private float nextRefresh;
    private bool picking;

    internal static void Attach(GameLocation map)
    {
        if (map == null || !ForageSpawns.HasForage(map.LocationName) || map.GetComponent<ForageView>() != null) return;
        map.gameObject.AddComponent<ForageView>().location = map;
    }

    private void OnEnable() => Views.Add(this);
    private void OnDisable() => Views.Remove(this);

    private static Dictionary<string, (int X, int Y)[]> Tiles
    {
        get
        {
            if (tiles != null) return tiles;
            var text = Resources.Load<TextAsset>("Data/ForageTiles");
            tiles = text != null ? ForageSpawns.ParseTiles(text.text) : new Dictionary<string, (int X, int Y)[]>();
            return tiles;
        }
    }

    private static OnlineGameState Online => LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>();

    private void Update()
    {
        var snapshot = Online?.Snapshot;
        if (snapshot == null || LocationManager.Instance?.CurrentLocation != location) return;
        if (snapshot.gameDay == shownDay && snapshot.version == shownVersion && Time.unscaledTime < nextRefresh) return;
        shownDay = snapshot.gameDay;
        shownVersion = snapshot.version;
        nextRefresh = Time.unscaledTime + 1f;
        Rebuild(snapshot);
    }

    private void Rebuild(OnlineGameSnapshot snapshot)
    {
        var picked = new HashSet<string>(snapshot.foragePicked ?? System.Array.Empty<string>());
        var keep = new HashSet<Vector2Int>();
        foreach (var spot in ForageSpawns.ForDay(Tiles, location.LocationName, snapshot.gameDay))
        {
            var tile = new Vector2Int(spot.X, spot.Y);
            if (picked.Contains($"{snapshot.gameDay}/{location.LocationName}/{spot.X}/{spot.Y}")) continue;
            Vector3 center = location.TileCenter(spot.X, spot.Y);
            // Never put forage under a tree, bush, rock or wall.
            if (Blocked(center)) continue;
            keep.Add(tile);
            if (shown.TryGetValue(tile, out var existing) && existing.Visual != null && existing.ItemId == spot.ItemId) continue;
            if (existing.Visual != null) Destroy(existing.Visual);
            shown[tile] = (CreateVisual(spot.ItemId, center), spot.ItemId);
        }
        var stale = new List<Vector2Int>();
        foreach (var pair in shown)
            if (!keep.Contains(pair.Key)) stale.Add(pair.Key);
        foreach (var tile in stale)
        {
            if (shown[tile].Visual != null) Destroy(shown[tile].Visual);
            shown.Remove(tile);
        }
    }

    private static bool Blocked(Vector3 center)
    {
        foreach (var hit in Physics2D.OverlapBoxAll(center, Vector2.one * 0.6f, 0f))
            if (!hit.isTrigger && hit.GetComponentInParent<CharacterMotor>() == null &&
                hit.GetComponentInParent<NpcActor>() == null) return true;
        return false;
    }

    private GameObject CreateVisual(string itemId, Vector3 center)
    {
        var visual = new GameObject("Forage " + itemId);
        visual.transform.SetParent(transform, false);
        visual.transform.position = center;
        var renderer = visual.AddComponent<SpriteRenderer>();
        Sprite icon = Online?.ResolveItem(itemId)?.GetIcon();
        if (icon != null)
        {
            renderer.sprite = icon;
            // Icons are cut for the UI; scale one to a single tile.
            float size = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
            if (size > 0f) visual.transform.localScale = Vector3.one / size;
        }
        renderer.sortingOrder = WorldSortOrder.GetOrder(center.y - 0.3f);
        return visual;
    }

    /// <summary>Picks the forage the farmer acts on, if any.</summary>
    internal static bool TryPick(Vector2? pointer, Vector3 standing, Vector2 facing)
    {
        var current = LocationManager.Instance?.CurrentLocation;
        foreach (var view in Views)
        {
            if (view.location != current || view.picking) continue;
            foreach (var pair in view.shown)
            {
                if (pair.Value.Visual == null ||
                    !NpcShop.HitActionTile(pointer, standing, facing, pair.Key.x, pair.Key.y)) continue;
                view.Pick(pair.Key, pair.Value.ItemId);
                return true;
            }
        }
        return false;
    }

    private void Pick(Vector2Int tile, string itemId)
    {
        var online = Online;
        if (online == null) return;
        picking = true;
        online.PickForage(location.LocationName, tile.x, tile.y, error =>
        {
            picking = false;
            if (error != null)
            {
                NpcConversation.ShowNotice(error);
                return;
            }
            // Picking up forage: the "harvest" sound and the item's toast.
            GameAudio.Play("harvest");
            if (shown.TryGetValue(tile, out var entry) && entry.Visual != null) Destroy(entry.Visual);
            shown.Remove(tile);
            var item = online.ResolveItem(itemId);
            if (item != null) HudToast.ShowItem(item, 1);
        });
    }
}
