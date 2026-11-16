using System;
using System.Collections.Generic;
using System.Linq;
using DailyEvents;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Places the festival cast independently of villagers' normal routes.</summary>
internal sealed class FestivalCast : MonoBehaviour
{
    private readonly List<(string Name, Transform Transform)> actors = new();
    private int year;

    private void OnDestroy()
    {
        foreach (var actor in actors)
            if (actor.Transform != null) Destroy(actor.Transform.gameObject);
    }

    internal static void Populate(GameLocation map, DailyEventDefinition entry, IReadOnlyList<string> villagers,
        OnlineDailyEventStatus progress, Vector2 standing, int year)
    {
        var cast = map.GetComponent<FestivalCast>();
        if (cast == null) cast = map.gameObject.AddComponent<FestivalCast>();
        if (cast.year == year) return;
        cast.year = year;
        foreach (var actor in cast.actors) Destroy(actor.Transform.gameObject);
        cast.actors.Clear();

        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (catalog == null) return;
        var tiles = OpenTiles(map, NpcPathfinder.WorldToTile(standing)).GetEnumerator();
        var names = entry.people.Select(name => name switch
        {
            "%secretsanta" => progress.secretRecipient,
            "%secretgiver" => progress.secretGiver,
            _ => name
        });
        if (entry.crowd) names = names.Concat(villagers.Where(name => name != progress.secretGiver ||
            entry.activity != "exchange"));
        foreach (string name in names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal))
        {
            var definition = catalog.characters.Find(npc => npc.name == name);
            Sprite sprite = NpcSprites.CharacterIdle(definition?.animationFrames);
            sprite ??= NpcSprites.AtWorldScale(Resources.Load<FestivalPortraits>("DailyEvents/FestivalPortraits")?.Find(name));
            if (sprite == null) continue;
            if (!tiles.MoveNext()) break;
            var actor = new GameObject(name);
            actor.transform.SetParent(map.transform);
            Vector3 position = map.TileCenter(tiles.Current.x, tiles.Current.y);
            actor.transform.position = position + NpcSprites.FeetOffset;
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = WorldSortOrder.GetOrder(position.y);
            cast.actors.Add((name, actor.transform));
        }
        tiles.Dispose();
    }

    internal static IEnumerable<Vector2Int> OpenTiles(GameLocation map, Vector2Int origin)
    {
        var back = map.transform.Find("Back")?.GetComponent<Tilemap>();
        var colliders = new Collider2D[8];
        var filter = new ContactFilter2D { useTriggers = false };
        for (int radius = 2; radius <= 12; radius += 2)
        {
            for (int y = -radius; y <= radius; y += 2)
            for (int x = -radius; x <= radius; x += 2)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                Vector2Int tile = origin + new Vector2Int(x, y);
                Vector3 position = map.TileCenter(tile.x, tile.y);
                if (back != null && !back.HasTile(back.WorldToCell(position))) continue;
                if (map.IsCollidingWithWarp(tile.x, tile.y) != null) continue;
                if (Physics2D.OverlapCircle(position, 0.28f, filter, colliders) == 0) yield return tile;
            }
        }
    }

    internal static bool TryFind(GameLocation map, Vector2? pointer, Vector2 standing, Vector2 facing,
        out string name)
    {
        name = null;
        var cast = map.GetComponent<FestivalCast>();
        if (cast == null) return false;
        float best = 2.2f * 2.2f;
        foreach (var actor in cast.actors)
        {
            Vector2 position = actor.Transform.position;
            Vector2 offset = position - standing;
            if (offset.sqrMagnitude >= best) continue;
            if (pointer.HasValue)
            {
                Vector2 delta = pointer.Value - position;
                if (Mathf.Abs(delta.x) > 0.65f || delta.y < -0.35f || delta.y > 1.8f) continue;
            }
            else if (Vector2.Dot(offset.normalized, facing) < 0.35f) continue;
            best = offset.sqrMagnitude;
            name = actor.Name;
        }
        return name != null;
    }
}
