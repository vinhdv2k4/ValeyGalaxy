using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmCutscenes;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Composition root for this scene's presentation, triggers and online progress.</summary>
internal sealed class CutsceneDirector : MonoBehaviour, IEventProgressTracker
{
    private const string MineOpeningId = "mine_landslide_clear";
    // Day 5: Lewis meets the farmer in Town and walks them to the old Community Center door;
    // the second half plays inside, where the Junimos dance while Lewis is not looking.
    private const string CommunityCenterId = "community_center";
    private const string CommunityCenterInsideId = "community_center_junimos";
    private const int CommunityCenterEntryX = 32, CommunityCenterEntryY = 23;
    private readonly CutsceneRunner runner = new();
    private readonly EventConditionEvaluator evaluator = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, int> friendship = new(StringComparer.Ordinal);
    private readonly HashSet<string> mail = new(StringComparer.Ordinal);
    private EventDefinition mineOpening;
    private EventDefinition communityCenterInside;
    private bool chaining;
    private IReadOnlyList<EventDefinition> locationEvents;
    private EventTriggerListener triggers;
    private FarmDayClock clock;
    private LocationManager locations;
    private OnlineGameState online;
    private CharacterMotor farmer;
    private MilestoneSprites sprites;
    private MilestoneWorldView world;
    private UnityCutsceneStage stage;
    private StartupApiClient.MilestoneAction completion;
    private bool dirty;
    private bool claimingStarterGift;
    private string waitingForRewardSpace;

    internal IEventService Service => runner;
    private OnlineMilestoneStatus Status => online?.Snapshot?.milestones;

    internal void Initialize(OnlineGameState game)
    {
        online = game;
        clock = FarmDayClock.Instance;
        locations = LocationManager.Instance;
        farmer = GetComponent<CharacterMotor>();
        sprites = Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites");
        world = new MilestoneWorldView(sprites, online.ResolveItem);
        var asset = Resources.Load<TextAsset>("Cutscenes/Milestones");
        if (asset == null || clock == null || locations == null || farmer == null)
        {
            Debug.LogError("Cutscene runtime is missing its catalog or scene dependencies.");
            return;
        }
        var repository = new EventRepository(JsonConvert.DeserializeObject<EventScriptFile>(asset.text),
            new EventConditionFactory(), UnityCutsceneStage.CreateCommandFactory());
        mineOpening = repository.Events.FirstOrDefault(entry => entry.Id == MineOpeningId);
        communityCenterInside = repository.Events.FirstOrDefault(entry => entry.Id == CommunityCenterInsideId);
        locationEvents = repository.Events.Where(entry => entry != mineOpening && entry != communityCenterInside)
            .ToArray();
        triggers = new EventTriggerListener(clock, locations, online, OnTick, OnWorldChanged);
        OnWorldChanged();
    }

    private void OnTick()
    {
        if (locations.CurrentLocation != null) world.Sync(locations.CurrentLocation, Status, clock.Day);
        dirty = true;
    }

    private void OnWorldChanged()
    {
        friendship.Clear();
        foreach (var friend in Status?.friends ?? Array.Empty<OnlineMilestoneFriend>())
            friendship[friend.npc] = friend.points;
        mail.Clear();
        if (online.Snapshot?.dailyEvents is OnlineDailyEventStatus daily)
            foreach (var entry in DailyEventAssets.Calendar.Events)
                if (entry.kind == "mail" && daily.CompletedOn(entry.Key(clock.Date)) > 0) mail.Add(entry.id);
        world.Sync(locations.CurrentLocation, Status, clock.Day);
        dirty = true;
    }

    private bool CanEvaluate => locationEvents != null && !chaining && !runner.IsBusy && Status != null &&
        !CommunityCenterRooms.HoldsEvents &&
        !clock.IsChangingDay && locations.CurrentLocation != null && clock.Day == online.Snapshot.gameDay;

