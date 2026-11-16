using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bag and chest requests: transfers, organizing, food and fish catches.</summary>
public sealed partial class OnlineGameState
{
    public void CreateChest(string name, int tileX, int tileY, Action<string> done = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.CreateChest(name, "FarmHouse", tileX, tileY,
            (snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    public void PlaceExistingChest(string chestId, int tileX, int tileY, Action<string> done = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.PlaceExistingChest(chestId, "FarmHouse", tileX, tileY,
            (snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    public void Transfer(string fromChestId, int fromSlot, string toChestId, int toSlot,
                         int amount, Action<string> done = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.Transfer(fromChestId, fromSlot, toChestId, toSlot, amount,
            (snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    public void SwapInventory(int fromSlot, int toSlot, Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.SwapInventory(fromSlot, toSlot, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    /// <summary>Chest menu: "quick-move" a stack across, "fill-stacks" or "organize" the chest.</summary>
    internal void ChestAction(string action, string chestId, bool fromChest, int slot, int amount, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ChestAction(action, chestId, fromChest, slot, amount, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done?.Invoke(error);
        }));
    }

    internal void BundleAction(string action, int bundle, int slot, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.BundleAction(action, bundle, slot, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done?.Invoke(error);
        }));
    }

    internal void OrganizeInventory(Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.OrganizeInventory((snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    public void DiscardInventory(int slot, string itemId, Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        long moneyBefore = Snapshot?.money ?? 0;
        StartCoroutine(api.DiscardInventory(slot, itemId, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            long refund = error == null && snapshot != null ? snapshot.money - moneyBefore : 0;
            if (refund > 0) HudToast.Show("Thùng rác hoàn lại +" + refund + "g", null);
            done?.Invoke(error);
        }));
    }

    public void Craft(string recipeName, Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.Craft(recipeName, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    public void ConsumeFood(int slot, string itemId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.ConsumeFood(slot, itemId, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    public void BeginFishAttempt(Action<StartupApiClient.FishAttemptResult, string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke(null, "Not signed in."); return; }
        StartCoroutine(api.BeginFishAttempt(done));
    }

    public void SaveFishCatch(ItemData fish, string attemptId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        if (fish == null || string.IsNullOrEmpty(fish.itemId) || string.IsNullOrEmpty(attemptId))
        { done?.Invoke("Fish is invalid."); return; }
        catalog.Register(fish);
        StartCoroutine(api.SaveFishCatch(attemptId, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }
}
