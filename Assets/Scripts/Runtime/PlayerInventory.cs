using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerItemKind { Hoe, Seeds, Material }

[Serializable]
public sealed class InventorySlot
{
    public string displayName;
    public PlayerItemKind kind;
    [Min(0)] public int amount;
}

public sealed class PlayerInventory : MonoBehaviour
{
    [SerializeField] private List<InventorySlot> slots = new()
    {
        new InventorySlot { displayName = "Cuốc", kind = PlayerItemKind.Hoe, amount = 1 },
        new InventorySlot { displayName = "Hạt giống", kind = PlayerItemKind.Seeds, amount = 10 },
        new InventorySlot { displayName = "Gỗ", kind = PlayerItemKind.Material, amount = 20 }
    };
    [SerializeField] private int selectedIndex;
    [SerializeField] private bool isOpen;

    public bool IsOpen => isOpen;
    public InventorySlot SelectedItem => slots.Count == 0 ? null : slots[Mathf.Clamp(selectedIndex, 0, slots.Count - 1)];
    public event Action<bool> VisibilityChanged;

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        for (var i = 0; i < Mathf.Min(9, slots.Count); i++)
        {
            if (keyboard[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit1 + i)].wasPressedThisFrame)
                selectedIndex = i;
        }

        if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
            SetOpen(false);
    }

    public void Toggle() => SetOpen(!isOpen);
    public void SetOpen(bool value)
    {
        if (isOpen == value) return;
        isOpen = value;
        VisibilityChanged?.Invoke(isOpen);
    }

    public bool ConsumeSelected(PlayerItemKind expectedKind, int amount = 1)
    {
        var item = SelectedItem;
        if (item == null || item.kind != expectedKind || item.amount < amount) return false;
        item.amount -= amount;
        return true;
    }

    private void OnGUI()
    {
        if (!isOpen) return;
        var area = new Rect(20, 20, 260, 52 + slots.Count * 28);
        GUI.Box(area, "Túi đồ  (Tab / I để đóng)");
        for (var i = 0; i < slots.Count; i++)
        {
            var item = slots[i];
            var prefix = i == selectedIndex ? ">" : " ";
            GUI.Label(new Rect(38, 52 + i * 28, 230, 24), $"{prefix} {i + 1}. {item.displayName} x{item.amount}");
        }
    }
}