    /// <summary>First event whose conditions hold for the farmer's current map and time.</summary>
    private EventDefinition DueEvent()
    {
        int rooms = 0;
        foreach (var room in Status.rooms ?? Array.Empty<OnlineBundleRoom>())
            if (room.done) rooms++;
        var context = new EventEvaluationContext(clock.Day, clock.TimeOfDay, locations.CurrentLocation.LocationName,
            clock.CurrentSeason, clock.IsRainingToday ? Weather.Rain : clock.IsSnowingToday ? Weather.Snow : Weather.Clear,
            online.Snapshot.totalEarnings, Status.firstCopperDay, rooms, friendship, this);
        var due = evaluator.FirstMatch(locationEvents, context);
        // Joja members get Morris's warehouse ceremony instead of the Junimos' opening.
        if (due?.Id == "community_opening" && Status.HasSeen("JojaMember")) return null;
        return due;
    }

    /// <summary>
    /// Asked by a warp before it fades in: an event about to start on this map should open out
    /// of the warp's black screen rather than flash the map first.
    /// </summary>
    internal bool HasArrivalEvent()
    {
        if (!CanEvaluate) return false;
        var entry = DueEvent();
        if (entry == null) return false;
        string rewardId = StoryReward(entry.Id);
        return rewardId == null || HasItem(rewardId) ||
            GetComponent<PlayerInventory>().CanAccept(online.ResolveItem(rewardId));
    }

    internal bool TryStart()
    {
        if (!dirty || locations.IsWarping || !CanEvaluate) return false;
        dirty = false;
        var entry = DueEvent();
        if (entry == null)
        {
            waitingForRewardSpace = null;
            return false;
        }
        string rewardId = StoryReward(entry.Id);
        if (rewardId != null && !HasItem(rewardId) &&
            !GetComponent<PlayerInventory>().CanAccept(online.ResolveItem(rewardId)))
        {
            if (waitingForRewardSpace == entry.Id) return false;
            waitingForRewardSpace = entry.Id;
            NpcConversation.ShowNotice("Hãy chừa một ô trống trong túi để nhận quà.");
            return true;
        }
        waitingForRewardSpace = null;
        _ = entry.Id == CommunityCenterId ? PlayCommunityCenterAsync(entry) : PlayAsync(entry);
        return true;
    }

    /// <summary>Town half, then a warp in the dark and the Junimo dance inside the Community Center.</summary>
    private async Task PlayCommunityCenterAsync(EventDefinition town)
    {
        chaining = true;
        try
        {
            bool inside = communityCenterInside != null;
            await PlayAsync(town, holdBlack: inside);
            if (!inside || this == null) return;
            if (!HasSeen(CommunityCenterId) ||
                !locations.WarpFarmerFromBlack("CommunityCenter", CommunityCenterEntryX, CommunityCenterEntryY, Vector2.up))
            {
                locations.SetCutsceneFade(0f);
                GameAudio.RefreshLocationMusic();
                return;
            }
            float timeout = Time.unscaledTime + 10f;
            while (this != null && Time.unscaledTime < timeout &&
                   (locations.IsWarping || locations.CurrentLocation?.LocationName != "CommunityCenter"))
                await Awaitable.NextFrameAsync(lifetime.Token);
            if (this == null) return;
            if (locations.CurrentLocation?.LocationName != "CommunityCenter")
            {
                locations.SetCutsceneFade(0f);
                return;
            }
            await PlayAsync(communityCenterInside);
        }
        catch (OperationCanceledException) { }
        finally { chaining = false; }
    }

    internal void HandleSkipInput()
    {
        if (Keyboard.current?.kKey.wasPressedThisFrame == true ||
            Gamepad.current?.startButton.wasPressedThisFrame == true)
            Service.Skip();
    }

    internal IEnumerator PlayMineOpeningAtDawn(int closingDay)
    {
        int openingDay = DailyEventAssets.Calendar.Find("mine_open").day;
        if (mineOpening == null || closingDay != openingDay - 1 || clock?.Day != openingDay ||
            online?.Snapshot?.gameDay != openingDay || HasSeen(MineOpeningId)) yield break;

        Task playback = PlayAsync(mineOpening, morningCutaway: true);
        while (!playback.IsCompleted) yield return null;
    }

