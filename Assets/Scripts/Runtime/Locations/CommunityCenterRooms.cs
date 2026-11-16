using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Per-room restoration of the ruined Community Center (CommunityCenter.loadArea): a finished
/// room copies its tiles from CommunityCenter_Refurbished. Rooms finished earlier are restored
/// on arrival; a room finished while the farmer is here (at its note or from the admin panel) plays
/// restoreAreaCutscene first: Junimos pop in all over the room, the screen glows white with the
/// area-completion message, the room is rebuilt, and its Junimo carries a star to the plaque. After
/// the last room the six Junimos dance goodbye and the hut leaves (Junimo.finalCutscene).
/// </summary>
internal sealed class CommunityCenterRooms : MonoBehaviour
{
    private const string RefurbishedMap = "CommunityCenter_Refurbished";
    private static readonly string[] Layers = { "Back", "Buildings", "Front", "Front2", "MapCollision" };
    private static readonly string[] RoomIds = { "pantry", "crafts", "fish", "boiler", "vault", "bulletin" };

    // Loaded once: the Junimo message is drawn every OnGUI pass while it shows.
    private static MilestoneSprites milestoneArt;
    private static MilestoneSprites MilestoneArt =>
        milestoneArt != null ? milestoneArt : milestoneArt = Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites");

    // CommunityCenter.getAreaBounds, in map tiles.
    private static readonly RectInt[] AreaBounds =
    {
        new(0, 0, 22, 11), new(0, 12, 21, 17), new(35, 4, 9, 9),
        new(52, 9, 16, 12), new(45, 0, 15, 9), new(22, 13, 28, 9)
    };

    // CommunityCenter.getNotePosition: the Junimo note (bundle "book") left in each room.
    private static readonly Vector2Int[] NotePositions =
        { new(14, 5), new(14, 23), new(40, 10), new(63, 14), new(55, 6), new(46, 11) };
    private const int BulletinArea = 5;

    // CommunityCenter.getJunimoNoteTileFrames: townInterior tile indices, 70 ms per frame.
    private static readonly int[] NoteFrames =
        { 1833, 1833, 1833, 1833, 1833, 1833, 1833, 1833, 1833, 1832,
          1824, 1825, 1826, 1827, 1828, 1829, 1830, 1831, 1832, 1833 };
    private static readonly int[] BulletinFrames =
        { 1741, 1741, 1741, 1741, 1741, 1741, 1741, 1741, 1741, 1773, 1805, 1805, 1773 };
    private const string NoteSheetPrefix = "townInterior_";

    // CommunityCenter.getAreaBounds 7 (rebuilt with the bulletin board) and 8 (the Junimo hut,
    // replaced after the goodbye), stored after the six rooms in the refurbished tile copy.
    private static readonly RectInt[] ExtraBounds = { new(44, 10, 6, 3), new(22, 4, 13, 9) };
    private const int BulletinExtra = 0, HutExtra = 1;

    // CommunityCenter.draw: plaque stars, top-left in world pixels at 4x.
    private static readonly Vector2[] StarPixels =
        { new(2136, 324), new(2136, 364), new(2096, 384), new(2056, 364), new(2056, 324), new(2096, 308) };
    private static readonly Vector2Int HutDoor = new(25, 10), PlaqueSpot = new(32, 9);
    // Junimo(position, whichArea) colors: LimeGreen, Orange, Turquoise, Tan, Gold, BlanchedAlmond.
    private static readonly Color[] AreaTints =
    {
        new Color32(50, 205, 50, 255), new Color32(255, 165, 0, 255), new Color32(64, 224, 208, 255),
        new Color32(210, 180, 140, 255), new Color32(255, 215, 0, 255), new Color32(255, 235, 205, 255)
    };
    // Strings\Locations:CommunityCenter_AreaCompletion1-6, by the number of rooms restored.
    private static readonly string[] CompletionMessages =
    {
        "Cảm ơn bạn. Chúng tôi sẽ không quên lòng tốt này.",
        "Sức sống đang dần quay lại nơi này...",
        "Một nửa căn nhà đã hồi sinh. Các Junimo vui lắm!",
        "Rừng và thị trấn lại gần nhau thêm một chút.",
        "Chỉ còn một phòng nữa thôi!",
        "Nhà văn hóa đã hồi sinh trọn vẹn. Cảm ơn bạn!"
    };
    private static readonly string[] GoodbyeLines = { "Tạm biệt!", "Cảm ơn bạn!" };
    private static readonly Color TextColor = new Color32(34, 17, 34, 255);
    private const float Frame = 1f / 60f;
    private const float GlowPerSecond = 0.005f * 60f;
    private const float TilesPerPixelFrame = 60f / 64f;
    private const float JumpSeconds = 0.45f;

    private readonly Queue<(int Area, string Name)> pending = new();
    private readonly HashSet<Vector2Int> occupied = new();
    // Bundle.getColorFromColorIndex: Lime, DeepPink, Orange, Orange, Red, LightBlue, Cyan.
    private static readonly Color[] BundleColors =
    {
        new Color32(0, 255, 0, 255), new Color32(255, 20, 147, 255), new Color32(255, 165, 0, 255),
        new Color32(255, 165, 0, 255), new Color32(255, 0, 0, 255), new Color32(173, 216, 230, 255),
        new Color32(0, 255, 255, 255)
    };
    private readonly HashSet<int> awaitingStar = new();
    private readonly SpriteRenderer[] stars = new SpriteRenderer[6];
    private readonly List<(Transform Anchor, string Text, float Until)> speech = new();
    private readonly bool[] extraApplied = new bool[2];
    private bool baselineTaken, playing;
    private string message;
    private float messageAlpha, nextMeep, nextWhip;

    private struct CellCopy
    {
        public Vector3Int Cell;
        public TileBase Tile;
        public Matrix4x4 Matrix;
        public Color Color;
        public TileFlags Flags;
    }

    // layer -> area -> cells of the refurbished map, read once per session.
    private static Dictionary<string, List<CellCopy>[]> refurbished;
    private static CommunityCenterRooms active;

