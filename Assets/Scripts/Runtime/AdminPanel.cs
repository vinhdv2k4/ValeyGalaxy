using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// In-game test tools for admin accounts (server setting Admin:Usernames). F1 opens the panel.
/// Every command goes through /api/admin and only changes this account's own save; checkpoints
/// let a tester go back to any earlier state. Other accounts never see the panel.
/// </summary>
internal sealed class AdminPanel : MonoBehaviour
{
    private static AdminPanel instance;
    internal static bool IsOpen => instance != null && instance.open;

    private static readonly string[] Tabs = { "Thời gian", "Đồ & tiền", "Tiến độ", "Điểm lưu" };
    private static readonly (string Label, int Value)[] Weathers =
        { ("Nắng", 0), ("Mưa", 1), ("Gió", 2), ("Bão", 3), ("Tuyết", 5) };
    private static readonly string[] Rooms =
        { "Nhà kho", "Phòng thủ công", "Bể cá", "Phòng lò hơi", "Két sắt", "Bảng thông báo" };

    private OnlineGameState online;
    private StartupApiClient api;
    private StartupApiClient.AdminCheckpointInfo[] checkpoints = Array.Empty<StartupApiClient.AdminCheckpointInfo>();
    private List<string> eventIds;
    private bool open, busy;
    private int tab;
    private string message = "";
    private Vector2 scroll;
    private string dayText = "5", timeText = "0800", moneyText = "10000", itemText = "parsnip", amountText = "10";
    private string npcText = "Lewis", pointsText = "1000", eventFilter = "", checkpointName = "";
    private string confirming;
    // Raised levels apply at once; off, they wait for the night level-up and profession screens.
    private bool skillApplyNow = true;

    private void Start()
    {
        online = GetComponent<OnlineGameState>();
        api = online != null ? online.Api : null;
        if (api == null) { Destroy(this); return; }
        // Non-admin accounts get 403 here and the panel removes itself silently.
        StartCoroutine(api.GetAdminStatus((status, error) =>
        {
            if (this == null) return;
            if (error != null || status == null || !status.admin) { Destroy(this); return; }
            instance = this;
            checkpoints = status.checkpoints ?? checkpoints;
            Debug.Log("[Admin] Admin tools ready. Press F1.");
        }));
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (instance != this) return;
        if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
        {
            open = !open;
            confirming = null;
            if (open) RefreshCheckpoints();
        }
        else if (open && Keyboard.current?.escapeKey.wasPressedThisFrame == true) open = false;
    }

    // ----- Commands -----

    private void Run(string command, StartupApiClient.AdminBody body, string done, Action after = null)
    {
        if (busy) return;
        busy = true;
        message = "Đang gửi...";
        StartCoroutine(api.AdminState(command, body, (snapshot, error) =>
        {
            busy = false;
            if (this == null) return;
            if (error != null) { message = "Lỗi: " + error; return; }
            online.ApplyAdminSnapshot(snapshot, null);
            message = done;
            after?.Invoke();
        }));
    }

    /// <summary>Commands whose result needs a fresh scene (calendar, checkpoint, reset).</summary>
    private void RunAndReload(string command, StartupApiClient.AdminBody body, bool saveSpotFirst, bool returnsSnapshot)
    {
        if (busy) return;
        busy = true;
        message = "Đang gửi...";
        // The spot is saved before the change so a day jump resumes on the same tile.
        if (saveSpotFirst) PlayerSessionKeeper.Active?.SaveNow();
        void Finish(string error)
        {
            busy = false;
            if (this == null) return;
            if (error != null) { message = "Lỗi: " + error; return; }
            open = false;
            StartupSession.ReloadGame(saveSpot: false);
        }
        if (returnsSnapshot) StartCoroutine(api.AdminState(command, body, (_, error) => Finish(error)));
        else StartCoroutine(api.AdminCommand(command, body, Finish));
    }

    private void RefreshCheckpoints()
    {
        StartCoroutine(api.GetAdminStatus((status, error) =>
        {
            if (this != null && error == null && status?.checkpoints != null) checkpoints = status.checkpoints;
        }));
    }

    // ----- UI -----

