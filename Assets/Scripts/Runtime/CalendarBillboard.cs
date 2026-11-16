using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The town calendar outside Pierre's (Town action tile 41, 56: "Billboard 1"), drawn like
/// Menus/Billboard with dailyQuest = false: the season's 28 days with festivals and villagers'
/// birthdays, past days greyed and today outlined in blue. Layout is in the original's 4x pixels.
/// </summary>
internal sealed class CalendarBillboard : MonoBehaviour
{
    // Town Buildings actions: "Billboard 1" (calendar) at (41, 56), "Billboard 3" (Help Wanted) at (42, 56).
    private const int CalendarTileX = 41, QuestBoardTileX = 42, BoardTileY = 56;
    private const float Height = 198f * 4f;
    private float Width => questBoard ? 338f * 4f : 301f * 4f;
    private bool questBoard;
    private static readonly string[] Seasons = { "Mùa xuân", "Mùa hè", "Mùa thu", "Mùa đông" };
    private static readonly Color TextColor = new Color32(34, 17, 34, 255);

    // NPC birthdays (season 0-3, day). Dwarf, Sandy and Krobus are left out like
    // the original does until the farmer has met them.
    private static readonly (string Name, int Season, int Day)[] Birthdays =
    {
        ("Kent", 0, 4), ("Lewis", 0, 7), ("Vincent", 0, 10), ("Haley", 0, 14), ("Pam", 0, 18),
        ("Shane", 0, 20), ("Pierre", 0, 26), ("Emily", 0, 27),
        ("Jas", 1, 4), ("Gus", 1, 8), ("Maru", 1, 10), ("Alex", 1, 13), ("Sam", 1, 17),
        ("Demetrius", 1, 19), ("Willy", 1, 24),
        ("Penny", 2, 2), ("Elliott", 2, 5), ("Jodi", 2, 11), ("Abigail", 2, 13), ("Marnie", 2, 18),
        ("Robin", 2, 21), ("George", 2, 24),
        ("Linus", 3, 3), ("Caroline", 3, 7), ("Sebastian", 3, 10), ("Harvey", 3, 14), ("Wizard", 3, 17),
        ("Evelyn", 3, 20), ("Leah", 3, 23), ("Clint", 3, 26)
    };

    private sealed class Day
    {
        public string Festival = "";
        public bool NightMarket;
        public string Birthday = "";
        public Sprite Villager;
    }

    private static CalendarBillboard instance;
    private readonly Day[] days = new Day[28];
    private Texture2D billboard;
    private int season, year, today;

    internal static bool IsOpen => instance != null && instance.enabled;

    /// <summary>Opens the calendar when the farmer acts on it in Town.</summary>
    internal static bool TryOpen(Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        if (LocationManager.Instance?.CurrentLocation?.LocationName != "Town") return false;
        bool calendar = NpcShop.HitActionTile(pointer, standing, facing, CalendarTileX, BoardTileY);
        bool quests = !calendar && NpcShop.HitActionTile(pointer, standing, facing, QuestBoardTileX, BoardTileY);
        if (!calendar && !quests) return false;
        if (instance == null)
        {
            var host = new GameObject("CalendarBillboard");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<CalendarBillboard>();
        }
        instance.questBoard = quests;
        instance.Build();
        instance.enabled = true;
        GameAudio.Play("bigSelect");
        InputController.SuppressWorldUseAfterModal();
        return true;
    }

    internal static void Close()
    {
        if (!IsOpen) return;
        instance.enabled = false;
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
    }

    private void Build()
    {
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        billboard = catalog != null ? catalog.billboard : null;
#if UNITY_EDITOR
        if (billboard == null)
            billboard = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/Billboard.png");
#endif
        int absolute = Mathf.Max(1, FarmDayClock.Instance?.Day ?? 1);
        season = (absolute - 1) / 28 % 4;
        year = (absolute - 1) / 112 + 1;
        today = (absolute - 1) % 28 + 1;
        for (int i = 0; i < days.Length; i++) days[i] = new Day();

        foreach (var entry in DailyEventAssets.Calendar.Events)
        {
            if (entry.kind != "festival") continue;
            int dayOfYear = (entry.day - 1) % 112;
            if (dayOfYear / 28 != season) continue;
            int dayOfMonth = dayOfYear % 28;
            // The Night Market runs three nights (winter 15-17) and gets its own marker.
            if (entry.id == "night_market")
                for (int d = dayOfMonth; d < Mathf.Min(28, dayOfMonth + 3); d++) days[d].NightMarket = true;
            else days[dayOfMonth].Festival = entry.title;
        }

        var sprites = new Dictionary<string, Sprite>();
        if (catalog != null)
            foreach (var npc in catalog.characters)
                if (npc?.animationFrames != null && npc.animationFrames.Length > 0) sprites[npc.name] = npc.animationFrames[0];
        foreach (var (name, birthdaySeason, day) in Birthdays)
        {
            if (birthdaySeason != season || days[day - 1].Festival.Length > 0) continue;
            days[day - 1].Birthday = "Sinh nhật của " + name;
            sprites.TryGetValue(name, out days[day - 1].Villager);
        }
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
            Keyboard.current?.eKey.wasPressedThisFrame == true ||
            Mouse.current?.rightButton.wasPressedThisFrame == true ||
            Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            Close();
    }