    private GameLocation location;
    private readonly bool[] applied = new bool[AreaBounds.Length];
    private readonly HashSet<int> celebrating = new();
    private readonly List<(SpriteRenderer Renderer, float Born, int Seed)> junimos = new();
    private readonly SpriteRenderer[] notes = new SpriteRenderer[AreaBounds.Length];
    private readonly bool[] noteBlocks = new bool[AreaBounds.Length];
    private readonly Dictionary<int, Sprite> noteSprites = new();
    private Texture2D noteSheet;
    private Vector2Int noteSheetTopLeft;
    private int noteTileSize = 16;
    private float noteSheetPpu = 16f;
    private bool noteSheetSearched;

    internal static bool IsCelebrating => active != null && active.celebrating.Count > 0;
    /// <summary>
    /// No story event starts over a restore cutscene, nor during the visit a room was restored in:
    /// the opening ceremony waits until the farmer comes back in.
    /// </summary>
    internal static bool HoldsEvents => active != null &&
        LocationManager.Instance?.CurrentLocation == active.location && (IsCelebrating || active.restoredThisVisit);
    private bool restoredThisVisit;

    /// <summary>Tiles that open a room's bundles: each visible note (the bulletin board also by its board tile).</summary>
    internal static IEnumerable<(int Area, int X, int Y)> NoteTiles()
    {
        if (active == null || LocationManager.Instance?.CurrentLocation != active.location) yield break;
        for (int area = 0; area < active.notes.Length; area++)
        {
            if (active.notes[area] == null) continue;
            Vector2Int tile = NotePositions[area];
            yield return (area, tile.x, tile.y);
            if (area == BulletinArea) yield return (area, tile.x, tile.y + 1);
        }
    }

    internal static void Attach(GameLocation map)
    {
        if (map == null || map.LocationName != "CommunityCenter" || map.GetComponent<CommunityCenterRooms>() != null)
            return;
        // The refurbished map already has every room; only the ruins are patched.
        if (map.transform.Find("Front2") != null || map.ContentMapName == RefurbishedMap) return;
        map.gameObject.AddComponent<CommunityCenterRooms>().location = map;
        // Read the refurbished tiles now, behind the warp's black screen, not mid-cutscene.
        Refurbished();
    }

    private void OnEnable()
    {
        active = this;
        restoredThisVisit = false;
    }
    private void OnDestroy()
    {
        foreach (var sprite in noteSprites.Values) if (sprite != null) Destroy(sprite);
        noteSprites.Clear();
    }
    private void OnDisable()
    {
        if (active == this) active = null;
        if (celebrating.Count > 0) EndCelebration();
    }

    private static OnlineGameSnapshot Snapshot =>
        LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>()?.Snapshot;

    private void Update()
    {
        var snapshot = Snapshot;
        if (snapshot == null) return;
        bool here = LocationManager.Instance?.CurrentLocation == location;
        for (int area = 0; area < AreaBounds.Length; area++)
        {
            if (applied[area] || celebrating.Contains(area) || !IsRestored(snapshot, area)) continue;
            // A room finished while the farmer is here (at its note, or from the admin panel) gets its
            // cutscene; rooms finished before arrival are simply restored.
            if (!baselineTaken || !here || !Celebrate(area, AreaName(snapshot, area))) Apply(area);
        }
        baselineTaken = true;
        if (!playing && !extraApplied[HutExtra] && Array.TrueForAll(applied, done => done)) ApplyExtra(HutExtra);
        UpdateStars();
        UpdateNotes(snapshot);
    }

    private static string AreaName(OnlineGameSnapshot snapshot, int area)
    {
        foreach (var room in snapshot.bundles?.areas ?? Array.Empty<OnlineBundleArea>())
            if (room != null && room.area == area) return room.name;
        return null;
    }

    // ----- Junimo notes -----

    private void UpdateNotes(OnlineGameSnapshot snapshot)
    {
        int completeBundles = 0;
        foreach (var room in snapshot.bundles?.areas ?? Array.Empty<OnlineBundleArea>())
            foreach (var bundle in room?.bundles ?? Array.Empty<OnlineBundle>())
                if (bundle != null && bundle.complete) completeBundles++;
        int frame = Mathf.FloorToInt(Time.time * 1000f / 70f);
        for (int area = 0; area < notes.Length; area++)
        {
            // Joja members no longer find Junimo notes.
            bool show = snapshot.bundles?.areas != null && !applied[area] && !celebrating.Contains(area) &&
                snapshot.milestones?.HasSeen("JojaMember") != true &&
                !IsRestored(snapshot, area) && NoteUnlocked(area, completeBundles);
            if (show && notes[area] == null) CreateNote(area);
            else if (!show && notes[area] != null) RemoveNote(area);
            var note = notes[area];
            if (note == null) continue;
            int[] frames = area == BulletinArea ? BulletinFrames : NoteFrames;
            note.sprite = NoteSprite(frames[frame % frames.Length]);
        }
    }

    /// <summary>CommunityCenter.shouldNoteAppearInArea: rooms open up as bundles are finished.</summary>
    private static bool NoteUnlocked(int area, int completeBundles) => area switch
    {
        1 => true,
        0 or 2 => completeBundles > 0,
        3 => completeBundles > 1,
        5 => completeBundles > 2,
        4 => completeBundles > 3,
        _ => false
    };

    private void CreateNote(int area)
    {
        Vector2Int tile = NotePositions[area];
        int[] frames = area == BulletinArea ? BulletinFrames : NoteFrames;
        Sprite first = NoteSprite(frames[0]);
        if (first == null) return;
        var note = new GameObject("Junimo note " + area);
        note.transform.SetParent(transform, false);
        note.transform.position = location.TileCenter(tile.x, tile.y);
        var renderer = note.AddComponent<SpriteRenderer>();
        renderer.sprite = first;
        // Buildings notes sit on the floor like furniture; the bulletin note hangs on the Front layer.
        renderer.sortingOrder = area == BulletinArea ? WorldSortOrder.LayerFront + 1
            : WorldSortOrder.GetOrder(note.transform.position.y - 0.5f);
        notes[area] = renderer;

        // A Buildings tile blocks the farmer; mirror that in the collision layer.
        var collision = Layer("MapCollision");
        var cell = new Vector3Int(tile.x, -tile.y, 0);
        if (area != BulletinArea && collision != null && !collision.HasTile(cell) && BlockerTile(collision) is TileBase block)
        {
            collision.SetTile(cell, block);
            RefreshCollision(collision);
            noteBlocks[area] = true;
        }
    }

