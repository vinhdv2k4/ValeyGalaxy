using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Progress requests: friendship, daily events, milestones, professions, the mines and the overnight settlement.</summary>
public sealed partial class OnlineGameState
{
    private readonly Dictionary<string, StartupApiClient.NpcFriendshipRecord> npcFriendships = new(StringComparer.Ordinal);
    private bool shippingSettlementInProgress;
    private bool mineArrivalPending;
    private float nextMineArrivalAttempt;

    public int GetNpcFriendshipPoints(string npcName) =>
        GetNpcFriendship(npcName)?.points ?? 0;

    internal StartupApiClient.NpcFriendshipRecord GetNpcFriendship(string npcName) =>
        npcName == null ? null : npcFriendships.GetValueOrDefault(npcName);

    public void GiveNpcGift(string npcName, int slot, string itemId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.GiveNpcGift(npcName, slot, itemId, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            if (string.IsNullOrEmpty(error)) RefreshNpcFriendships();
            done?.Invoke(error);
        }));
    }

    private void RefreshNpcFriendships()
    {
        if (api == null || !api.IsSignedIn) return;
        StartCoroutine(api.LoadNpcFriendships((response, error) =>
        {
            if (!string.IsNullOrEmpty(error) || response?.friends == null) return;
            npcFriendships.Clear();
            foreach (var friend in response.friends)
                if (friend != null && !string.IsNullOrEmpty(friend.npcName))
                    npcFriendships[friend.npcName] = friend;
        }));
    }

    private void OnDayStarted(int day) => RequestDaySettlement(day);

    internal void RequestDaySettlement(int day)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null || Snapshot.gameDay >= day || shippingSettlementInProgress) return;
        StartCoroutine(SettleShippingDay(day));
    }

    private IEnumerator SettleShippingDay(int day)
    {
        shippingSettlementInProgress = true;
        for (int attempt = 0; attempt < 3 && api != null && api.IsSignedIn; attempt++)
        {
            OnlineGameSnapshot settled = null;
            string settleError = null;
            yield return api.AdvanceGameDay(day, (snapshot, error) =>
            {
                settled = snapshot;
                settleError = error;
            }, PassedOutAt);
            if (string.IsNullOrEmpty(settleError) && settled != null)
            {
                PassedOutAt = null;
                ApplyResult(settled, null);
                if (LocationManager.Instance?.CurrentLocation?.LocationName == "Farm")
                    RefreshFarmCrops();
                shippingSettlementInProgress = false;
                yield break;
            }
            OnlineGameSnapshot refreshed = null;
            yield return api.LoadGameState((snapshot, error) =>
            {
                if (string.IsNullOrEmpty(error)) refreshed = snapshot;
            });
            if (refreshed != null)
            {
                ApplyResult(refreshed, null);
                if (refreshed.gameDay >= day)
                {
                    shippingSettlementInProgress = false;
                    yield break;
                }
            }
            yield return new WaitForSecondsRealtime(5f);
        }
        shippingSettlementInProgress = false;
        if (FarmDayClock.Instance?.IsChangingDay != true)
            NpcConversation.ShowNotice("Chưa thể quyết toán thùng bán hàng; hãy kiểm tra kết nối.");
    }

    internal void ActOnDailyEvent(StartupApiClient.DailyEventAction action, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ActOnDailyEvent(action, (snapshot, error) =>
        {
            if (error == null || action.action != "cutscene") ApplyResult(snapshot, error);
            done(error);
        }));
    }

    /// <summary>Saves the profession picked at a skill's level 5 or 10.</summary>
    internal void ChooseProfession(int skill, int level, int profession, Action<string> done)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null) { done?.Invoke("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ChooseProfession(skill, level, profession, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done?.Invoke(error);
        }));
    }

    /// <summary>Where the farmer passed out at 2 AM, sent with the next day's settlement.</summary>
    internal string PassedOutAt { get; set; }

    /// <summary>
    /// Settles a knockout on a mine floor. Calls back with the gold and items that were lost
    /// (worked out from the snapshots), or an error when the server refused it.
    /// </summary>
    internal void MineKnockout(int floor, Action<long, List<(string ItemId, int Amount)>, string> done)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null)
        {
            done(0, new List<(string, int)>(), "Not signed in.");
            return;
        }
        StartCoroutine(SettleKnockout(floor, done));
    }

    private IEnumerator SettleKnockout(int floor, Action<long, List<(string ItemId, int Amount)>, string> done)
    {
        var before = Snapshot;
        string lastError = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            // The server checks the farmer is still on that floor.
            yield return PushPresence();
            OnlineGameSnapshot result = null;
            string error = null;
            yield return api.MineKnockout(floor, (snapshot, failure) => { result = snapshot; error = failure; });
            if (error == null && result != null)
            {
                var lost = new List<(string, int)>();
                foreach (var stack in before.inventory ?? Array.Empty<OnlineStack>())
                {
                    if (stack == null || string.IsNullOrEmpty(stack.itemId)) continue;
                    bool kept = false;
                    foreach (var now in result.inventory ?? Array.Empty<OnlineStack>())
                        if (now != null && now.slot == stack.slot && now.itemId == stack.itemId) { kept = true; break; }
                    if (!kept) lost.Add((stack.itemId, stack.amount));
                }
                long gold = Math.Max(0, before.money - result.money);
                ApplyResult(result, null);
                done(gold, lost, null);
                yield break;
            }
            lastError = error;
        }
        done(0, new List<(string, int)>(), lastError);
    }

    internal void RecordMineArrival(int floor)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null || mineArrivalPending ||
            floor is < 1 or > 120 || floor != Snapshot.deepestMineFloor + 1 ||
            Time.unscaledTime < nextMineArrivalAttempt) return;
        StartCoroutine(SaveMineArrival(floor));
    }

    private IEnumerator SaveMineArrival(int floor)
    {
        mineArrivalPending = true;
        string requestId = Guid.NewGuid().ToString();
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                OnlineGameSnapshot saved = null;
                string error = null;
                yield return api.RecordMineArrival(floor, requestId,
                    (snapshot, failure) => { saved = snapshot; error = failure; });
                if (error == null && saved != null)
                {
                    if (floor % 5 == 0 && saved.deepestMineFloor > Snapshot.deepestMineFloor &&
                        MineManager.CurrentFloor == floor && MineManager.IsInMine)
                        GameAudio.Play("crystal");
                    ApplyResult(saved, null);
                    yield break;
                }
                if (error is not ("Vị trí người chơi chưa đồng bộ." or "State changed; reload and retry.")) break;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            nextMineArrivalAttempt = Time.unscaledTime + 2f;
        }
        finally { mineArrivalPending = false; }
    }

    internal void ActOnMilestone(StartupApiClient.MilestoneAction action, Action<string> done) =>
        ActOnMilestone(action, (_, error) => done(error));

    internal void ActOnMilestone(StartupApiClient.MilestoneAction action, Action<OnlineGameSnapshot, string> done)
    {
        if (api == null || Snapshot == null) { done(null, "Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ActOnMilestone(action, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            if (string.IsNullOrEmpty(error) && action.id == "pet_adoption") RefreshNpcFriendships();
            done(snapshot, error);
        }));
    }

    internal void ClaimMineTreasure(int floor, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Sign in to claim the mine reward."); return; }
        StartCoroutine(api.ClaimMineTreasure(floor, (snapshot, error) =>
        {
            if (error == null && snapshot != null)
            {
                if (snapshot.version >= Snapshot.version) ApplyResult(snapshot, null);
            }
            else if (error == null) error = "Could not claim the mine reward.";
            done(error);
        }));
    }
}
