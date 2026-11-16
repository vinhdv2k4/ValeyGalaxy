using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Money requests: shops, shipping, Joja, crafting and Clint's upgrades.</summary>
public sealed partial class OnlineGameState
{
    public void Buy(string itemId, int amount, Action<string> done = null, string shop = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.Buy(itemId, amount, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }, shop));
    }

    public void BuyBackpack(Action<string> done = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.BuyBackpack((snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    public void Sell(int slot, string itemId, int amount, Action<string> done = null, string shop = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.Sell(slot, itemId, amount, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }, shop));
    }

    public void Ship(int slot, string itemId, int amount, Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.Ship(slot, itemId, amount, (snapshot, error) =>
        {
            if (string.IsNullOrEmpty(error)) ApplyResult(snapshot, null);
            done?.Invoke(error);
        }));
    }

    public void RetrieveLastShipped(Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.RetrieveLastShipped((snapshot, error) =>
        {
            if (string.IsNullOrEmpty(error)) ApplyResult(snapshot, null);
            done?.Invoke(error);
        }));
    }

    /// <summary>Joja membership (project -1) or one Community Development project (0-4).</summary>
    internal void JojaPurchase(int project, Action<string> done)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null) { done?.Invoke("Bạn cần đăng nhập."); return; }
        StartCoroutine(PushThenJoja(project, done));
    }

    private IEnumerator PushThenJoja(int project, Action<string> done)
    {
        // The server checks the farmer stands at Morris's desk.
        yield return PushPresence();
        Action<OnlineGameSnapshot, string> apply = (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done?.Invoke(error);
        };
        yield return project < 0 ? api.JoinJoja(apply) : api.BuyJojaProject(project, apply);
    }

    internal void LoadShop(string shop, Action<StartupApiClient.ShopOfferList, string> done)
    {
        if (api == null || Snapshot == null) { done(null, "Not signed in."); return; }
        StartCoroutine(api.LoadShop(shop, done));
    }

    internal void CrackGeode(int slot, string itemId, Action<OnlineGeodeReward, string> done)
    {
        if (api == null || Snapshot == null) { done(null, "Not signed in."); return; }
        StartCoroutine(api.CrackGeode(slot, itemId, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            var reward = snapshot?.geodeReward;
            done(reward != null && reward.amount > 0 && !string.IsNullOrEmpty(reward.itemId) ? reward : null, error);
        }));
    }

    internal void UpgradeHouse(Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Not signed in."); return; }
        StartCoroutine(api.UpgradeHouse((snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done(error);
        }));
    }

    internal void UpgradeTool(int slot, string itemId, bool collect, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Not signed in."); return; }
        if (collect && (Snapshot.ActiveToolUpgrade is not OnlineToolUpgrade order || Snapshot.gameDay < order.readyDay))
        {
            done("Your tool is not ready.");
            return;
        }
        StartCoroutine(api.UpgradeTool(slot, itemId, collect, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }
}
