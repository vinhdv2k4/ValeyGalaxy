#nullable enable
using System;

namespace DailyEvents
{
    /// <summary>The valley weather rules.</summary>
    public static class FarmWeather
    {
        public const int Clear = 0, Rain = 1, Wind = 2, Storm = 3, Festival = 4, Snow = 5;

        public static bool IsRain(int weather) => weather is Rain or Storm;

        public static int ForDay(int absoluteDay, Guid saveId, DailyEventCalendar calendar)
        {
            var date = new FarmDate(absoluteDay);
            if (calendar.IsFestivalDay(date)) return Festival;
            if (date.Day == 3) return Rain;
            if (date.Day <= 4 || date.DayOfSeason == 1 ||
                date.SeasonIndex == 3 && date.DayOfSeason is >= 14 and <= 16) return Clear;
            if (date.SeasonIndex == 1 && date.DayOfSeason % 13 == 0) return Storm;

            // Rolls tomorrow's weather using the current day's season and date.
            var previous = new FarmDate(date.Day - 1);
            uint state = Seed(saveId, previous.Day);
            double chance = previous.SeasonIndex switch
            {
                1 => previous.DayOfSeason > 1 ? 0.12 + previous.DayOfSeason * 0.003 : 0.0,
                3 => 0.63,
                _ => 0.183
            };
            if (Sample(ref state) < chance)
            {
                if (previous.SeasonIndex == 3) return Snow;
                bool lightning = previous.SeasonIndex == 1 && Sample(ref state) < 0.85 ||
                    Sample(ref state) < 0.25 && previous.DayOfSeason > 2 && previous.Day > 27;
                return lightning ? Storm : Rain;
            }
            bool wind = previous.Day > 2 && (previous.SeasonIndex == 0 && Sample(ref state) < 0.2 ||
                previous.SeasonIndex == 2 && Sample(ref state) < 0.6);
            return wind ? Wind : Clear;
        }

        private static uint Seed(Guid saveId, int day)
        {
            // A stable roll for each save and day, including after relogging.
            uint seed = 2166136261;
            foreach (byte part in saveId.ToByteArray())
                seed = unchecked((seed ^ part) * 16777619);
            return unchecked((seed ^ (uint)day) * 16777619) | 1u;
        }

        private static double Sample(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state >> 8) / 16777216.0;
        }
    }
}