    private void OnGUI()
    {
        GUI.depth = -60;
        float scale = Mathf.Min(1f, (Screen.width - 32f) / Width, (Screen.height - 32f) / Height);
        Vector2 origin = new((Screen.width - Width * scale) / 2f, (Screen.height - Height * scale) / 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        if (Event.current.type == EventType.Repaint)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1f)) * previousMatrix;
        Vector2 mouse = Event.current.mousePosition;
        string hover = "";

        if (questBoard)
        {
            // Billboard.draw with dailyQuestBoard: no daily quests in this game yet (Billboard_NothingPosted).
            if (Event.current.type == EventType.Repaint)
            {
                DrawBillboard(new Rect(0f, 0f, Width, Height), new Rect(0, 0, 338, 198));
                // Display when nothing is posted.
                GameText.Draw(new Rect(384f, 320f, 640f, 80f), "Chưa có thông báo nào.", GameText.Face.Dialogue,
                    TextColor, shadow: false);
            }
            DrawCloseButton();
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            return;
        }

        if (Event.current.type == EventType.Repaint)
        {
            DrawBillboard(new Rect(0f, 0f, Width, Height), new Rect(0, 198, 301, 198));
            // Regular font, no shadow.
            GameText.Draw(new Rect(160f, 80f, 280f, 60f), Seasons[season], GameText.Face.Dialogue, TextColor, shadow: false);
            GameText.Draw(new Rect(448f, 80f, 300f, 60f), "Năm " + year, GameText.Face.Dialogue, TextColor, shadow: false);
        }

        for (int i = 0; i < days.Length; i++)
        {
            var day = days[i];
            var cell = new Rect(152f + i % 7 * 128f, 200f + i / 7 * 128f, 124f, 124f);
            if (cell.Contains(mouse))
                hover = day.Birthday.Length > 0 ? day.Birthday + (day.NightMarket ? "\nChợ Đêm" : "")
                    : day.Festival.Length > 0 ? day.Festival : day.NightMarket ? "Chợ Đêm" : "";
            if (Event.current.type != EventType.Repaint) continue;
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 2f;
            if (day.NightMarket)
                StardewUiAtlas.DrawCursorsSprite(new Rect(cell.x + 12f, cell.y + 60f - bob, 32f, 32f), new Rect(346, 392, 8, 8));
            if (day.Festival.Length > 0)
            {
                int frame = (int)(Time.unscaledTime * 1000f % 600f / 100f);
                DrawBillboard(new Rect(cell.x + 40f, cell.y + 56f - bob, 56f, 48f), new Rect(1 + frame * 14, 398, 14, 12));
            }
            if (day.Villager != null) DrawSprite(new Rect(cell.x + 48f, cell.y + 28f, 64f, 96f), day.Villager);
            if (today > i + 1)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.25f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (today == i + 1)
            {
                float grow = 4f * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.05f) / 8f * 4f;
                StardewUiAtlas.DrawTextureBox(new Rect(cell.x - grow, cell.y - grow, cell.width + grow * 2f, cell.height + grow * 2f),
                    StardewUiAtlas.Cursors, new Rect(379, 357, 3, 3), Color.blue, 4f, false);
            }
        }

        DrawCloseButton();
        GUI.matrix = previousMatrix;
        if (hover.Length > 0 && Event.current.type == EventType.Repaint) DrawHover(hover);
        GUI.color = previousColor;
    }

    /// <summary>upperRightCloseButton; a click outside the board closes it too.</summary>
    private void DrawCloseButton()
    {
        var close = new Rect(Width - 20f, 0f, 48f, 48f);
        if (Event.current.type == EventType.Repaint)
            StardewUiAtlas.DrawCursorsSprite(close, new Rect(337, 494, 12, 12));
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector2 mouse = Event.current.mousePosition;
            if (close.Contains(mouse) || !new Rect(0f, 0f, Width, Height).Contains(mouse)) Close();
            Event.current.Use();
        }
    }

    /// <summary>Draws the hover text menu box beside the mouse, kept on screen.</summary>
    private static void DrawHover(string text)
    {
        int split = text.IndexOf('\n');
        if (split < 0) GameText.DrawTooltip(text, null, Screen.width, Screen.height);
        else GameText.DrawTooltip(text.Substring(0, split), text.Substring(split + 1), Screen.width, Screen.height);
    }

    private void DrawBillboard(Rect destination, Rect source)
    {
        if (billboard == null) return;
        GUI.DrawTextureWithTexCoords(destination, billboard, new Rect(source.x / billboard.width,
            1f - (source.y + source.height) / billboard.height, source.width / billboard.width, source.height / billboard.height));
    }

    private static void DrawSprite(Rect destination, Sprite sprite)
    {
        Rect source = sprite.textureRect;
        Texture2D texture = sprite.texture;
        GUI.DrawTextureWithTexCoords(destination, texture, new Rect(source.x / texture.width,
            source.y / texture.height, source.width / texture.width, source.height / texture.height));
    }
}
