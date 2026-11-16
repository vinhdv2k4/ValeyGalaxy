#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace DailyEvents
{
    public readonly struct FarmDate
    {
        public const int DaysPerSeason = 28;
        public const int DaysPerYear = 112;
        public int Day { get; }
        public int Year => (Day - 1) / DaysPerYear + 1;
        public int DayOfYear => (Day - 1) % DaysPerYear + 1;
        public int SeasonIndex => (DayOfYear - 1) / DaysPerSeason;
        public int DayOfSeason => (DayOfYear - 1) % DaysPerSeason + 1;
        public string Season => SeasonIndex switch
        {
            0 => "spring",
            1 => "summer",
            2 => "fall",
            _ => "winter"
        };

        public FarmDate(int day) => Day = Math.Max(1, day);
    }

    [Serializable]
    public sealed class DailyEventDefinition
    {
        public string id = "", kind = "", title = "", body = "", location = "", actor = "";
        public string quest = "", objective = "", target = "", item = "", rewardItem = "", success = "";
        public string map = "", activity = "", requires = "";
        public string[] people = Array.Empty<string>();
        public int day, until, x, y, spotX, spotY, daysAfter, gold;
        public int start = 600, end = 2600, quantity = 1;
        public bool crowd, clearWeather, annual, exactDay, cutscene;
        public string seasonOnly = "";

        public bool Repeats => annual || kind is "festival" or "forage" or "season";
        public bool HasTile => x != 0 || y != 0;
        public bool HasLostItem => kind == "mail" && item.Length > 0 && HasTile;
        public bool IsDue(FarmDate date)
        {
            int calendarDay = Repeats ? date.DayOfYear : date.Day;
            return calendarDay >= day && calendarDay <= Math.Max(day, until);
        }

        public bool WithinHours(int time) => time >= start && time < end && time % 100 < 60;

        public string Key(FarmDate date) => Repeats ? id + "/" + date.Year : id;
    }

    [Serializable]
    public sealed class DailyEventFile
    {
        public DailyEventDefinition[] events = Array.Empty<DailyEventDefinition>();
        public string[] villagers = Array.Empty<string>();
        public DailyEventCrop[] crops = Array.Empty<DailyEventCrop>();
    }

    [Serializable]
    public sealed class DailyEventCrop
    {
        public string seed = "", harvest = "", name = "", seedName = "", season = "";
        public int seedIcon, harvestIcon, row, regrow;
        public int[] phases = Array.Empty<int>();
        public int GrowthDays => phases.Sum();
    }

    public sealed class DailyEventCalendar
    {
        private readonly Dictionary<string, DailyEventDefinition> byId;
        private readonly Dictionary<string, DailyEventCrop> crops;
        private readonly HashSet<int> festivalDays = new();
        public IReadOnlyList<DailyEventDefinition> Events { get; }
        public IReadOnlyList<string> Villagers { get; }

        public DailyEventCalendar(DailyEventFile file)
        {
            if (file == null || file.events == null || file.events.Length == 0 ||
                file.villagers == null || file.villagers.Distinct(StringComparer.Ordinal).Count() < 2)
                throw new ArgumentException("Daily event catalog is empty or invalid.");
            var ordered = file.events.OrderBy(entry => entry.day).ToArray();
            Events = Array.AsReadOnly(ordered);
            Villagers = Array.AsReadOnly((string[])file.villagers.Clone());
            byId = ordered.ToDictionary(entry => entry.id, StringComparer.Ordinal);
            crops = file.crops.ToDictionary(crop => crop.seed, StringComparer.Ordinal);
            foreach (DailyEventCrop crop in crops.Values)
                if (crop.seed.Length == 0 || crop.harvest.Length == 0 || crop.row < 0 || crop.regrow < 0 ||
                    crop.phases.Length == 0 || crop.phases.Any(days => days <= 0) ||
                    crop.season is not ("spring" or "summer" or "fall" or "winter"))
                    throw new ArgumentException("Invalid daily event crop " + crop.seed);
            foreach (DailyEventDefinition entry in ordered)
            {
                if (string.IsNullOrWhiteSpace(entry.id) || entry.day < 1 || entry.day > FarmDate.DaysPerYear ||
                    entry.until != 0 && (entry.until < entry.day || entry.until > FarmDate.DaysPerYear) ||
                    entry.start < 600 || entry.end > 2600 || entry.start >= entry.end ||
                    entry.start % 100 >= 60 || entry.end % 100 >= 60 || entry.daysAfter < 0 ||
                    entry.quantity < 1 || entry.gold < 0 || entry.people == null ||
                    entry.kind is not ("story" or "mail" or "festival" or "weather" or "unlock" or "forage" or "season") ||
                    entry.kind == "festival" && (entry.map.Length == 0 || entry.location.Length == 0) ||
                    entry.requires.Length > 0 &&
                    (!byId.TryGetValue(entry.requires, out var prior) || prior.day > entry.day))
                    throw new ArgumentException("Invalid daily event " + entry.id);
                if (entry.kind == "festival" && entry.activity != "market")
                    for (int day = entry.day; day <= Math.Max(entry.day, entry.until); day++)
                        festivalDays.Add(day);
            }
            foreach (DailyEventDefinition entry in ordered)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { entry.id };
                for (string key = entry.requires; key.Length > 0; key = byId[key].requires)
                    if (!visited.Add(key)) throw new ArgumentException("Circular daily event prerequisite " + key);
            }
        }

        public DailyEventDefinition? Find(string? id) =>
            id != null && byId.TryGetValue(id, out var entry) ? entry : null;

        public DailyEventCrop? FindCrop(string id) => crops.TryGetValue(id, out var crop) ? crop : null;

        public IEnumerable<DailyEventDefinition> On(FarmDate date) =>
            Events.Where(entry => entry.IsDue(date));

        public bool IsAvailable(DailyEventDefinition entry, FarmDate date, Func<string, int> completedOn)
        {
            int calendarDay = entry.Repeats ? date.DayOfYear : date.Day;
            if (calendarDay < entry.day || entry.exactDay && !entry.IsDue(date) ||
                entry.seasonOnly.Length > 0 && date.Season != entry.seasonOnly ||
                completedOn(entry.Key(date)) > 0) return false;
            if (entry.requires.Length == 0) return true;
            int prerequisiteDay = completedOn(byId[entry.requires].Key(date));
            return prerequisiteDay > 0 && date.Day - prerequisiteDay >= entry.daysAfter;
        }

        public DailyEventDefinition? FirstUnreadMail(FarmDate date, Func<string, int> completedOn)
        {
            DailyEventDefinition? first = null;
            int firstDeliveryDay = int.MaxValue;
            foreach (DailyEventDefinition entry in Events)
            {
                if (entry.kind != "mail" || !IsAvailable(entry, date, completedOn)) continue;
                int deliveryDay = entry.Repeats ? date.Day - date.DayOfYear + entry.day : entry.day;
                if (entry.requires.Length > 0)
                    deliveryDay = Math.Max(deliveryDay, completedOn(byId[entry.requires].Key(date)) + entry.daysAfter);
                if (deliveryDay >= firstDeliveryDay) continue;
                firstDeliveryDay = deliveryDay;
                first = entry;
            }
            return first;
        }

        public bool IsFestivalDay(FarmDate date) => festivalDays.Contains(date.DayOfYear);
    }
}
