using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>One reward chest attached to its logical mine floor.</summary>
public sealed class MineTreasure : MonoBehaviour
{
    [Serializable]
    private sealed class RewardFile { public Reward[] rewards = Array.Empty<Reward>(); }

    [Serializable]
    private sealed class Reward
    {
        public int floor = 0, x = 0, y = 0, amount = 0, weaponType = 0;
        public string itemId = "", name = "";
        public Vector3Int Cell => new(x, -y, 0);
    }

    private const string ChestName = "Mine Reward Chest";
    private static Reward[] rewards;
    private static readonly Sprite[] chestFrames = new Sprite[4];
    private static readonly Dictionary<string, MeleeWeaponData> rewardWeapons = new(StringComparer.Ordinal);
    private Reward reward;
    private OnlineGameState online;
    private SpriteRenderer lid;
    private bool claiming;

    private static Reward[] Rewards
    {
        get
        {
            if (rewards != null) return rewards;
            var data = Resources.Load<MapDoorCatalog>("MapDoorCatalog")?.mineTreasureRewards;
            return rewards = (data != null ? JsonUtility.FromJson<RewardFile>(data.text)?.rewards : null)
                ?? Array.Empty<Reward>();
        }
    }

    internal static bool TryGetCell(int floor, out Vector3Int cell)
    {
        var entry = Array.Find(Rewards, value => value.floor == floor);
        cell = entry?.Cell ?? default;
        return entry != null;
    }

    internal static void Attach(GameLocation location, int floor, OnlineGameState online)
    {
        if (location.transform.Find(ChestName) != null ||
            online?.Snapshot?.HasClaimedMineChest(floor) == true) return;
        var entry = Array.Find(Rewards, value => value.floor == floor);
        if (entry == null) return;
        var back = location.transform.Find("Back")?.GetComponent<Tilemap>();
        if (back == null || !back.HasTile(entry.Cell)) return;
        if (chestFrames[0] == null)
        {
            var atlas = Resources.Load<Texture2D>("Maps/springobjects");
            for (int i = 0; i < chestFrames.Length; i++) chestFrames[i] = ItemData.CreateAtlasSprite(atlas, 500 + i);
        }
        if (chestFrames[0] == null || chestFrames[1] == null) return;
        var go = new GameObject(ChestName);
        go.transform.SetParent(location.transform, false);
        go.transform.position = back.GetCellCenterWorld(entry.Cell);
        var chest = go.AddComponent<MineTreasure>();
        chest.reward = entry;
        chest.online = online;
        var body = go.AddComponent<SpriteRenderer>();
        body.sprite = chestFrames[0];
        body.sortingOrder = WorldSortOrder.GetOrder(go.transform.position.y);
        body.color = floor == 120 ? new Color(1f, 0.75f, 0.8f) : Color.white;
        var lidObject = new GameObject("Lid");
        lidObject.transform.SetParent(go.transform, false);
        lidObject.transform.localPosition = Vector3.up * 0.5f;
        chest.lid = lidObject.AddComponent<SpriteRenderer>();
        chest.lid.sprite = chestFrames[1];
        chest.lid.sortingOrder = body.sortingOrder + 1;
        chest.lid.color = body.color;
        go.AddComponent<BoxCollider2D>().size = new Vector2(0.85f, 0.7f);
    }

    private void Update()
    {
        if (!claiming && online?.Snapshot?.HasClaimedMineChest(reward.floor) == true)
            Destroy(gameObject);
    }

    internal static bool TryInteract(GameLocation location, Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        var chest = location?.transform.Find(ChestName)?.GetComponent<MineTreasure>();
        if (chest == null) return false;
        Vector2 center = chest.transform.position;
        Vector2 delta = center - standing;
        if (delta.sqrMagnitude > 2.25f) return false;
        if (pointer.HasValue)
        {
            Vector2 target = pointer.Value - center;
            if (Mathf.Abs(target.x) > 0.55f || target.y < -0.55f || target.y > 1.05f) return false;
        }
        else if (delta.sqrMagnitude > 0.16f && Vector2.Dot(delta.normalized, facing) < 0.4f) return false;

        if (chest.claiming) return true;
        if (chest.online == null) { NpcConversation.ShowNotice("Sign in to claim the mine reward."); return true; }
        var player = chest.online.GetComponent<NetworkPlayerController>();
        chest.claiming = true;
        chest.online.ClaimMineTreasure(chest.reward.floor, error =>
        {
            if (error != null)
            {
                if (chest == null) return;
                chest.claiming = false;
                if (chest.isActiveAndEnabled) NpcConversation.ShowNotice(error);
                return;
            }
            if (player != null) player.StartCoroutine(chest.Open(player));
        });
        return true;
    }

    private IEnumerator Open(NetworkPlayerController player)
    {
        if (this != null && isActiveAndEnabled)
        {
            GameAudio.Play("openChest");
            lid.sprite = chestFrames[2];
            lid.transform.localPosition = Vector3.up * 0.625f;
            yield return new WaitForSecondsRealtime(0.1f);
            if (lid != null)
            {
                lid.sprite = chestFrames[3];
                lid.transform.localPosition = Vector3.up * 0.9375f;
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }
        bool stardrop = reward.itemId == "object_434";
        string message = stardrop ? "Đã nhận Stardrop! Năng lượng tối đa tăng thêm 34." :
            $"Đã nhận {reward.amount} x {reward.name}.";
        player.PresentReward(GetRewardIcon(), message,
            stardrop ? "stardrop" : "getNewSpecialItem");
        if (this != null) Destroy(gameObject);
    }

    private Sprite GetRewardIcon()
    {
        if (reward.itemId != "skull_key") return online.ResolveItem(reward.itemId)?.GetIcon();

        var atlas = StardewUiAtlas.Cursors;
        // SpecialItem's Skull Key is entry 4 in the row starting at (129, 320).
        return atlas == null ? null : AtlasSprites.Get(atlas, new Rect(193, atlas.height - 336, 16, 16),
            Vector2.one * 0.5f, 16f);
    }

    internal static MeleeWeaponData ResolveWeapon(string id)
    {
        if (!id.StartsWith("weapon_", StringComparison.Ordinal) || id is "weapon_32" or "weapon_33") return null;
        if (rewardWeapons.TryGetValue(id, out var weapon)) return weapon;
        var entry = Array.Find(Rewards, value => value.itemId == id);
        if (entry == null && id != "weapon_0" || !int.TryParse(id.Substring(7), out int tile)) return null;
        var atlas = Resources.Load<NpcCatalog>("NpcCatalog")?.weaponsAtlas;
        var icon = ItemData.CreateAtlasSprite(atlas, tile);
        if (icon == null) return null;
        return rewardWeapons[id] = MeleeWeaponData.Create(id, entry?.name ?? "Rusty Sword", icon,
            entry == null ? MeleeWeaponType.Sword : (MeleeWeaponType)entry.weaponType);
    }
}