    private void RemoveNote(int area)
    {
        if (notes[area] != null) Destroy(notes[area].gameObject);
        notes[area] = null;
        if (!noteBlocks[area]) return;
        noteBlocks[area] = false;
        // A restored room already copied its own collision over this cell.
        if (applied[area]) return;
        var collision = Layer("MapCollision");
        if (collision == null) return;
        Vector2Int tile = NotePositions[area];
        collision.SetTile(new Vector3Int(tile.x, -tile.y, 0), null);
        RefreshCollision(collision);
    }

    private static TileBase BlockerTile(Tilemap collision)
    {
        BoundsInt bounds = collision.cellBounds;
        foreach (var cell in bounds.allPositionsWithin)
            if (collision.GetTile(cell) is TileBase tile) return tile;
        return null;
    }

    private static void RefreshCollision(Tilemap collision)
    {
        collision.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        collision.GetComponent<CompositeCollider2D>()?.GenerateGeometry();
    }

    /// <summary>One townInterior tile, cut from the sheet the ruins map is painted with.</summary>
    private Sprite NoteSprite(int index)
    {
        if (noteSprites.TryGetValue(index, out var cached)) return cached;
        if (!FindNoteSheet()) return null;
        int columns = noteSheet.width / noteTileSize;
        var rect = new Rect(noteSheetTopLeft.x + index % columns * noteTileSize,
            noteSheetTopLeft.y - (index / columns + 1) * noteTileSize, noteTileSize, noteTileSize);
        if (rect.x < 0 || rect.y < 0 || rect.xMax > noteSheet.width || rect.yMax > noteSheet.height) return null;
        var sprite = Sprite.Create(noteSheet, rect, new Vector2(0.5f, 0.5f), noteSheetPpu, 0, SpriteMeshType.FullRect);
        sprite.name = NoteSheetPrefix + index;
        noteSprites[index] = sprite;
        return sprite;
    }

    private bool FindNoteSheet()
    {
        if (noteSheet != null) return true;
        if (noteSheetSearched) return false;
        noteSheetSearched = true;
        foreach (var tilemap in GetComponentsInChildren<Tilemap>(true))
        {
            var used = new Sprite[tilemap.GetUsedSpritesCount()];
            tilemap.GetUsedSpritesNonAlloc(used);
            foreach (var sprite in used)
            {
                if (sprite == null || sprite.packed || !sprite.name.StartsWith(NoteSheetPrefix, StringComparison.Ordinal) ||
                    !int.TryParse(sprite.name.Substring(NoteSheetPrefix.Length), out int index)) continue;
                Rect rect = sprite.textureRect;
                noteTileSize = Mathf.RoundToInt(rect.width);
                int columns = sprite.texture.width / noteTileSize;
                noteSheet = sprite.texture;
                noteSheetPpu = sprite.pixelsPerUnit;
                // Pixel x of column 0 and pixel y of the top edge of row 0 (texture y grows upward).
                noteSheetTopLeft = new Vector2Int(Mathf.RoundToInt(rect.x) - index % columns * noteTileSize,
                    Mathf.RoundToInt(rect.yMax) + index / columns * noteTileSize);
                return true;
            }
        }
        Debug.LogWarning("[CommunityCenterRooms] townInterior sheet not found; Junimo notes are hidden.");
        return false;
    }

    private static bool IsRestored(OnlineGameSnapshot snapshot, int area)
    {
        // A room paid for through the Joja form is done, but the Junimos never rebuild it.
        if (snapshot.milestones?.HasSeen("joja_" + RoomIds[area]) == true) return false;
        foreach (var room in snapshot.bundles?.areas ?? Array.Empty<OnlineBundleArea>())
            if (room != null && room.area == area && room.complete) return true;
        foreach (var room in snapshot.milestones?.rooms ?? Array.Empty<OnlineBundleRoom>())
            if (room != null && room.done && room.id == RoomIds[area]) return true;
        return false;
    }

    // ----- Restore cutscene (CommunityCenter.restoreAreaCutscene / UpdateWhenCurrentLocation) -----

    /// <summary>
    /// Queues the restore cutscene for a room finished while the farmer is in the ruined
    /// Community Center. Returns false when the farmer is elsewhere.
    /// </summary>
    internal static bool Celebrate(int area, string roomName)
    {
        if (active == null || area < 0 || area >= AreaBounds.Length || active.applied[area] ||
            LocationManager.Instance?.CurrentLocation != active.location)
            return false;
        if (!active.celebrating.Add(area)) return true;
        active.restoredThisVisit = true;
        active.pending.Enqueue((area, roomName));
        if (!active.playing) active.StartCoroutine(active.PlayPending());
        return true;
    }

    private IEnumerator PlayPending()
    {
        playing = true;
        var locations = LocationManager.Instance;
        var farmer = locations.LocalFarmer;
        if (FarmDayClock.Instance != null) FarmDayClock.Instance.IsCutscenePaused = true;
        if (farmer != null)
        {
            farmer.SetMovement(Vector2.zero);
            farmer.IsMovementEnabled = false;
        }
        while (pending.Count > 0)
        {
            var (area, name) = pending.Dequeue();
            yield return RestoreAreaCutscene(area, name);
        }
        playing = false;
        EndCelebration();
    }

