using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Farm requests: crops, soil, watering, natural resources and forage.</summary>
public sealed partial class OnlineGameState
{
    private int farmCropRefreshVersion;
    private readonly Queue<(string Location, int X, int Y, string Kind, bool Stump, Action<bool> Done)>
        naturalRemovals = new();
    private bool savingNaturalRemovals;

    private void RefreshFarmCrops(Action<string> done = null)
    {
        if (api == null || !api.IsSignedIn)
        {
            done?.Invoke("Not signed in.");
            return;
        }
        int refreshVersion = ++farmCropRefreshVersion;
        GameLocation farm = LocationManager.Instance?.CurrentLocation;
        var farming = GetComponent<PlayerFarmingInteractor>();
        StartCoroutine(api.LoadFarmCrops((response, error) =>
        {
            if (!string.IsNullOrEmpty(error)) LastError = error;
            else if (refreshVersion == farmCropRefreshVersion && farm != null &&
                farm.LocationName == "Farm" && LocationManager.Instance?.CurrentLocation == farm)
            {
                farming?.RestoreServerCrops(response, ResolveItem);
                FarmResourceSpawner.Instance?.RestoreVegetation(farm, Snapshot, cropsLoaded: true);
            }
            done?.Invoke(error);
        }));
    }

    public void CollectResource(string location, int tileX, int tileY, ItemData item,
                                int amount, Action<string> done = null)
    {
        if (api == null || Snapshot == null || item == null)
        { done?.Invoke("Not signed in or invalid resource."); return; }
        catalog.Register(item);
        StartCoroutine(CollectWithFreshPresence(location, tileX, tileY, item.itemId, amount, done));
    }

    private IEnumerator CollectWithFreshPresence(string location, int tileX, int tileY, string itemId,
                                                 int amount, Action<string> done)
    {
        // The server checks the claim against the last presence sync and drops any presence
        // older than 8 seconds. One slow or failed background sync was enough to reject a
        // pickup made right next to the resource, so push the current position first.
        yield return PushPresence();
        yield return api.CollectResource(location, tileX, tileY, itemId, amount,
            (snapshot, error) =>
            {
                // A refused pickup is normal play (already collected, bag full); not an error to log.
                if (error == null) ApplyResult(snapshot, null);
                else LastError = error;
                done?.Invoke(error);
            });
    }

    public void PlantFarmCrop(int tileX, int tileY, int seedSlot, string seedItemId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.PlantFarmCrop(tileX, tileY, seedSlot, seedItemId,
            (snapshot, error) =>
            {
                if (!string.IsNullOrEmpty(error)) { done?.Invoke(error); return; }
                ApplyResult(snapshot, null);
                RefreshFarmCrops(done);
            }));
    }

    internal void ChangeFarmSoil(int tileX, int tileY, bool clear, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Đăng nhập để canh tác."); return; }
        StartCoroutine(api.ChangeFarmSoil(tileX, tileY, clear, (snapshot, error) =>
        {
            if (error != null) { done(error); return; }
            ApplyResult(snapshot, null);
            RefreshFarmCrops(done);
        }));
    }

    /// <summary>Charged Hoe: tills every eligible tile of the area in one request.</summary>
    internal void ChangeFarmSoilArea(int power, IReadOnlyList<Vector2Int> tiles, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Đăng nhập để canh tác."); return; }
        StartCoroutine(api.ChangeFarmSoilArea(power, tiles, (snapshot, error) =>
        {
            if (error != null) { done(error); return; }
            ApplyResult(snapshot, null);
            RefreshFarmCrops(done);
        }));
    }

    /// <summary>Charged Watering Can: waters every tile of the area in one request.</summary>
    internal void WaterFarmArea(int power, IReadOnlyList<Vector2Int> tiles, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Not signed in."); return; }
        StartCoroutine(api.WaterFarmArea(power, tiles, (snapshot, error) =>
        {
            ApplyResult(snapshot, error);
            done?.Invoke(error);
        }));
    }

    internal void PayBaseFarmRentDebt(Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Đăng nhập để thanh toán."); return; }
        StartCoroutine(api.PayBaseFarmRentDebt((snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    public void WaterFarmCrop(int tileX, int tileY, Action<string> done)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.WaterFarmCrop(tileX, tileY,
            (snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    public void HarvestFarmCrop(int tileX, int tileY, Action<string> done = null)
    {
        if (api == null || Snapshot == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.HarvestFarmCrop(tileX, tileY,
            (snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    internal void ClearNaturalResource(string location, int tileX, int tileY, string kind, bool stump,
        Action<bool> done)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null)
        {
            done(false);
            return;
        }
        naturalRemovals.Enqueue((location, tileX, tileY, kind, stump, done));
        if (!savingNaturalRemovals)
            StartCoroutine(SaveNaturalResources());
    }

    private IEnumerator SaveNaturalResources()
    {
        // A scythe can clear several markers with one swing. Save them in order.
        savingNaturalRemovals = true;
        while (naturalRemovals.Count > 0)
        {
            var removal = naturalRemovals.Dequeue();
            yield return SaveNaturalResource(removal.Location, removal.X, removal.Y,
                removal.Kind, removal.Stump, removal.Done);
        }
        savingNaturalRemovals = false;
    }

    private IEnumerator SaveNaturalResource(string location, int tileX, int tileY, string kind, bool stump,
        Action<bool> done)
    {
        string requestId = Guid.NewGuid().ToString();
        string error = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            OnlineGameSnapshot saved = null;
            yield return api.ClearNaturalResource(requestId, location, tileX, tileY, kind, stump,
                (snapshot, failure) => { saved = snapshot; error = failure; });
            if (saved != null && error == null)
            {
                done(true);
                ApplyResult(saved, null);
                yield break;
            }
            if (error == null || !(error.StartsWith("Cannot reach game server", StringComparison.Ordinal) ||
                error is "State changed; reload and retry." or "Request was already processed.")) break;
        }
        LastError = error;
        Debug.LogWarning("Could not save vegetation: " + error);
        done(false);
        if (error == "Vegetation has changed. Re-enter the map.")
            Reload(_ => FarmResourceSpawner.Instance?.RestoreVegetation(LocationManager.Instance?.CurrentLocation, Snapshot));
    }

    /// <summary>Picks a piece of the day's forage; the server checks the farmer stands next to it.</summary>
    internal void PickForage(string location, int x, int y, Action<string> done)
    {
        if (api == null || !api.IsSignedIn || Snapshot == null) { done?.Invoke("Bạn cần đăng nhập."); return; }
        StartCoroutine(PushThenForage(location, x, y, done));
    }

    private IEnumerator PushThenForage(string location, int x, int y, Action<string> done)
    {
        yield return PushPresence();
        yield return api.PickForage(location, x, y, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done?.Invoke(error);
        });
    }

    internal void Fertilize(int tileX, int tileY, string itemId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.Fertilize(tileX, tileY, itemId, (snapshot, error) =>
        {
            if (error == null)
            {
                ApplyResult(snapshot, null);
                RefreshFarmCrops();
            }
            done(error);
        }));
    }
}
