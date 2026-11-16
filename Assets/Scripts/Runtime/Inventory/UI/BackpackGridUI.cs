using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the 36-slot backpack grid (3 rows x 12 cols),
/// row 1 hotbar number indicators, and right-side action buttons.
/// Adheres strictly to standard slot metrics & visuals.
/// </summary>
public class BackpackGridUI : MonoBehaviour
{
    private static readonly Color SelectedNumberColor = new Color(0.88f, 0.19f, 0.19f, 1f); // Red #E03030
    private static readonly Color NormalNumberColor = new Color(0.32f, 0.22f, 0.15f, 1f);   // Dark wood brown #51361F

    [Header("Grid References")]
    [SerializeField] private Transform gridContainer;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Hotbar Numbers Row")]
    [SerializeField] private TextMeshProUGUI[] hotbarNumberTexts;

    [Header("Right Side Buttons")]
    [SerializeField] private Button communityCenterButton;
    [SerializeField] private Button organizeButton;
    [SerializeField] private Button trashCanButton;

    public event Action<int> SlotClicked;
    public event Action OrganizeClicked;
    public event Action TrashCanClicked;

    private readonly List<InventorySlotUI> slotViews = new();
    private Sprite atlasNormal, atlasSelected, atlasLocked;

    internal static Rect InventorySlotRect(int index)
    {
        int row = index / 12;
        // InventoryMenu: a larger gap follows the hotbar row, then 4px between rows.
        return new Rect(56 + index % 12 * 64, row == 0 ? 56 : 136 + (row - 1) * 68, 64, 64);
    }

    private void Awake()
    {
        if (organizeButton != null)
        {
            organizeButton.onClick.AddListener(() => OrganizeClicked?.Invoke());
        }

        if (trashCanButton != null)
        {
            trashCanButton.onClick.AddListener(() => TrashCanClicked?.Invoke());
            RectTransform rect = trashCanButton.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(880f, -328f);
        }
    }

    private RawImage trashBody, trashLid;

    /// <summary>Shows the trash can for the player's upgrade level, with the lid open while an item hovers.</summary>
    internal void UpdateTrashCan(int level, bool holdingItem)
    {
        if (trashCanButton == null || !trashCanButton.gameObject.activeInHierarchy) return;
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        var rect = (RectTransform)trashCanButton.transform;
        if (trashBody == null)
        {
            if (trashCanButton.targetGraphic != null) trashCanButton.targetGraphic.color = new Color(1f, 1f, 1f, 0f);
            trashBody = NewPart("Trash Body", new Vector2(0f, 0f), new Vector2(1f, 1f));
            trashLid = NewPart("Trash Lid", new Vector2(0f, 1f - 10f / 26f), new Vector2(1f, 1f));
            trashLid.rectTransform.pivot = new Vector2(0f, 0f);
        }
        trashBody.texture = trashLid.texture = cursors;
        trashBody.uvRect = StardewUiAtlas.ToUvCursors(StardewUiAtlas.TrashCanBody(level));
        trashLid.uvRect = StardewUiAtlas.ToUvCursors(StardewUiAtlas.TrashCanLid(level));
        bool hover = UnityEngine.InputSystem.Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(rect,
            UnityEngine.InputSystem.Mouse.current.position.ReadValue(), GetComponentInParent<Canvas>()?.worldCamera);
        float target = holdingItem && hover ? 35f : 0f;
        float angle = Mathf.MoveTowardsAngle(trashLid.rectTransform.localEulerAngles.z, target, 240f * Time.unscaledDeltaTime);
        trashLid.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);

        RawImage NewPart(string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(rect, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = min;
            image.rectTransform.anchorMax = max;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }
    }

    public void SetButtonsActive(bool showSideButtons, bool showTrashOnly = false)
    {
        if (communityCenterButton != null) communityCenterButton.gameObject.SetActive(showSideButtons && !showTrashOnly);
        if (organizeButton != null) organizeButton.gameObject.SetActive(showSideButtons && !showTrashOnly);
        if (trashCanButton != null) trashCanButton.gameObject.SetActive(showSideButtons || showTrashOnly);
    }

    public void InitializeSlots(int totalCapacity, Sprite normalSprite, Sprite selectedSprite, Sprite lockedSprite)
    {
        if (gridContainer == null || slotPrefab == null) return;

        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        slotViews.Clear();

        Texture2D atlas = StardewUiAtlas.MenuTiles;
        if (atlas != null && atlasNormal == null)
        {
            atlasNormal = MakeSlotSprite(10);
            atlasSelected = MakeSlotSprite(56);
            atlasLocked = MakeSlotSprite(57);
        }
        if (gridContainer.TryGetComponent(out GridLayoutGroup grid)) grid.enabled = false;
        var containerRect = (RectTransform)gridContainer;
        containerRect.anchorMin = containerRect.anchorMax = containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(864f, 664f);

        for (int i = 0; i < totalCapacity; i++)
        {
            int index = i;
            var slotUI = Instantiate(slotPrefab, gridContainer);
            slotUI.Initialize(index, atlasNormal != null ? atlasNormal : normalSprite,
                atlasSelected != null ? atlasSelected : selectedSprite,
                atlasLocked != null ? atlasLocked : lockedSprite, showNumber: false);
            var rect = (RectTransform)slotUI.transform;
            Rect slot = InventorySlotRect(index);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(slot.center.x, -slot.center.y);
            rect.sizeDelta = slot.size;
            slotUI.UseInventoryPageStyle();
            slotUI.Clicked += OnInternalSlotClicked;
            slotViews.Add(slotUI);
        }

        Sprite MakeSlotSprite(int index)
        {
            Rect source = StardewUiAtlas.MenuTileRect(index);
            return FarmerSpriteCatalog.Slice(atlas, (int)source.x, (int)source.y, 64, 64);
        }
    }

    private void OnDestroy()
    {
        Destroy(atlasNormal);
        Destroy(atlasSelected);
        Destroy(atlasLocked);
    }

    private void OnInternalSlotClicked(int index)
    {
        SlotClicked?.Invoke(index);
    }

    public void RenderSlot(int index, IReadOnlyItemSlot slot, bool isSelected, bool isLocked)
    {
        if (index >= 0 && index < slotViews.Count)
        {
            slotViews[index].Render(slot, isSelected, isLocked);
        }
    }

    public void UpdateHotbarNumbers(int selectedToolbarIndex)
    {
        if (hotbarNumberTexts == null) return;

        for (int i = 0; i < hotbarNumberTexts.Length; i++)
        {
            if (hotbarNumberTexts[i] != null)
            {
                hotbarNumberTexts[i].color = (i == selectedToolbarIndex) ? SelectedNumberColor : NormalNumberColor;
            }
        }
    }

    public void UseBitmapNumbers()
    {
        if (hotbarNumberTexts == null) return;
        foreach (TextMeshProUGUI label in hotbarNumberTexts)
            if (label != null) label.enabled = false;
    }

    public int SlotCount => slotViews.Count;
}