    private IEnumerator RestoreAreaCutscene(int area, string roomName)
    {
        var locations = LocationManager.Instance;
        var farmer = locations.LocalFarmer;
        var frames = JunimoSprites.Frames(MilestoneArt);
        var back = Layer("Back");
        var collision = Layer("MapCollision");
        RectInt bounds = AreaBounds[area];
        int roomsDone = 1;
        foreach (bool done in applied) if (done) roomsDone++;

        // Phase 0: one second of silence (changeMusicTrack "none"), then the farmer faces the room.
        GameAudio.PlayMusic("none");
        yield return Wait(1f);
        Face(farmer, Vector2.down);

        // Phase 1 (3 s): each frame, a 40% chance of a Junimo popping in somewhere in the room.
        float frameClock = 0f;
        for (float elapsed = 0f; elapsed < 3f; elapsed += Time.unscaledDeltaTime)
        {
            for (frameClock += Time.unscaledDeltaTime; frameClock >= Frame; frameClock -= Frame)
                if (frames != null && UnityEngine.Random.value < 0.4f) SpawnJunimo(bounds, frames, back, collision);
            Animate(frames);
            yield return null;
        }

        // Phase 2: screenGlowOnce(white, hold) rises at 0.005 a frame while the wind builds up.
        GameAudio.Play("wind");
        for (float glow = 0f; glow < 1f; )
        {
            glow = Mathf.Min(1f, glow + GlowPerSecond * Time.unscaledDeltaTime);
            locations.SetCutsceneFade(glow, Color.white);
            Animate(frames);
            yield return null;
        }
        // At full glow the message fades in (0.008 a frame) and the Junimos meep more and more for 5.2 s.
        message = CompletionMessages[Mathf.Clamp(roomsDone, 1, CompletionMessages.Length) - 1];
        messageAlpha = 0f;
        for (float elapsed = 0f; elapsed < 5.2f; elapsed += Time.unscaledDeltaTime)
        {
            messageAlpha = Mathf.Min(1f, messageAlpha + 0.48f * Time.unscaledDeltaTime);
            // (5200 - timer) / 10000 per frame, throttled so overlapping cues stay audible.
            float chance = elapsed / 10f * Time.unscaledDeltaTime / Frame;
            if (UnityEngine.Random.value < chance && Time.unscaledTime >= nextMeep)
            {
                nextMeep = Time.unscaledTime + 0.06f;
                GameAudio.Play(UnityEngine.Random.value < 0.5f ? "dustMeep" : "junimoMeep1");
            }
            Animate(frames);
            yield return null;
        }

        // Phase 3: the room is rebuilt under the flash.
        message = null;
        messageAlpha = 0f;
        ClearJunimos();
        Apply(area);
        awaitingStar.Add(area);
        KeepFarmerClear(locations, farmer);
        GameAudio.Play("wand");
        GameAudio.PlayMusic("junimoStarSong");
        GameAudio.Play("woodyHit");
        yield return Flash(locations, 0.6f);

        // restoreAreaTimer 2000: at 1000 the room's Junimo comes back for its star.
        yield return Wait(1f);
        bool allDone = Array.TrueForAll(applied, done => done);
        if (allDone)
        {
            // returnToJunimoHutToFetchFinalStar: the last star goes up at once, then the goodbye.
            awaitingStar.Remove(area);
            yield return GoodbyeCutscene(frames, farmer);
            yield break;
        }
        if (frames != null) StartCoroutine(DeliverStar(area, frames));
        else awaitingStar.Remove(area);
        yield return Wait(1f);
        if (!string.IsNullOrEmpty(roomName)) NpcConversation.ShowNotice(roomName + " đã được khôi phục!");
    }

    /// <summary>
    /// Junimo.returnToJunimoHutToFetchStar: the room's Junimo carries the bundle to the hut (25, 10),
    /// fetches a star, walks it to (32, 9) and throws it up onto the plaque. The farmer can move meanwhile.
    /// </summary>
    private IEnumerator DeliverStar(int area, Sprite[] frames)
    {
        Vector3 start = OpenTileNear(NotePositions[area]);
        var junimo = CreateJunimo("Junimo " + area, start, AreaTints[area], frames[44]);
        Texture2D junimoSheet = frames[0].texture;
        // Junimo.draw: holdingBundle draws Junimo (0, 96, 16, 13) in the bundle's colour over its head.
        junimo.Carried = Carry(junimo, junimoSheet, new RectInt(0, 96, 16, 13), BundleColor(area), 0.375f);
        SpawnPuff(5, start);
        GameAudio.Play("junimoMeep1");
        try
        {
            // holdingBundle at speed 3, then holdingStar at speed 1 (pixels per frame at 4x).
            yield return Walk(junimo, location.TileCenter(HutDoor.x, HutDoor.y), 3f * TilesPerPixelFrame, frames, true);
            // junimoReachedHutToFetchStar: the bundle stays at the hut; the star (0, 109, 16, 19) comes out.
            GameAudio.Play("dwop");
            if (junimo.Carried != null) Destroy(junimo.Carried.gameObject);
            junimo.Carried = Carry(junimo, junimoSheet, new RectInt(0, 109, 16, 19), Color.white, 0.25f);
            yield return Walk(junimo, location.TileCenter(PlaqueSpot.x, PlaqueSpot.y), TilesPerPixelFrame, frames, false);
            junimo.Renderer.sprite = frames[0];
            junimo.Renderer.flipX = false;
            if (junimo.Carried != null) Destroy(junimo.Carried.gameObject);

            // placeStar: the star spins up from above the Junimo (8 frames, 40 ms, 10 loops).
            GameAudio.Play("tinyWhip");
            var star = new GameObject("Plaque star");
            star.transform.SetParent(transform, false);
            var starRenderer = star.AddComponent<SpriteRenderer>();
            starRenderer.sortingOrder = WorldSortOrder.LayerFront + 2;
            Texture2D sheet = frames[0].texture;
            Vector3 origin = junimo.Root.position + Vector3.up;
            for (float elapsed = 0f; elapsed < 3.2f; elapsed += Time.unscaledDeltaTime)
            {
                int frame = Mathf.FloorToInt(elapsed / 0.04f) % 8;
                starRenderer.sprite = AtlasSprites.FromTop(sheet, new RectInt(frame * 16, 109, 16, 19), new Vector2(0.5f, 0.5f));
                // motion (0.22, -2) px a frame, acceleration (0, 0.01), in 64 px tiles.
                float frames60 = elapsed / Frame;
                star.transform.position = origin + new Vector3(0.22f * frames60,
                    2f * frames60 - 0.005f * frames60 * frames60, 0f) / 64f;
                yield return null;
            }
            Destroy(star);
            // performStartDoneSpinning: yoba twice and a flash; the star is now on the plaque.
            GameAudio.Play("yoba");
            awaitingStar.Remove(area);
            UpdateStars();
            if (!playing) yield return Flash(LocationManager.Instance, 0.5f);

            // Afterwards the Junimo wanders off; here it fades away.
            for (float alpha = 1f; alpha > 0f; alpha -= 0.9f * Time.unscaledDeltaTime)
            {
                junimo.Renderer.color = WithAlpha(junimo.Renderer.color, alpha);
                yield return null;
            }
        }
        finally
        {
            awaitingStar.Remove(area);
            if (junimo.Root != null) Destroy(junimo.Root.gameObject);
        }
    }

