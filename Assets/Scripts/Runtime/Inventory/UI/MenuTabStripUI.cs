using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SRP: Manages the eight GameMenu tabs and close button.
/// Lowers the active tab by 8px as GameMenu.draw adds eight screen pixels.
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
    private RectTransform closeTransform;
    private Vector2 closePosition;

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].tabTransform != null)
            {
                // Older prefab versions already raised Inventory by 8 px.
                Vector2 position = tabs[i].tabTransform.anchoredPosition;
                if (i == 0 && position.y > 352f + 1f)
                    position.y -= activeTabElevation;
                originalPositions.Add(position);
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
            closeTransform = closeButton.GetComponent<RectTransform>();
            closePosition = closeTransform.anchoredPosition;
        }
        // GameMenu.cs defines exactly eight tabs; the ninth hammer is a stale
        // decoration left by the prefab builder and has no page in the source.
        if (extraHammerButton != null) extraHammerButton.gameObject.SetActive(false);
    }

    public void SelectTab(int index)
    {
        if (tabs.Count == 0) return;
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
                // Screen Y grows downward; Unity anchored Y grows upward.
                pos.y -= activeTabElevation;
            }
            tabs[i].tabTransform.anchoredPosition = pos;
        }
    }

    public int CurrentTab => currentTab;

    internal void ShowPage(int page, float width)
    {
        gameObject.SetActive(page != 3);
        if (closeTransform != null)
        {
            closeTransform.gameObject.SetActive(page != 3);
            closeTransform.anchoredPosition = closePosition + new Vector2(width - 864f, 0f);
        }
    }
}
