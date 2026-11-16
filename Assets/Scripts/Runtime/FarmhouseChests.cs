using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>
/// The farmer's chests inside the farmhouse: the chest objects mirrored from the snapshot,
/// placing one with the use or action button like any other object, and opening one.
/// Requests go through <see cref="OnlineGameState"/>, which owns this.
/// </summary>
internal sealed class FarmhouseChests
{
    private readonly OnlineGameState owner;
    private readonly PlayerInventory inventory;
    private readonly Dictionary<string, PlacedPlayerChest> placedChests = new(StringComparer.Ordinal);
    // Reused by every snapshot instead of allocating new collections.
    private readonly HashSet<string> activeIds = new(StringComparer.Ordinal);
    private readonly List<string> staleIds = new();
    private readonly PlacedChestUI chestUi;
    private readonly GameObject chestPrefab;
    private readonly GameObject chestRoot;
    private bool placingBusy;

    internal FarmhouseChests(OnlineGameState owner, PlayerInventory inventory)
    {
        this.owner = owner;
        this.inventory = inventory;
        chestPrefab = Resources.Load<GameObject>("Startup/PlayerChest");
        var panelPrefab = Resources.Load<GameObject>("Startup/PlacedChestPanel");
        if (panelPrefab != null)
        {
            var panel = Object.Instantiate(panelPrefab);
            chestUi = panel.GetComponent<PlacedChestUI>();
            if (chestUi != null) chestUi.Initialize(owner);
        }
        chestRoot = new GameObject("PlayerPlacedChests");
    }

    /// <summary>The chest panel is open.</summary>
    internal bool IsInteracting => chestUi != null && chestUi.IsOpen;

    /// <summary>Closes the chest panel (night, leaving the house).</summary>
    internal void CloseAll()
    {
        if (chestUi != null && chestUi.IsOpen) chestUi.Close();
    }

    internal void Tick()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && chestUi != null && chestUi.IsOpen)
            chestUi.Close();
        bool atHome = IsAtHome();
        if (chestRoot != null && chestRoot.activeSelf != atHome) chestRoot.SetActive(atHome);
        if (!atHome) CloseAll();
    }

    private static bool IsAtHome()
    {
        var location = LocationManager.Instance?.CurrentLocation;
        return location != null && location.LocationName == "FarmHouse";
    }

    private bool IsValidTile(int x, int y)
    {
        if (x is < 1 or > 11 || y is < 2 or > 10) return false;
        if (Vector2.Distance(owner.transform.position, new Vector2(x + 0.5f, -y - 0.5f)) > 2.2f) return false;
        foreach (var chest in placedChests.Values)
            if (chest != null && Mathf.FloorToInt(chest.transform.position.x) == x &&
                Mathf.FloorToInt(-chest.transform.position.y) == y) return false;
        foreach (var hit in Physics2D.OverlapBoxAll(new Vector2(x + 0.5f, -y - 0.5f),
                                                     new Vector2(0.8f, 0.8f), 0f))
            if (hit != null && !hit.isTrigger && hit.transform != owner.transform &&
                hit.GetComponentInParent<PlacedPlayerChest>() == null) return false;
        return true;
    }

    /// <summary>
    /// Places a chest on the clicked tile, or the one in front, like any placeable object
    /// (Utility.tryToPlaceItem). A saved chest that was never placed goes down first; otherwise
    /// the server uses a crafted chest from the bag. Chests are kept in the farmhouse only.
    /// </summary>
    internal bool TryPlaceAt(Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        if (owner.Snapshot == null || chestPrefab == null) return false;
        if (!IsAtHome())
        {
            NpcConversation.ShowNotice("Chỉ có thể đặt rương trong nhà.");
            return true;
        }
        if (placingBusy || (chestUi != null && chestUi.IsOpen) || inventory?.IsOpen == true) return true;
        Vector2 target = pointer ?? standing + (facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down);
        int x = Mathf.FloorToInt(target.x);
        int y = Mathf.FloorToInt(-target.y);
        if (!IsValidTile(x, y))
        {
            GameAudio.Play("cancel");
            return true;
        }
        placingBusy = true;
        Action<string> finished = error =>
        {
            placingBusy = false;
            if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
            else GameAudio.Play("woodyStep");
        };
        var unplaced = FirstUnplacedChest();
        if (unplaced != null) owner.PlaceExistingChest(unplaced.id, x, y, finished);
        else owner.CreateChest("Chest " + ((owner.Snapshot.chests?.Length ?? 0) + 1), x, y, finished);
        return true;
    }

    private OnlineChest FirstUnplacedChest()
    {
        foreach (var chest in owner.Snapshot?.chests ?? Array.Empty<OnlineChest>())
            if (chest != null && string.IsNullOrEmpty(chest.location) &&
                !string.IsNullOrEmpty(chest.id)) return chest;
        return null;
    }

    internal bool TryOpenChest(Vector2? pointer, Vector3 playerPosition)
    {
        if (!IsAtHome() || chestUi == null || chestUi.IsOpen) return false;
        PlacedPlayerChest selected = null;
        float closest = pointer.HasValue ? float.MaxValue : 1.35f;
        foreach (var chest in placedChests.Values)
        {
            if (chest == null || Vector2.Distance(playerPosition, chest.transform.position) > 2.1f)
                continue;
            if (pointer.HasValue && !chest.GetComponent<SpriteRenderer>().bounds.Contains(pointer.Value))
                continue;
            float distance = Vector2.Distance(pointer ?? (Vector2)playerPosition, chest.transform.position);
            if (distance < closest) { closest = distance; selected = chest; }
        }
        if (selected == null) return false;
        string id = selected.ChestId;
        owner.Reload(error => { if (string.IsNullOrEmpty(error)) chestUi.Open(id); });
        return true;
    }

    /// <summary>Creates, moves and removes chest objects to match the snapshot.</summary>
    internal void Sync(OnlineGameSnapshot snapshot)
    {
        if (chestPrefab == null || chestRoot == null || snapshot == null) return;
        activeIds.Clear();
        foreach (var entry in snapshot.chests ?? Array.Empty<OnlineChest>())
        {
            if (entry == null || entry.location != "FarmHouse" || string.IsNullOrEmpty(entry.id))
                continue;
            activeIds.Add(entry.id);
            if (!placedChests.TryGetValue(entry.id, out var chest) || chest == null)
            {
                var instance = Object.Instantiate(chestPrefab, chestRoot.transform);
                instance.name = "PlayerChest_" + entry.id;
                chest = instance.GetComponent<PlacedPlayerChest>();
                if (chest == null) { Object.Destroy(instance); continue; }
                placedChests[entry.id] = chest;
            }
            chest.Bind(entry.id, entry.tileX, entry.tileY);
        }
        staleIds.Clear();
        foreach (var id in placedChests.Keys)
            if (!activeIds.Contains(id)) staleIds.Add(id);
        foreach (var id in staleIds)
        {
            if (placedChests[id] != null) Object.Destroy(placedChests[id].gameObject);
            placedChests.Remove(id);
        }
        chestRoot.SetActive(IsAtHome());
    }

    internal void RefreshOpenPanel()
    {
        if (chestUi != null && chestUi.IsOpen) chestUi.Refresh();
    }

    internal void Dispose()
    {
        if (chestRoot != null) Object.Destroy(chestRoot);
        if (chestUi != null) Object.Destroy(chestUi.gameObject);
    }
}
