using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Energy/Stamina and Health HUD meter controller.
/// Displays at the bottom-right corner of the screen with authentic pixel-art styling.
/// Features:
/// - 4x pixel scaled wooden meter frame with gold 'E' badge.
/// - Dynamic vertical stamina fill bar with smooth Red-Yellow-Green gradient and 3D top edge.
/// - Auto-sync with local player's NetworkCharacterStats.
/// - Shake effect on stamina spend or low stamina (<= 20).
/// - Hover tooltip showing exact "{stamina}/{maxStamina}".
/// - Dynamic Health Bar when player is wounded (< maxHealth) or in combat.
/// </summary>
public sealed class EnergyBarHud : MonoBehaviour
{
    private static EnergyBarHud instance;
    public static EnergyBarHud Instance => instance;

    [Header("Target Stats")]
    [SerializeField] private NetworkCharacterStats characterStats;

    [Header("Visual Tuning")]
    [SerializeField] private bool showHealthWhenDamaged = true;

    private float staminaShakeTimer;
    private int lastStamina = -1;
    private Texture2D whiteTexture;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        CreateWhiteTexture();
        ResolveStats();
    }

    private void Start()
    {
        ResolveStats();
    }

    private void Update()
    {
        if (characterStats == null)
        {
            ResolveStats();
            return;
        }

        if (lastStamina >= 0 && characterStats.Stamina < lastStamina)
        {
            staminaShakeTimer = 0.35f;
        }
        lastStamina = characterStats.Stamina;

        if (staminaShakeTimer > 0f)
            staminaShakeTimer -= Time.unscaledDeltaTime;
    }

    private void ResolveStats()
    {
        if (characterStats != null) return;
        var localPlayer = FindFirstObjectByType<NetworkPlayerController>();
        characterStats = localPlayer != null
            ? localPlayer.GetComponent<NetworkCharacterStats>()
            : GetComponent<NetworkCharacterStats>();
        if (characterStats != null)
            lastStamina = characterStats.Stamina;
    }

    private void CreateWhiteTexture()
    {
        if (whiteTexture != null) return;
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    public bool ShouldShowHealthBar()
    {
        if (showHealthWhenDamaged && characterStats != null && characterStats.Health < characterStats.MaxHealth)
            return true;

        string locName = LocationManager.Instance?.CurrentLocation?.LocationName ?? "";
        if (locName.IndexOf("Mine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            locName.IndexOf("Cave", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            locName.IndexOf("Underground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            int.TryParse(locName, out _))
        {
            return true;
        }

        if (MineManager.IsInMine)
            return true;

        return false;
    }

    private void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "GamePlay" ||
            StartupSession.IsOpening || FullInventoryMenuUI.IsOpen) return;

        ResolveStats();
        if (characterStats == null) return;

        CreateWhiteTexture();

        DrawStaminaBar();

        if (ShouldShowHealthBar())
        {
            DrawHealthBar();
        }
    }

    private void DrawStaminaBar()
    {
        const float scale = 4f;
        const float barW = 12f * scale; // 48px
        const float totalH = 240f;
        float x = Screen.width - 56f;
        float y = Screen.height - totalH - 16f;

        // Shake if low stamina or recently spent
        if (staminaShakeTimer > 0f || characterStats.Stamina <= 20)
        {
            x += Random.Range(-2.5f, 2.5f);
            y += Random.Range(-2.5f, 2.5f);
        }

        // The source game draws the frame before the 168px fill.
        DrawMeterFrame(x, y, barW, scale, isHealth: false);
        DrawMeterCaps(x, y, barW, scale, isHealth: false);

        // 2. Draw Stamina Fill
        float maxStamina = Mathf.Max(1, characterStats.MaxStamina);
        float ratio = Mathf.Clamp01((float)characterStats.Stamina / maxStamina);
        const float maxFillH = 168f;
        float fillH = maxFillH * ratio;
        float fillY = y + 48f + (maxFillH - fillH);

        Color fillColor = GetRedToGreenLerpColor(ratio);
        DrawSolidRect(new Rect(x + 12f, fillY, 24f, fillH), fillColor);

        // Darker 4px top edge highlight
        if (fillH > 4f)
        {
            Color darkLip = new Color(
                Mathf.Max(0f, fillColor.r - 0.25f),
                Mathf.Max(0f, fillColor.g - 0.25f),
                Mathf.Max(0f, fillColor.b - 0.25f),
                1f
            );
            DrawSolidRect(new Rect(x + 12f, fillY, 24f, 4f), darkLip);
        }

        // Hover Tooltip
        Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
        if (mouse.x >= x && mouse.x <= x + barW && mouse.y >= y && mouse.y <= y + totalH)
        {
            string label = $"{characterStats.Stamina}/{characterStats.MaxStamina}";
            DrawTooltip(label, new Vector2(x - 90f, y + 100f));
        }
    }

    private void DrawHealthBar()
    {
        const float scale = 4f;
        const float barW = 12f * scale; // 48px
        const float totalH = 240f;
        float staminaX = Screen.width - 56f;
        float x = staminaX - 56f; // Exactly 56px to the left of stamina bar
        float y = Screen.height - totalH - 16f;

        DrawMeterFrame(x, y, barW, scale, isHealth: true);
        DrawMeterCaps(x, y, barW, scale, isHealth: true);

        float maxHealth = Mathf.Max(1, characterStats.MaxHealth);
        float ratio = Mathf.Clamp01((float)characterStats.Health / maxHealth);
        const float maxFillH = 168f;
        float fillH = maxFillH * ratio;
        float fillY = y + 48f + (maxFillH - fillH);

        // Health fill uses RedToGreen lerp (green at 100%, yellow at 50%, red at low)
        Color fillColor = GetRedToGreenLerpColor(ratio);
        DrawSolidRect(new Rect(x + 12f, fillY, 24f, fillH), fillColor);

        if (fillH > 4f)
        {
            Color darkLip = new Color(
                Mathf.Max(0f, fillColor.r - 0.25f),
                Mathf.Max(0f, fillColor.g - 0.25f),
                Mathf.Max(0f, fillColor.b - 0.25f),
                1f
            );
            DrawSolidRect(new Rect(x + 12f, fillY, 24f, 4f), darkLip);
        }

        Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
        if (mouse.x >= x && mouse.x <= x + barW && mouse.y >= y && mouse.y <= y + totalH)
        {
            string label = $"{characterStats.Health}/{characterStats.MaxHealth}";
            DrawTooltip(label, new Vector2(x - 90f, y + 100f));
        }
    }

    private void DrawMeterFrame(float x, float y, float width, float scale, bool isHealth)
    {
        Rect midRect = isHealth ? StardewUiAtlas.HealthMidRect : StardewUiAtlas.StaminaMidRect;
        // Tube middle background
        StardewUiAtlas.DrawCursorsSprite(new Rect(x, y + 64f, width, 104f), midRect);
    }

    private void DrawMeterCaps(float x, float y, float width, float scale, bool isHealth)
    {
        Rect topRect = isHealth ? StardewUiAtlas.HealthTopRect : StardewUiAtlas.StaminaTopRect;
        Rect botRect = isHealth ? StardewUiAtlas.HealthBotRect : StardewUiAtlas.StaminaBotRect;

        // Top Badge (with "E" or Cross)
        StardewUiAtlas.DrawCursorsSprite(new Rect(x, y, width, 16f * scale), topRect);
        // Bottom rounded Cap
        StardewUiAtlas.DrawCursorsSprite(new Rect(x, y + 160f, width, 16f * scale), botRect);
    }

    private void DrawSolidRect(Rect rect, Color color)
    {
        if (whiteTexture == null) CreateWhiteTexture();
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = oldColor;
    }

    private static Color GetRedToGreenLerpColor(float ratio)
    {
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) * 2f;
            return Color.Lerp(new Color(1f, 0.88f, 0.1f), new Color(0.18f, 0.82f, 0.22f), t);
        }
        else
        {
            float t = ratio * 2f;
            return Color.Lerp(new Color(0.88f, 0.18f, 0.14f), new Color(1f, 0.88f, 0.1f), t);
        }
    }

    private void DrawTooltip(string text, Vector2 pos)
    {
        const float tipW = 84f;
        const float tipH = 32f;
        Rect boxRect = new Rect(pos.x, pos.y, tipW, tipH);

        // Draw background box
        StardewUiAtlas.DrawDialogueBox(boxRect, Color.white, drawShadow: true);

        // Draw text
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.35f, 0.20f, 0.12f) }
        };
        GUI.Label(boxRect, text, style);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureHudBootstrap()
    {
        if (FindFirstObjectByType<EnergyBarHud>() == null)
        {
            var hudObj = new GameObject("EnergyBarHud");
            hudObj.AddComponent<EnergyBarHud>();
            DontDestroyOnLoad(hudObj);
        }
    }
}