    /// <summary>
    /// The goodbye after the last room (Junimo.finalCutscene, CommunityCenter.junimoGoodbyeDance,
    /// startGoodbyeDance, endGoodbyeDance and loadJunimoHut).
    /// </summary>
    private IEnumerator GoodbyeCutscene(Sprite[] frames, CharacterMotor farmer)
    {
        var locations = LocationManager.Instance;
        // globalFadeToBlack at 0.005 a frame.
        for (float fade = 0f; fade < 1f; )
        {
            fade = Mathf.Min(1f, fade + GlowPerSecond * Time.unscaledDeltaTime);
            locations.SetCutsceneFade(fade, Color.black);
            yield return null;
        }

        // finalCutscene: the farmer stands at (29, 11) facing left, the view on the hut at (32.5, 6).
        PlaceFarmer(locations, farmer, 29, 11, Vector2.left);
        var focus = new GameObject("Goodbye camera").transform;
        focus.SetParent(transform, false);
        focus.position = location.TileCenter(32, 6) + new Vector3(0.5f, 0f, 0f);
        locations.SetCutsceneCameraTarget(focus);
        locations.SnapCutsceneCamera();
        var dancers = new List<(Junimo Junimo, int Area, float JumpAt)>();
        if (frames != null)
        {
            Vector2Int[] spots = { new(23, 11), new(27, 11), new(24, 12), new(28, 12), new(26, 12), new(25, 11) };
            for (int area = 0; area < spots.Length; area++)
            {
                var junimo = CreateJunimo("Goodbye Junimo " + area, location.TileCenter(spots[area].x, spots[area].y),
                    AreaTints[area], frames[16]);
                junimo.Renderer.color = WithAlpha(junimo.Renderer.color, 0f);
                dancers.Add((junimo, area, -1f));
            }
        }
        try
        {
            for (float fade = 1f; fade > 0f; )
            {
                fade = Mathf.Max(0f, fade - GlowPerSecond * Time.unscaledDeltaTime);
                locations.SetCutsceneFade(fade, Color.black);
                yield return null;
            }
            yield return Wait(1f);

            // junimoGoodbyeDance: the Junimos fade back in (0.02 a frame), facing right, while the view
            // pans to the farmer at 2 px a frame.
            Face(farmer, Vector2.left);
            Vector3 target = farmer != null ? farmer.StandingPosition : location.TileCenter(29, 11);
            float alpha = 0f;
            while ((focus.position - target).sqrMagnitude > 0.0004f)
            {
                focus.position = Vector3.MoveTowards(focus.position, target, 2f * TilesPerPixelFrame * Time.unscaledDeltaTime);
                alpha = Mathf.Min(1f, alpha + 1.2f * Time.unscaledDeltaTime);
                foreach (var dancer in dancers) dancer.Junimo.Renderer.color = WithAlpha(dancer.Junimo.Renderer.color, alpha);
                yield return null;
            }

            // startGoodbyeDance, held for 5 s (moveViewportTo ... 5000).
            yield return Dance(dancers, frames, 5f, alpha, fadeOut: false);
            // endGoodbyeDance: fadeAway (stayPut, -0.005 a frame) and loadJunimoHut after 3.6 s.
            yield return Dance(dancers, frames, 3.6f, alpha, fadeOut: true);
        }
        finally
        {
            speech.Clear();
            foreach (var dancer in dancers) if (dancer.Junimo.Root != null) Destroy(dancer.Junimo.Root.gameObject);
            if (focus != null) Destroy(focus.gameObject);
            locations.SetCutsceneCameraTarget(null);
        }

        // loadJunimoHut: the hut is gone, flash and wand.
        ApplyExtra(HutExtra);
        GameAudio.Play("wand");
        yield return Flash(locations, 0.6f);
        NpcConversation.ShowNotice("Các Junimo đã trở về nhà của chúng. Nhà văn hóa đã được hồi sinh trọn vẹn!");
    }