    private void OnGUI()
    {
        if (instance != this || !open) return;
        float scale = Mathf.Clamp(Screen.height / 720f, 1f, 2f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            var area = new Rect(12f, 12f, 430f, Mathf.Min(560f, Screen.height / scale - 24f));
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
            GUILayout.Label($"<b>ADMIN</b> · ngày {online.Snapshot?.gameDay} · {FarmDayClock.Instance?.TimeOfDay:0000} · {online.Snapshot?.money}g   (F1 đóng)",
                new GUIStyle(GUI.skin.label) { richText = true });
            tab = GUILayout.Toolbar(tab, Tabs);
            GUI.enabled = !busy;
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawTime(); break;
                case 1: DrawItems(); break;
                case 2: DrawProgress(); break;
                default: DrawCheckpoints(); break;
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
        finally { GUI.matrix = previous; }
    }

    private void DrawTime()
    {
        GUILayout.Label("Nhảy tới ngày (game tải lại, giữ chỗ đứng; cây không lớn thêm):");
        GUILayout.BeginHorizontal();
        dayText = GUILayout.TextField(dayText, GUILayout.Width(80f));
        if (GUILayout.Button("Nhảy ngày") && int.TryParse(dayText, out int day) && day >= 1)
            RunAndReload("day", new StartupApiClient.AdminBody { day = day }, saveSpotFirst: true, returnsSnapshot: true);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Đặt giờ trong ngày (HHMM, 0600-2550):");
        GUILayout.BeginHorizontal();
        timeText = GUILayout.TextField(timeText, GUILayout.Width(80f));
        if (GUILayout.Button("Đặt giờ") && int.TryParse(timeText, out int time) && time is >= 600 and <= 2550 &&
            time % 100 < 60 && FarmDayClock.Instance != null)
        {
            FarmDayClock.Instance.RestoreTimeOfDay(time);
            message = $"Đã đặt giờ {time:0000}.";
        }
        foreach (int quick in new[] { 600, 800, 1200, 1800 })
            if (GUILayout.Button($"{quick / 100}h") && FarmDayClock.Instance != null)
            {
                FarmDayClock.Instance.RestoreTimeOfDay(quick);
                message = $"Đã đặt giờ {quick:0000}.";
            }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Thời tiết hôm nay:");
        GUILayout.BeginHorizontal();
        foreach (var (label, value) in Weathers)
            if (GUILayout.Button(label))
                Run("weather", new StartupApiClient.AdminBody { weather = value }, "Đã đổi thời tiết hôm nay: " + label);
        GUILayout.EndHorizontal();
        GUILayout.Label("Thời tiết ngày mai:");
        GUILayout.BeginHorizontal();
        foreach (var (label, value) in Weathers)
            if (GUILayout.Button(label))
                Run("weather", new StartupApiClient.AdminBody { weather = value, tomorrow = true },
                    "Đã đổi thời tiết ngày mai: " + label);
        GUILayout.EndHorizontal();
    }

    private void DrawItems()
    {
        GUILayout.Label("Tiền (số âm để trừ):");
        GUILayout.BeginHorizontal();
        moneyText = GUILayout.TextField(moneyText, GUILayout.Width(120f));
        if (GUILayout.Button("Cộng tiền") && long.TryParse(moneyText, out long money))
            Run("money", new StartupApiClient.AdminBody { amount = money }, $"Đã cộng {money}g.");
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Thêm vật phẩm (mã item, ví dụ parsnip, wood, object_388, fish_145):");
        GUILayout.BeginHorizontal();
        itemText = GUILayout.TextField(itemText, GUILayout.Width(200f));
        amountText = GUILayout.TextField(amountText, GUILayout.Width(50f));
        if (GUILayout.Button("Thêm") && int.TryParse(amountText, out int amount))
            Run("item", new StartupApiClient.AdminBody { itemId = itemText.Trim(), amount = amount },
                $"Đã thêm {amount} {itemText.Trim()}.");
        GUILayout.EndHorizontal();
        var known = online.ResolveItem(itemText.Trim());
        GUILayout.Label(known != null ? "→ " + known.displayName : "→ (client không nhận ra mã này)");
    }

    private void DrawProgress()
    {
        DrawSkills();

        GUILayout.Space(6f);
        GUILayout.Label("Độ thân thiết (0-2500, 250 = 1 tim):");
        GUILayout.BeginHorizontal();
        npcText = GUILayout.TextField(npcText, GUILayout.Width(140f));
        pointsText = GUILayout.TextField(pointsText, GUILayout.Width(60f));
        if (GUILayout.Button("Đặt") && int.TryParse(pointsText, out int points))
            Run("friendship", new StartupApiClient.AdminBody { npc = npcText.Trim(), points = points },
                $"{npcText.Trim()}: {points} điểm.", () => online.ReloadFriendships());
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Phòng Nhà văn hóa:");
        GUILayout.BeginHorizontal();
        // Done in the Community Center plays each room's restore cutscene; reset reloads the maps.
        if (GUILayout.Button("Hoàn thành cả 6 phòng"))
            Run("bundle", new StartupApiClient.AdminBody { area = -1, done = true }, "Đã hoàn thành cả nhà văn hóa.");
        if (GUILayout.Button("Làm lại cả nhà văn hóa"))
            RunAndReload("bundle", new StartupApiClient.AdminBody { area = -1, done = false },
                saveSpotFirst: true, returnsSnapshot: true);
        GUILayout.EndHorizontal();
        for (int area = 0; area < Rooms.Length; area++)
        {
            bool done = online.Snapshot?.bundles?.areas?.Any(room => room != null && room.area == area && room.complete) == true;
            GUILayout.BeginHorizontal();
            GUILayout.Label((done ? "✔ " : "· ") + Rooms[area], GUILayout.Width(170f));
            int index = area;
            if (GUILayout.Button("Hoàn thành"))
                Run("bundle", new StartupApiClient.AdminBody { area = index, done = true }, Rooms[index] + " đã hoàn thành.");
            // A restored room's tiles only go back to ruins when the map is loaded again.
            if (GUILayout.Button("Làm lại"))
                RunAndReload("bundle", new StartupApiClient.AdminBody { area = index, done = false },
                    saveSpotFirst: true, returnsSnapshot: true);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        GUILayout.Label("Sự kiện / cutscene (✔ = đã xem):");
        eventFilter = GUILayout.TextField(eventFilter);
        foreach (string id in EventIds())
        {
            if (eventFilter.Length > 0 && id.IndexOf(eventFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool seen = online.Snapshot?.milestones?.HasSeen(id) == true ||
                        online.Snapshot?.dailyEvents?.CompletedOn(id) > 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label((seen ? "✔ " : "· ") + id, GUILayout.Width(250f));
            string eventId = id;
            if (GUILayout.Button(seen ? "Chưa xem" : "Đã xem"))
                Run("event", new StartupApiClient.AdminBody { id = eventId, done = !seen },
                    eventId + (seen ? " sẽ phát lại khi đủ điều kiện." : " được đánh dấu đã xem."));
            GUILayout.EndHorizontal();
        }
    }

    private void DrawSkills()
    {
        GUILayout.Label("Cấp kỹ năng (0-10):");
        skillApplyNow = GUILayout.Toggle(skillApplyNow,
            skillApplyNow ? " Áp dụng ngay (bỏ qua màn lên cấp)" : " Lên cấp khi ngủ (có màn chọn nghề ở cấp 5/10)");
        int[] levels = online.Snapshot?.skillLevels;
        for (int skill = 0; skill < 5; skill++)
        {
            int level = levels != null && skill < levels.Length ? levels[skill] : 0;
            int shown = NightResultsMenu.ShownSkillLevel(api.UserName, levels, skill);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{NightResultsMenu.SkillName(skill)}: {level}" + (shown < level ? $" (đang hiện {shown})" : ""),
                GUILayout.Width(170f));
            int index = skill;
            if (GUILayout.Button("-1", GUILayout.Width(36f))) SetSkill(index, level - 1);
            if (GUILayout.Button("+1", GUILayout.Width(36f))) SetSkill(index, level + 1);
            if (GUILayout.Button("0", GUILayout.Width(30f))) SetSkill(index, 0);
            if (GUILayout.Button("10", GUILayout.Width(36f))) SetSkill(index, 10);
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tất cả lên 10")) SetSkill(-1, 10);
        if (GUILayout.Button("Tất cả về 0")) SetSkill(-1, 0);
        GUILayout.EndHorizontal();
    }

    private void SetSkill(int skill, int level)
    {
        level = Mathf.Clamp(level, 0, 10);
        string name = skill < 0 ? "Tất cả kỹ năng" : NightResultsMenu.SkillName(skill);
        Run("skill", new StartupApiClient.AdminBody { skill = skill, level = level }, $"{name}: cấp {level}.", () =>
        {
            int[] levels = online.Snapshot?.skillLevels;
            for (int index = 0; index < 5; index++)
            {
                if (skill >= 0 && skill != index) continue;
                // A lower level always shows at once; a higher one waits for the night unless applied now.
                int shown = NightResultsMenu.ShownSkillLevel(api.UserName, levels, index);
                NightResultsMenu.SetShownSkillLevel(api.UserName, index, skillApplyNow ? level : Mathf.Min(shown, level));
                // The server dropped professions above the new level; old local picks must not return them.
                if (level < 10) PlayerPrefs.DeleteKey(NightResultsMenu.ProfessionKey(api.UserName, index, 10));
                if (level < 5) PlayerPrefs.DeleteKey(NightResultsMenu.ProfessionKey(api.UserName, index, 5));
            }
            PlayerPrefs.Save();
            GetComponent<NetworkCharacterStats>()?.ApplySkillProgress(levels, api.UserName, online.Snapshot?.professions);
        });
    }

    private void DrawCheckpoints()
    {
        GUILayout.Label("Lưu trạng thái hiện tại (đồ, tiền, ngày, tiến độ, cây trồng, thân thiết, chỗ đứng):");
        GUILayout.BeginHorizontal();
        checkpointName = GUILayout.TextField(checkpointName, GUILayout.Width(220f));
        if (GUILayout.Button("Lưu") && checkpointName.Trim().Length > 0)
        {
            busy = true;
            PlayerSessionKeeper.Active?.SaveNow();
            string name = checkpointName.Trim();
            StartCoroutine(api.AdminCommand("checkpoints/save", new StartupApiClient.AdminBody { name = name }, error =>
            {
                busy = false;
                message = error == null ? "Đã lưu điểm \"" + name + "\"." : "Lỗi: " + error;
                RefreshCheckpoints();
            }));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        foreach (var checkpoint in checkpoints)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{checkpoint.name} · ngày {checkpoint.gameDay}", GUILayout.Width(220f));
            string name = checkpoint.name;
            if (Confirm("load:" + name, "Nạp"))
                RunAndReload("checkpoints/load", new StartupApiClient.AdminBody { name = name }, false, false);
            if (Confirm("delete:" + name, "Xóa"))
            {
                busy = true;
                StartCoroutine(api.AdminCommand("checkpoints/delete", new StartupApiClient.AdminBody { name = name }, error =>
                {
                    busy = false;
                    message = error == null ? "Đã xóa \"" + name + "\"." : "Lỗi: " + error;
                    RefreshCheckpoints();
                }));
            }
            GUILayout.EndHorizontal();
        }
        if (checkpoints.Length == 0) GUILayout.Label("(chưa có điểm lưu)");

        GUILayout.Space(10f);
        if (Confirm("reset", "Reset về nông trại mới (ngày 1)"))
            RunAndReload("reset", new StartupApiClient.AdminBody(), false, false);
    }

    /// <summary>A button that needs a second click, for commands that replace the save.</summary>
    private bool Confirm(string key, string label)
    {
        bool armed = confirming == key;
        if (!GUILayout.Button(armed ? "Bấm lần nữa để " + label.ToLowerInvariant() : label)) return false;
        if (armed) { confirming = null; return true; }
        confirming = key;
        return false;
    }

    private IEnumerable<string> EventIds()
    {
        if (eventIds != null) return eventIds;
        var ids = new List<string>();
        foreach (var entry in DailyEventAssets.Calendar.Events)
            if (entry.kind == "story" && !ids.Contains(entry.id)) ids.Add(entry.id);
        try
        {
            var asset = Resources.Load<TextAsset>("Cutscenes/Milestones");
            if (asset != null)
                foreach (var token in JObject.Parse(asset.text)["events"] ?? new JArray())
                    if (token["id"]?.ToString() is string id && id.Length > 0 && !ids.Contains(id)) ids.Add(id);
        }
        catch (Exception error) { Debug.LogWarning("[Admin] Could not list cutscenes: " + error.Message); }
        return eventIds = ids;
    }
}
