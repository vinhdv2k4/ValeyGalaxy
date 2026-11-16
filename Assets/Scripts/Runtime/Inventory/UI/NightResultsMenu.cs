using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NightResultsMenu : MonoBehaviour
{
    private static readonly string[] Skills = { "Trồng trọt", "Câu cá", "Hái lượm", "Khai khoáng", "Chiến đấu" };
    private static readonly int[] SkillSprites = { 10, 20, 60, 30, 120 };
    private static readonly string[] Categories = { "Trồng trọt", "Hái lượm", "Câu cá", "Khai khoáng", "Khác", "Tổng" };
    private static readonly string[] Seasons = { "xuân", "hè", "thu", "đông" };
    private static readonly (string Name, string Description)[] Professions =
    {
        ("Người chăn nuôi", "Sản phẩm chăn nuôi bán được thêm 20%."),
        ("Người trồng trọt", "Nông sản bán được thêm 10%."),
        ("Chuyên gia gia cầm", "Thân với vật nuôi trong chuồng nhỏ nhanh hơn, trứng nở nhanh gấp đôi."),
        ("Người chăn cừu", "Thân với vật nuôi trong chuồng lớn nhanh hơn, cừu cho len thường xuyên hơn."),
        ("Nghệ nhân", "Đồ thủ công như rượu, phô mai và dầu bán được thêm 40%."),
        ("Nhà nông học", "Cây trồng sinh trưởng nhanh hơn 10%."),
        ("Ngư dân", "Cá bán được thêm 25%."),
        ("Người đặt lờ", "Chế tạo lờ bắt cua tốn ít nguyên liệu hơn."),
        ("Cao thủ câu cá", "Cá bán được thêm 50%."),
        ("Hải tặc", "Cơ hội câu được rương kho báu tăng gấp đôi."),
        ("Thủy thủ", "Lờ bắt cua không vớt phải rác."),
        ("Bậc thầy mồi", "Lờ bắt cua không cần mồi."),
        ("Người làm rừng", "Thu thêm 25% gỗ khi chặt cây."),
        ("Người hái lượm", "Có cơ hội nhặt được gấp đôi vật phẩm hái lượm."),
        ("Tiều phu", "Cây thường có thể rơi gỗ cứng."),
        ("Người lấy nhựa", "Xi-rô bán được thêm 25%."),
        ("Nhà thực vật học", "Vật phẩm hái lượm luôn đạt chất lượng cao nhất."),
        ("Người dò tìm", "Hiện dấu chỉ tới vật phẩm có thể hái lượm."),
        ("Thợ mỏ", "Mỗi mạch quặng cho thêm một quặng."),
        ("Nhà địa chất", "Đá quý có cơ hội xuất hiện thành cặp."),
        ("Thợ rèn", "Thỏi kim loại bán được thêm 50%."),
        ("Người tìm quặng", "Cơ hội tìm thấy than tăng gấp đôi."),
        ("Nhà khai quật", "Cơ hội tìm thấy hốc đá tăng gấp đôi."),
        ("Nhà đá quý", "Đá quý bán được thêm 30%."),
        ("Đấu sĩ", "Gây thêm 10% sát thương và nhận thêm 15 máu tối đa."),
        ("Trinh sát", "Tăng 50% cơ hội đánh chí mạng."),
        ("Chiến binh", "Gây thêm 15% sát thương."),
        ("Hộ vệ", "Nhận thêm 25 máu tối đa."),
        ("Nhào lộn", "Giảm một nửa thời gian hồi chiêu đặc biệt."),
        ("Kẻ liều lĩnh", "Đòn chí mạng gây sát thương mạnh hơn.")
    };

    private const float Width = 1120f, Height = 840f;
    // ShippingMenu: introTimer starts at 3500 ms; category i shows once it drops below 2500 - 500 i.
    private const float IntroLength = 3.5f, FirstRowTime = 1f, RowInterval = .5f;
    private const float FadeLength = .8f, PlaqueSlide = .55f, MorningSoundDelay = 1.5f;
    // The shipping screens use ShippingMenu's pixel layout on a 1080-pixel-high canvas, where
    // itemsPerCategoryPage = (1080 - 96) / 68.
    private const float UiHeight = 1080f, SpriteZoom = 3f;
    private const int PageSize = 14, TotalCategory = 5;
    private static readonly Color Maroon = new(128 / 255f, 0f, 0f);
    private static readonly Color SlateGray = new(112 / 255f, 128 / 255f, 144 / 255f);
    private static readonly Color DarkSlateGray = new(47 / 255f, 79 / 255f, 79 / 255f);
    private static readonly Color LightSlateGray = new(119 / 255f, 136 / 255f, 153 / 255f);
    private static readonly string[] RowSounds = { "harvest", "leafrustle", "button1", "hammer", "coin", "money" };
    private static readonly Rect[] SkillIcons =
    {
        new(0, 0, 16, 16), new(16, 0, 16, 16), new(80, 0, 16, 16), new(32, 0, 16, 16), new(128, 16, 16, 16)
    };
    private static readonly Rect ConfirmIcon = new(128, 256, 64, 64);
    private static readonly Rect BackIcon = new(352, 495, 12, 11);
    private static readonly Rect ForwardIcon = new(365, 495, 12, 11);
    private static readonly Color Ink = new(.32f, .13f, .045f);
    private static readonly Color Highlight = new(.16f, .48f, .18f);

    private enum Stage { Hidden, Levels, Shipping, Morning }
    private sealed class Shipment
    {
        internal ItemData Item;
        internal string Id;
        internal long Quantity;
        internal int UnitPrice;
        internal long Value => Quantity * UnitPrice;
    }

    private readonly List<(int Skill, int Level)> levels = new();
    private readonly List<(int Skill, int Level, string Name, int Tile, bool Big, bool Cooking)> recipes = new();
    private readonly List<Shipment>[] categories = { new(), new(), new(), new(), new() };
    private readonly long[] totals = new long[6];

    private Stage stage;
    private OnlineGameState online;
    private FarmDayClock clock;
    private NetworkCharacterStats stats;
    private Texture2D buffsAtlas;
    private Texture2D objectsAtlas;
    private Texture2D craftablesAtlas;
    private GUIStyle text;
    private string account, date, nextDate;
    private bool rainAtNight, winter, winterFeast, fullMoon, ufo, dawnStarted, dawnSoundPlayed;
    private int day, nextLevel, selectedProfession, detailCategory, detailPage, revealedRows, moonClicks;
    private bool savingProfession;
    private string professionError;
    private float introElapsed, dialTickAt, morningElapsed, levelOpenedAt, moonShakeUntil;
    // Night sky (ShippingMenu.update): real time since the screen opened and since the intro ended.
    private float skyStartedAt, introEndedAt = -1f, santaY, ufoY;
    private bool santaSpawned, ufoSpawned;
    private readonly List<SkySprite> skySprites = new();

    /// <summary>A TemporaryAnimatedSprite of the night sky (shooting star, birds, sleigh, UFO).</summary>
    private sealed class SkySprite
    {
        internal Rect Source;
        internal int Frames = 1;
        internal float Interval = 1f, Scale = 4f, Rotation, BornAt, Life;
        internal Vector2 Position, Velocity;
        internal Color Color = Color.white;
        internal bool Flip, Bob;
    }
    private bool IntroFinished => introElapsed >= IntroLength;
    private bool MorningSaved => online == null || online.Snapshot?.gameDay >= day + 1;
    private bool CanRetry => dawnStarted && !MorningSaved && online != null && !online.IsSettlingDay &&
        morningElapsed >= FadeLength + PlaqueSlide + MorningSoundDelay;

    internal static void ObserveInitialSkillLevels(string user, int[] levels)
    {
        if (string.IsNullOrEmpty(user) || levels == null) return;
        for (int skill = 0; skill < Mathf.Min(levels.Length, Skills.Length); skill++)
        {
            string key = SkillKey(user, skill);
            if (!PlayerPrefs.HasKey(key)) PlayerPrefs.SetInt(key, levels[skill]);
        }
        PlayerPrefs.Save();
    }

    private static string SkillKey(string user, int skill) => $"NightResults.{user}.Skill{skill}";
    internal static int ShownSkillLevel(string user, int[] levels, int skill)
    {
        if (levels == null || skill >= levels.Length) return 0;
        int reached = Mathf.Clamp(levels[skill], 0, 10);
        return string.IsNullOrEmpty(user) ? reached : Mathf.Clamp(PlayerPrefs.GetInt(SkillKey(user, skill), reached), 0, reached);
    }
    internal static string ProfessionKey(string user, int skill, int level) => $"NightResults.{user}.Profession{skill}.{level}";

    /// <summary>
    /// Admin test tool: marks a skill level as already shown, so it applies now instead of
    /// waiting for the level-up screens after sleeping.
    /// </summary>
    internal static void SetShownSkillLevel(string user, int skill, int level)
    {
        if (string.IsNullOrEmpty(user) || skill < 0 || skill >= Skills.Length) return;
        PlayerPrefs.SetInt(SkillKey(user, skill), Mathf.Clamp(level, 0, 10));
        PlayerPrefs.Save();
    }

    internal static string SkillName(int skill) => skill >= 0 && skill < Skills.Length ? Skills[skill] : "";

    internal IEnumerator Show(int closingDay, OnlineShippingEntry[] shipments, OnlineGameState state)
    {
        day = Mathf.Max(1, closingDay);
        online = state;
        clock = FarmDayClock.Instance;
        stats = GetComponent<NetworkCharacterStats>();
        account = StartupSession.Api?.UserName;
        date = DateText(day);
        nextDate = DateText(day + 1);
        rainAtNight = clock?.IsRainingToday == true;
        winter = (day - 1) / 28 % 4 == 3;
        fullMoon = day % 28 == 0;
        winterFeast = winter && day % 28 == 25;
        ufo = !rainAtNight && !fullMoon && !winterFeast && UnityEngine.Random.value < .006f;
        dawnStarted = dawnSoundPlayed = false;
        nextLevel = selectedProfession = detailPage = revealedRows = moonClicks = 0;
        detailCategory = -1;
        introElapsed = dialTickAt = morningElapsed = moonShakeUntil = 0f;

        var inventoryUi = online?.InventoryUi;
        objectsAtlas = inventoryUi?.ObjectsAtlas ?? Resources.Load<Texture2D>("Maps/springobjects");
        craftablesAtlas = inventoryUi?.CraftablesAtlas;
        buffsAtlas = Resources.Load<NpcCatalog>("NpcCatalog")?.buffsIcons;
        LoadShipments(shipments);
        savingProfession = false;
        professionError = null;
        yield return MigrateLocalProfessions();
        LoadLevels(online?.Snapshot?.skillLevels);
        recipes.Clear();
        ReadRecipes(inventoryUi?.CraftingRecipes, cooking: false);
        ReadRecipes(inventoryUi?.CookingRecipes, cooking: true);
        if (levels.Count > 0) OpenLevel();
        else StartShipping();

        while (stage != Stage.Hidden) yield return null;
        online = null;
    }

    private void LoadShipments(OnlineShippingEntry[] shipments)
    {
        Array.Clear(totals, 0, totals.Length);
        foreach (var list in categories) list.Clear();
        if (shipments == null) return;
        var grouped = new Dictionary<(string Id, int UnitPrice), Shipment>();
        foreach (var entry in shipments)
        {
            if (entry == null || entry.amount <= 0 || entry.unitPrice < 0 || string.IsNullOrEmpty(entry.itemId)) continue;
            var key = (entry.itemId, entry.unitPrice);
            if (!grouped.TryGetValue(key, out var shipment))
            {
                shipment = new Shipment { Id = entry.itemId, Item = online?.ResolveItem(entry.itemId), UnitPrice = entry.unitPrice };
                grouped.Add(key, shipment);
                categories[ShippingCategory(entry.itemId)].Add(shipment);
            }
            shipment.Quantity += entry.amount;
        }

        for (int category = 0; category < categories.Length; category++)
        {
            foreach (var shipment in categories[category]) totals[category] += shipment.Value;
            totals[TotalCategory] += totals[category];
        }
    }

    private void LoadLevels(int[] earned)
    {
        levels.Clear();
        if (string.IsNullOrEmpty(account) || earned == null) return;
        for (int skill = 0; skill < Mathf.Min(earned.Length, Skills.Length); skill++)
        {
            int shown = ShownSkillLevel(account, earned, skill);
            int reached = Mathf.Clamp(earned[skill], 0, 10);
            // A level 5 or 10 already passed without a saved profession asks for one again.
            foreach (int level in new[] { 5, 10 })
                if (level <= shown && level <= reached && !HasProfessionFor(skill, level)) levels.Add((skill, level));
            for (int level = shown + 1; level <= reached; level++) levels.Add((skill, level));
        }
    }

    /// <summary>The profession picked for a skill at level 5 or 10, from the account's saved choices.</summary>
    private bool HasProfessionFor(int skill, int level)
    {
        var snapshot = online?.Snapshot;
        if (snapshot == null) return true;
        int first = skill * 6;
        return level == 5
            ? snapshot.HasProfession(first) || snapshot.HasProfession(first + 1)
            : snapshot.HasProfession(first + 2) || snapshot.HasProfession(first + 3) ||
              snapshot.HasProfession(first + 4) || snapshot.HasProfession(first + 5);
    }

    /// <summary>Professions used to be kept in PlayerPrefs on one computer; move them to the account.</summary>
    private IEnumerator MigrateLocalProfessions()
    {
        if (online?.Snapshot == null || string.IsNullOrEmpty(account)) yield break;
        for (int skill = 0; skill < Skills.Length; skill++)
            foreach (int level in new[] { 5, 10 })
            {
                string key = ProfessionKey(account, skill, level);
                if (!PlayerPrefs.HasKey(key) || HasProfessionFor(skill, level)) continue;
                bool done = false;
                online.ChooseProfession(skill, level, PlayerPrefs.GetInt(key), _ => done = true);
                while (!done) yield return null;
                PlayerPrefs.DeleteKey(key);
            }
        PlayerPrefs.Save();
    }

    private void ReadRecipes(TextAsset source, bool cooking)
    {
        if (source == null) return;
        var definitions = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(source.text);
        if (definitions == null) return;
        int conditionIndex = cooking ? 3 : 4;
        foreach (var definition in definitions)
        {
            string[] fields = definition.Value.Split('/');
            if (fields.Length <= conditionIndex ||
                !SkillRecipeRequirement.TryRead(fields[conditionIndex], out int skill, out int level)) continue;
            int tile = int.TryParse(fields[2].Split(' ')[0], out int parsed) ? parsed : -1;
            recipes.Add((skill, level, definition.Key, tile, !cooking && fields[3] == "true", cooking));
        }
    }

    internal static string DateText(int absoluteDay) =>
        $"Ngày {(absoluteDay - 1) % 28 + 1} mùa {Seasons[(absoluteDay - 1) / 28 % 4]}, Năm thứ {(absoluteDay - 1) / 112 + 1}";

    internal static int ShippingCategory(string id)
    {
        if (id.StartsWith("fish_", StringComparison.Ordinal)) return 2;
        if (id is "parsnip" or "potato" or "green_bean" or "cauliflower") return 0;
        if (id is "copper_ore" or "iron_ore" or "gold_ore" or "iridium_ore" or "coal" or "stone") return 3;
        if (!id.StartsWith("object_", StringComparison.Ordinal) || !int.TryParse(id.Substring(7), out int index))
            return 4;
        if (IsAnimalProduct(id) || index is 24 or 188 or 190 or 192 or 248 or 250 or 252 or 254 or 256 or
            258 or 260 or 262 or 264 or 266 or 268 or 270 or 271 or 272 or 274 or 276 or 278 or 280 or 282 or
            284 or 300 or 304 or 398 or 400 or 433 or 454 or 591 or 593 or 595) return 0;
        if (index is 16 or 18 or 20 or 22 or 78 or 257 or 259 or 281 or 283 or 296 or 396 or 402 or 404 or 406 or
            408 or 410 or 412 or 414 or 416 or 418 or 420 or 422 or 430) return 1;
        if (index is >= 128 and <= 165 or 267 or 269 or 372 or 392 or 393 or 394 or 397 or
            >= 698 and <= 707 or 715 or 716 or 717 or 718 or 719 or 720 or 721 or 722 or 723) return 2;
        if (index is 60 or 62 or 64 or 66 or 68 or 70 or 72 or 74 or 80 or 82 or 84 or 86 or
            378 or 380 or 382 or 384 or 386 or 390 or >= 535 and <= 578 or 749 or 848 or 909) return 3;
        return 4;
    }

    private static bool IsAnimalProduct(string id) => id is "object_174" or "object_176" or "object_180" or
        "object_182" or "object_184" or "object_186" or "object_289" or "object_305" or "object_306" or
        "object_307" or "object_308" or "object_424" or "object_426" or "object_428" or "object_436" or
        "object_437" or "object_438" or "object_440" or "object_442" or "object_444" or "object_446";

    private void Update()
    {
        if (stage == Stage.Hidden) return;
        var key = Keyboard.current;
        var pad = Gamepad.current;
        bool confirm = key?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true;
        bool left = key?.leftArrowKey.wasPressedThisFrame == true || pad?.dpad.left.wasPressedThisFrame == true;
        bool right = key?.rightArrowKey.wasPressedThisFrame == true || pad?.dpad.right.wasPressedThisFrame == true;
        bool back = key?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true;
        if (stage == Stage.Levels)
        {
            if (savingProfession) return;
            var (skill, level) = levels[nextLevel];
            if (level is 5 or 10)
            {
                if (left) selectedProfession = 0;
                else if (right) selectedProfession = 1;
                if (confirm) AcceptLevel(FirstProfession(skill, level) + selectedProfession);
            }
            else if (confirm) AcceptLevel();
        }
        else if (stage == Stage.Shipping)
        {
            UpdateShipping();
            if (!IntroFinished) return;
            if (detailCategory >= 0)
            {
                if (back || confirm) CloseDetails();
                else if (left) PreviousDetail();
                else if (right && (detailPage + 1) * PageSize < categories[detailCategory].Count) ChangePage(1);
            }
            else if (confirm) BeginMorning();
        }
        else UpdateMorning(confirm);
    }

    private void UpdateShipping()
    {
        float speed = !IntroFinished && Mouse.current?.leftButton.isPressed == true ? 3f : 1f;
        introElapsed += Time.unscaledDeltaTime * speed;
        while (revealedRows < Categories.Length && introElapsed >= RowTime(revealedRows))
        {
            string cue = RowSounds[revealedRows];
            if (revealedRows < TotalCategory && categories[revealedRows].Count == 0) cue = "stoneStep";
            else if (revealedRows == 0 && IsAnimalProduct(categories[0][0].Id)) cue = "cluck";
            GameAudio.Play(cue);
            revealedRows++;
        }
        // Only the total's MoneyDial plays sound: one "moneyDial" every max(6, 100 / (speed + 1)) frames.
        long total = totals[TotalCategory];
        float sinceTotal = introElapsed - RowTime(TotalCategory);
        if (total > 0 && sinceTotal >= dialTickAt && DialValue(total, sinceTotal) < total)
        {
            GameAudio.Play("moneyDial");
            dialTickAt = sinceTotal + Mathf.Max(6, 100 / (int)Math.Min(int.MaxValue, total / 100 + 1)) / 60f;
        }
        if (IntroFinished && introEndedAt < 0f) introEndedAt = Time.unscaledTime;
        UpdateSkySprites();
    }

    private void StartShipping()
    {
        stage = Stage.Shipping;
        skyStartedAt = Time.unscaledTime;
        introEndedAt = -1f;
        skySprites.Clear();
        santaSpawned = ufoSpawned = false;
        santaY = UnityEngine.Random.Range(0, 200);
        ufoY = UnityEngine.Random.Range(0, 200);
    }

    private static float RowTime(int category) => FirstRowTime + category * RowInterval;

    /// <summary>
    /// MoneyDial counts from 0 by speed + 1 each frame (speed = target / 100), so it reaches the
    /// total in about 100 frames, or one per frame for totals under 100.
    /// </summary>
    private static long DialValue(long target, float seconds)
    {
        if (target <= 0 || seconds <= 0f) return 0;
        long step = target / 100 + 1;
        return Math.Min(target, (long)(seconds * 60f) * step);
    }

    /// <summary>
    /// ShippingMenu.update after the intro, on clear nights except the 28th: rare shooting
    /// stars, a drifting star and birds, plus the Feast of the Winter Star sleigh and the UFO.
    /// </summary>
    private void UpdateSkySprites()
    {
        float now = Time.unscaledTime;
        float width = Screen.width / UiScale;
        // A plain loop: a lambda capturing "now" would allocate every frame.
        for (int index = skySprites.Count - 1; index >= 0; index--)
            if (now - skySprites[index].BornAt > skySprites[index].Life) skySprites.RemoveAt(index);
        // The sleigh starts 3 s after the menu opens (delayBeforeAnimationStart = 3000).
        if (winterFeast && !santaSpawned && now - skyStartedAt >= 3f)
        {
            santaSpawned = true;
            skySprites.Add(new SkySprite { Source = new Rect(640, 800, 32, 16), Frames = 2, Interval = .08f,
                Position = new Vector2(width, santaY), Velocity = new Vector2(-240f, 0f), BornAt = now,
                Life = (width + 200f) / 240f });
        }
        if (introEndedAt < 0f || rainAtNight || fullMoon || stage != Stage.Shipping) return;
        if (ufo && !ufoSpawned)
        {
            ufoSpawned = true;
            skySprites.Add(new SkySprite { Source = new Rect(640, 784, 16, 16), Frames = 4, Interval = .075f,
                Position = new Vector2(width, ufoY), Velocity = new Vector2(-180f, 0f), BornAt = now,
                Life = (width + 100f) / 180f, Bob = true });
        }
        // Chances per 60 Hz frame, scaled to this frame's length.
        float frames = Time.unscaledDeltaTime * 60f;
        bool Roll(double perFrame) => UnityEngine.Random.value < 1f - Mathf.Pow(1f - (float)perFrame, frames);
        float height = UiHeight;
        if (Roll(.001))
        {
            bool flipped = UnityEngine.Random.value < .5f;
            bool streak = UnityEngine.Random.value < .5f;
            skySprites.Add(new SkySprite
            {
                Source = streak ? new Rect(640, 826, 16, 8) : new Rect(258, 1680, 16, 16), Frames = 4, Interval = .04f,
                Rotation = streak ? 180f : 0f, Flip = flipped, BornAt = now, Life = .16f,
                Position = new Vector2(UnityEngine.Random.Range(0f, width), UnityEngine.Random.Range(0f, height / 2f)),
                Velocity = new Vector2(flipped ? -480f : 480f, 480f)
            });
        }
        else if (Roll(.0002))
        {
            skySprites.Add(new SkySprite { Source = new Rect(684, 1075, 1, 1), BornAt = now, Life = (width + 8f) / 15f,
                Position = new Vector2(width, UnityEngine.Random.Range(4f, 256f)), Velocity = new Vector2(-15f, 0f),
                Color = new Color(1f, 1f, 1f, Mathf.Min(1f, .25f + UnityEngine.Random.value)) });
        }
        else if (Roll(.00005))
        {
            int flock = UnityEngine.Random.Range(1, 4);
            for (int bird = 0; bird < flock; bird++)
                foreach (int side in new[] { -1, 1 })
                {
                    float interval = UnityEngine.Random.Range(60, 101) / 1000f;
                    skySprites.Add(new SkySprite { Source = new Rect(640, 752, 16, 16), Frames = 4, Interval = interval,
                        Color = Color.black, BornAt = now, Life = (width + 200f) / 60f, Velocity = new Vector2(-60f, 0f),
                        Position = new Vector2(width + (bird + 1) * UnityEngine.Random.Range(15, 18),
                            height - 192f + side * (bird + 1) * 20f) });
                }
        }
    }

    private void OpenLevel()
    {
        stage = Stage.Levels;
        levelOpenedAt = Time.unscaledTime;
        GameAudio.Play("newRecipe");
    }

    private int FirstProfession(int skill, int level)
    {
        int first = skill * 6;
        // LevelUpMenu.update: level 10 offers the two professions that follow the level 5 choice.
        return level == 5 ? first : first + (online?.Snapshot?.HasProfession(first) == true ? 2 : 4);
    }

    private void AcceptLevel(int profession = -1)
    {
        if (savingProfession) return;
        var (skill, level) = levels[nextLevel];
        if (profession >= 0 && online != null)
        {
            // The choice is saved to the account before moving on; sale bonuses are worked out on the server.
            savingProfession = true;
            professionError = null;
            online.ChooseProfession(skill, level, profession, error =>
            {
                savingProfession = false;
                if (error != null)
                {
                    professionError = error;
                    GameAudio.Play("cancel");
                    return;
                }
                FinishLevel(skill, level);
            });
            return;
        }
        FinishLevel(skill, level);
    }

    private void FinishLevel(int skill, int level)
    {
        nextLevel++;
        selectedProfession = 0;
        professionError = null;
        string key = SkillKey(account, skill);
        PlayerPrefs.SetInt(key, Mathf.Max(PlayerPrefs.GetInt(key), level));
        PlayerPrefs.Save();
        stats?.ApplySkillProgress(online?.Snapshot?.skillLevels, account, online?.Snapshot?.professions);
        stats?.RestoreHealth(stats.MaxHealth);
        stats?.RestoreStamina(stats.MaxStamina);
        GameAudio.Play("smallSelect");
        if (nextLevel < levels.Count) OpenLevel();
        else StartShipping();
    }

    private void BeginMorning()
    {
        stage = Stage.Morning;
        // ShippingMenu.okClicked.
        GameAudio.Play("bigDeSelect");
    }

    private void UpdateMorning(bool confirm)
    {
        morningElapsed += Time.unscaledDeltaTime;
        const float morningStart = FadeLength + PlaqueSlide;
        if (!dawnStarted && morningElapsed >= morningStart)
        {
            dawnStarted = true;
            clock?.AdvanceDay();
            online?.RequestDaySettlement(day + 1);
            GameAudio.Play("newRecipe");
        }
        if (!dawnSoundPlayed && morningElapsed >= morningStart + MorningSoundDelay)
        {
            dawnSoundPlayed = true;
            GameAudio.Play(clock?.IsRainingToday == true ? "rainsound" : "rooster");
        }
        if (CanRetry && confirm) online.RequestDaySettlement(day + 1);
        if (MorningSaved && dawnSoundPlayed && morningElapsed >= morningStart + MorningSoundDelay + .3f)
            stage = Stage.Hidden;
    }

    private void OnGUI()
    {
        if (stage == Stage.Hidden) return;
        GUI.depth = -10000;
        Rect screen = new(0, 0, Screen.width, Screen.height);
        if (stage == Stage.Levels) Fill(screen, new Color(0, 0, 0, .55f));
        else if (stage != Stage.Shipping || detailCategory < 0)
        {
            DrawSky();
            if (stage == Stage.Morning)
                Fill(screen, new Color(0, 0, 0, Mathf.Clamp01(morningElapsed / FadeLength)));
        }

        Matrix4x4 previous = GUI.matrix;
        try
        {
            if (stage == Stage.Shipping)
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f));
                if (detailCategory >= 0) DrawDetails();
                else DrawSummary();
                return;
            }
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2f,
                (Screen.height - Height * scale) / 2f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            if (stage == Stage.Levels) DrawLevel();
            else DrawMorning();
        }
        finally { GUI.matrix = previous; }
    }

    private void DrawLevel()
    {
        var (skill, level) = levels[nextLevel];
        bool choose = level is 5 or 10;
        float height = choose ? 494f : 256f + 46f * (System.Linq.Enumerable.Count(LevelNotes(skill, level)) - 1);
        foreach (var recipe in recipes)
            if (!choose && recipe.Skill == skill && recipe.Level == level)
                height += recipe.Big ? 70f : 52f;
        Rect panel = new(80, (Height - height) * .5f, 960, height);
        DrawStars(panel);
        Box(panel);
        StardewUiAtlas.DrawCursorsSprite(new Rect(444, panel.y - 62, 232, 88), new Rect(363, 87, 58, 22));
        DrawSkillIcon(skill, new Rect(144, panel.y + 40, 64, 64));
        DrawSkillIcon(skill, new Rect(912, panel.y + 40, 64, 64));
        BitmapLabel(new Rect(232, panel.y + 49, 656, 50), $"{Skills[skill]} cấp {level}", 3f);

        if (choose)
        {
            Label(new Rect(232, panel.y + 103, 656, 46), "Chọn một nghề để tiếp tục", 22);
            DrawPartitions(panel);
            int first = FirstProfession(skill, level);
            float bottom = panel.yMax - 62;
            Rect left = new(panel.x + 52, panel.y + 214, 408, bottom - panel.y - 214);
            Rect right = new(panel.center.x + 22, left.y, 408, left.height);
            if (DrawProfession(left, first, selectedProfession == 0)) AcceptLevel(first);
            else if (DrawProfession(right, first + 1, selectedProfession == 1)) AcceptLevel(first + 1);
            Label(new Rect(panel.x + 64, panel.yMax - 49, 832, 30),
                savingProfession ? "Đang lưu lựa chọn..." :
                professionError ?? "Lựa chọn nghề được lưu vào tài khoản và không thể đổi lại.", 17);
        }
        else
        {
            float y = panel.y + 121;
            foreach (string line in LevelNotes(skill, level))
            {
                Label(new Rect(panel.x + 56, y, 848, 40), line, 24);
                y += 46;
            }
            foreach (var recipe in recipes)
            {
                if (recipe.Skill != skill || recipe.Level != level) continue;
                float row = recipe.Big ? 70f : 52f;
                Label(new Rect(panel.x + 48, y, 774, row),
                    (recipe.Cooking ? "Công thức nấu ăn mới  " : "Đã học chế tạo  ") + recipe.Name, 22);
                if (recipe.Tile >= 0)
                    DrawRecipeIcon(recipe.Big, recipe.Tile, new Rect(panel.xMax - 154, y + 3, 42, recipe.Big ? 62 : 42));
                y += row;
            }
            if (Button(new Rect(panel.xMax - 103, panel.yMax - 89, 64, 64), ConfirmIcon)) AcceptLevel();
        }
    }

    private static IEnumerable<string> LevelNotes(int skill, int level)
    {
        switch (skill)
        {
            case 0: yield return "+1 độ thành thạo cuốc"; yield return "+1 độ thành thạo bình tưới"; break;
            case 1: yield return "+1 độ thành thạo cần câu"; break;
            case 2:
                yield return "+1 độ thành thạo rìu";
                if (level == 1) yield return "Cây đôi khi rơi hạt giống";
                if (level is 4 or 8) yield return "+1 quả mọng mỗi bụi";
                break;
            case 3: yield return "+1 độ thành thạo cuốc chim"; break;
            case 4: yield return "+5 máu tối đa"; break;
        }
    }

    private void DrawSkillIcon(int skill, Rect rect)
    {
        if (buffsAtlas != null) DrawAtlas(buffsAtlas, rect, SkillIcons[skill]);
        else StardewUiAtlas.DrawCursorsSprite(rect, new Rect(SkillSprites[skill], 428, 10, 10));
    }

    private void DrawRecipeIcon(bool big, int index, Rect rect)
    {
        Texture2D atlas = big ? craftablesAtlas : objectsAtlas;
        if (atlas == null || atlas.width < 16) return;
        int columns = atlas.width / 16;
        int rowHeight = big ? 32 : 16;
        int y = index / columns * rowHeight;
        if (y + rowHeight <= atlas.height)
            DrawAtlas(atlas, rect, new Rect(index % columns * 16, y, 16, rowHeight));
    }

    private static void DrawPartitions(Rect panel)
    {
        float y = panel.y + 162f;
        float center = panel.center.x - 32f;
        StardewUiAtlas.DrawMenuTile(4, new Rect(panel.x, y, 64, 64));
        StardewUiAtlas.DrawMenuTile(6, new Rect(panel.x + 64, y, panel.width - 128, 64));
        StardewUiAtlas.DrawMenuTile(7, new Rect(panel.xMax - 64, y, 64, 64));
        StardewUiAtlas.DrawMenuTile(59, new Rect(center, y, 64, 64));
        StardewUiAtlas.DrawMenuTile(63, new Rect(center, y + 64, 64, panel.yMax - y - 128));
        StardewUiAtlas.DrawMenuTile(62, new Rect(center, panel.yMax - 64, 64, 64));
    }

    private bool DrawProfession(Rect card, int profession, bool selected)
    {
        bool hover = card.Contains(Event.current.mousePosition);
        var data = Professions[profession];
        if (hover || selected) Fill(card, new Color(1f, .92f, .57f, hover ? .25f : .14f));
        Label(new Rect(card.x + 20, card.y + 23, card.width - 104, 58),
            data.Name, 28, hover || selected ? Highlight : Ink, TextAnchor.MiddleLeft, bold: true, wrap: true);
        StardewUiAtlas.DrawCursorsSprite(new Rect(card.xMax - 79, card.y + 20, 64, 64),
            new Rect(profession % 6 * 16, 624 + profession / 6 * 16, 16, 16));
        Label(new Rect(card.x + 20, card.y + 102, card.width - 40, card.height - 104),
            data.Description, 24, align: TextAnchor.UpperLeft, wrap: true);
        return GUI.Button(card, GUIContent.none, GUIStyle.none);
    }

    private void DrawStars(Rect panel)
    {
        float elapsed = Time.unscaledTime - levelOpenedAt;
        for (int i = 0; i < 24; i++)
        {
            float phase = Mathf.Repeat(elapsed * .45f + i * .137f, 1f);
            float x = panel.x - 30 + Mathf.Repeat(i * .618f, 1f) * (panel.width + 56);
            float y = Mathf.Lerp(panel.y - 86, panel.yMax + 26, phase);
            int frame = (int)(phase * 12f) % 7;
            StardewUiAtlas.DrawCursorsSprite(new Rect(x, y, 16, 16), new Rect(364 + 5 * frame, 79, 5, 5),
                new Color(1, 1, 1, Mathf.Sin(phase * Mathf.PI)));
        }
    }

    private static float UiScale => Mathf.Max(.01f, Screen.height / UiHeight);

    /// <summary>
    /// ShippingMenu.draw backdrop on the 1080-pixel canvas: everything fades in over the intro
    /// (1 - introTimer / 3500); clear nights show the night hills, rainy ones three cloud layers.
    /// </summary>
    private void DrawSky()
    {
        float scale = UiScale;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            float w = Screen.width / scale, h = UiHeight;
            float fade = Mathf.Clamp01(introElapsed / IntroLength);
            float weatherX = (Time.unscaledTime - skyStartedAt) * 30f;
            int hillRow = winter ? 1034 : 737;
            Fill(new Rect(0, 0, w, h), Color.black);
            if (rainAtNight)
            {
                Sky(new Rect(0, 0, w, h), new Rect(639, 858, 1, 184), winter ? LightSlateGray : WithAlpha(SlateGray, fade));
                Clouds(w, 32f, weatherX / 2f, WithAlpha(DarkSlateGray, fade));
                Strip(w, h - 192f, new Rect(0, hillRow, 639, 48),
                    winter ? new Color(1, 1, 1, .25f * Mathf.Clamp01(fade - .5f)) : new Color(30 / 255f, 62 / 255f, 50 / 255f, Mathf.Clamp01(fade - .5f)), true);
                Strip(w, h - 128f, new Rect(0, hillRow, 639, 32),
                    winter ? new Color(1, 1, 1, .5f * fade) : new Color(30 / 255f, 62 / 255f, 50 / 255f, fade), false);
                Sky(new Rect(160, h - 104, 40, 40), new Rect(653, 880, 10, 10), new Color(1, 1, 1, fade));
                Clouds(w, -32f, weatherX, WithAlpha(SlateGray, .85f * fade));
                if (stage == Stage.Shipping) DrawSkyEffects(h);
                Clouds(w, -128f, weatherX * 1.5f, WithAlpha(LightSlateGray, fade));
            }
            else
            {
                Sky(new Rect(0, 0, w, h), new Rect(639, 858, 1, 184), new Color(1, 1, 1, fade));
                for (float x = 0; x < w; x += 2556f)
                    Sky(new Rect(x, 0, 2556, 780), new Rect(0, 1453, 639, 195), new Color(1, 1, 1, fade));
                if (fullMoon) DrawMoon(w, fade);
                Strip(w, h - 192f, new Rect(0, hillRow, 639, 48),
                    winter ? new Color(1, 1, 1, .25f * Mathf.Clamp01(fade - .35f)) : new Color(0, 20 / 255f, 40 / 255f, Mathf.Clamp01(fade - .35f)), true);
                Strip(w, h - 128f, new Rect(0, hillRow, 639, 32),
                    winter ? new Color(1, 1, 1, .5f * fade) : new Color(0, 32 / 255f, 20 / 255f, fade), false);
                Sky(new Rect(160, h - 104, 40, 40), new Rect(653, 880, 10, 10), new Color(1, 1, 1, fade));
                if (stage == Stage.Shipping) DrawSkyEffects(h);
            }
        }
        finally { GUI.matrix = previous; }
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, Mathf.Clamp01(alpha));

    /// <summary>One 61x53 cloud every 244 px from -244, scrolled by the weather offset.</summary>
    private static void Clouds(float width, float y, float offset, Color color)
    {
        for (float x = -244f; x < width + 244f; x += 244f)
            Sky(new Rect(x + offset % 244f, y, 244, 212), new Rect(643, 1142, 61, 53), color);
    }

    /// <summary>A 639 px hill strip at 4x, drawn every 2556 px like the original's two copies.</summary>
    private static void Strip(float width, float y, Rect source, Color color, bool flip)
    {
        for (float x = 0; x < width; x += 2556f)
            Sky(new Rect(x, y, 2556, source.height * 4f), source, color, flip);
    }

    private static void Sky(Rect destination, Rect source, Color color, bool flipX = false)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null || Event.current.type != EventType.Repaint || color.a <= 0f) return;
        Rect uv = StardewUiAtlas.ToUvCursors(source);
        if (flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTextureWithTexCoords(destination, cursors, uv);
        GUI.color = previous;
    }

    /// <summary>
    /// Chimney smoke (a 4 px puff every 50 ms rising and drifting left, fading over 400 frames)
    /// and the rare sky sprites. Neither shows before the intro ends or on the 28th.
    /// </summary>
    private void DrawSkyEffects(float h)
    {
        if (Event.current.type != EventType.Repaint) return;
        float now = Time.unscaledTime;
        if (introEndedAt >= 0f && !fullMoon)
        {
            float since = now - introEndedAt;
            Color puff = rainAtNight ? SlateGray : Color.white;
            int newest = Mathf.FloorToInt(since / .05f);
            for (int k = Mathf.Max(0, newest - 134); k <= newest; k++)
            {
                float frames = (since - k * .05f) * 60f;
                float alpha = 1f - frames * .0025f;
                if (alpha <= 0f) continue;
                // motion.y = -Random(25, 75) / 100 / 4 per frame; acceleration.x = -0.001.
                float rise = (25 + (int)((uint)(k * 2654435761u) % 51u)) / 400f;
                Fill(new Rect(188f - .0005f * frames * (frames + 1f), h - 108f - rise * frames, 4f, 4f),
                    WithAlpha(puff, alpha));
            }
        }
        foreach (var sprite in skySprites)
        {
            float age = now - sprite.BornAt;
            if (age < 0f) continue;
            int frame = (int)(age / sprite.Interval) % Mathf.Max(1, sprite.Frames);
            Rect source = sprite.Source;
            source.x += frame * source.width;
            Vector2 position = sprite.Position + sprite.Velocity * age;
            // yPeriodic: +-8 px over a 1 s loop, with a little shake.
            if (sprite.Bob) position += new Vector2(UnityEngine.Random.Range(-.5f, .5f), Mathf.Sin(age * Mathf.PI * 2f) * 8f);
            var destination = new Rect(position, source.size * sprite.Scale);
            Matrix4x4 previous = GUI.matrix;
            if (sprite.Rotation != 0f) GUIUtility.RotateAroundPivot(sprite.Rotation, destination.center);
            Sky(destination, source, sprite.Color, sprite.Flip);
            GUI.matrix = previous;
        }
    }

    /// <summary>The full moon of the 28th; poking it 11 times shows a face (ShippingMenu).</summary>
    private void DrawMoon(float width, float fade)
    {
        Rect moon = new(width - 176f, 4f, 172f, 172f);
        if (stage == Stage.Shipping && detailCategory < 0 && IntroFinished && moonClicks <= 10 &&
            Event.current.type == EventType.MouseDown && Event.current.button == 0 && moon.Contains(Event.current.mousePosition))
        {
            moonClicks++;
            moonShakeUntil = Time.unscaledTime + .1f;
            GameAudio.Play(moonClicks > 10 ? "shadowDie" : "thudStep");
            Event.current.Use();
        }
        Vector2 shake = Time.unscaledTime < moonShakeUntil
            ? new Vector2(UnityEngine.Random.Range(-1, 2), UnityEngine.Random.Range(-1, 2)) : Vector2.zero;
        Sky(new Rect(moon.position + shake, moon.size), new Rect(642, 835, 43, 43), new Color(1, 1, 1, fade));
        if (moonClicks <= 10) return;
        double ms = Time.unscaledTime * 1000.0;
        bool blink = ms % 4000.0 < 200.0 || (ms % 8000.0 > 7600.0 && ms % 8000.0 < 7800.0);
        Sky(new Rect(new Vector2(width - 136f, 48f) + shake, new Vector2(76f, 84f)),
            new Rect(685, 844 + (blink ? 21 : 0), 19, 21), new Color(1, 1, 1, fade));
    }

    /// <summary>
    /// ShippingMenu summary, centred like the original: rows 108 px apart from centreY - 300,
    /// each a 512x104 label box, six digit cells with a 7-digit MoneyDial and the g sign, then
    /// the item slot and + button for categories that shipped something.
    /// </summary>
    private void DrawSummary()
    {
        float centerX = Mathf.Floor(Screen.width / UiScale / 2f), centerY = UiHeight / 2f;
        float plaqueY = centerY - 428f;
        if (plaqueY >= 0f) ScrollText(date, centerX, plaqueY);
        for (int category = 0; category < revealedRows; category++)
        {
            float y = centerY - 300f + category * 108f;
            // category.getVector2() + (12, -8), with the row offset num = -20.
            float vx = centerX + 300f, vy = y - 8f;
            if (category < TotalCategory && categories[category].Count > 0)
            {
                if (Button(new Rect(centerX + 288f, y, 40f, 44f), new Rect(392, 361, 10, 11), IntroFinished))
                {
                    detailCategory = category;
                    detailPage = 0;
                    GameAudio.Play("shwip");
                    return;
                }
                StardewUiAtlas.DrawCursorsSprite(new Rect(vx - 104f, vy - 16f, 96f, 96f), new Rect(293, 360, 24, 24));
                DrawIcon(categories[category][0].Item, new Rect(vx - 88f, vy - 4f, 64f, 64f));
            }
            Box(new Rect(vx - 96f - 512f - 12f, vy - 20f, 512f, 104f));
            SpriteLabel(Categories[category], vx - 96f - 512f + 8f, vy + 4f);
            for (int cell = 0; cell < 6; cell++)
                StardewUiAtlas.DrawCursorsSprite(new Rect(vx - 96f - 192f - 24f + cell * 24f, vy + 12f, 28f, 44f),
                    new Rect(355, 476, 7, 11));
            DrawDial(new Vector2(vx - 96f - 192f - 48f + 4f, vy + 20f),
                DialValue(totals[category], introElapsed - RowTime(category)));
            StardewUiAtlas.DrawCursorsSprite(new Rect(vx - 96f - 64f - 4f, vy + 12f, 36f, 44f), new Rect(408, 476, 9, 11));
        }
        if (IntroFinished && Button(new Rect(centerX + 328f - 144f + 32f, centerY + 236f, 64f, 64f), ConfirmIcon))
            BeginMorning();
    }

    /// <summary>
    /// ShippingMenu category page: a full-screen menu box, one 68 px row per item with
    /// "Name x{price}g", a dotted leader and the line total; back and forward arrows below.
    /// </summary>
    private void DrawDetails()
    {
        float w = Screen.width / UiScale, h = UiHeight;
        StardewUiAtlas.DrawTextureBox(new Rect(0, 0, w, h), StardewUiAtlas.MenuTiles,
            StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f, true);
        var shipments = categories[detailCategory];
        int first = detailPage * PageSize;
        int last = Mathf.Min(shipments.Count, first + PageSize);
        float x = 32f, y = 32f;
        float dotWidth = SpriteWidth(" .");
        for (int index = first; index < last; index++, y += 68f)
        {
            var line = shipments[index];
            var slot = new Rect(x, y, 64f, 64f);
            DrawIcon(line.Item, slot);
            StardewUiAtlas.DrawStackCount(slot, (int)Math.Min(line.Quantity, int.MaxValue), line.Id);
            string label = (line.Item?.displayName ?? line.Id) + " x" + line.UnitPrice + "g";
            string value = line.Value + "g";
            var dots = new System.Text.StringBuilder(".");
            float room = w - 96f - SpriteWidth(label + value);
            for (float filled = 0f; filled < room && dotWidth > 0f; filled += dotWidth) dots.Append(" .");
            SpriteLabel(label, x + 64f + 12f, y + 12f);
            SpriteLabel(dots.ToString(), x + 80f + SpriteWidth(label), y + 8f);
            SpriteLabel(value, x + w - 64f - SpriteWidth(value), y + 12f);
        }
        if (Button(new Rect(32f, h - 64f, 48f, 44f), BackIcon)) { PreviousDetail(); return; }
        if (last < shipments.Count && Button(new Rect(w - 32f - 48f, h - 64f, 48f, 44f), ForwardIcon)) ChangePage(1);
    }

    private void PreviousDetail()
    {
        if (detailPage > 0) ChangePage(-1);
        else CloseDetails();
    }

    private void ChangePage(int offset)
    {
        detailPage += offset;
        GameAudio.Play("shwip");
    }

    private void CloseDetails()
    {
        detailCategory = -1;
        GameAudio.Play("shwip");
    }

    private void DrawMorning()
    {
        if (morningElapsed < FadeLength)
        {
            DatePlaque(date, 32);
            return;
        }
        float slide = Mathf.Clamp01((morningElapsed - FadeLength) / PlaqueSlide);
        DatePlaque(nextDate, Mathf.SmoothStep(Height + 16f, Height * .44f, slide));
        if (!dawnStarted || MorningSaved) return;
        if (CanRetry)
        {
            Rect retry = new(360, Height * .57f, 400, 64);
            Box(retry);
            Label(retry, "Chưa lưu xong. Thử lại", 24);
            if (GUI.Button(retry, GUIContent.none, GUIStyle.none)) online.RequestDaySettlement(day + 1);
        }
        else Label(new Rect(240, Height * .56f, 640, 50), "Đang lưu ngày mới...", 22, Color.white);
    }

    private void DatePlaque(string value, float y)
    {
        float span = Mathf.Clamp(NpcBitmapText.GetStringWidth(value, 2.65f) + 88f, 460f, 770f);
        Scroll(new Rect((Width - span) * .5f, y, span, 62));
        BitmapLabel(new Rect((Width - span) * .5f + 36, y + 3, span - 72, 56), value, 2.65f);
    }

    /// <summary>MoneyDial.draw: seven maroon 5x8 digits 24 px apart at 4x, no leading zeros.</summary>
    private static void DrawDial(Vector2 position, long value)
    {
        if (Event.current.type != EventType.Repaint) return;
        long place = 1_000_000;
        bool started = false;
        for (int index = 0; index < 7; index++, place /= 10)
        {
            int digit = (int)(value / place % 10);
            if (digit > 0 || index == 6) started = true;
            if (started)
                StardewUiAtlas.DrawCursorsSprite(new Rect(position.x + index * 24f, position.y, 20f, 32f),
                    new Rect(286, 502 - digit * 8, 5, 8), Maroon);
        }
    }

    /// <summary>SpriteText.drawStringWithScrollCenteredAt: 12x18 scroll ends around the text at 4x.</summary>
    private static void ScrollText(string value, float centerX, float y)
    {
        float width = SpriteWidth(value);
        float x = Mathf.Max(0f, centerX - width / 2f);
        float textY = y + 4f;
        StardewUiAtlas.DrawCursorsSprite(new Rect(x - 48f, textY - 12f, 48f, 72f), new Rect(325, 318, 12, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(x, textY - 12f, width, 72f), new Rect(337, 318, 1, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(x + width, textY - 12f, 48f, 72f), new Rect(338, 318, 12, 18));
        SpriteLabel(value, x, y);
    }

    private static float SpriteWidth(string value) => NpcBitmapText.GetStringWidth(value, SpriteZoom);

    /// <summary>SpriteText.drawString at fontPixelZoom 3, which moves the text 4 px down.</summary>
    private static void SpriteLabel(string value, float x, float y)
    {
        var font = StardewUiAtlas.FontBold;
        if (font == null || Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
        NpcBitmapText.Draw(font, new Rect(x, y + 4f, SpriteWidth(value) + 8f, 80f), value, SpriteZoom, false);
    }

    private static void DrawIcon(ItemData item, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        Sprite icon = item?.GetIcon();
        if (icon == null) return;
        Rect source = icon.rect;
        float scale = Mathf.Min(rect.width / source.width, rect.height / source.height);
        Vector2 size = source.size * scale;
        DrawAtlas(icon.texture, new Rect(rect.center - size * .5f, size),
            new Rect(source.x, icon.texture.height - source.yMax, source.width, source.height));
    }

    private static void DrawAtlas(Texture2D atlas, Rect destination, Rect source)
    {
        if (atlas != null && Event.current.type == EventType.Repaint)
            GUI.DrawTextureWithTexCoords(destination, atlas, StardewUiAtlas.ToUv(source, atlas.width, atlas.height));
    }

    private static void Scroll(Rect rect)
    {
        const float end = 40f;
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.x, rect.y, end, rect.height), new Rect(325, 318, 12, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.x + end, rect.y, rect.width - 2 * end, rect.height),
            new Rect(337, 318, 1, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(rect.xMax - end, rect.y, end, rect.height), new Rect(338, 318, 12, 18));
    }

    private static void Box(Rect rect) => StardewUiAtlas.DrawTextureBox(rect,
        StardewUiAtlas.Cursors, new Rect(384, 373, 18, 18), Color.white, 4f, false);

    private static bool Button(Rect rect, Rect source, bool enabled = true)
    {
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        Rect art = hover ? new Rect(rect.center - rect.size * .55f, rect.size * 1.1f) : rect;
        StardewUiAtlas.DrawCursorsSprite(art, source, enabled ? Color.white : new Color(.58f, .58f, .58f));
        return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private static void Fill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void BitmapLabel(Rect rect, string value, float scale, bool centered = true)
    {
        var font = StardewUiAtlas.FontBold;
        if (font != null)
        {
            scale = Mathf.Min(scale, rect.width / Mathf.Max(1f, NpcBitmapText.GetStringWidth(value, 1f)));
            rect.y += Mathf.Max(0, (rect.height - NpcBitmapText.GlyphHeight * scale) * .5f);
            if (Event.current.type == EventType.Repaint) NpcBitmapText.Draw(font, rect, value, scale, centered);
        }
        else Label(rect, value, Mathf.RoundToInt(scale * 13f), align: centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
    }

    private void Label(Rect rect, string value, int size, Color? color = null, TextAnchor align = TextAnchor.MiddleCenter,
        bool bold = false, bool wrap = false)
    {
        text ??= new GUIStyle(GUI.skin.label);
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = align;
        text.wordWrap = wrap;
        text.normal.textColor = color ?? Ink;
        GUI.Label(rect, value, text);
    }
}
