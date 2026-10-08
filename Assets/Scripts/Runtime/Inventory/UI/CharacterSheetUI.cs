using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the bottom character & farm sheet:
/// - 4 Equipment slots (Hat, Rings, Boots)
/// - Portrait frame (daybg + player character sprite + name)
/// - Farm stats (Farm name, calendar icon, current funds, total earnings, pet icon & name)
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

    public void UpdateProfile(
        string playerName,
        string farmName,
        int currentFunds,
        int totalEarnings,
        string petName,
        Sprite characterSprite = null)
    {
        if (playerNameText != null) playerNameText.text = playerName;
        if (farmNameText != null) farmNameText.text = farmName;
        if (currentFundsText != null) currentFundsText.text = $"Current Funds: {currentFunds:N0}g";
        if (totalEarningsText != null) totalEarningsText.text = $"Total Earnings: {totalEarnings:N0}g";
        if (petNameText != null) petNameText.text = petName;

        if (characterPreviewImage != null && characterSprite != null)
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
