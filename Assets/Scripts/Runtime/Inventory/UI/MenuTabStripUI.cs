using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the top tab strip (8 tabs + extra buttons + close button).
/// Raises the active tab by 8px matching Stardew Valley GameMenu.cs.
/// </summary>
public class MenuTabStripUI : MonoBehaviour
{
    [System.Serializable]
    public class TabEntry
    {
        public string tabName;
        public RectTransform tabTransform;
        public Button tabButton;
    }

    [Header("Tabs")]
    [SerializeField] private List<TabEntry> tabs = new();
    [SerializeField] private Button closeButton;
    [SerializeField] private Button extraHammerButton;
    [SerializeField] private float activeTabElevation = 8f;

    public event Action<int> TabSelected;
    public event Action CloseRequested;

    private int currentTab = 0;
    private readonly List<Vector2> originalPositions = new();

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].tabTransform != null)
            {
                originalPositions.Add(tabs[i].tabTransform.anchoredPosition);
            }
            else
            {
                originalPositions.Add(Vector2.zero);
            }

            if (tabs[i].tabButton != null)
            {
                tabs[i].tabButton.onClick.AddListener(() => SelectTab(index));
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }
    }

    public void SelectTab(int index)
    {
        currentTab = Mathf.Clamp(index, 0, tabs.Count - 1);
        UpdateTabVisuals();
        TabSelected?.Invoke(currentTab);
    }

    public void UpdateTabVisuals()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].tabTransform == null || i >= originalPositions.Count) continue;

            Vector2 pos = originalPositions[i];
            if (i == currentTab)
            {
                // Active tab is elevated upwards by activeTabElevation (matching Stardew Valley)
                pos.y += activeTabElevation;
            }
            tabs[i].tabTransform.anchoredPosition = pos;
        }
    }

    public int CurrentTab => currentTab;
}
