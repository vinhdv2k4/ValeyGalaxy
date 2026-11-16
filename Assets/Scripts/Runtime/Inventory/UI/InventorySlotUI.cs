using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages visual representation and user interactions for a single inventory slot.
/// Slot metrics (64x64 tile with icon, red active border, tiny count, and hover scale).
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image highlightBorder;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI slotNumberText;
    [SerializeField] private GameObject waterGauge;
    [SerializeField] private RectTransform waterFillRect;

    [Header("Sprite Assets (MenuTiles)")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite selectedSlotSprite;
    [SerializeField] private Sprite lockedSlotSprite;

    private int slotIndex;
    private bool isSelected;
    private bool isLocked;
    private Vector3 baseScale = Vector3.one;
    private PlayerFarmingInteractor waterSource;
    private bool showsWater;
    private bool inventoryPageStyle;
    private readonly List<UnityEngine.UI.RawImage> amountDigits = new();

    public int SlotIndex => slotIndex;

    public event Action<int> Clicked;
    public event Action<int> Hovered;

    public void UseInventoryPageStyle()
    {
        inventoryPageStyle = true;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        if (amountText != null) amountText.enabled = false;
    }

    private void OnDestroy()
    {
        if (waterSource != null) waterSource.WaterChanged -= OnWaterChanged;
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
        showsWater = !locked && slot != null && !slot.IsEmpty &&
            slot.Data != null && slot.Data.kind == PlayerItemKind.WateringCan;
        if (showsWater) EnsureWaterSource();
        if (waterGauge != null) waterGauge.SetActive(showsWater);

        // 1. Background Frame
        if (backgroundImage != null)
        {
            if (isLocked && !inventoryPageStyle)
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

        // 2. Selection Border (Tile 56 red border)
        if (highlightBorder != null)
        {
            bool lockOverlay = inventoryPageStyle && isLocked;
            highlightBorder.sprite = lockOverlay ? lockedSlotSprite : selectedSlotSprite;
            highlightBorder.color = lockOverlay ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            highlightBorder.enabled = lockOverlay || isSelected && !isLocked;
        }

        // 3. Item Content
        if (isLocked || slot == null || slot.IsEmpty)
        {
            if (iconImage != null) iconImage.enabled = false;
            RenderStackCount(0);
            RenderQuality(null);
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = slot.Data.GetIcon();
                iconImage.enabled = iconImage.sprite != null;
            }

            RenderStackCount(showsWater ? 0 : slot.Amount);
            RenderQuality(slot.Data?.itemId);
        }
        RefreshWaterVisuals();
    }

    private UnityEngine.UI.RawImage qualityStar;

    private void RenderQuality(string itemId)
    {
        Rect? source = ItemQuality.StarSource(itemId);
        if (source == null)
        {
            if (qualityStar != null) qualityStar.enabled = false;
            return;
        }
        if (qualityStar == null)
        {
            var view = new GameObject("Quality Star", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            view.transform.SetParent(transform, false);
            qualityStar = view.GetComponent<UnityEngine.UI.RawImage>();
            qualityStar.raycastTarget = false;
            RectTransform rect = qualityStar.rectTransform;
            rect.anchorMin = new Vector2(2f / 64f, 2f / 64f);
            rect.anchorMax = new Vector2(26f / 64f, 26f / 64f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        qualityStar.texture = StardewUiAtlas.Cursors;
        qualityStar.uvRect = StardewUiAtlas.ToUvCursors(source.Value);
        qualityStar.enabled = true;
        qualityStar.transform.SetAsLastSibling();
    }

    private void RenderStackCount(int amount)
    {
        string digits = StardewUiAtlas.StackDigits(amount);
        Texture2D atlas = digits.Length > 0 ? StardewUiAtlas.Cursors : null;
        int count = atlas != null ? digits.Length : 0;
        while (amountDigits.Count < count)
        {
            var view = new GameObject("Stack Digit", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            view.transform.SetParent(transform, false);
            var image = view.GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            // Object.drawInMenu puts the count over the slot's bottom-right edge, after every slot's
            // background is drawn; on its own canvas the next slot no longer covers it.
            var parentCanvas = GetComponentInParent<Canvas>();
            var layer = view.AddComponent<Canvas>();
            layer.overrideSorting = true;
            if (parentCanvas != null)
            {
                layer.sortingLayerID = parentCanvas.sortingLayerID;
                layer.sortingOrder = parentCanvas.sortingOrder + 2;
            }
            amountDigits.Add(image);
        }

        for (int i = 0; i < amountDigits.Count; i++)
        {
            var image = amountDigits[i];
            image.enabled = i < count;
            if (!image.enabled) continue;
            image.texture = atlas;
            image.uvRect = StardewUiAtlas.ToUvCursors(StardewUiAtlas.TinyDigitSource(digits[i]));

            Rect bounds = StardewUiAtlas.StackDigitBounds(new Rect(0, 0, 64, 64), count, i);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(bounds.xMin / 64f, 1f - bounds.yMax / 64f);
            rect.anchorMax = new Vector2(bounds.xMax / 64f, 1f - bounds.yMin / 64f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    private void EnsureWaterSource()
    {
        if (waterSource != null) return;
        waterSource = FindFirstObjectByType<PlayerFarmingInteractor>();
        if (waterSource != null) waterSource.WaterChanged += OnWaterChanged;
    }

    private void OnWaterChanged(int remaining, int capacity)
    {
        RefreshWaterVisuals();
    }

    private void RefreshWaterVisuals()
    {
        if (waterFillRect != null && waterSource != null)
            waterFillRect.sizeDelta = new Vector2(48f *
                (waterSource.WaterCapacity <= 0 ? 0f : Mathf.Clamp01((float)waterSource.WaterLeft / waterSource.WaterCapacity)), 8f);
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