    private async Task PlayAsync(EventDefinition entry, bool morningCutaway = false, bool holdBlack = false)
    {
        clock.IsCutscenePaused = true;
        completion = Request("complete", entry.Id);
        string rewardId = StoryReward(entry.Id);
        bool receivingReward = rewardId != null && !HasItem(rewardId);
        Action restoreMap = null;
        try
        {
            if (morningCutaway)
            {
                restoreMap = locations.ShowMountainCutaway();
                if (restoreMap == null) throw new InvalidOperationException("Không tải được cảnh dọn lối vào mỏ.");
            }
            stage = new UnityCutsceneStage(locations, farmer, sprites, holdBlackOnDispose: morningCutaway || holdBlack);
            var cast = stage.Prepare(entry);
            var context = new EventExecutionContext(entry, cast, new UnityCutsceneDialogue(sprites),
                stage, stage, runner.State);
            await runner.RunAsync(context, this, ending =>
            {
                stage.Finish(entry, ending);
                RestoreStage();
            }, lifetime.Token);
            if (receivingReward && HasItem(rewardId))
                GetComponent<NetworkPlayerController>()?.PresentReward(online.ResolveItem(rewardId)?.GetIcon(),
                    entry.Id == "willy_rod" ? "Bạn đã nhận được Cần câu tre!" : "Bạn đã nhận được Kiếm gỉ!");
            if (entry.Id == "grandpa_evaluation")
                NpcConversation.ShowNotice($"Ông nội đã thắp {Status.candles}/4 ngọn nến trên bàn thờ.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            Debug.LogWarning($"Cutscene {entry.Id}: {error.Message}");
            if (this != null) NpcConversation.ShowNotice(error.Message);
        }
        finally
        {
            RestoreStage();
            stage = null;
            if (clock != null) clock.IsCutscenePaused = false;
            completion = null;
        }

        void RestoreStage()
        {
            stage?.Dispose();
            restoreMap?.Invoke();
            restoreMap = null;
        }
    }

    public bool HasSeen(string id) => Status?.HasSeen(id) == true ||
        online.Snapshot?.dailyEvents?.CompletedOn(id) > 0;

    public bool HasMail(string id) => mail.Contains(id);

    /// <summary>Item a calendar story cutscene hands over, or null when it gives nothing.</summary>
    private static string StoryReward(string id)
    {
        var entry = DailyEventAssets.Calendar.Find(id);
        return entry?.cutscene == true && !string.IsNullOrEmpty(entry.rewardItem) ? entry.rewardItem : null;
    }

    /// <summary>Calendar stories played as cutscenes are completed through the daily event endpoint.</summary>
    private static bool IsCalendarCutscene(string id) =>
        DailyEventAssets.Calendar.Find(id) is { kind: "story", cutscene: true };

    private bool HasItem(string id) => online.Snapshot.inventory?.Any(stack =>
        stack != null && stack.itemId == id && stack.amount > 0) == true;

    public async Task CompleteAsync(string id, string choice, string name, string friendshipNpc, int friendshipDelta,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // The inside half is presentation only; the Town half already recorded the story.
        if (id == CommunityCenterInsideId) return;
        Action<Action<string>> send;
        if (IsCalendarCutscene(id))
        {
            var action = new StartupApiClient.DailyEventAction
            {
                action = "cutscene", eventId = id, day = completion.day, time = completion.time,
                location = completion.location, rain = completion.rain
            };
            send = done => online.ActOnDailyEvent(action, done);
        }
        else
        {
            completion.choice = choice;
            completion.name = name;
            send = done => online.ActOnMilestone(completion, done);
        }
        for (int attempt = 0; ; attempt++)
        {
            var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => reply.TrySetCanceled()))
            {
                send(error => reply.TrySetResult(error));
                string error = await reply.Task;
                if (string.IsNullOrEmpty(error)) return;
                if (error != "Vị trí người chơi chưa đồng bộ." || attempt == 2)
                    throw new InvalidOperationException(error);
            }
            // The server keeps a position for 8 s only; a long cutscene can outlast the last sync.
            var pushed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => pushed.TrySetCanceled()))
            {
                online.RefreshPresence(() => pushed.TrySetResult(true));
                await pushed.Task;
            }
        }
    }

    internal bool TryInteract(Vector2? pointer, string selectedItem)
    {
        if (Status == null || locations.CurrentLocation == null || clock.Day != online.Snapshot.gameDay) return false;
        var map = locations.CurrentLocation;
        if (map.LocationName == "FarmHouse" && MilestoneWorldView.HasStarterGift(Status) &&
            Hits(pointer, MilestoneWorldView.StarterGiftX, MilestoneWorldView.StarterGiftY))
        {
            if (!claimingStarterGift)
            {
                claimingStarterGift = true;
                online.ActOnMilestone(Request("claim", MilestoneWorldView.StarterGiftId), (snapshot, error) =>
                {
                    claimingStarterGift = false;
                    if (string.IsNullOrEmpty(error))
                    {
                        GameAudio.Play("Ship");
                        GetComponent<NetworkPlayerController>().PresentReward(
                            online.ResolveItem("seeds_parsnip")?.GetIcon(), "Đã nhận 15 hạt giống củ cải vàng.",
                            followUp: snapshot?.farmTileLimit > 0
                                ? $"Bạn có thể cuốc và gieo trồng tối đa {snapshot.farmTileLimit} ô đất. " +
                                  "Hãy dùng TV để thuê thêm đất khi cần."
                                : null);
                    }
                    else NpcConversation.ShowNotice(error);
                });
            }
            return true;
        }
        if (map.LocationName == "CommunityCenter")
            foreach (var (area, x, y) in CommunityCenterRooms.NoteTiles())
                if (Hits(pointer, x, y))
                {
                    JunimoNoteMenu.Open(online, area);
                    return true;
                }
        if (map.LocationName == "FarmCave" && !string.IsNullOrEmpty(Status.cave) && Hits(pointer, 5, 6))
        {
            if (Status.caveHarvestDay == clock.Day) NpcConversation.ShowNotice("Đã thu hoạch hang hôm nay.");
            else Send("harvest", "", () => NpcConversation.ShowNotice("Đã thu hoạch sản vật trong hang."));
            return true;
        }
        if (map.LocationName == "Farm" && !string.IsNullOrEmpty(Status.pet) && Hits(pointer, 68, 19))
        {
            NpcConversation.ShowNotice(Status.petName + " đang chơi trong sân.");
            return true;
        }
        if (selectedItem != "big_craftable_13") return false;
        NpcConversation.ShowChoices("Lò nung · 5 quặng và 1 than", new (string, Action)[]
        {
            ("Luyện đồng", () => Smelt("copper_ore")),
            ("Luyện sắt", () => Smelt("iron_ore")),
            ("Luyện vàng", () => Smelt("gold_ore"))
        });
        return true;
    }

    private bool Hits(Vector2? pointer, int x, int y) =>
        NpcShop.HitActionTile(pointer, farmer.StandingPosition, farmer.Facing, x, y);

    private void Smelt(string ore) => Send("smelt", ore, () => NpcConversation.ShowNotice("Đã luyện một thỏi kim loại."));

    private void Send(string action, string id, Action success)
    {
        online.ActOnMilestone(Request(action, id), error =>
        {
            if (string.IsNullOrEmpty(error)) success();
            else NpcConversation.ShowNotice(error);
        });
    }

    private StartupApiClient.MilestoneAction Request(string action, string id) => new()
    {
        action = action, id = id, day = clock.Day, time = clock.TimeOfDay,
        location = locations.CurrentLocation.LocationName, rain = clock.IsRainingToday || clock.IsSnowingToday
    };

    private void OnDestroy()
    {
        triggers?.Dispose();
        lifetime.Cancel();
        stage?.Dispose();
        world?.Dispose();
        lifetime.Dispose();
    }
}

internal sealed class EventTriggerListener : IDisposable
{
    private readonly FarmDayClock clock;
    private readonly LocationManager locations;
    private readonly OnlineGameState online;
    private readonly Action tick, changed;

    internal EventTriggerListener(FarmDayClock clock, LocationManager locations, OnlineGameState online,
        Action tick, Action changed)
    {
        this.clock = clock;
        this.locations = locations;
        this.online = online;
        this.tick = tick;
        this.changed = changed;
        clock.TimeTick += tick;
        locations.LocationChanged += changed;
        online.SnapshotApplied += changed; // Includes gifts, mining and settled shipping.
    }
    public void Dispose()
    {
        clock.TimeTick -= tick;
        locations.LocationChanged -= changed;
        online.SnapshotApplied -= changed;
    }
}
