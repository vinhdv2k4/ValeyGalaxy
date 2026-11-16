#nullable enable
using System;
using System.Collections.Generic;

namespace DailyEvents
{
    /// <summary>
    /// Daily forage: each day a few
    /// seasonal items appear on a map's Spawnable tiles. Shared by the game and the server, so both
    /// agree on what lies where; the server alone hands the item over.
    /// </summary>
    public static class ForageSpawns
    {
        public readonly struct Spot
        {
            public readonly int X, Y;
            public readonly string ItemId;
            public Spot(int x, int y, string itemId) { X = x; Y = y; ItemId = itemId; }
        }

        // Spring, summer, fall, winter forage by map.
        private static readonly int[] Spring = { 16, 18, 20, 22 };          // Wild Horseradish, Daffodil, Leek, Dandelion
        private static readonly int[] Summer = { 396, 398, 402 };           // Spice Berry, Grape, Sweet Pea
        private static readonly int[] Fall = { 404, 406, 408, 410 };        // Common Mushroom, Wild Plum, Hazelnut, Blackberry
        private static readonly int[] Winter = { 414, 418, 283 };           // Crystal Fruit, Crocus, Holly
        private static readonly Dictionary<string, int[][]> Tables = new(StringComparer.Ordinal)
        {
            ["Town"] = new[] { Spring, Summer, Fall, Winter },
            ["Mountain"] = new[] { Spring, Summer, Fall, Winter },
            ["BusStop"] = new[] { Spring, Summer, Fall, Winter },
            ["Railroad"] = new[] { Spring, Summer, Fall, Winter },
            ["Backwoods"] = new[] { Spring, Summer, Fall, Winter },
            // Cindersap Forest also grows Spring Onions in spring.
            ["Forest"] = new[] { new[] { 16, 20, 22, 399 }, Summer, Fall, Winter },
            // Secret Woods: Morel, Fiddlehead Fern, Chanterelle and mushrooms.
            ["Woods"] = new[] { new[] { 257, 404, 22 }, new[] { 259, 420, 404 }, new[] { 281, 404, 420 }, new[] { 414, 283 } },
            // Beach: Clam, Coral, Sea Urchin, Cockle, Mussel; Rainbow Shell in summer, Nautilus in winter.
            ["Beach"] = new[] { new[] { 372, 393, 397, 718, 719 }, new[] { 372, 393, 394, 397, 718, 719 },
                new[] { 372, 393, 397, 718, 719 }, new[] { 372, 392, 393, 397 } }
        };

        /// <summary>Spawnable tiles by map.</summary>
        public static Dictionary<string, (int X, int Y)[]> ParseTiles(string text)
        {
            var tiles = new Dictionary<string, (int X, int Y)[]>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cells = new (int X, int Y)[(parts.Length - 1) / 2];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = (int.Parse(parts[1 + i * 2]), int.Parse(parts[2 + i * 2]));
                tiles[parts[0]] = cells;
            }
            return tiles;
        }

        public static bool HasForage(string location) => Tables.ContainsKey(location);

        /// <summary>The forage lying on a map on an absolute game day: 3 to 6 items (fewer when tiles are scarce).</summary>
        public static IReadOnlyList<Spot> ForDay(IReadOnlyDictionary<string, (int X, int Y)[]> tiles, string location, int day)
        {
            var spots = new List<Spot>();
            if (!Tables.TryGetValue(location, out var table) || !tiles.TryGetValue(location, out var cells) ||
                cells.Length == 0) return spots;
            int[] items = table[(Math.Max(1, day) - 1) / 28 % 4];
            uint state = Seed(location, day);
            int count = Math.Min(cells.Length, 3 + (int)(Next(ref state) % 4));
            var used = new HashSet<int>();
            for (int attempt = 0; spots.Count < count && attempt < count * 8; attempt++)
            {
                int cell = (int)(Next(ref state) % (uint)cells.Length);
                int item = items[Next(ref state) % (uint)items.Length];
                if (!used.Add(cell)) continue;
                spots.Add(new Spot(cells[cell].X, cells[cell].Y, "object_" + item));
            }
            return spots;
        }

        /// <summary>Spot.ItemId lying at a tile today, or null.</summary>
        public static string? At(IReadOnlyDictionary<string, (int X, int Y)[]> tiles, string location, int day, int x, int y)
        {
            foreach (var spot in ForDay(tiles, location, day))
                if (spot.X == x && spot.Y == y) return spot.ItemId;
            return null;
        }

        // A fixed hash and xorshift, identical in Unity and on the server (System.Random is not).
        private static uint Seed(string location, int day)
        {
            uint hash = 2166136261u;
            foreach (char c in location) hash = (hash ^ c) * 16777619u;
            hash = (hash ^ (uint)day) * 16777619u;
            return hash == 0 ? 0x9E3779B9u : hash;
        }

        private static uint Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
