using System;
using System.Collections.Generic;
using System.Globalization;
using DailyEvents;
using Newtonsoft.Json;
using UnityEngine;

internal readonly struct NpcScheduleStop
{
    public readonly int Time;
    public readonly string Location;
    public readonly Vector2Int Tile;
    public readonly int Facing;
    public readonly string Behavior;

    public NpcScheduleStop(int time, string location, Vector2Int tile, int facing, string behavior)
    {
        Time = time;
        Location = location;
        Tile = tile;
        Facing = facing;
        Behavior = behavior;
    }
}

internal static class NpcSchedule
{
    private static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static IReadOnlyList<NpcScheduleStop> Load(TextAsset source, int absoluteDay, bool raining)
    {
        if (source == null) return Array.Empty<NpcScheduleStop>();

        Dictionary<string, string> schedules;
        try
        {
            schedules = JsonConvert.DeserializeObject<Dictionary<string, string>>(source.text);
        }
        catch (JsonException error)
        {
            Debug.LogWarning($"[NPC] Invalid schedule {source.name}: {error.Message}");
            return Array.Empty<NpcScheduleStop>();
        }

        if (schedules == null) return Array.Empty<NpcScheduleStop>();
        var date = new FarmDate(absoluteDay);
        int seasonDay = date.DayOfSeason;
        string season = date.Season;
        string weekday = Weekdays[(date.Day - 1) % Weekdays.Length];
        string[] keys =
        {
            raining && date.Day % 2 == 0 ? "rain2" : null, raining ? "rain" : null,
            season + "_" + seasonDay, seasonDay.ToString(CultureInfo.InvariantCulture),
            season + "_" + weekday, weekday, season,
            "spring_" + weekday, "spring", "default"
        };

        foreach (string key in keys)
        {
            if (key == null) continue;
            if (!TryResolve(schedules, key, season, out string route)) continue;
            List<NpcScheduleStop> stops = ParseRoute(route, schedules);
            if (stops.Count > 0) return stops;
        }

        return Array.Empty<NpcScheduleStop>();
    }

    private static bool TryResolve(Dictionary<string, string> schedules, string key,
        string season, out string route)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(key) && schedules.TryGetValue(key, out route) && !string.IsNullOrWhiteSpace(route))
        {
            string[] segments = route.Split('/');
            int start = 0;
            // A new save has no friendship or mail flags, so
            // parseMasterSchedule skips the NOT/MAIL header in that state.
            if (segments[0].StartsWith("NOT friendship ", StringComparison.OrdinalIgnoreCase) ||
                segments[0].StartsWith("MAIL ", StringComparison.OrdinalIgnoreCase)) start++;
            if (start >= segments.Length) break;
            string first = segments[start].Trim();
            if (!first.StartsWith("GOTO ", StringComparison.OrdinalIgnoreCase))
            {
                route = string.Join("/", segments, start, segments.Length - start);
                return true;
            }
            key = first.Substring(5).Trim();
            if (key.Equals("season", StringComparison.OrdinalIgnoreCase)) key = season;
            if (key.Equals("NO_SCHEDULE", StringComparison.OrdinalIgnoreCase)) break;
        }

        route = null;
        return false;
    }

    private static List<NpcScheduleStop> ParseRoute(string route,
        Dictionary<string, string> schedules)
    {
        var stops = new List<NpcScheduleStop>();
        foreach (string segment in route.Split('/'))
        {
            string[] fields = segment.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2) continue;
            string rawTime = fields[0].StartsWith("a", StringComparison.OrdinalIgnoreCase)
                ? fields[0].Substring(1) : fields[0];
            if (!int.TryParse(rawTime, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int time) || time < 0) continue;
            string location = fields[1];
            int x, y, facing;
            if (location.Equals("bed", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFindBed(schedules, out location, out x, out y, out facing)) continue;
            }
            else if (fields.Length >= 5 &&
                int.TryParse(fields[2], out x) && int.TryParse(fields[3], out y) &&
                int.TryParse(fields[4], out facing)) { }
            else continue;
            if (x < 0 || y < 0 || facing is < 0 or > 3) continue;

            string behavior = null;
            for (int i = 5; i < fields.Length; i++)
                if (!fields[i].StartsWith("\"", StringComparison.Ordinal) &&
                    !fields[i].StartsWith("Strings", StringComparison.Ordinal))
                { behavior = fields[i]; break; }
            stops.Add(new NpcScheduleStop(time, location,
                new Vector2Int(x, y), facing, behavior));
        }

        stops.Sort((left, right) => left.Time.CompareTo(right.Time));
        return stops;
    }

    private static bool TryFindBed(Dictionary<string, string> schedules,
        out string location, out int x, out int y, out int facing)
    {
        foreach (string key in new[] { "default", "spring" })
        {
            if (!schedules.TryGetValue(key, out string route)) continue;
            string[] segments = route.Split('/');
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string[] fields = segments[i].Trim().Split(' ');
                if (fields.Length < 5 ||
                    !int.TryParse(fields[2], out x) ||
                    !int.TryParse(fields[3], out y) ||
                    !int.TryParse(fields[4], out facing)) continue;
                location = fields[1];
                return true;
            }
        }
        location = null;
        x = y = facing = 0;
        return false;
    }
}
