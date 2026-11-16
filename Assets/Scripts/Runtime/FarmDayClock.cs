using System;
using DailyEvents;
using UnityEngine;

/// <summary>Tracks game time and holds at 02:00 until the player starts a new day.</summary>
public sealed class FarmDayClock : MonoBehaviour
{
    // Reference realMilliSecondsPerGameTenMinutes = 7000.
    // 06:00 to 02:00 is 120 ten-minute steps.
    [SerializeField, Min(1f)] private float secondsPerDay = 840f;
    [SerializeField] private int day = 1;
    [SerializeField] private int songIndex = 3;
    [SerializeField] private float dailyLuck = 0.001f;
    private float elapsed;
    private int weatherToday, weatherForTomorrow, weatherSnapshotDay;
    private static FarmDayClock instance;

    public static FarmDayClock Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                instance = FindAnyObjectByType<FarmDayClock>();
                if (instance == null) instance = new GameObject("Farm Day Clock").AddComponent<FarmDayClock>();
            }
            return instance;
        }
    }

    public int Day
    {
        get
        {
#if UNITY_EDITOR
            if (previewDay > 0) return previewDay;
#endif
            return day;
        }
    }
    internal bool IsChangingDay { get; set; }
    internal bool IsCutscenePaused { get; set; }
    public int WeatherForTomorrow
    {
        get
        {
#if UNITY_EDITOR
            if (previewDay > 0) return LocalWeather(previewDay + 1);
#endif
            return weatherForTomorrow;
        }
    }
    public bool IsRainingToday => FarmWeather.IsRain(CurrentWeather);
    internal bool IsSnowingToday => CurrentWeather == FarmWeather.Snow;
    internal bool IsRainingOutdoors(GameLocation location) =>
        IsRainingToday && location != null && location.IsOutdoors &&
        location.LocationName is not ("Desert" or "Summit" or "Mine") &&
        !MineManager.IsMineFloor(location.LocationName);
    internal int CurrentWeather
    {
        get
        {
#if UNITY_EDITOR
            if (previewDay > 0) return LocalWeather(previewDay);
#endif
            return weatherToday;
        }
    }
    public int SongIndex => Mathf.Clamp(songIndex, 1, 3);
    public float DailyLuck => dailyLuck;
    public float DayProgress
    {
        get
        {
#if UNITY_EDITOR
            if (previewDay > 0) return Mathf.Clamp01((previewTime / 100 * 60 + previewTime % 100 - 360) / 1200f);
#endif
            return Mathf.Clamp01(elapsed / Mathf.Max(1f, secondsPerDay));
        }
    }
    public float SecondsPerGameMinute => Mathf.Max(1f, secondsPerDay) / 1200f;
    public int TimeOfDay
    {
        get
        {
#if UNITY_EDITOR
            if (previewDay > 0) return previewTime;
#endif
            int minutes = 360 + Mathf.FloorToInt(DayProgress * 120f) * 10;
            return minutes / 60 * 100 + minutes % 60;
        }
    }
    /// <summary>Resumes a saved session's clock; inverse of TimeOfDay.</summary>
    internal void RestoreTimeOfDay(int time)
    {
        int minutes = Mathf.Clamp(time / 100 * 60 + time % 100, 360, 1560);
        elapsed = Mathf.Clamp01((minutes - 360) / 1200f) * Mathf.Max(1f, secondsPerDay);
        TimeTick?.Invoke();
    }

    public event Action<int> DayStarted;
    public event Action TimeTick;
    internal void RestoreDay(int serverDay, OnlineFarmWeather weather)
    {
        if (serverDay < day) return;
        int previousWeather = weatherToday;
        if (serverDay > day)
        {
            day = serverDay;
            songIndex = (day + 1) % 3 + 1;
            elapsed = 0f;
            weatherToday = LocalWeather(day);
            weatherForTomorrow = LocalWeather(day + 1);
        }
        if (weather != null)
        {
            weatherSnapshotDay = day;
            weatherToday = weather.today;
            weatherForTomorrow = weather.tomorrow;
        }
        if (weatherToday != previousWeather) GameAudio.RefreshLocationMusic();
    }
    public FarmDate Date => new(Day);
    public int SeasonIndex => Date.SeasonIndex;
    public string CurrentSeason => Date.Season;

    private static int LocalWeather(int absoluteDay) =>
        FarmWeather.ForDay(absoluteDay, Guid.Empty, DailyEventAssets.Calendar);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            weatherToday = LocalWeather(day);
            weatherForTomorrow = LocalWeather(day + 1);
            gameObject.AddComponent<WorldRainView>();
        }
        else if (instance != this) { Destroy(gameObject); return; }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (previewDay > 0) return;
#endif
        if (IsChangingDay || IsCutscenePaused) return;
        int previous = TimeOfDay;
        elapsed = Mathf.Min(elapsed + Time.deltaTime, Mathf.Max(1f, secondsPerDay));
        if (TimeOfDay != previous) TimeTick?.Invoke();
    }

    [ContextMenu("Advance Day")]
    public void AdvanceDay()
    {
#if UNITY_EDITOR
        if (previewDay > 0) return;
#endif
        elapsed = 0f;
        day++;
        weatherToday = weatherSnapshotDay == day - 1 ? weatherForTomorrow : LocalWeather(day);
        weatherForTomorrow = LocalWeather(day + 1);
        if (!IsRainingToday)
            songIndex = (day - 1) % 28 == 0 ? 1 : songIndex % 3 + 1;
        dailyLuck = Mathf.Min(0.1f, UnityEngine.Random.Range(-100, 101) / 1000f);
        DayStarted?.Invoke(day);
        TimeTick?.Invoke();
        GameAudio.RefreshLocationMusic();
    }

#if UNITY_EDITOR
    private int previewDay, previewTime;

    internal void SetEventPreviewTime(int absoluteDay, int time)
    {
        previewDay = absoluteDay;
        previewTime = time;
        GameAudio.RefreshLocationMusic();
    }

    internal void ClearEventPreviewTime()
    {
        previewDay = 0;
        GameAudio.RefreshLocationMusic();
    }
#endif
}
