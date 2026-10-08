using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the 36-slot backpack grid (3 rows x 12 cols),
/// row 1 hotbar number indicators, and right-side action buttons.
/// Adheres strictly to Stardew Valley metrics & visuals.
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

    private void Awake()
    {
        if (organizeButton != null)
        {
            organizeButton.onClick.AddListener(() => OrganizeClicked?.Invoke());
        }

        if (trashCanButton != null)
        {
            trashCanButton.onClick.AddListener(() => TrashCanClicked?.Invoke());
        }
    }

    public void InitializeSlots(int totalCapacity, Sprite normalSprite, Sprite selectedSprite, Sprite lockedSprite)
    {
        if (gridContainer == null || slotPrefab == null) return;

        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        slotViews.Clear();

        for (int i = 0; i < totalCapacity; i++)
        {
            int index = i;
            var slotUI = Instantiate(slotPrefab, gridContainer);
            slotUI.Initialize(index, normalSprite, selectedSprite, lockedSprite, showNumber: false);
            slotUI.Clicked += OnInternalSlotClicked;
            slotViews.Add(slotUI);
        }
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

    public int SlotCount => slotViews.Count;
}