    private IEnumerator Dance(List<(Junimo Junimo, int Area, float JumpAt)> dancers, Sprite[] frames, float seconds,
        float alpha, bool fadeOut)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            if (fadeOut) alpha = Mathf.Max(0f, alpha - 0.3f * Time.unscaledDeltaTime);
            float now = Time.unscaledTime;
            for (int i = 0; i < dancers.Count; i++)
            {
                var (junimo, area, jumpAt) = dancers[i];
                // Junimo.update while sayingGoodbye: even areas run in place, odd ones wave.
                junimo.Renderer.sprite = area % 2 == 0
                    ? frames[16 + Mathf.FloorToInt(now / 0.05f) % 8]
                    : frames[28 + Mathf.FloorToInt(now / 0.08f) % 4];
                junimo.Renderer.color = WithAlpha(junimo.Renderer.color, alpha);
                float airborne = jumpAt < 0f ? -1f : now - jumpAt;
                if (airborne < 0f || airborne > JumpSeconds)
                {
                    jumpAt = -1f;
                    // A 1% chance a frame to jump, and 15% of jumps say goodbye.
                    if (alpha > 0.5f && UnityEngine.Random.value < 0.01f * Time.unscaledDeltaTime / Frame)
                    {
                        jumpAt = now;
                        if (UnityEngine.Random.value < 0.15f)
                            Say(junimo.Root, GoodbyeLines[UnityEngine.Random.Range(0, GoodbyeLines.Length)]);
                    }
                    airborne = jumpAt < 0f ? -1f : 0f;
                }
                float height = airborne < 0f ? 0f : 4f * 0.3f * airborne / JumpSeconds * (1f - airborne / JumpSeconds);
                junimo.Renderer.transform.localPosition = Vector3.up * height;
                dancers[i] = (junimo, area, jumpAt);
            }
            yield return null;
        }
    }

    private void Say(Transform anchor, string text)
    {
        speech.RemoveAll(line => line.Anchor == anchor);
        speech.Add((anchor, text, Time.unscaledTime + 3f));
    }

    private sealed class Junimo
    {
        public Transform Root;
        public SpriteRenderer Renderer;
        public SpriteRenderer Carried;
    }

    /// <summary>What a Junimo holds over its head, drawn at Position + (8, -64 + offset) like Junimo.draw.</summary>
    private static SpriteRenderer Carry(Junimo junimo, Texture2D sheet, RectInt source, Color color, float bottom)
    {
        var held = new GameObject("Carried");
        held.transform.SetParent(junimo.Renderer.transform, false); // hops with the Junimo
        held.transform.localPosition = new Vector3(0.125f, bottom, 0f);
        var renderer = held.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(sheet, source, new Vector2(0.5f, 0f));
        renderer.color = color;
        renderer.sortingOrder = junimo.Renderer.sortingOrder + 1;
        return renderer;
    }

    /// <summary>Colour of the room's last bundle, which the Junimo brings back to the hut.</summary>
    private static Color BundleColor(int area)
    {
        int color = 0;
        foreach (var room in Snapshot?.bundles?.areas ?? Array.Empty<OnlineBundleArea>())
            if (room != null && room.area == area)
                foreach (var bundle in room.bundles ?? Array.Empty<OnlineBundle>())
                    if (bundle != null) color = bundle.color;
        return BundleColors[Mathf.Clamp(color, 0, BundleColors.Length - 1)];
    }

    private Junimo CreateJunimo(string name, Vector3 position, Color tint, Sprite first)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.position = position;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = first;
        renderer.color = tint;
        renderer.sortingOrder = WorldSortOrder.GetOrder(position.y);
        return new Junimo { Root = root.transform, Renderer = renderer };
    }

    private IEnumerator Walk(Junimo junimo, Vector3 goal, float speed, Sprite[] frames, bool fadeIn)
    {
        List<Vector2> path = NpcPathfinder.FindPath(location, junimo.Root.position, goal, allowBlocked: true)
            ?? new List<Vector2> { goal };
        float alpha = fadeIn ? 0f : junimo.Renderer.color.a;
        float started = Time.unscaledTime;
        foreach (Vector2 point in path)
        {
            Vector3 next = new(point.x, point.y, junimo.Root.position.z);
            while ((junimo.Root.position - next).sqrMagnitude > 0.0001f)
            {
                Vector3 step = Vector3.MoveTowards(junimo.Root.position, next, speed * Time.unscaledDeltaTime);
                if (Mathf.Abs(step.x - junimo.Root.position.x) > 0.0001f)
                    junimo.Renderer.flipX = step.x < junimo.Root.position.x;
                junimo.Root.position = step;
                // Sprite.Animate(44, 4, 200): arms up, carrying the bundle or the star.
                junimo.Renderer.sprite = frames[44 + Mathf.FloorToInt((Time.unscaledTime - started) / 0.2f) % 4];
                junimo.Renderer.sortingOrder = WorldSortOrder.GetOrder(step.y);
                if (junimo.Carried != null)
                {
                    junimo.Carried.sortingOrder = junimo.Renderer.sortingOrder + 1;
                    junimo.Carried.color = WithAlpha(junimo.Carried.color, alpha);
                }
                alpha = Mathf.Min(1f, alpha + 1.2f * Time.unscaledDeltaTime);
                junimo.Renderer.color = WithAlpha(junimo.Renderer.color, alpha);
                yield return null;
            }
        }
    }

    /// <summary>Utility.getRandomAdjacentOpenTile around a note, as a world position.</summary>
    private Vector3 OpenTileNear(Vector2Int tile)
    {
        var collision = Layer("MapCollision");
        Vector2Int[] around = { new(0, 1), new(1, 0), new(-1, 0), new(0, -1), new(1, 1), new(-1, 1) };
        foreach (var offset in around)
        {
            Vector2Int cell = tile + offset;
            if (collision == null || !collision.HasTile(new Vector3Int(cell.x, -cell.y, 0)))
                return location.TileCenter(cell.x, cell.y);
        }
        return location.TileCenter(tile.x, tile.y + 1);
    }

    private IEnumerator Flash(LocationManager locations, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            locations?.SetCutsceneFade(1f - elapsed / seconds, Color.white);
            yield return null;
        }
        locations?.SetCutsceneFade(0f);
    }

    private static void Face(CharacterMotor farmer, Vector2 direction)
    {
        if (farmer == null) return;
        farmer.SetCutscenePose(farmer.transform.position, direction, false);
    }

    private void PlaceFarmer(LocationManager locations, CharacterMotor farmer, int x, int y, Vector2 facing)
    {
        if (farmer == null) return;
        Vector3 offset = farmer.StandingPosition - farmer.transform.position;
        Vector3 desired = location.TileCenter(x, y);
        if (!locations.TryGetCutsceneStandingPosition(location, desired, out var standing)) standing = desired;
        farmer.SetCutscenePose(standing - offset, facing, false);
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    // ----- Plaque stars (CommunityCenter.draw) -----

    private void UpdateStars()
    {
        int count = -awaitingStar.Count;
        foreach (bool done in applied) if (done) count++;
        for (int i = 0; i < stars.Length; i++)
        {
            bool show = i < count;
            if (show && stars[i] == null)
            {
                var star = new GameObject("Plaque star " + i);
                star.transform.SetParent(transform, false);
                Vector2 pixel = StarPixels[i];
                star.transform.position = location.TileCenter(0, 0) +
                    new Vector3(pixel.x / 64f - 0.5f, 0.5f - pixel.y / 64f, 0f);
                stars[i] = star.AddComponent<SpriteRenderer>();
                stars[i].sprite = AtlasSprites.FromTop(StardewUiAtlas.Cursors, new RectInt(354, 401, 7, 7), Vector2.up);
                stars[i].sortingOrder = WorldSortOrder.LayerFront + 1;
            }
            else if (!show && stars[i] != null)
            {
                Destroy(stars[i].gameObject);
                stars[i] = null;
            }
        }
    }

    // ----- Overlay text: the area-completion message and Junimo speech -----

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        bool showMessage = !string.IsNullOrEmpty(message) && messageAlpha > 0f;
        speech.RemoveAll(line => line.Anchor == null || Time.unscaledTime > line.Until);
        if (!showMessage && speech.Count == 0) return;
        Color previous = GUI.color;
        GUI.depth = -100;

        if (showMessage)
        {
            // drawAboveAlwaysFrontLayer: a lime Junimo above the quoted message, 640 px wide at 2/3 height.
            var frames = JunimoSprites.Frames(MilestoneArt);
            float top = Screen.height * 2f / 3f;
            if (frames != null)
            {
                GUI.color = new Color(0.196f, 0.804f, 0.196f, messageAlpha);
                Sprite junimo = frames[Mathf.FloorToInt(Time.unscaledTime * 1000f % 800f / 100f)];
                DrawSprite(new Rect(Screen.width / 2f - 32f, top - 80f, 64f, 64f), junimo);
            }
            GUI.color = Color.white;
            GameText.Draw(new Rect(Screen.width / 2f - 320f, top, 640f, 300f), "\"" + message + "\"",
                GameText.Face.Dialogue, new Color(TextColor.r, TextColor.g, TextColor.b, messageAlpha * 0.6f), shadow: false);
        }

        var camera = Camera.main;
        if (camera != null)
        {
            GUI.color = Color.white;
            foreach (var (anchor, text, _) in speech)
            {
                // Junimo.drawAboveAlwaysFrontLayer: text in a scroll two tiles above the Junimo.
                Vector3 screen = camera.WorldToScreenPoint(anchor.position + Vector3.up * 2f);
                float width = GameText.Measure(text, GameText.Face.Dialogue) + 8f;
                float x = screen.x - width / 2f, y = Screen.height - screen.y - 36f;
                StardewUiAtlas.DrawCursorsSprite(new Rect(x - 48f, y - 12f, 48f, 72f), new Rect(325, 318, 12, 18));
                StardewUiAtlas.DrawCursorsSprite(new Rect(x, y - 12f, width, 72f), new Rect(337, 318, 1, 18));
                StardewUiAtlas.DrawCursorsSprite(new Rect(x + width, y - 12f, 48f, 72f), new Rect(338, 318, 12, 18));
                GameText.Draw(new Rect(x, y + 10f, width + 8f, 48f), text, GameText.Face.Dialogue, TextColor, shadow: false);
            }
        }
        GUI.color = previous;
    }

    private static void DrawSprite(Rect destination, Sprite sprite)
    {
        if (sprite == null) return;
        Rect source = sprite.textureRect;
        Texture2D texture = sprite.texture;
        GUI.DrawTextureWithTexCoords(destination, texture, new Rect(source.x / texture.width,
            source.y / texture.height, source.width / texture.width, source.height / texture.height));
    }

    private void EndCelebration()
    {
        ClearJunimos();
        celebrating.Clear();
        pending.Clear();
        message = null;
        messageAlpha = 0f;
        var locations = LocationManager.Instance;
        locations?.SetCutsceneFade(0f);
        locations?.SetCutsceneCameraTarget(null);
        if (FarmDayClock.Instance != null) FarmDayClock.Instance.IsCutscenePaused = false;
        var farmer = locations?.LocalFarmer;
        if (farmer != null) farmer.IsMovementEnabled = true;
        GameAudio.RefreshLocationMusic();
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>
    /// UpdateWhenCurrentLocation phase 1: one random spot a frame; skipped when it collides with a
    /// wall or another Junimo (isCollidingPosition), so the room fills up without stacking.
    /// </summary>
    private void SpawnJunimo(RectInt bounds, Sprite[] frames, Tilemap back, Tilemap collision)
    {
        int x = UnityEngine.Random.Range(bounds.xMin, bounds.xMax);
        int y = UnityEngine.Random.Range(bounds.yMin, bounds.yMax);
        var cell = new Vector3Int(x, -y, 0);
        if (back != null && !back.HasTile(cell) || collision != null && collision.HasTile(cell)) return;
        if (!occupied.Add(new Vector2Int(x, y))) return;
        int seed = junimos.Count * 7 + x + y;
        Vector3 position = location.TileCenter(x, y);
        var junimo = CreateJunimo("Junimo", position, JunimoSprites.Tint(seed), frames[0]);
        junimos.Add((junimo.Renderer, Time.unscaledTime, seed));
        // broadcastSprites: animations row 5 (puff) or 46 (sparkle) at position + (16, 16).
        SpawnPuff(UnityEngine.Random.value < 0.5f ? 5 : 46, position + new Vector3(0.25f, -0.25f, 0f));
        if (Time.unscaledTime >= nextWhip)
        {
            nextWhip = Time.unscaledTime + 0.05f;
            GameAudio.Play("tinyWhip");
        }
    }

    /// <summary>TemporaryAnimatedSprite(row, position, White): 8 frames of 64 px, 100 ms each, drawn on top.</summary>
    private void SpawnPuff(int row, Vector3 center)
    {
        Texture2D sheet = Resources.Load<NpcCatalog>("NpcCatalog")?.animationsAtlas;
        if (sheet == null) return;
        var puff = new GameObject("Junimo puff");
        puff.transform.SetParent(transform, false);
        puff.transform.position = center;
        var renderer = puff.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = WorldSortOrder.LayerFront + 20;
        StartCoroutine(PlayPuff(renderer, sheet, row));
    }

    private static IEnumerator PlayPuff(SpriteRenderer renderer, Texture2D sheet, int row)
    {
        for (int frame = 0; frame < 8 && renderer != null; frame++)
        {
            // The sheet is drawn at 1x (64 px = one tile), unlike the 16 px tilesheets.
            renderer.sprite = AtlasSprites.FromTop(sheet, new RectInt(frame * 64, row * 64, 64, 64),
                new Vector2(0.5f, 0.5f), 64f);
            for (float t = 0f; t < 0.1f; t += Time.unscaledDeltaTime) yield return null;
        }
        if (renderer != null) Destroy(renderer.gameObject);
    }

    private void Animate(Sprite[] frames)
    {
        if (frames == null) return;
        foreach (var (renderer, born, seed) in junimos)
        {
            if (renderer == null) continue;
            float age = Time.unscaledTime - born;
            renderer.sprite = frames[JunimoSprites.DanceFrame(age, seed)];
            // The renderer sits on a child, so hopping never moves the Junimo's feet.
            renderer.transform.localPosition = Vector3.up * JunimoSprites.Hop(age, (seed & 0xff) / 255f);
            renderer.color = WithAlpha(renderer.color, Mathf.Clamp01(age / 0.2f));
        }
    }

    private void ClearJunimos()
    {
        occupied.Clear();
        foreach (var (renderer, _, _) in junimos)
            if (renderer != null) Destroy(renderer.transform.parent.gameObject);
        junimos.Clear();
    }

    private void KeepFarmerClear(LocationManager locations, CharacterMotor farmer)
    {
        if (farmer == null) return;
        Physics2D.SyncTransforms();
        Vector3 offset = farmer.StandingPosition - farmer.transform.position;
        if (locations.TryGetCutsceneStandingPosition(location, farmer.StandingPosition, out var standing))
            farmer.SetCutscenePose(standing - offset, farmer.Facing, false);
    }

    private Tilemap Layer(string name) => transform.Find(name)?.GetComponent<Tilemap>();

    private void Apply(int area)
    {
        applied[area] = true;
        CopyTiles(area);
        // loadArea(5) also loads area 7.
        if (area == BulletinArea) ApplyExtra(BulletinExtra);
    }

    private void ApplyExtra(int extra)
    {
        extraApplied[extra] = true;
        CopyTiles(AreaBounds.Length + extra);
    }

    private void CopyTiles(int area)
    {
        var source = Refurbished();
        if (source == null) return;
        foreach (string layerName in Layers)
        {
            if (!source.TryGetValue(layerName, out var areas)) continue;
            var target = Layer(layerName) ?? CreateLayer(layerName);
            if (target == null) continue;
            foreach (var copy in areas[area])
            {
                target.SetTile(copy.Cell, copy.Tile);
                if (copy.Tile == null) continue;
                target.SetTileFlags(copy.Cell, TileFlags.None);
                target.SetTransformMatrix(copy.Cell, copy.Matrix);
                target.SetColor(copy.Cell, copy.Color);
                target.SetTileFlags(copy.Cell, copy.Flags);
            }
            if (layerName == "MapCollision")
            {
                target.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
                target.GetComponent<CompositeCollider2D>()?.GenerateGeometry();
            }
        }
    }

    /// <summary>The ruins have no Front2 layer; it is cloned empty from the refurbished map.</summary>
    private Tilemap CreateLayer(string layerName)
    {
        GameObject prefab = LocationManager.Instance?.FindMapPrefab(RefurbishedMap);
        Transform template = prefab != null ? prefab.transform.Find(layerName) : null;
        if (template == null || template.GetComponent<Tilemap>() == null) return null;
        var clone = Instantiate(template.gameObject, transform, false);
        clone.name = layerName;
        var tilemap = clone.GetComponent<Tilemap>();
        tilemap.ClearAllTiles();
        return tilemap;
    }

    private static Dictionary<string, List<CellCopy>[]> Refurbished()
    {
        if (refurbished != null) return refurbished;
        GameObject prefab = LocationManager.Instance?.FindMapPrefab(RefurbishedMap);
        if (prefab == null)
        {
            Debug.LogWarning("[CommunityCenterRooms] Missing " + RefurbishedMap + " prefab.");
            return null;
        }
        // Instantiated under an inactive parent so the map never wakes up; only its tiles are read.
        var holder = new GameObject("Refurbished tiles");
        holder.SetActive(false);
        try
        {
            var copy = Instantiate(prefab, holder.transform, false);
            refurbished = new Dictionary<string, List<CellCopy>[]>(StringComparer.Ordinal);
            foreach (string layerName in Layers)
            {
                var tilemap = copy.transform.Find(layerName)?.GetComponent<Tilemap>();
                if (tilemap == null) continue;
                var areas = new List<CellCopy>[AreaBounds.Length + ExtraBounds.Length];
                for (int area = 0; area < areas.Length; area++)
                {
                    areas[area] = new List<CellCopy>();
                    RectInt bounds = area < AreaBounds.Length ? AreaBounds[area] : ExtraBounds[area - AreaBounds.Length];
                    for (int x = bounds.xMin; x < bounds.xMax; x++)
                        for (int y = bounds.yMin; y < bounds.yMax; y++)
                        {
                            var cell = new Vector3Int(x, -y, 0);
                            TileBase tile = tilemap.GetTile(cell);
                            areas[area].Add(new CellCopy
                            {
                                Cell = cell, Tile = tile,
                                Matrix = tile != null ? tilemap.GetTransformMatrix(cell) : Matrix4x4.identity,
                                Color = tile != null ? tilemap.GetColor(cell) : Color.white,
                                Flags = tile != null ? tilemap.GetTileFlags(cell) : TileFlags.None
                            });
                        }
                }
                refurbished[layerName] = areas;
            }
            return refurbished;
        }
        finally { Destroy(holder); }
    }
}
