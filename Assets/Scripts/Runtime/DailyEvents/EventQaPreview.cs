#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DailyEvents;
using FarmCutscenes;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class EventQaPreview : MonoBehaviour
{
    private static EventQaPreview active;
    private readonly CutsceneRunner runner = new();
    private CancellationTokenSource playback;
    private QaDailyEventSession session;
    private DailyEventPlayer daily;
    private UnityCutsceneStage stage;
    private MilestoneWorldView world;
    private OnlineMilestoneStatus sampleMilestones;
    private LocationManager locations;
    private FarmDayClock clock;
    private CharacterMotor farmer;
    private FishingController fishing;
    private GameLocation originalMap;
    private Vector2Int originalTile;
    private Vector2 originalFacing;
    private bool transitioning;

    public string Status { get; private set; } = "";
    public bool IsActive => active == this;
    public bool IsTransitioning => transitioning;
    public bool IsPlayingCutscene => runner.IsBusy;
    public bool CanPreviewDelivery => IsActive && !transitioning && !runner.IsBusy &&
        !string.IsNullOrEmpty(session?.Selected?.target);
    public bool CanPreview => isActiveAndEnabled && Application.isPlaying && (active == null || active == this) &&
        StartupSession.Api == null && FindAnyObjectByType<OnlineGameState>(FindObjectsInactive.Include) == null &&
        LocationManager.Instance?.LocalFarmer != null;

    internal static EventQaPreview For(CharacterMotor motor) =>
        active != null && active.farmer == motor ? active : null;

    internal bool HasDailyFlag(string id) => session?.Snapshot.dailyEvents.CompletedOn(id) > 0;
    internal bool HasMilestoneFlag(string id) => sampleMilestones?.HasSeen(id) == true;

    public void PreviewDay(int day) =>
        StartPreview(Mathf.Clamp(day, 1, 10_000), 600, "Farm", new Vector2Int(64, 19), null, null);

    public void PreviewDaily(string id, int requestedDay)
    {
        var entry = DailyEventAssets.Calendar.Find(id);
        if (entry == null) return;
        if (entry.cutscene) { PreviewCutscene(id); return; }
        var requested = new FarmDate(Mathf.Clamp(requestedDay, 1, 10_000));
        int day = entry.IsDue(requested) ? requested.Day : entry.day;
        if (entry.id == "farm_intro")
        {
            if (!CanPreview || transitioning || runner.IsBusy) return;
            var scene = StartupIntroScript.Load().CompileWorldScenes().Farm;
            var spawn = scene.Actors.First(actor => actor.Id == "Player").Tile;
            StartPreview(day, entry.start, entry.location,
                new Vector2Int(Mathf.RoundToInt(spawn.X), Mathf.RoundToInt(spawn.Y)), entry, scene);
            return;
        }
        string target = entry.kind == "festival" ? entry.map :
            entry.kind == "unlock" && entry.id == "earthquake" ? "Mountain" :
            entry.kind == "forage" ? "Forest" :
            string.IsNullOrEmpty(entry.location) ? "Farm" : entry.location;
        Vector2Int tile = entry.HasTile ? new Vector2Int(entry.x, entry.y + (entry.kind == "festival" ? 0 : 1)) :
            EntryTile(target);
        StartPreview(day, entry.start, target, tile, entry, null);
    }

    public void PreviewCutscene(string id)
    {
        if (!CanPreview || transitioning) return;
        var asset = Resources.Load<TextAsset>("Cutscenes/Milestones");
        if (asset == null) { Status = "Thiếu tài nguyên Cutscenes/Milestones."; return; }
        var script = JsonConvert.DeserializeObject<EventScriptFile>(asset.text);
        var source = Array.Find(script.events, entry => entry.id == id);
        if (source == null) return;
        var repository = new EventRepository(script, new EventConditionFactory(), UnityCutsceneStage.CreateCommandFactory());
        var definition = repository.Events.First(entry => entry.Id == id);
        string target = source.conditions.First(node => node.type == "location").value;
        int hour = source.conditions.First(node => node.type == "time").min;
        int day = source.conditions.FirstOrDefault(node => node.type == "progress" && node.value == "day")?.min ?? 1;
        var position = definition.Actors.Where(actor => actor.Id == "Player").Select(actor =>
            new Vector2Int((int)actor.Tile.X, (int)actor.Tile.Y)).DefaultIfEmpty(EntryTile(target)).First();
        StartPreview(day, hour, target, position, null, definition);
    }

    private void StartPreview(int day, int time, string map, Vector2Int tile,
        DailyEventDefinition entry, EventDefinition cutscene)
    {
        if (!CanPreview || transitioning || runner.IsBusy) return;
        if (!IsActive)
        {
            locations = LocationManager.Instance;
            if (locations.IsWarping || locations.CurrentLocation == null) return;
            farmer = locations.LocalFarmer;
            var inventory = farmer.GetComponent<PlayerInventory>();
            fishing = farmer.GetComponent<FishingController>();
            clock = FarmDayClock.Instance;
            if (inventory == null || fishing == null || clock == null) return;
            originalMap = locations.CurrentLocation;
            originalTile = NpcPathfinder.WorldToTile(farmer.StandingPosition);
            originalFacing = farmer.Facing;
            active = this;
            locations.LocationChanged += RefreshMilestoneProps;
        }
        Status = "Đang mở preview…";
        StartCoroutine(Open(day, time, map, tile, entry, cutscene));
    }

    private IEnumerator Open(int day, int time, string map, Vector2Int tile,
        DailyEventDefinition entry, EventDefinition cutscene)
    {
        transitioning = true;
        ClosePresentation();
        while (locations.IsWarping) yield return null;
        clock.SetEventPreviewTime(day, time);
        session = new QaDailyEventSession(day, entry);
        daily = new DailyEventPlayer(session, farmer.GetComponent<PlayerInventory>(), farmer, fishing);
        if (locations.CurrentLocation.LocationName != map || entry?.HasTile == true || cutscene != null)
        {
            if (map == "Mine") locations.WarpFarmerToMineFloor(0, false);
            else locations.WarpFarmer(map, tile.x, tile.y, Vector2.down);
            while (locations.IsWarping) yield return null;
        }
        transitioning = false;
        if (locations.CurrentLocation.LocationName != map)
        {
            Status = $"Không vào được bản đồ {map}. Xem Console để kiểm tra điểm đến.";
            yield break;
        }
        Status = $"{entry?.title ?? cutscene?.Title ?? "Bối cảnh ngày"} · ngày {day} · {time / 100:00}:{time % 100:00} · {map}";
        if (cutscene != null) _ = Play(cutscene, applyMilestone: entry == null);
        else if (entry != null) daily.OpenPreview(entry, EntryTile(entry.location));
    }

    private async Task Play(EventDefinition definition, bool applyMilestone)
    {
        using var token = new CancellationTokenSource();
        playback = token;
        clock.IsCutscenePaused = true;
        try
        {
            var sprites = Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites");
            using var presentation = new UnityCutsceneStage(locations, farmer, sprites);
            stage = presentation;
            var context = new EventExecutionContext(definition, presentation.Prepare(definition),
                new UnityCutsceneDialogue(sprites), presentation, presentation, runner.State);
            var progress = new PreviewProgress(applyMilestone ? ApplySampleOutcome : null);
            await runner.RunAsync(context, progress, ending => presentation.Finish(definition, ending), token.Token);
            Status = $"Đã chạy {definition.Title}. Kết quả thuộc phiên QA.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Status = error.Message;
            Debug.LogException(error);
        }
        finally
        {
            if (clock != null) clock.IsCutscenePaused = false;
            stage = null;
            playback = null;
        }
    }

    private void ApplySampleOutcome(string id, string choice, string name)
    {
        sampleMilestones = new OnlineMilestoneStatus { seen = new[] { id } };
        if (id == "pet_adoption" && choice is "dog" or "cat")
        {
            sampleMilestones.pet = choice;
            sampleMilestones.petName = name;
        }
        if (id == "farm_cave") sampleMilestones.cave = choice;
        if (id == "grandpa_evaluation") sampleMilestones.candles = 4;
        world = new MilestoneWorldView(Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites"), session.ResolveItem);
        RefreshMilestoneProps();
    }

    private void RefreshMilestoneProps() => world?.Sync(locations.CurrentLocation, sampleMilestones, clock.Day);

    public void StopPreview()
    {
        if (!IsActive) return;
        StopAllCoroutines();
        playback?.Cancel();
        StartCoroutine(ReturnToOrigin());
    }

    public void PreviewDelivery()
    {
        if (!CanPreviewDelivery || !CanPreview) return;
        if (NpcConversation.IsOpen) NpcConversation.Close();
        daily?.PreviewDelivery(session.Selected);
    }

    private IEnumerator ReturnToOrigin()
    {
        transitioning = true;
        while (runner.IsBusy || locations.IsWarping) yield return null;
        ClosePresentation();
        if (originalMap != null)
        {
            locations.WarpFarmer(originalMap.LocationName, originalTile.x, originalTile.y, originalFacing);
            while (locations.IsWarping) yield return null;
        }
        Release();
        Status = "Đã dừng preview.";
    }

    private void ClosePresentation()
    {
        if (NpcConversation.IsOpen) NpcConversation.Close();
        if (fishing != null && fishing.IsFishing) fishing.CancelFishing();
        daily?.ClearPreview();
        daily = null;
        world?.Dispose();
        world = null;
        sampleMilestones = null;
        session?.Dispose();
        session = null;
        foreach (var cast in FindObjectsByType<FestivalCast>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(cast);
        foreach (var show in FindObjectsByType<FestivalJellyShow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(show.gameObject);
    }

    private void Release()
    {
        if (locations != null) locations.LocationChanged -= RefreshMilestoneProps;
        if (clock != null)
        {
            clock.IsCutscenePaused = false;
            clock.ClearEventPreviewTime();
        }
        transitioning = false;
        if (active == this) active = null;
    }

    private void OnDisable()
    {
        if (!IsActive) return;
        StopAllCoroutines();
        playback?.Cancel();
        stage?.Dispose();
        ClosePresentation();
        Release();
    }

    internal void HandleInput(InputController input)
    {
        if (StartupSession.Api != null || farmer.GetComponent<OnlineGameState>() != null)
        {
            playback?.Cancel();
            stage?.Dispose();
            ClosePresentation();
            Release();
            return;
        }
        if (Keyboard.current?.kKey.wasPressedThisFrame == true) runner.Skip();
        if (transitioning || runner.IsBusy || locations.IsWarping || NpcConversation.IsOpen)
        {
            if (!runner.IsBusy) farmer.SetMovement(Vector2.zero);
            input.SuppressUseUntilReleased();
            return;
        }
        if (fishing.IsFishing)
        {
            farmer.SetMovement(Vector2.zero);
            if (input.ConsumeUse(out _)) fishing.NotifyActionPressed();
            return;
        }
        if (Keyboard.current?.jKey.wasPressedThisFrame == true)
        {
            farmer.SetMovement(Vector2.zero);
            daily?.OpenJournal();
            return;
        }
        if (daily?.Tick(autoStartStory: false) == true) return;
        farmer.SetMovement(input.move);
        if (!input.ConsumeUse(out var pointer, out _, out bool interact) || !interact) return;
        if (daily?.TryInteract(pointer) == true) return;
        if (locations.TryUseDoor(pointer, farmer.StandingPosition, farmer.Facing)) return;
        NpcConversation.TryOpen(pointer, farmer.StandingPosition, farmer.Facing, farmer);
    }

    private static Vector2Int EntryTile(string location) => location switch
    {
        "Farm" => new Vector2Int(64, 19), "Town" => new Vector2Int(35, 62),
        "Forest" => new Vector2Int(40, 60), "Beach" => new Vector2Int(20, 5),
        "Mountain" => new Vector2Int(22, 16), "WizardHouse" => new Vector2Int(5, 17),
        "BusStop" => new Vector2Int(10, 11), _ => new Vector2Int(8, 9)
    };

    private sealed class PreviewProgress : IEventProgressTracker
    {
        private readonly Action<string, string, string> showOutcome;
        internal PreviewProgress(Action<string, string, string> showOutcome) => this.showOutcome = showOutcome;
        public bool HasSeen(string id) => false;
        public bool HasMail(string id) => false;
        public Task CompleteAsync(string id, string choice, string name, string friendshipNpc, int friendshipDelta,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            showOutcome?.Invoke(id, choice, name);
            return Task.CompletedTask;
        }
    }
}
#endif
