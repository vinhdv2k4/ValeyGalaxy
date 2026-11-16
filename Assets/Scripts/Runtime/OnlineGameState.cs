using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The farmer's server-side game: loads and applies snapshots, keeps the bag in sync and is the
/// one place gameplay sends requests through. Requests are grouped by system in the partial
/// files OnlineGameState.Farm / Inventory / Economy / Progress / Ranch / Objects; item ids are
/// resolved by <see cref="OnlineItemCatalog"/> and farmhouse chests live in <see cref="FarmhouseChests"/>.
/// </summary>
public sealed partial class OnlineGameState : MonoBehaviour
{
    private StartupApiClient api;
    private PlayerInventory inventory;
    private FullInventoryMenuUI inventoryUi;
    private readonly OnlineItemCatalog catalog = new();
    private FarmhouseChests chests;
    private bool wasAtFarm;
    private bool clockSubscribed;

    public OnlineGameSnapshot Snapshot { get; private set; }
    internal OnlinePlayerSession SavedSession { get; private set; }
    internal event Action SnapshotApplied;
    public string LastError { get; private set; }
    internal bool IsSettlingDay => shippingSettlementInProgress;
    internal FullInventoryMenuUI InventoryUi => inventoryUi;
    public bool IsChestInteractionOpen => (chests != null && chests.IsInteracting) || JunimoNoteMenu.IsOpen ||
        CommunityCenterRooms.IsCelebrating || AdminPanel.IsOpen;
    internal StartupApiClient Api => api;
    /// <summary>Applies a snapshot returned by an admin command.</summary>
    internal void ApplyAdminSnapshot(OnlineGameSnapshot snapshot, string error) => ApplyResult(snapshot, error);
    internal void ReloadFriendships() => RefreshNpcFriendships();

    internal ItemData ResolveItem(string id) => catalog.Resolve(id);
    internal static ItemData ResolveAnimalTool(string id) => OnlineItemCatalog.ResolveAnimalTool(id);
    /// <summary>Places a chest on the clicked tile or the one in front (farmhouse only).</summary>
    internal bool TryPlaceChest(Vector2? pointer, Vector2 standing, Vector2 facing) =>
        chests != null && chests.TryPlaceAt(pointer, standing, facing);
    public bool TryOpenChest(Vector2? pointer, Vector3 playerPosition) =>
        chests != null && chests.TryOpenChest(pointer, playerPosition);

    public void Initialize(StartupApiClient client, OnlineGameSnapshot initialSnapshot = null)
    {
        api = client;
        inventory = GetComponent<PlayerInventory>();
        inventoryUi = FindFirstObjectByType<FullInventoryMenuUI>();
        catalog.InventoryUi = inventoryUi;
        catalog.LoadBundled();
        chests = new FarmhouseChests(this, inventory);
        if (inventory != null) inventory.IsLocked = true;
        if (inventoryUi != null) inventoryUi.SetServerFunds(0);
        if (FarmDayClock.Instance != null)
        {
            FarmDayClock.Instance.DayStarted += OnDayStarted;
            clockSubscribed = true;
        }
        if (GetComponent<NetworkPlayerController>() != null && GetComponent<CutsceneDirector>() == null)
            gameObject.AddComponent<CutsceneDirector>().Initialize(this);
        if (initialSnapshot != null) ApplyResult(initialSnapshot, null);
        else StartCoroutine(api.LoadGameState(ApplyResult));
        RefreshNpcFriendships();
        StartCoroutine(MonitorServer());
    }

    private IEnumerator MonitorServer()
    {
        bool wasConnected = true;
        while (api != null && api.IsSignedIn)
        {
            yield return api.Probe(error =>
            {
                if (wasConnected && error != null)
                    Debug.LogWarning("Game server connection lost: " + error);
                else if (!wasConnected && error == null)
                    Debug.Log("Game server connection restored. Players online: " + api.OnlinePlayers);
                wasConnected = error == null;
                if (error == null && !shippingSettlementInProgress && Snapshot != null &&
                    FarmDayClock.Instance != null && FarmDayClock.Instance.Day > Snapshot.gameDay)
                    StartCoroutine(SettleShippingDay(Snapshot.gameDay + 1));
            });
            yield return new WaitForSecondsRealtime(15f);
        }
    }

