using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

internal static class NpcPathfinder
{
    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    private readonly struct SearchNode
    {
        public readonly Vector2Int Tile;
        public readonly int Cost;
        public readonly int Priority;
        public readonly int Sequence;

        public SearchNode(Vector2Int tile, int cost, int priority, int sequence)
        {
            Tile = tile;
            Cost = cost;
            Priority = priority;
            Sequence = sequence;
        }
    }

    public static List<Vector2> FindPath(GameLocation location, Vector2 startWorld,
        Vector2 goalWorld, bool allowBlocked = false)
    {
        Vector2Int start = WorldToTile(startWorld);
        Vector2Int goal = WorldToTile(goalWorld);
        if (start == goal) return new List<Vector2> { goalWorld };

        int minX = Math.Min(start.x, goal.x) - 12;
        int maxX = Math.Max(start.x, goal.x) + 12;
        int minY = Math.Min(start.y, goal.y) - 12;
        int maxY = Math.Max(start.y, goal.y) + 12;
        var bestCost = new Dictionary<Vector2Int, int> { [start] = 0 };
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var blocked = new Dictionary<Vector2Int, bool>();
        var open = new SortedSet<SearchNode>(Comparer<SearchNode>.Create((left, right) =>
        {
            int result = left.Priority.CompareTo(right.Priority);
            return result != 0 ? result : left.Sequence.CompareTo(right.Sequence);
        }));
        int sequence = 0;
        open.Add(new SearchNode(start, 0, Distance(start, goal), sequence++));

        int expanded = 0;
        while (open.Count > 0 && expanded++ < 10000)
        {
            SearchNode current = open.Min;
            open.Remove(current);
            if (current.Cost != bestCost[current.Tile]) continue;
            if (current.Tile == goal) return Reconstruct(previous, start, goal);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current.Tile + direction;
                if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                bool obstructed = next != goal && IsBlocked(location, next, blocked);
                if (obstructed && !allowBlocked) continue;

                // Some converted maps mark traversable furniture/door tiles as solid.
                // Prefer open corridors, but still find an NPC route if no clear one exists.
                int cost = current.Cost + (obstructed ? 20 : 1);
                if (bestCost.TryGetValue(next, out int knownCost) && cost >= knownCost) continue;
                bestCost[next] = cost;
                previous[next] = current.Tile;
                open.Add(new SearchNode(next, cost, cost + Distance(next, goal), sequence++));
            }
        }

        return null;
    }

    private static bool IsBlocked(GameLocation location, Vector2Int tile,
        Dictionary<Vector2Int, bool> cache)
    {
        if (cache.TryGetValue(tile, out bool blocked)) return blocked;
        Vector2 center = TileToWorld(tile);
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, Vector2.one * 0.48f, 0f))
        {
            if (hit.isTrigger || !hit.transform.IsChildOf(location.transform) ||
                (!(hit is TilemapCollider2D) && !(hit is CompositeCollider2D))) continue;
            cache[tile] = true;
            return true;
        }
        cache[tile] = false;
        return false;
    }

    private static List<Vector2> Reconstruct(Dictionary<Vector2Int, Vector2Int> previous,
        Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2>();
        Vector2Int current = goal;
        while (current != start)
        {
            path.Add(TileToWorld(current));
            current = previous[current];
        }
        path.Reverse();
        return path;
    }

    private static int Distance(Vector2Int first, Vector2Int second) =>
        Math.Abs(first.x - second.x) + Math.Abs(first.y - second.y);

    internal static Vector2Int WorldToTile(Vector2 position) =>
        new Vector2Int(Mathf.FloorToInt(position.x), -Mathf.FloorToInt(position.y));

    // Map builder places row y at Unity cell (x, -y).
    internal static Vector2 TileToWorld(Vector2Int tile) =>
        new Vector2(tile.x + 0.5f, 0.5f - tile.y);
}
