using UnityEngine;

/// <summary>
/// Loading screen cleared to background color and "Loading" with 1-3 dots in
/// text on the wooden scroll at the bottom-left. Shown from the menu's
/// fade-out until the farm is ready, across the scene change, with the hourglass cursor.
/// </summary>
internal sealed class GameLoadingScreen : MonoBehaviour
{
    private const string Text = "Đang tải";
    private const float MaxSeconds = 45f;
    private static readonly Color Background = new(5f / 255f, 3f / 255f, 4f / 255f, 1f);

    private static GameLoadingScreen instance;
    private Texture2D font;
    private float shownAt;

    internal static bool IsShowing => instance != null && instance.enabled;

    internal static void Show()
    {
        if (instance == null)
        {
            var host = new GameObject("GameLoadingScreen");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GameLoadingScreen>();
        }
        instance.shownAt = Time.unscaledTime;
        instance.enabled = true;
    }

    internal static void Hide()
    {
        if (instance != null) instance.enabled = false;
    }

    private void Update()
    {
        // Never leave a player stuck behind the loading screen if something failed to load.
        if (Time.unscaledTime - shownAt > MaxSeconds) enabled = false;
    }

    private void OnGUI()
    {
        GUI.depth = -10000;
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = Background;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (font == null) font = Resources.Load<NpcCatalog>("NpcCatalog")?.dialogueFont;
        if (font != null)
        {
            // for (i = 0; i < ms % 999 / 333; i++) str += "."
            int dots = Mathf.CeilToInt(Time.unscaledTime * 1000f % 999f / 333f);
            string shown = Text + new string('.', dots);
            // The scroll is sized for "Loading... " so it never changes width.
            float width = NpcBitmapText.GetStringWidth(Text + "... ");
            // x = 64, y = viewport bottom - 64; offset moves y down 4 px.
            float x = 64f, y = Screen.height - 64f + 4f;
            StardewUiAtlas.DrawCursorsSprite(new Rect(x - 48f, y - 12f, 48f, 72f), new Rect(325, 318, 12, 18));
            StardewUiAtlas.DrawCursorsSprite(new Rect(x, y - 12f, width, 72f), new Rect(337, 318, 1, 18));
            StardewUiAtlas.DrawCursorsSprite(new Rect(x + width, y - 12f, 48f, 72f), new Rect(338, 318, 12, 18));
            NpcBitmapText.Draw(font, new Rect(x, y, width + 48f, 64f), shown);
        }
        GUI.color = previous;
    }
}
