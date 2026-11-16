using System.Collections.Generic;
using UnityEngine;

/// <summary>Small messages with an item icon stacked in the bottom-left corner, like the HUD messages of the original.</summary>
internal sealed class HudToast : MonoBehaviour
{
    private const float Lifetime = 3.5f;

    private sealed class Entry
    {
        public string ItemId;
        public int Count;
        public string Text;
        public Sprite Icon;
        public float Until;
    }

    private static HudToast instance;
    private readonly List<Entry> entries = new();

    /// <summary>"Gỗ x5" in the corner; picking up more of the same item adds to its count.</summary>
    internal static void ShowItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;
        EnsureInstance();
        foreach (Entry entry in instance.entries)
        {
            if (entry.ItemId != item.itemId) continue;
            entry.Count += amount;
            entry.Text = $"{item.displayName} x{entry.Count}";
            entry.Until = Time.unscaledTime + Lifetime;
            return;
        }
        instance.entries.Add(new Entry
        {
            ItemId = item.itemId, Count = amount, Text = $"{item.displayName} x{amount}",
            Icon = item.GetIcon(), Until = Time.unscaledTime + Lifetime
        });
        if (instance.entries.Count > 5) instance.entries.RemoveAt(0);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("HUD Toasts");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HudToast>();
    }

    internal static void Show(string text, Sprite icon)
    {
        if (instance == null)
        {
            var go = new GameObject("HUD Toasts");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HudToast>();
        }
        foreach (Entry entry in instance.entries)
        {
            if (entry.ItemId != null || entry.Icon != icon || icon == null) continue;
            entry.Text = text;
            entry.Until = Time.unscaledTime + Lifetime;
            return;
        }
        instance.entries.Add(new Entry { Text = text, Icon = icon, Until = Time.unscaledTime + Lifetime });
        if (instance.entries.Count > 5) instance.entries.RemoveAt(0);
    }

    private void OnGUI()
    {
        entries.RemoveAll(entry => Time.unscaledTime >= entry.Until);
        if (entries.Count == 0) return;
        GUI.depth = -40;
        float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 1.5f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float height = Screen.height / scale;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[entries.Count - 1 - i];
            float alpha = Mathf.Clamp01((entry.Until - Time.unscaledTime) / 0.5f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float width = GameText.Measure(entry.Text, GameText.Face.Dialogue) + 110f;
            var box = new Rect(16f, height - 120f - i * 84f, width, 76f);
            StardewUiAtlas.DrawTextureBox(box, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, GUI.color, 1f);
            if (entry.Icon != null && Event.current.type == EventType.Repaint)
            {
                Rect source = entry.Icon.textureRect;
                Texture2D texture = entry.Icon.texture;
                GUI.DrawTextureWithTexCoords(new Rect(box.x + 18f, box.y + 14f, 48f, 48f), texture,
                    new Rect(source.x / texture.width, source.y / texture.height,
                        source.width / texture.width, source.height / texture.height));
            }
            GameText.Draw(new Rect(box.x + 78f, box.y + 16f, width - 80f, 44f), entry.Text, GameText.Face.Dialogue,
                new Color(GameText.TextColor.r, GameText.TextColor.g, GameText.TextColor.b, GUI.color.a));
            GUI.color = previous;
        }
        GUI.matrix = old;
    }
}
