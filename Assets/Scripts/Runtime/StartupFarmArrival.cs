using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FarmCutscenes;
using UnityEngine;

/// <summary>Runs the private part of event 60367 before enabling the Day 1 world.</summary>
internal sealed class StartupFarmArrival : MonoBehaviour
{
    private const string BusStop = "BusStop";
    private const string Farm = "Farm";
    private const string FarmHouse = "FarmHouse";
    private const int FirstDay = 1;
    private const int Morning = 600;
    private FarmerIntroMode mode;
    private CancellationTokenSource playback;

    internal void Initialize(FarmerIntroMode intro) => mode = intro;

    private void Update()
    {
        if (StartupIntroScript.SkipPressed) playback?.Cancel();
    }

    private IEnumerator Start()
    {
        var locations = LocationManager.Instance;
        var farmer = GetComponent<CharacterMotor>();
        if (locations == null || farmer == null)
        {
            yield return Abort("Không thể tìm vị trí bắt đầu cho nhân vật.");
            yield break;
        }
        farmer.IsMovementEnabled = false;
        farmer.SetMovement(Vector2.zero);
        var dayHud = FindFirstObjectByType<DayTimeHud>();
        if (dayHud != null) dayHud.gameObject.SetActive(false);
        FullInventoryMenuUI.Instance?.RefreshHudToolbar();

        if (mode == FarmerIntroMode.Watch)
        {
            var scenes = StartupIntroScript.Load().CompileWorldScenes();
            var maps = new[] { BusStop, Farm };
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            playback = cancellation;
            GameAudio.PlayMusic("spring_night_ambient");
            for (int i = 0; i < maps.Length && !cancellation.IsCancellationRequested; i++)
            {
                EventDefinition entry = i == 0 ? scenes.Arrival : scenes.Farm;
                TilePoint spawn = entry.Actors[0].Tile;
                yield return locations.WarpForOpening(maps[i],
                    new Vector2Int(Mathf.RoundToInt(spawn.X), Mathf.RoundToInt(spawn.Y)));
                if (cancellation.IsCancellationRequested) break;
                if (locations.CurrentLocation?.LocationName != maps[i])
                {
                    playback = null;
                    yield return Abort("Không thể mở bản đồ cho cảnh đón tiếp.");
                    yield break;
                }

                Task scene = PlayScene(entry, locations, farmer, cancellation.Token);
                yield return new WaitUntil(() => scene.IsCompleted);
                if (scene.IsFaulted)
                {
                    playback = null;
                    Debug.LogException(scene.Exception.GetBaseException());
                    yield return Abort("Cảnh đón tiếp gặp lỗi. Hãy tải lại nhân vật để thử lại.");
                    yield break;
                }
            }
            playback = null;
        }

        GameAudio.PlayMusic(null);
        yield return locations.WarpForOpening(FarmHouse, default);
        if (locations.CurrentLocation?.LocationName != FarmHouse)
        {
            yield return Abort("Không thể đưa nhân vật về nhà.");
            yield break;
        }

        GetComponent<NetworkPlayerController>()?.MarkMorningArrival();
        yield return locations.RevealOpening();
        var online = GetComponent<OnlineGameState>();
        var request = new StartupApiClient.MilestoneAction
        {
            action = "complete", id = StartupSession.ArrivalEventId, day = FirstDay,
            time = Morning, location = FarmHouse
        };
        while (true)
        {
            bool settled = false;
            string error = null;
            online.ActOnMilestone(request, failure =>
            {
                error = failure;
                settled = true;
            });
            yield return new WaitUntil(() => settled);
            if (online.Snapshot?.milestones?.HasSeen(StartupSession.ArrivalEventId) == true) break;
            bool retry = false;
            NpcConversation.ShowChoices(error ?? "Không lưu được tiến độ mở đầu.",
                new (string, Action)[] { ("Thử lại", () => retry = true), ("Về menu", () => { }) });
            yield return new WaitUntil(() => !NpcConversation.IsOpen);
            if (retry) continue;
            StartupSession.ReturnToTitle();
            yield break;
        }

        farmer.IsMovementEnabled = true;
        StartupSession.FinishOpening();
        if (dayHud != null) dayHud.gameObject.SetActive(true);
        FullInventoryMenuUI.Instance?.RefreshHudToolbar();
        GameAudio.RefreshLocationMusic();
        Destroy(this);
    }

    private static async Task PlayScene(EventDefinition entry, LocationManager locations, CharacterMotor farmer,
        CancellationToken token)
    {
        var sprites = Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites");
        using var stage = new UnityCutsceneStage(locations, farmer, sprites, holdBlackOnDispose: true);
        var state = new EventRuntimeState();
        state.Begin();
        var actors = stage.Prepare(entry, exactPlayerTile: true);
        var context = new EventExecutionContext(entry, actors, new UnityCutsceneDialogue(sprites), stage, stage, state);
        await entry.Commands.ExecuteAsync(context, token);
    }

    private static IEnumerator Abort(string message)
    {
        LocationManager.Instance?.SetCutsceneFade(0f);
        GameAudio.PlayMusic(null);
        NpcConversation.ShowNotice(message);
        yield return new WaitUntil(() => !NpcConversation.IsOpen);
        StartupSession.ReturnToTitle();
    }
}
