using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmCutscenes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal sealed class UnityCutsceneStage : IAudioService, ICameraService, IDisposable
{
    private readonly LocationManager locations;
    private readonly GameLocation map;
    private readonly CharacterMotor farmer;
    private readonly MilestoneSprites sprites;
    private readonly Vector3 originalPivot, standingOffset;
    private readonly Vector2 originalFacing;
    private readonly bool originalMovement, originalSimulation;
    private readonly bool holdBlackOnDispose;
    private readonly List<NpcActor> hidden = new();
    private readonly Dictionary<string, ICutsceneActor> actors = new(StringComparer.Ordinal);
    private readonly List<Sprite> props = new();
    private GameObject root;
    private TilePoint? exitTile;
    private float fade = 1f;
    private bool focusedCamera, disposed;

    internal static EventCommandFactory CreateCommandFactory()
    {
        var factory = new EventCommandFactory();
        factory.Register("clearMineRubble", _ => new ClearMineRubbleCommand());
        factory.Register("guide", CreateGuideCommand);
        factory.Register("appear", node => new FadeActorCommand(node.actor, true, node.seconds));
        factory.Register("vanish", node => new FadeActorCommand(node.actor, false, node.seconds));
        factory.Register("dance", node => new DanceCommand(node.actor, node.seconds));
        return factory;
    }

    private sealed class FadeActorCommand : IEventCommand
    {
        private readonly string actor;
        private readonly bool visible;
        private readonly float seconds;
        internal FadeActorCommand(string actor, bool visible, float seconds)
        { this.actor = actor; this.visible = visible; this.seconds = Mathf.Max(0f, seconds); }
        public Task ExecuteAsync(EventExecutionContext context, CancellationToken ct) =>
            ((StageActor)context.Actor(actor)).FadeAsync(visible ? 1f : 0f, seconds, ct);
    }

    private sealed class DanceCommand : IEventCommand
    {
        private readonly string actor;
        private readonly float seconds;
        internal DanceCommand(string actor, float seconds) { this.actor = actor; this.seconds = Mathf.Max(0f, seconds); }
        public Task ExecuteAsync(EventExecutionContext context, CancellationToken ct) =>
            ((StageActor)context.Actor(actor)).DanceAsync(seconds, ct);
    }

    internal UnityCutsceneStage(LocationManager locations, CharacterMotor farmer, MilestoneSprites sprites,
        bool holdBlackOnDispose = false)
    {
        this.locations = locations;
        map = locations.CurrentLocation;
        this.farmer = farmer;
        this.sprites = sprites;
        this.holdBlackOnDispose = holdBlackOnDispose;
        originalPivot = farmer.transform.position;
        standingOffset = farmer.StandingPosition - originalPivot;
        originalFacing = farmer.Facing;
        originalMovement = farmer.IsMovementEnabled;
        originalSimulation = farmer.body != null && farmer.body.simulated;
    }

    internal IDictionary<string, ICutsceneActor> Prepare(EventDefinition definition, bool exactPlayerTile = false)
    {
        root = new GameObject("Cutscene · " + definition.Id);
        root.transform.SetParent(map.transform, false);
        locations.SetCutsceneFade(fade);
        farmer.SetMovement(Vector2.zero);
        farmer.IsMovementEnabled = false;
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        var definitions = catalog.characters.ToDictionary(npc => npc.name, StringComparer.Ordinal);
        if (catalog.marlon != null) definitions.TryAdd("Marlon", catalog.marlon);
        var staged = definition.Actors.Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
        bool crowd = definition.Id == "community_opening";
        foreach (var npc in UnityEngine.Object.FindObjectsByType<NpcActor>(FindObjectsSortMode.None))
        {
            if (!staged.Contains(npc.DisplayName) && (!crowd || !npc.IsVisible)) continue;
            npc.SetCutsceneHidden(true);
            hidden.Add(npc);
        }
        foreach (var cue in definition.Actors)
        {
            Vector3 desired = map.TileCenter(Mathf.RoundToInt(cue.Tile.X), Mathf.RoundToInt(cue.Tile.Y));
            if (cue.Id == "Player")
            {
                if (exactPlayerTile)
                    farmer.SetCutscenePose(desired - standingOffset, Vector2.down, false);
                else if (locations.TryGetCutsceneStandingPosition(map, desired, out var safe))
                    farmer.SetCutscenePose(safe - standingOffset, Vector2.down, false);
                if (farmer.body != null) farmer.body.simulated = false;
                actors.Add(cue.Id, new StageActor(cue.Id, map, farmer.transform, standingOffset, farmer,
                    null, null, sprites));
                continue;
            }
            definitions.TryGetValue(cue.Id, out var npc);
            Spawn(cue.Id, desired, npc?.animationFrames, cue.Hidden);
        }
        if (crowd)
        {
            // The rest of the town gathers in the hall behind where the farmer will stand (32, 14),
            // clear of Lewis, the named guests and Morris's way out down the middle.
            var center = new Vector2Int(32, 16);
            // Only behind the farmer, so nobody stands between them and Lewis or in Morris's way out.
            var seats = FestivalCast.OpenTiles(map, center)
                .Where(tile => tile.y >= center.y && tile.x is not (32 or 33)).GetEnumerator();
            foreach (string name in DailyEventAssets.Calendar.Villagers)
            {
                if (staged.Contains(name) || !definitions.TryGetValue(name, out var npc)) continue;
                if (!seats.MoveNext()) break;
                var tile = seats.Current;
                Spawn(name, map.TileCenter(tile.x, tile.y), npc.animationFrames);
            }
            seats.Dispose();
        }
        locations.SnapCutsceneCamera();
        return actors;
    }

    private SpriteRenderer AddAtlasProp(string name, RectInt source, Vector3 position, int sortingOrder)
    {
        var part = new GameObject(name);
        part.transform.SetParent(root.transform, false);
        part.transform.position = position;
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateAtlasSprite(source);
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private Sprite CreateAtlasSprite(RectInt source)
    {
        return AtlasSprites.FromTop(StardewUiAtlas.Cursors, source, Vector2.up);
    }

    private sealed class ClearMineRubbleCommand : IEventCommand
    {
        public async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            var stage = (UnityCutsceneStage)context.CameraService;
            stage.root.transform.position = stage.map.TileCenter(52, 7);
            Vector3 topLeft = MineEntranceBlock.RubbleTopLeft(stage.map);
            // Worker frames and offsets.
            Vector3 workerPosition = topLeft + new Vector3(172f / 64f, -2f);
            int order = WorldSortOrder.GetOrder(topLeft.y - 5f);
            var rubble = stage.AddAtlasProp("Mine rubble", MineEntranceBlock.RubbleSource, topLeft, order);
            stage.AddAtlasProp("Joja equipment", new RectInt(335, 1410, 21, 21),
                workerPosition + Vector3.right, order + 1);
            var worker = stage.AddAtlasProp("Joja worker", new RectInt(288, 1349, 19, 28), workerPosition, order + 2);
            Sprite[] swing = { worker.sprite, stage.CreateAtlasSprite(new RectInt(307, 1349, 19, 28)),
                stage.CreateAtlasSprite(new RectInt(326, 1349, 19, 28)),
                stage.CreateAtlasSprite(new RectInt(345, 1349, 19, 28)) };
            stage.focusedCamera = true;
            stage.locations.SetCutsceneCameraTarget(stage.root.transform);
            WorldLightingController.SetCutsceneOutdoorTime(2200);
            var lamp = new GameObject("Joja work light").AddComponent<Light2D>();
            lamp.transform.SetParent(stage.root.transform, false);
            lamp.transform.position = workerPosition + new Vector3(0f, -0.6f);
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = new Color(1f, 0.76f, 0.43f);
            lamp.intensity = 0.7f;
            lamp.pointLightOuterRadius = 3.75f;
            await stage.FadeAsync(false, 0.35f, ct);
            await Hold(0.3f);
            for (int strike = 0; strike < 3; strike++)
            {
                for (int frame = 0; frame < swing.Length; frame++)
                {
                    worker.sprite = swing[frame];
                    if (frame == 2) stage.PlaySound("hammer");
                    await Hold(0.12f);
                }
            }
            worker.sprite = swing[0];
            stage.PlaySound("boulderBreak");
            for (float elapsed = 0; elapsed < 0.4f; elapsed += Time.unscaledDeltaTime)
            {
                rubble.color = new Color(1, 1, 1, 1 - elapsed / 0.4f);
                await Awaitable.NextFrameAsync(ct);
            }
            rubble.enabled = false;
            await Hold(0.55f);
            await stage.FadeAsync(true, 0.35f, ct);

            async Task Hold(float seconds)
            {
                for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
                    await Awaitable.NextFrameAsync(ct);
            }
        }
    }

    internal static IEventCommand CreateGuideCommand(EventNode node) => new GuideCommand(node);

    private sealed class GuideCommand : IEventCommand
    {
        private readonly string leaderId, followerId;
        private readonly float speed;
        private readonly Vector2Int[] waypoints;

        internal GuideCommand(EventNode node)
        {
            leaderId = node.actor;
            followerId = node.value;
            speed = node.speed;
            waypoints = node.children.Select(tile => new Vector2Int(Mathf.RoundToInt(tile.x), Mathf.RoundToInt(tile.y))).ToArray();
            if (!(speed > 0) || float.IsInfinity(speed) || waypoints.Length == 0)
                throw new ArgumentException("A guide command needs a positive speed and a route.");
        }

        public async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            var leader = (StageActor)context.Actor(leaderId);
            var follower = (StageActor)context.Actor(followerId);
            var stage = (UnityCutsceneStage)context.CameraService;
            var route = new Vector2[waypoints.Length + 2];
            route[0] = follower.Feet;
            route[1] = leader.Feet;
            for (int i = 0; i < waypoints.Length; i++)
                route[i + 2] = stage.map.TileCenter(waypoints[i].x, waypoints[i].y);

            var distances = new float[route.Length];
            for (int i = 1; i < route.Length; i++)
                distances[i] = distances[i - 1] + Vector2.Distance(route[i - 1], route[i]);

            // Both actors travel on one route, with the follower always trailing
            // by the distance between the two starting tiles, including at turns.
            float spacing = distances[1];
            float end = distances[distances.Length - 1];
            float progress = spacing;
            if (spacing <= 0.01f) throw new InvalidOperationException("Guide actors share a starting tile.");
            while (progress < end)
            {
                await Awaitable.NextFrameAsync(ct);
                progress = Mathf.Min(end, progress + speed * Time.unscaledDeltaTime);
                Apply(leader, progress);
                Apply(follower, progress - spacing);
            }

            void Apply(StageActor actor, float distance)
            {
                int i = 1;
                while (i < route.Length - 1 && distances[i] <= distance) i++;
                float t = Mathf.InverseLerp(distances[i - 1], distances[i], distance);
                actor.SetTravelPose(Vector2.Lerp(route[i - 1], route[i], t), route[i] - route[i - 1], progress < end);
            }
        }
    }

    private void Spawn(string id, Vector3 feet, Sprite[] frames, bool hidden = false)
    {
        frames = NpcSprites.CharacterFrames(frames);
        var actor = new GameObject(id);
        actor.transform.SetParent(root.transform, false);
        actor.transform.position = feet;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(actor.transform, false);
        if (frames.Length > 0) visual.transform.localPosition = NpcSprites.FeetOffset;
        var renderer = visual.AddComponent<SpriteRenderer>();
        bool junimo = id.StartsWith("Junimo", StringComparison.Ordinal);
        Sprite[] junimoFrames = junimo ? JunimoSprites.Frames(sprites) : null;
        string spriteId = id == "Pet" ? (StartupSession.Farmer?.pet == 1 ? "cat" : "dog") : junimo ? "Junimos" : id;
        renderer.sprite = junimoFrames != null ? junimoFrames[0] :
            frames.Length > 0 ? frames[0] : sprites?.SpriteFor(spriteId);
        if (junimoFrames != null) renderer.color = JunimoSprites.Tint(actors.Count + id.Length);
        if (hidden) renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 0f);
        renderer.sortingOrder = WorldSortOrder.GetOrder(feet.y);
        if (id == "Grandpa")
        {
            renderer.color = new Color(1f, 0.84f, 0.44f, 0.87f);
            var light = actor.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.74f, 0.28f);
            light.intensity = 0.65f;
            light.pointLightOuterRadius = 3f;
        }
        actors.Add(id, new StageActor(id, map, actor.transform, Vector3.zero, null, renderer, frames, sprites)
        {
            JunimoFrames = junimoFrames
        });
    }

    public void PlaySound(string cue) => GameAudio.Play(cue);
    public void PlayMusic(string cue) => GameAudio.PlayMusic(cue);
    public async Task FadeAsync(bool toBlack, float seconds, CancellationToken ct)
    {
        float start = fade, target = toBlack ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            locations.SetCutsceneFade(fade = Mathf.Lerp(start, target, elapsed / seconds));
            await Task.Yield();
        }
        ct.ThrowIfCancellationRequested();
        locations.SetCutsceneFade(fade = target);
    }

    internal void Finish(EventDefinition definition, EventEnding ending)
    {
        exitTile = definition.ExitTile(ending);
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (root != null) UnityEngine.Object.Destroy(root);
        foreach (Sprite prop in props) UnityEngine.Object.Destroy(prop);
        foreach (var npc in hidden)
            if (npc != null) npc.SetCutsceneHidden(false);
        if (farmer != null)
        {
            farmer.SetCutscenePose(originalPivot, originalFacing, false);
            if (farmer.body != null) farmer.body.simulated = originalSimulation;
            if (exitTile.HasValue && locations != null && locations.CurrentLocation == map)
            {
                TilePoint tile = exitTile.Value;
                Vector3 desired = map.TileCenter((int)tile.X, (int)tile.Y);
                Physics2D.SyncTransforms();
                if (locations.TryGetCutsceneStandingPosition(map, desired, out var standing))
                    farmer.SetCutscenePose(standing - standingOffset, originalFacing, false);
            }
            farmer.IsMovementEnabled = originalMovement;
        }
        if (focusedCamera) WorldLightingController.SetCutsceneOutdoorTime(null);
        if (locations != null)
        {
            if (focusedCamera) locations.SetCutsceneCameraTarget(null);
            else locations.SnapCutsceneCamera();
            locations.SetCutsceneFade(holdBlackOnDispose ? 1f : 0f);
            if (!holdBlackOnDispose) GameAudio.RefreshLocationMusic();
        }
    }

    private sealed class StageActor : ICutsceneActor
    {
        private static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        private readonly GameLocation map;
        private readonly Transform pose;
        private readonly Vector3 offset;
        private readonly CharacterMotor farmer;
        private readonly SpriteRenderer renderer;
        private readonly Sprite[] frames;
        private readonly MilestoneSprites sprites;
        private Direction facing = Direction.Down;
        public string ActorId { get; }
        internal Sprite[] JunimoFrames { get; set; }
        internal Vector2 Feet => pose.position + offset;
        public TilePoint Position
        {
            get
            {
                var tile = NpcPathfinder.WorldToTile(pose.position + offset);
                return new TilePoint(tile.x, tile.y);
            }
        }
        internal StageActor(string id, GameLocation map, Transform pose, Vector3 offset, CharacterMotor farmer,
            SpriteRenderer renderer, Sprite[] frames, MilestoneSprites sprites)
        {
            ActorId = id;
            this.map = map;
            this.pose = pose;
            this.offset = offset;
            this.farmer = farmer;
            this.renderer = renderer;
            this.frames = frames;
            this.sprites = sprites;
        }

        public async Task MoveToAsync(TilePoint tile, float speed, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Vector2 goal = map.TileCenter(Mathf.RoundToInt(tile.X), Mathf.RoundToInt(tile.Y));
            var route = NpcPathfinder.FindPath(map, pose.position + offset, goal);
            if (route == null) return;
            try
            {
                foreach (var next in route)
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        Vector2 delta = next - (Vector2)(pose.position + offset);
                        if (delta.sqrMagnitude <= 0.0001f) break;
                        Vector3 pivot = Vector2.MoveTowards(pose.position, (Vector3)next - offset,
                            speed * Time.unscaledDeltaTime);
                        SetTravelPose(pivot + offset, delta, true);
                        await Task.Yield();
                    }
                }
            }
            finally
            {
                if (pose != null) ApplyPose(pose.position, false);
            }
        }

        public void FaceDirection(Direction direction) { facing = direction; ApplyPose(pose.position, false); }

        internal void SetTravelPose(Vector2 feet, Vector2 heading, bool walking)
        {
            facing = Mathf.Abs(heading.x) > Mathf.Abs(heading.y)
                ? (heading.x > 0 ? Direction.Right : Direction.Left)
                : (heading.y > 0 ? Direction.Up : Direction.Down);
            Vector3 pivot = (Vector3)feet - offset;
            pivot.z = pose.position.z;
            ApplyPose(pivot, walking);
        }

        private void ApplyPose(Vector3 pivot, bool walking)
        {
            if (farmer != null) { farmer.SetCutscenePose(pivot, Directions[(int)facing], walking); return; }
            pose.position = pivot;
            renderer.sortingOrder = WorldSortOrder.GetOrder(pivot.y);
            if (JunimoFrames != null)
            {
                int junimoRow = facing switch { Direction.Down => 0, Direction.Right => 1, Direction.Up => 2, _ => 3 };
                renderer.sprite = JunimoFrames[JunimoSprites.WalkFrame(junimoRow, walking, Time.unscaledTime)];
                return;
            }
            if (frames == null || frames.Length < 16) return;
            int row = facing switch { Direction.Down => 0, Direction.Right => 1, Direction.Up => 2, _ => 3 };
            int frame = walking ? Mathf.FloorToInt(Time.unscaledTime * 8f) % 4 : 0;
            renderer.sprite = frames[row * 4 + frame];
        }

        internal async Task FadeAsync(float alpha, float seconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (renderer == null) return;
            Color start = renderer.color;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                renderer.color = new Color(start.r, start.g, start.b, Mathf.Lerp(start.a, alpha, elapsed / seconds));
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
            }
            renderer.color = new Color(start.r, start.g, start.b, alpha);
        }

        /// <summary>Hops in place; Junimos throw their arms up while they dance.</summary>
        internal async Task DanceAsync(float seconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (renderer == null)
            {
                for (float until = Time.unscaledTime + seconds; Time.unscaledTime < until;) await Task.Yield();
                return;
            }
            Transform visual = renderer.transform;
            Vector3 rest = visual.localPosition;
            int seed = ActorId.GetHashCode() & 0xff;
            float phase = seed / 255f;
            try
            {
                for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
                {
                    ct.ThrowIfCancellationRequested();
                    visual.localPosition = rest + Vector3.up * JunimoSprites.Hop(elapsed, phase);
                    if (JunimoFrames != null) renderer.sprite = JunimoFrames[JunimoSprites.DanceFrame(elapsed, seed)];
                    await Task.Yield();
                }
            }
            finally
            {
                if (visual != null) visual.localPosition = rest;
                if (pose != null) ApplyPose(pose.position, false);
            }
        }

        public async Task ShowEmoteAsync(EmoteType emote, float seconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            int index = emote switch { EmoteType.Heart => 20, EmoteType.Exclamation => 16,
                EmoteType.Sweat => 28, _ => 32 };
            Sprite sprite = ItemData.CreateAtlasSprite(sprites?.emotes, index);
            if (sprite == null) return;
            var bubble = new GameObject("Emote " + emote);
            bubble.transform.SetParent(pose, false);
            bubble.transform.localPosition = Vector3.up * 1.65f;
            var visual = bubble.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.sortingOrder = 30000;
            try
            {
                float until = Time.unscaledTime + seconds;
                while (Time.unscaledTime < until)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            finally
            {
                if (bubble != null) UnityEngine.Object.Destroy(bubble);
            }
        }
    }
}
