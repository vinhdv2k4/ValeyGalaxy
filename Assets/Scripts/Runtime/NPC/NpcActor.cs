using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class NpcActor : MonoBehaviour
{
    private static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    private static Dictionary<string, string> activityDescriptions;

    [SerializeField, Min(0.1f)] private float moveSpeed = 2f;
    [SerializeField] private NpcDefinition definition;

    private Rigidbody2D body;
    private CircleCollider2D hitbox;
    private SpriteRenderer spriteRenderer;
    private Sprite[] animationFrames;
    private IReadOnlyList<NpcScheduleStop> stops = Array.Empty<NpcScheduleStop>();
    private Dictionary<string, string> lines;
    private string currentLocation;
    private Vector2 destination;
    private List<Vector2> path;
    private int pathIndex;
    private int stopIndex;
    private int facing;
    private int destinationFacing;
    private int loadedDay = -1;
    private bool loadedRain;
    private int lastTalkDay = -1;
    private float animationTime;
    private bool isTalking;
    private bool hiddenForCutscene;
    private int nextStopTravelMinutes = -1;
    private List<LocationWarp> warpRoute;
    private int warpIndex;
    private bool travelling;
    private bool pathAttemptedForCurrentLeg;
    private int[] activityIntro = Array.Empty<int>();
    private int[] activityLoop = Array.Empty<int>();
    private int[] activityOutro = Array.Empty<int>();
    private int activityPhase;
    private int activityStartedForStop = -1;
    private float activityTime;
    private Vector2 squareCenter;
    private Vector2 squareTarget;
    private List<Vector2> squarePath;
    private int squarePathIndex;
    private int squareWidth;
    private int squareHeight;
    private int squareFacing = -1;
    private float squarePause;

    public string DisplayName => definition?.name ?? name;
    public Sprite Portrait => PortraitFor(0);
    public Sprite PortraitFor(int index) => NpcSprites.PortraitFrame(definition?.portrait, index);
    public bool IsVisible => spriteRenderer != null && spriteRenderer.enabled;
    internal string LocationName => currentLocation;
    public bool ContainsPointer(Vector2 point)
    {
        if (spriteRenderer == null) return false;
        Bounds bounds = spriteRenderer.bounds;
        bounds.Expand(new Vector3(0.4f, 0.4f, 0f));
        return bounds.Contains(new Vector3(point.x, point.y, bounds.center.z));
    }

    public void SetTalking(bool talking)
    {
        isTalking = talking;
        if (talking) Animate(false);
    }

    internal void SetCutsceneHidden(bool hidden)
    {
        hiddenForCutscene = hidden;
        if (hidden)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (hitbox != null) hitbox.enabled = false;
        }
        else RefreshVisibility();
    }

    public void FaceTowards(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) facing = direction.x > 0f ? 1 : 3;
        else facing = direction.y > 0f ? 0 : 2;
        Animate(false);
    }

    public void Configure(NpcDefinition npc)
    {
        definition = npc;
        name = npc.name;
        Initialize();
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        hitbox = GetComponent<CircleCollider2D>();
        hitbox.radius = 0.25f;
        hitbox.enabled = false;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (definition == null) return;
        animationFrames = NpcSprites.CharacterFrames(definition.animationFrames);
        if (spriteRenderer != null && animationFrames.Length > 0)
        {
            // The 16x32 frame sits 0.25 tile below the standing point.
            spriteRenderer.transform.localPosition = NpcSprites.FeetOffset;
            spriteRenderer.transform.localScale = Vector3.one;
            spriteRenderer.sprite = animationFrames[0];
        }

        if (activityDescriptions == null)
        {
            TextAsset source = Resources.Load<NpcCatalog>("NpcCatalog")?.animationDescriptions;
            activityDescriptions = source == null
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(source.text);
        }

        try
        {
            lines = definition.dialogue == null ? null :
                JsonConvert.DeserializeObject<Dictionary<string, string>>(definition.dialogue.text);
        }
        catch (JsonException error)
        {
            Debug.LogWarning($"[NPC] Invalid dialogue {DisplayName}: {error.Message}");
            lines = null;
        }
    }

    private void Update()
    {
        if (definition == null || hiddenForCutscene) return;
        FarmDayClock clock = FarmDayClock.Instance;
        if (clock == null) return;
        if (loadedDay != clock.Day || loadedRain != clock.IsRainingToday) LoadDay(clock.Day, clock.IsRainingToday);
        if (stops.Count == 0) return;

        int clockTime = clock.TimeOfDay;
        int clockMinutes = ToMinutes(clockTime);
        int dueIndex = stopIndex;
        if (!travelling)
        {
            for (int i = stopIndex + 1; i < stops.Count && stops[i].Time <= clockTime; i++) dueIndex = i;
            if (dueIndex == stopIndex && stopIndex + 1 < stops.Count)
            {
                NpcScheduleStop next = stops[stopIndex + 1];
                int walkingMinutes = EstimateTravelMinutes(next);
                if (clockMinutes >= ToMinutes(next.Time) - walkingMinutes) dueIndex++;
            }
        }
        if (dueIndex != stopIndex) SetStop(dueIndex, false);
        if (travelling && !pathAttemptedForCurrentLeg) BuildPath(LocationManager.Instance);
        RefreshVisibility();
    }

    private void FixedUpdate()
    {
        if (hiddenForCutscene || stops.Count == 0 || stopIndex < 0 || body == null || isTalking) return;
        if (StepActivity()) return;
        if (!travelling && StepSquare()) return;
        Vector2 current = body.position;
        Vector2 waypoint = path != null && pathIndex < path.Count ? path[pathIndex] : destination;
        Vector2 delta = waypoint - current;
        if (delta.sqrMagnitude < 0.0004f)
        {
            if (path != null && pathIndex < path.Count) pathIndex++;
            if (path == null || pathIndex >= path.Count)
            {
                if (travelling && warpRoute != null && warpIndex < warpRoute.Count)
                {
                    CrossWarp();
                    return;
                }
                facing = destinationFacing;
                if (travelling) travelling = false;
                if (activityPhase == 0) BeginActivity(stops[stopIndex].Behavior);
            }
            if (activityPhase == 0) Animate(false);
            return;
        }

        Vector2 next = Vector2.MoveTowards(current, waypoint, moveSpeed * Time.fixedDeltaTime);
        body.MovePosition(next);
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) facing = delta.x > 0f ? 1 : 3;
        else facing = delta.y > 0f ? 0 : 2;
        Animate(true);
    }

    private void LoadDay(int day, bool raining)
    {
        loadedDay = day;
        loadedRain = raining;
        // Marlon stays in the guild and has no daily schedule.
        stops = definition.name == "Marlon"
            ? new[] { new NpcScheduleStop(600, "AdventureGuild", new Vector2Int(5, 11), 2, null) }
            : NpcSchedule.Load(definition.schedule, day, raining);
        stopIndex = 0;
        if (stops.Count == 0)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (hitbox != null) hitbox.enabled = false;
            Debug.LogWarning($"[NPC] No supported schedule for {DisplayName} on day {day}.");
            return;
        }

        // At daybreak the NPC is at home, before the first timed appointment.
        // The final stop in schedule data is normally the previous night's bed.
        NpcScheduleStop first = stops[0];
        NpcScheduleStop home = stops[stops.Count - 1];
        if (stops.Count > 1 && home.Time > first.Time &&
            (!string.Equals(home.Location, first.Location, StringComparison.OrdinalIgnoreCase) ||
             home.Tile != first.Tile))
        {
            currentLocation = home.Location;
            destination = StopPosition(home);
            body.position = destination;
            transform.position = destination;
            facing = home.Facing;
            stopIndex = -1;
            travelling = false;
            warpRoute = null;
            path = null;
            nextStopTravelMinutes = -1;
            activityPhase = 0;
            Animate(false);
            if (!string.IsNullOrEmpty(home.Behavior) &&
                home.Behavior.EndsWith("_sleep", StringComparison.Ordinal) &&
                activityDescriptions != null &&
                activityDescriptions.TryGetValue(home.Behavior, out string sleepAnimation))
            {
                int[] sleepFrames = ParseFrames(sleepAnimation.Split('/')[0]);
                if (sleepFrames.Length > 0) SetFrame(sleepFrames[0]);
            }
            RefreshVisibility();
        }
        else SetStop(0, true);
    }

    private void SetStop(int index, bool placeImmediately)
    {
        NpcScheduleStop stop = stops[index];
        bool changedLocation = !string.Equals(currentLocation, stop.Location, StringComparison.OrdinalIgnoreCase);
        bool playOutro = !placeImmediately && activityPhase != 0 &&
            activityOutro.Length > 0;
        stopIndex = index;
        activityStartedForStop = -1;
        squareWidth = squareHeight = 0;
        squarePath = null;
        destinationFacing = stop.Facing;
        warpRoute = null;
        warpIndex = 0;
        path = null;
        pathIndex = 0;
        pathAttemptedForCurrentLeg = false;
        nextStopTravelMinutes = -1;
        LocationManager manager = LocationManager.Instance;
        if (placeImmediately)
        {
            currentLocation = stop.Location;
            facing = stop.Facing;
            destination = StopPosition(stop);
            body.position = destination;
            transform.position = destination;
            travelling = false;
            activityPhase = 0;
            BeginActivity(stop.Behavior);
        }
        else
        {
            if (changedLocation)
            {
                warpRoute = manager?.FindNpcWarpRoute(currentLocation, stop.Location);
                if (warpRoute == null || warpRoute.Count == 0)
                {
                    // A schedule can mention a map with no prefab warp metadata.
                    // Place the actor at that stop only when its scheduled time arrives.
                    currentLocation = stop.Location;
                    destination = StopPosition(stop);
                    body.position = destination;
                    transform.position = destination;
                    travelling = false;
                    activityPhase = 0;
                    BeginActivity(stop.Behavior);
                    RefreshVisibility();
                    return;
                }
            }
            travelling = true;
            destination = warpRoute != null ? WarpContact(warpRoute[0]) : StopPosition(stop);
            BuildPath(manager);
            if (playOutro)
            {
                activityPhase = 3;
                activityTime = 0f;
            }
            else activityPhase = 0;
        }
        RefreshVisibility();
    }

    private static Vector2 StopPosition(NpcScheduleStop stop) => NpcPathfinder.TileToWorld(stop.Tile);

    private static Vector2 WarpContact(LocationWarp warp) => NpcPathfinder.TileToWorld(new Vector2Int(warp.x, warp.y));

    private static Vector2 WarpArrival(LocationWarp warp) => NpcPathfinder.TileToWorld(new Vector2Int(warp.targetX, warp.targetY));

    private void BuildPath(LocationManager manager)
    {
        path = null;
        pathIndex = 0;
        if (manager?.CurrentLocation == null ||
            !string.Equals(manager.CurrentLocation.LocationName, currentLocation,
                StringComparison.OrdinalIgnoreCase)) return;
        pathAttemptedForCurrentLeg = true;
        path = NpcPathfinder.FindPath(manager.CurrentLocation, body.position, destination) ??
            NpcPathfinder.FindPath(manager.CurrentLocation, body.position, destination, true);
        if (path == null)
            Debug.LogWarning($"[NPC] No clear route for {DisplayName} in {currentLocation}; walking directly to schedule tile.");
    }

    private void CrossWarp()
    {
        LocationWarp warp = warpRoute[warpIndex++];
        currentLocation = warp.targetName;
        Vector2 arrival = WarpArrival(warp);
        body.position = arrival;
        transform.position = arrival;
        destination = warpIndex < warpRoute.Count
            ? WarpContact(warpRoute[warpIndex]) : StopPosition(stops[stopIndex]);
        pathAttemptedForCurrentLeg = false;
        BuildPath(LocationManager.Instance);
        RefreshVisibility();
    }

    private void BeginActivity(string behavior)
    {
        if (activityStartedForStop == stopIndex) return;
        activityStartedForStop = stopIndex;
        activityPhase = 0;
        activityIntro = activityLoop = activityOutro = Array.Empty<int>();
        if (!string.IsNullOrEmpty(behavior) && behavior.StartsWith("square_", StringComparison.Ordinal))
        {
            string[] size = behavior.Split('_');
            if (size.Length >= 3 && int.TryParse(size[1], out squareWidth) &&
                int.TryParse(size[2], out squareHeight))
            {
                squareCenter = StopPosition(stops[stopIndex]);
                squareTarget = squareCenter;
                squareFacing = size.Length > 3 && int.TryParse(size[3], out int direction)
                    ? direction : -1;
                squarePause = UnityEngine.Random.Range(6f, 12f);
                return;
            }
            squareWidth = squareHeight = 0;
        }
        if (string.IsNullOrEmpty(behavior) || activityDescriptions == null ||
            !activityDescriptions.TryGetValue(behavior, out string description)) return;
        string[] parts = description.Split('/');
        if (parts.Length < 3) return;
        activityIntro = ParseFrames(parts[0]);
        activityLoop = ParseFrames(parts[1]);
        activityOutro = ParseFrames(parts[2]);
        activityPhase = activityIntro.Length > 0 ? 1 : activityLoop.Length > 0 ? 2 : 0;
        activityTime = 0f;
        if (activityPhase != 0) SetFrame(activityIntro.Length > 0 ? activityIntro[0] : activityLoop[0]);
    }

    private static int[] ParseFrames(string source)
    {
        string[] parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var frames = new List<int>(parts.Length);
        foreach (string part in parts)
            if (int.TryParse(part, out int frame) && frame >= 0) frames.Add(frame);
        return frames.ToArray();
    }

    private bool StepActivity()
    {
        if (activityPhase == 0) return false;
        activityTime += Time.fixedDeltaTime;
        int frame = Mathf.FloorToInt(activityTime / 0.1f);
        if (activityPhase == 1 && frame >= activityIntro.Length)
        {
            activityPhase = activityLoop.Length > 0 ? 2 : 0;
            activityTime = 0f;
            frame = 0;
        }
        else if (activityPhase == 3 && frame >= activityOutro.Length)
        {
            activityPhase = 0;
            return false;
        }
        int[] sequence = activityPhase == 1 ? activityIntro :
            activityPhase == 2 ? activityLoop : activityOutro;
        if (sequence.Length == 0) return false;
        SetFrame(sequence[activityPhase == 2 ? frame % sequence.Length :
            Mathf.Min(frame, sequence.Length - 1)]);
        return true;
    }

    private bool StepSquare()
    {
        if (squareWidth <= 0 || squareHeight <= 0) return false;
        if (squarePause > 0f)
        {
            squarePause -= Time.fixedDeltaTime;
            if (squareFacing >= 0) facing = squareFacing;
            Animate(false);
            return true;
        }
        if (squarePath == null)
        {
            int x = UnityEngine.Random.Range(0, squareWidth) - squareWidth / 2;
            int y = UnityEngine.Random.Range(0, squareHeight) - squareHeight / 2;
            squareTarget = squareCenter + new Vector2(x, -y);
            LocationManager manager = LocationManager.Instance;
            if (manager?.CurrentLocation != null &&
                string.Equals(manager.CurrentLocation.LocationName, currentLocation,
                    StringComparison.OrdinalIgnoreCase))
                squarePath = NpcPathfinder.FindPath(manager.CurrentLocation, body.position, squareTarget);
            squarePath ??= new List<Vector2> { squareTarget };
            squarePathIndex = 0;
        }
        Vector2 next = squarePath[squarePathIndex];
        Vector2 delta = next - body.position;
        if (delta.sqrMagnitude < 0.0004f)
        {
            if (++squarePathIndex >= squarePath.Count)
            {
                squarePath = null;
                squarePause = UnityEngine.Random.Range(6f, 12f);
            }
            return true;
        }
        body.MovePosition(Vector2.MoveTowards(body.position, next, moveSpeed * Time.fixedDeltaTime));
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) facing = delta.x > 0f ? 1 : 3;
        else facing = delta.y > 0f ? 0 : 2;
        Animate(true);
        return true;
    }

    private void SetFrame(int index)
    {
        Sprite[] frames = animationFrames;
        if (spriteRenderer != null && frames != null && index >= 0 && index < frames.Length)
            spriteRenderer.sprite = frames[index];
    }

    private static int ToMinutes(int hhmm) => hhmm / 100 * 60 + hhmm % 100;

    private int EstimateTravelMinutes(NpcScheduleStop next)
    {
        if (nextStopTravelMinutes >= 0) return nextStopTravelMinutes;
        LocationManager manager = LocationManager.Instance;
        Vector2 target = StopPosition(next);
        float tiles = 0f;
        if (string.Equals(currentLocation, next.Location, StringComparison.OrdinalIgnoreCase))
        {
            tiles = Mathf.Abs(target.x - body.position.x) + Mathf.Abs(target.y - body.position.y);
            if (manager?.CurrentLocation != null &&
                string.Equals(manager.CurrentLocation.LocationName, currentLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                List<Vector2> route = NpcPathfinder.FindPath(manager.CurrentLocation, body.position, target)
                    ?? NpcPathfinder.FindPath(manager.CurrentLocation, body.position, target, true);
                if (route != null) tiles = route.Count;
            }
        }
        else
        {
            List<LocationWarp> route = manager?.FindNpcWarpRoute(currentLocation, next.Location);
            if (route == null || route.Count == 0) return 0;
            Vector2 start = body.position;
            foreach (LocationWarp warp in route)
            {
                Vector2 contact = WarpContact(warp);
                tiles += Mathf.Abs(contact.x - start.x) + Mathf.Abs(contact.y - start.y);
                start = WarpArrival(warp);
            }
            tiles += Mathf.Abs(target.x - start.x) + Mathf.Abs(target.y - start.y);
        }
        nextStopTravelMinutes = Mathf.CeilToInt(tiles / Mathf.Max(0.1f, moveSpeed) / 0.7f);
        return nextStopTravelMinutes;
    }

    private void LateUpdate()
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
        }
    }

    private void RefreshVisibility()
    {
        LocationManager manager = LocationManager.Instance;
        bool visible = manager != null && manager.CurrentLocation != null &&
            string.Equals(manager.CurrentLocation.LocationName, currentLocation, StringComparison.OrdinalIgnoreCase);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
            spriteRenderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
        }
        if (hitbox != null) hitbox.enabled = visible;
    }

    private void Animate(bool moving)
    {
        Sprite[] frames = animationFrames;
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;
        // Source AnimatedSprite direction: 0=up, 1=right, 2=down, 3=left.
        // Character sheets are laid out down/right/up/left, four frames per row.
        int row = facing == 0 ? 2 : facing == 2 ? 0 : facing;
        animationTime = moving ? animationTime + Time.fixedDeltaTime : 0f;
        int frame = moving ? Mathf.FloorToInt(animationTime / 0.175f) % 4 : 0;
        int spriteIndex = row * 4 + frame;
        spriteRenderer.sprite = frames[Mathf.Clamp(spriteIndex, 0, frames.Length - 1)];
    }

    public List<NpcDialoguePage> Talk(int day)
    {
        if (lines == null || lines.Count == 0)
            return new List<NpcDialoguePage> { new NpcDialoguePage("...", 0) };
        string weekday = Weekdays[(Math.Max(1, day) - 1) % Weekdays.Length];
        string key = day == 1 && lastTalkDay < 0 && lines.ContainsKey("Introduction")
            ? "Introduction" : lastTalkDay == day ? weekday + "2" : weekday;
        if (!lines.TryGetValue(key, out string text) &&
            !lines.TryGetValue(weekday, out text) &&
            !lines.TryGetValue("Introduction", out text))
            return new List<NpcDialoguePage> { new NpcDialoguePage("...", 0) };

        lastTalkDay = day;
        string farmerName = StartupSession.Farmer?.name ?? "farmer";
        bool male = StartupSession.Farmer == null || StartupSession.Farmer.gender != 1;
        List<NpcDialoguePage> pages = NpcDialogue.Parse(text, farmerName, male);
        if (pages.Count == 0) pages.Add(new NpcDialoguePage("...", 0));
        return pages;
    }
}
