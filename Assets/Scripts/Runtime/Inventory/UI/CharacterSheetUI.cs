using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the bottom character & farm sheet:
/// Supplies the front-facing farmer portrait to the inventory and skills pages.
/// </summary>
public class CharacterSheetUI : MonoBehaviour
{
    [Header("Equipment Slots")]
    [SerializeField] private Image hatSlotImage;
    [SerializeField] private Image leftRingSlotImage;
    [SerializeField] private Image rightRingSlotImage;
    [SerializeField] private Image bootsSlotImage;

    [Header("Player Portrait")]
    [SerializeField] private Image portraitBackgroundImage;
    [SerializeField] private Image characterPreviewImage;
    [SerializeField] private Texture2D portraitDay;
    [SerializeField] private Texture2D portraitNight;
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Farm Stats")]
    [SerializeField] private TextMeshProUGUI farmNameText;
    [SerializeField] private TextMeshProUGUI currentFundsText;
    [SerializeField] private TextMeshProUGUI totalEarningsText;

    [Header("Calendar")]
    [SerializeField] private Button calendarButton;
    [SerializeField] private GameObject calendarTooltip;

    [Header("Pet Section")]
    [SerializeField] private Image petIconImage;
    [SerializeField] private TextMeshProUGUI petNameText;

    private void Awake()
    {
        if (calendarTooltip != null)
        {
            calendarTooltip.SetActive(false);
        }
    }

    private Sprite portraitBody, portraitArms, portraitShirt, portraitDye, portraitPants, portraitAccessory, portraitHair;
    private Color skinColor, hairColor, pantsColor;
    private int accessoryStyle, hairOffset, femaleOffset;
    private PlayerAppearance portraitAppearance;
    private bool portraitPrepared;

    public void BindPortrait(PlayerAppearance appearance)
    {
        ReleasePortrait();
        portraitAppearance = appearance;
        portraitPrepared = false;
    }

    private void PreparePortrait()
    {
        portraitPrepared = true;
        FarmerSpriteCatalog catalog = FarmerSpriteCatalog.Load();
        if (portraitAppearance == null || catalog == null) return;
        var outfit = portraitAppearance.GetPortraitAppearance();
        Texture2D bodyTexture = outfit.Body != null ? outfit.Body : outfit.Female ? catalog.femaleBase : catalog.maleBase;
        const int front = 2;
        portraitBody = catalog.Body(bodyTexture, 0);
        portraitArms = catalog.Body(bodyTexture, 0, true);
        portraitShirt = catalog.Shirt(outfit.Shirt, front);
        portraitDye = catalog.Shirt(outfit.Shirt, front, true);
        portraitPants = catalog.Pants(outfit.Pants, front, outfit.Female);
        portraitAccessory = catalog.Accessory(outfit.Accessory, front);
        portraitHair = outfit.Hair;
        skinColor = outfit.SkinColor;
        hairColor = outfit.HairColor;
        pantsColor = outfit.PantsColor;
        accessoryStyle = outfit.Accessory;
        femaleOffset = outfit.Female ? 1 : 0;
        int hairStyle = StartupSession.Farmer?.hair ?? 0;
        hairOffset = outfit.Female ? (hairStyle < 16 ? 1 : 0) : (hairStyle >= 16 ? -1 : 0);
    }

    internal void DrawPortrait(Rect frame)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!portraitPrepared) PreparePortrait();
        bool night = (FarmDayClock.Instance?.TimeOfDay ?? 600) >= 1900;
        Texture2D background = night && portraitNight != null ? portraitNight : portraitDay;
        if (portraitBody == null || background == null)
        {
            DrawLayer(portraitBackgroundImage != null ? portraitBackgroundImage.sprite : null, frame, Color.white);
            return;
        }
        GUI.DrawTexture(frame, background);
        float pixel = frame.width / 32f;
        DrawFarmer(frame.x + 8f * pixel, frame.y + 8f * pixel, pixel, night);
    }

    internal void DrawMiniPortrait(Rect head)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!portraitPrepared) PreparePortrait();
        GUI.BeginGroup(head);
        try
        {
            float pixel = head.width / 16f;
            DrawLayer(portraitBody, new Rect(0, 0, head.width, 32 * pixel), skinColor);
            DrawLayer(portraitHair, new Rect(0, hairOffset * pixel, head.width, 32 * pixel), hairColor);
        }
        finally { GUI.EndGroup(); }
    }

    private void DrawFarmer(float x, float y, float pixel, bool night)
    {
        Rect body = new(x, y, 16f * pixel, 32f * pixel);
        Rect shirt = new(x + 4f * pixel, y + (14 + femaleOffset) * pixel, 8f * pixel, 8f * pixel);
        Rect hair = new(x, y + hairOffset * pixel, body.width, body.height);
        Rect accessory = new(x, y + (4 + femaleOffset) * pixel, body.width, body.width);
        Color light = night ? new Color(0.7f, 0.7f, 0.85f) : Color.white;
        Color accessoryColor = accessoryStyle < 6 ? hairColor : Color.white;
        DrawLayer(portraitBody, body, skinColor * light);
        DrawLayer(portraitPants, body, pantsColor * light);
        DrawLayer(portraitShirt, shirt, light);
        DrawLayer(portraitDye, shirt, light);
        if (accessoryStyle < 8) DrawLayer(portraitAccessory, accessory, accessoryColor * light);
        DrawLayer(portraitHair, hair, hairColor * light);
        if (accessoryStyle >= 8) DrawLayer(portraitAccessory, accessory, accessoryColor * light);
        DrawLayer(portraitArms, body, skinColor * light);
    }

    private static void DrawLayer(Sprite sprite, Rect destination, Color tint)
    {
        if (sprite == null || sprite.texture == null) return;
        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(destination, texture,
            new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
        GUI.color = previous;
    }

    private void OnDestroy() => ReleasePortrait();

    private void ReleasePortrait()
    {
        Destroy(portraitBody);
        Destroy(portraitArms);
        Destroy(portraitShirt);
        Destroy(portraitDye);
        Destroy(portraitPants);
        Destroy(portraitAccessory);
        portraitBody = null;
    }

    public void UseBitmapView()
    {
        if (!TryGetComponent(out CanvasGroup legacyView)) legacyView = gameObject.AddComponent<CanvasGroup>();
        legacyView.alpha = 0f;
        legacyView.blocksRaycasts = false;
        legacyView.interactable = false;
    }

    public void UpdateProfile(
        string playerName,
        string farmName,
        int currentFunds,
        long totalEarnings,
        string petName,
        Sprite characterSprite = null)
    {
        if (playerNameText != null) playerNameText.text = playerName;
        if (farmNameText != null) farmNameText.text = farmName;
        if (currentFundsText != null) currentFundsText.text = $"Current Funds: {currentFunds:N0}g";
        if (totalEarningsText != null) totalEarningsText.text = $"Total Earnings: {totalEarnings:N0}g";
        if (petNameText != null) petNameText.text = petName;

        if (characterPreviewImage != null && characterPreviewImage != portraitBackgroundImage &&
            characterSprite != null)
        {
            characterPreviewImage.sprite = characterSprite;
            characterPreviewImage.enabled = true;
        }
    }

    public void SetCalendarTooltipVisible(bool visible)
    {
        if (calendarTooltip != null)
        {
            calendarTooltip.SetActive(visible);
        }
    }
}