    private void Update()
    {
        if (Snapshot == null) return;
        var location = LocationManager.Instance;
        if (location != null && !location.IsWarping &&
            int.TryParse(location.CurrentLocation?.LocationName, out int floor))
            RecordMineArrival(floor);
        if (FarmDayClock.Instance?.IsChangingDay == true)
        {
            chests?.CloseAll();
            return;
        }
        bool atFarm = LocationManager.Instance?.CurrentLocation?.LocationName == "Farm";
        if (atFarm && !wasAtFarm)
        {
            wasAtFarm = true;
            RefreshFarmCrops();
        }
        else if (!atFarm) wasAtFarm = false;
        chests?.Tick();
    }

    public void Reload(Action<string> done = null)
    {
        if (api == null) { done?.Invoke("Not signed in."); return; }
        StartCoroutine(api.LoadGameState((snapshot, error) => { ApplyResult(snapshot, error); done?.Invoke(error); }));
    }

    /// <summary>Sends the current position now, for a position-checked claim that was refused.</summary>
    internal void RefreshPresence(Action done) => StartCoroutine(PushPresenceThen(done));

    private IEnumerator PushPresenceThen(Action done)
    {
        yield return PushPresence();
        done?.Invoke();
    }

    private IEnumerator PushPresence()
    {
        var motor = GetComponent<CharacterMotor>();
        string here = LocationManager.Instance?.CurrentLocation?.LocationName;
        if (motor == null || string.IsNullOrEmpty(here)) yield break;
        Vector3 position = motor.StandingPosition;
        Vector2 facing = motor.Facing;
        yield return api.SyncWorldPresence(position.x, position.y, facing.x, facing.y, here, (_, error) =>
        {
            if (error != null) Debug.LogWarning("Presence sync before collecting failed: " + error);
        });
    }

    private void ApplyResult(OnlineGameSnapshot snapshot, string error)
    {
        LastError = error;
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Could not load online game state: " + error);
            return;
        }
        // Only /api/game/state carries the saved session; keep it before later snapshots replace it.
        if (SavedSession == null && snapshot?.session != null && snapshot.session.day > 0)
            SavedSession = snapshot.session;
        if (snapshot == null || Snapshot != null && snapshot.version < Snapshot.version) return;
        if (Snapshot != null && snapshot != null && snapshot.achievements != null)
        {
            foreach (int id in snapshot.achievements)
                if (Snapshot.achievements == null ||
                    Array.IndexOf(Snapshot.achievements, id) < 0)
                {
                    GameAudio.Play("achievement");
                    Debug.Log("Achievement unlocked: " + id);
                }
        }
        bool vegetationDayChanged = Snapshot == null || Snapshot.gameDay != snapshot.gameDay;
        if (Snapshot != null && !vegetationDayChanged && snapshot.piecesOfHay > Snapshot.piecesOfHay)
            HudToast.Show("Cỏ khô +" + (snapshot.piecesOfHay - Snapshot.piecesOfHay), ResolveItem("object_178")?.GetIcon());
        if (Snapshot == null)
            NightResultsMenu.ObserveInitialSkillLevels(api.UserName, snapshot.skillLevels);
        Snapshot = snapshot;
        var stats = GetComponent<NetworkCharacterStats>();
        stats?.ApplyMineStardrop(snapshot.HasClaimedMineChest(100));
        stats?.ApplySkillProgress(snapshot.skillLevels, api?.UserName, snapshot.professions);
        FarmDayClock.Instance?.RestoreDay(snapshot.gameDay, snapshot.weather);
        if (inventory != null)
        {
            inventory.SetUnlockedSlots(snapshot.bagSize);
            inventory.ReplaceFromServer(snapshot.inventory, ResolveItem);
            inventory.IsLocked = false;
        }
        if (inventoryUi != null)
        {
            inventoryUi.SetServerFunds(snapshot.money);
            inventoryUi.SetServerTotalEarnings(snapshot.totalEarnings);
        }
        chests?.Sync(snapshot);
        FarmRanchView.Refresh(LocationManager.Instance?.CurrentLocation, snapshot);
        PlacedObjectsView.Refresh(LocationManager.Instance?.CurrentLocation, snapshot);
        if (vegetationDayChanged)
            FarmResourceSpawner.Instance?.RestoreVegetation(LocationManager.Instance?.CurrentLocation, snapshot);
        chests?.RefreshOpenPanel();
        SnapshotApplied?.Invoke();
    }

    private void OnDestroy()
    {
        if (clockSubscribed && FarmDayClock.Instance != null)
            FarmDayClock.Instance.DayStarted -= OnDayStarted;
        chests?.Dispose();
    }
}
