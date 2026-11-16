using UnityEngine;
using UnityEngine.UI;
using DailyEvents;

/// <summary>Updates the authored DayTimeHud prefab using the original clock atlas.</summary>
public sealed class DayTimeHud : MonoBehaviour
{
    [SerializeField] private ClockBitmapText dateText;
    [SerializeField] private ClockBitmapText timeText;
    [SerializeField] private Image weatherIcon;
    [SerializeField] private Image seasonIcon;
    [SerializeField] private RectTransform clockHand;
    [SerializeField] private Image[] moneyDigits = new Image[8];
    [SerializeField] private Sprite[] weatherSprites = new Sprite[8];
    [SerializeField] private Sprite[] seasonSprites = new Sprite[4];
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    private FullInventoryMenuUI inventoryMenu;
    private int lastDay = -1;
    private int lastTime = -1;
    private int lastMoney = int.MinValue;
    private int lastWeather = -1;
    private static readonly string[] Weekdays = { "Mon.", "Tue.", "Wed.", "Thu.", "Fri.", "Sat.", "Sun." };

    private static DayTimeHud active;

    /// <summary>Screen rectangle (GUI coordinates) of the HUD money frame, for menus that redraw it on top.</summary>
    internal static Rect? MoneyFrameScreenRect
    {
        get
        {
            if (active == null || !active.isActiveAndEnabled) return null;
            var frame = active.transform.Find("Money Frame") as RectTransform;
            if (frame == null) return null;
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Canvas canvas = frame.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
        }
    }

    private void OnEnable() => active = this;

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private void Start()
    {
        inventoryMenu = GetComponentInParent<FullInventoryMenuUI>();
        Refresh(true);
    }

    private void Update() => Refresh(false);

    private void Refresh(bool force)
    {
        FarmDayClock clock = FarmDayClock.Instance;
        if (clock == null || dateText == null || timeText == null || weatherIcon == null ||
            seasonIcon == null || clockHand == null || moneyDigits == null || moneyDigits.Length != 8)
            return;

        int day = Mathf.Max(1, clock.Day);
        float clockTicks = clock.DayProgress * 120f;
        int tenMinuteStep = Mathf.FloorToInt(clockTicks);
        int minutesSinceMidnight = (6 * 60 + tenMinuteStep * 10) % (24 * 60);
        int hour = minutesSinceMidnight / 60;
        int minute = minutesSinceMidnight % 60;
        int money = inventoryMenu == null ? 0 : inventoryMenu.CurrentFunds;

        if (force || day != lastDay)
        {
            dateText.SetText($"{Weekdays[(day - 1) % 7]} {((day - 1) % 28) + 1}");
            int season = ((day - 1) / 28) % 4;
            if (season < seasonSprites.Length) seasonIcon.sprite = seasonSprites[season];
            lastDay = day;
        }
        // Weather icon display index.
        int weatherIndex = clock.CurrentWeather switch
        {
            FarmWeather.Rain => 4,
            FarmWeather.Storm => 5,
            FarmWeather.Festival => 1,
            FarmWeather.Snow => 7,
            FarmWeather.Wind => clock.SeasonIndex == 2 ? 6 : 3,
            _ => 2
        };
        if ((force || weatherIndex != lastWeather) && weatherIndex < weatherSprites.Length)
        {
            weatherIcon.sprite = weatherSprites[weatherIndex];
            lastWeather = weatherIndex;
        }
        if (force || tenMinuteStep != lastTime)
        {
            timeText.SetText($"{(hour % 12 == 0 ? 12 : hour % 12)}:{minute:00} {(hour < 12 ? "am" : "pm")}");
            lastTime = tenMinuteStep;
        }
        // DayTimeMoneyBox.draw rotates around (3,17) from the sprite's top left.
        int time = clock.TimeOfDay;
        int minuteTens = time % 100 / 10;
        float dialTime = time - time % 100 + (int)(minuteTens * 16.66f) +
            (clockTicks - tenMinuteStep) * 16.6f;
        clockHand.localEulerAngles = new Vector3(0f, 0f, -180f - 180f * Mathf.Clamp01((dialTime - 600f) / 2000f));

        if (force || money != lastMoney)
        {
            string digits = Mathf.Max(0, money).ToString();
            for (int i = 0; i < moneyDigits.Length; i++)
            {
                Image digitImage = moneyDigits[i];
                if (digitImage == null) continue;
                int sourceIndex = i - (moneyDigits.Length - digits.Length);
                bool visible = sourceIndex >= 0 && sourceIndex < digits.Length;
                digitImage.gameObject.SetActive(visible);
                if (visible) digitImage.sprite = digitSprites[digits[sourceIndex] - '0'];
            }
            lastMoney = money;
        }
    }

}
