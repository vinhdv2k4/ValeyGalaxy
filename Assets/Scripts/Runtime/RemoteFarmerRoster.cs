using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The other farmers on the local farmer's map, keyed by user id: turns each presence response
/// into puppets (spawn, move, held item, action), removes those who left, and hides them all
/// while the local farmer watches a private cutscene.
/// </summary>
internal sealed class RemoteFarmerRoster
{
    private readonly Dictionary<string, RemotePlayerPuppet> farmers = new(StringComparer.Ordinal);
    // Reused by every presence reply (10 a second) instead of allocating new collections.
    private readonly HashSet<string> present = new(StringComparer.Ordinal);
    private readonly List<string> departed = new();
    private bool hidden;

    /// <summary>Đồng bộ hiển thị ngoại hình, vị trí, hành động và bảng tên của người chơi khác.</summary>
    internal void Apply(StartupApiClient.RemotePlayerDto[] players, string location, OnlineGameState template)
    {
        present.Clear();
        // Farm và FarmCave là bản đồ riêng của từng tài khoản.
        bool shared = location != "Farm" && location != "FarmCave";
        if (shared && players != null)
        {
            foreach (var player in players)
            {
                if (player == null || string.IsNullOrEmpty(player.userId) ||
                    !string.Equals(player.location, location, StringComparison.OrdinalIgnoreCase)) continue;
                present.Add(player.userId);
                var puppet = GetOrSpawn(player, template);
                if (puppet == null) continue;
                puppet.UpdateTarget(new Vector2(player.x, player.y), new Vector2(player.facingX, player.facingY));
                if (template != null) puppet.ApplyHeldItem(player.heldItemId, template.ResolveItem);
                puppet.ApplyAction(player.actionSeq, player.actionName,
                    new Vector2(player.actionFacingX, player.actionFacingY));
            }
        }

        // Xóa những người chơi đã rời game hoặc đã chuyển sang khu vực khác
        departed.Clear();
        foreach (var id in farmers.Keys)
            if (!present.Contains(id)) departed.Add(id);
        foreach (var id in departed) Remove(id);
    }

    private RemotePlayerPuppet GetOrSpawn(StartupApiClient.RemotePlayerDto player, OnlineGameState template)
    {
        if (farmers.TryGetValue(player.userId, out var puppet) && puppet != null) return puppet;
        puppet = RemotePlayerPuppet.Spawn(template, player);
        if (puppet == null) return null;
        farmers[player.userId] = puppet;
        if (hidden) puppet.gameObject.SetActive(false);
        return puppet;
    }

    /// <summary>Cutscenes are private: other farmers disappear for the one watching.</summary>
    internal void SetHidden(bool hide)
    {
        if (hide == hidden) return;
        hidden = hide;
        foreach (var puppet in farmers.Values)
            if (puppet != null) puppet.gameObject.SetActive(!hide);
    }

    internal void Remove(string userId)
    {
        if (!farmers.TryGetValue(userId, out var puppet)) return;
        if (puppet != null) UnityEngine.Object.Destroy(puppet.gameObject);
        farmers.Remove(userId);
    }

    internal void Clear()
    {
        foreach (var puppet in farmers.Values)
            if (puppet != null) UnityEngine.Object.Destroy(puppet.gameObject);
        farmers.Clear();
    }

    /// <summary>
    /// The other farmer the local one is gifting to (Farmer.checkAction): under the pointer, or
    /// in front when using the action key, within 2.4 tiles like an NPC gift.
    /// </summary>
    internal bool TryFindNear(Vector2? pointer, Vector3 standing, Vector2 facing,
        out string userId, out string displayName, out Vector3 feet)
    {
        userId = null;
        displayName = null;
        feet = default;
        float best = 2.4f * 2.4f;
        foreach (var pair in farmers)
        {
            var puppet = pair.Value;
            if (puppet == null || !puppet.gameObject.activeInHierarchy) continue;
            // A puppet's transform sits on the standing point the other client reported.
            Vector3 at = puppet.transform.position;
            Vector2 offset = (Vector2)(at - standing);
            float distance = offset.sqrMagnitude;
            if (distance > best) continue;
            if (pointer.HasValue)
            {
                // The farmer sprite covers about a tile across and two tiles up from the feet.
                Vector2 local = pointer.Value - (Vector2)at;
                if (Mathf.Abs(local.x) > 0.75f || local.y < -0.75f || local.y > 2f) continue;
            }
            else if (distance > 0.0001f && Vector2.Dot(offset.normalized, facing) < 0.35f) continue;

            best = distance;
            userId = pair.Key;
            displayName = puppet.DisplayName;
            feet = at;
        }
        return userId != null;
    }
}
