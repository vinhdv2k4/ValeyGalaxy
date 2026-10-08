using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages visual representation and user interactions for a single inventory slot.
/// Matches Stardew Valley slot metrics (64x64 tile with icon, red active border, tiny count, and hover scale).
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image highlightBorder;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI slotNumberText;

    [Header("Sprite Assets (Stardew MenuTiles)")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Sprite lockedSlotSprite;

    private int slotIndex;
    private bool isSelected;
    private bool isLocked;
    private Vector3 baseScale = Vector3.one;

    public int SlotIndex => slotIndex;

    public event Action<int> Clicked;
    public event Action<int> Hovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void Initialize(int index, Sprite normal, Sprite selected, Sprite locked, bool showNumber = true)
    {
        slotIndex = index;
        normalSlotSprite = normal;
        selectedSlotSprite = selected;
        lockedSlotSprite = locked;

        if (highlightBorder != null)
        {
            highlightBorder.sprite = selectedSlotSprite;
            highlightBorder.enabled = false;
        }

        UpdateSlotNumberLabel(showNumber);
    }

    private void UpdateSlotNumberLabel(bool showNumber = true)
    {
        if (slotNumberText == null) return;

        if (!showNumber)
        {
            slotNumberText.enabled = false;
            return;
        }

        // Toolbar slots 0..11 show keys 1..9, 0, -, =
        if (slotIndex < 12)
        {
            string label = slotIndex switch
            {
                9 => "0",
                10 => "-",
                11 => "=",
                _ => (slotIndex + 1).ToString()
            };
            slotNumberText.text = label;
            slotNumberText.enabled = true;
        }
        else
        {
            slotNumberText.enabled = false;
        }
    }

    public void Render(IReadOnlyItemSlot slot, bool selected, bool locked)
    {
        isSelected = selected;
        isLocked = locked;

        // 1. Background Frame
        if (backgroundImage != null)
        {
            if (isLocked)
            {
                backgroundImage.sprite = lockedSlotSprite;
                backgroundImage.color = new Color(1f, 1f, 1f, 0.6f);
            }
            else
            {
                backgroundImage.sprite = normalSlotSprite;
                backgroundImage.color = Color.white;
            }
        }

        // 2. Selection Border (Stardew Valley Tile 56 red border)
        if (highlightBorder != null)
        {
            highlightBorder.enabled = isSelected && !isLocked;
        }

        // 3. Item Content
        if (isLocked || slot == null || slot.IsEmpty)
        {
            if (iconImage != null) iconImage.enabled = false;
            if (amountText != null) amountText.text = "";
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = slot.Data.icon;
                iconImage.enabled = slot.Data.icon != null;
            }

            if (amountText != null)
            {
                if (slot.Amount > 1)
                {
                    amountText.text = slot.Amount.ToString();
                    amountText.enabled = true;
                }
                else
                {
                    amountText.text = "";
                }
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;
        Clicked?.Invoke(slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return;
        // Hover scaling (Toolbar.cs performHoverAction scale = 1.1f)
        transform.localScale = baseScale * 1.08f;
        Hovered?.Invoke(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = baseScale;
    }
}
