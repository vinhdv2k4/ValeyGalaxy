using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages mine floor state, top-left floor indicator plaque, and elevator interaction.
/// </summary>
public sealed class MineManager : MonoBehaviour
{
    private static MineManager instance;
    public static MineManager Instance => instance;

    public static int CurrentFloor { get; set; } = 0;
    private int floorTextFor = int.MinValue;
    private string cachedFloorText = "";

    /// <summary>Changes on every arrival on a floor; a rebuilt floor's rocks can be mined again.</summary>
    internal static int FloorVisit { get; set; }
    public static bool ForceInMine { get; set; } = false;
    internal static int LastElevatorStop =>
        Mathf.Clamp(LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>()?.Snapshot
            ?.deepestMineFloor ?? 0, 0, 120) / 5 * 5;

    public static bool IsInMine
    {
        get
        {
            if (ForceInMine) return true;
            string loc = LocationManager.Instance?.CurrentLocation?.LocationName ?? "";
            if (string.IsNullOrEmpty(loc) && UnityEngine.SceneManagement.SceneManager.GetActiveScene() != null)
                loc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            return loc.Equals("Mine", System.StringComparison.OrdinalIgnoreCase) ||
                   int.TryParse(loc, out _);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance == null)
        {
            var go = new GameObject("MineManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MineManager>();
            instance.enabled = true;
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public static void SetFloor(int floor)
    {
        LocationManager.Instance?.WarpFarmerToMineFloor(floor, true);
    }

    internal static bool IsMineFloor(string locationName) =>
        int.TryParse(locationName, out int floor) && (floor is >= 1 and <= 120 || floor == 77377);

    internal static int RoomTemplate(int floor)
    {
        if (floor is 120 or 77377) return floor;
        int cycle = floor % 40;
        if (cycle == 20) return 20;
        return floor % 10 == 0 ? 10 : cycle;
    }

    public static bool TryInteract(Vector2? pointer, Vector3 standing, Vector2 facing)
    {
        var manager = LocationManager.Instance;
        if (!IsInMine || manager == null || manager.IsWarping) return false;
        if (MineTreasure.TryInteract(manager.CurrentLocation, pointer, standing, facing)) return true;
        var buildings = manager.CurrentLocation?.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (buildings == null) return false;
        Vector3Int origin = buildings.WorldToCell(standing);
        for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++)
        {
            Vector3Int cell = origin + new Vector3Int(x, y, 0);
            int index = TileSpriteIndex.Get(buildings.GetSprite(cell));
            if (index != 112 && index != 115 && index != 173) continue;
            Vector2 center = buildings.GetCellCenterWorld(cell);
            Vector2 delta = center - (Vector2)standing;
            if (delta.sqrMagnitude > 4f) continue;
            if (pointer.HasValue)
            {
                Vector3Int clicked = buildings.WorldToCell(pointer.Value);
                bool doorArt = (index == 112 || index == 115) && clicked.x == cell.x &&
                    clicked.y >= cell.y && clicked.y <= cell.y + 2;
                if (clicked != cell && !doorArt) continue;
            }
            else if (delta.sqrMagnitude > 0.25f && Vector2.Dot(delta.normalized, facing) < 0.4f) continue;

            if (index == 112)
            {
                if (CurrentFloor > 120) return false;
                if (LastElevatorStop < 5)
                    NpcConversation.ShowNotice("Thang máy chưa hoạt động. Hãy xuống đến tầng 5 trước.");
                else MineElevatorUI.Open(CurrentFloor, LastElevatorStop, SetFloor);
            }
            else if (index == 115) manager.WarpFarmerToMineFloor(0, false);
            else
            {
                // Mine.tbin also contains the separate quarry portal at (67, 9).
                int next = CurrentFloor == 0 && cell.x == 67 ? 77377 : CurrentFloor + 1;
                if (CurrentFloor >= 120) return false;
                GameAudio.Play("stairsdown");
                manager.WarpFarmerToMineFloor(next, false);
            }
            return true;
        }
        return false;
    }

    internal static bool TryGetArrivalPosition(GameLocation location, bool lobby, int sourceFloor,
        bool byElevator, out Vector3 position)
    {
        position = default;
        if (lobby)
        {
            // ExitMine_Leave and MineElevatorMenu use different Mine.tbin tiles.
            Vector3Int cell = byElevator ? new Vector3Int(17, -4, 0)
                : sourceFloor == 77377 ? new Vector3Int(67, -10, 0) : new Vector3Int(23, -8, 0);
            Grid grid = location.GetComponent<Grid>();
            position = grid != null ? grid.GetCellCenterWorld(cell)
                : location.transform.TransformPoint((Vector3)cell + new Vector3(0.5f, 0.5f, 0f));
            return true;
        }

        var buildings = location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (buildings == null) return false;
        int wanted = byElevator ? 112 : 115;
        for (int attempt = 0; attempt < (byElevator ? 2 : 1); attempt++, wanted = 115)
        foreach (Vector3Int cell in buildings.cellBounds.allPositionsWithin)
        {
            if (TileSpriteIndex.Get(buildings.GetSprite(cell)) != wanted) continue;
            position = buildings.GetCellCenterWorld(cell + Vector3Int.down);
            return true;
        }
        return false;
    }

    public static void OnRockBroken(Vector3 position, int luckBonus)
    {
        var location = LocationManager.Instance?.CurrentLocation;
        if (!IsInMine || CurrentFloor <= 0 || CurrentFloor >= 120 || location == null) return;
        var buildings = location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (buildings == null) return;
        int remaining = 0;
        foreach (MineRock rock in location.GetComponentsInChildren<MineRock>())
            if (!rock.IsBroken) remaining++;
        // The supported mines currently have no enemies, so the original adds 4%.
        float dailyLuck = FarmDayClock.Instance?.DailyLuck ?? 0f;
        if (remaining > 0 && Random.value >= 0.06f + 1f / remaining + luckBonus / 100f + dailyLuck / 5f) return;
        TryPlaceDownLadder(buildings, buildings.WorldToCell(position));
    }

    internal static void TryPlaceDownLadder(Tilemap buildings, Vector3Int cell)
    {
        if (buildings == null || buildings.HasTile(cell)) return;
        Sprite sheetSprite = null;
        foreach (Vector3Int tile in buildings.cellBounds.allPositionsWithin)
        {
            Sprite sprite = buildings.GetSprite(tile);
            int index = TileSpriteIndex.Get(sprite);
            if (index == 173 || index == 174) return;
            if (index == 112 || index == 115) sheetSprite = sprite;
        }
        if (sheetSprite == null) return;
        Sprite ladderSprite = ItemData.CreateAtlasSprite(sheetSprite.texture, 173);
        if (ladderSprite == null) return;
        var ladder = ScriptableObject.CreateInstance<Tile>();
        ladder.name = "Mine ladder down";
        ladder.sprite = ladderSprite;
        ladder.colliderType = Tile.ColliderType.None;
        buildings.SetTile(cell, ladder);
    }

    private void OnGUI()
    {
        // Don't draw if full inventory is open
        if (FullInventoryMenuUI.IsOpen) return;

        // Draw top-left mine floor indicator plaque if in mine/cave
        if (IsInMine)
        {
            DrawMineFloorIndicator();
        }
    }

    /// <summary>
    /// Renders the mine floor indicator at top-left.
    /// </summary>
    private void DrawMineFloorIndicator()
    {
        const float scale = 4f;
        const float x = 16f;
        const float y = 16f;
        const float capW = 3f * scale; // 12px
        const float plaqueH = 17f * scale; // 68px

        // OnGUI runs a few times a frame; only build the label when the floor changes.
        if (floorTextFor != CurrentFloor)
        {
            floorTextFor = CurrentFloor;
            cachedFloorText = CurrentFloor.ToString();
        }
        string floorText = cachedFloorText;
        Texture2D font = StardewUiAtlas.FontBold;

        // Text measurements: each glyph is 8 * 2 = 16px wide, 16 * 2 = 32px high
        const float textScale = 2f;
        float textW = floorText.Length * (8f * textScale);
        float textH = 16f * textScale;

        float midW = Mathf.Max(28f, textW + 16f);
        float totalW = capW * 2f + midW;

        // Draw Plaque Left Cap
        StardewUiAtlas.DrawCursorsSprite(new Rect(x, y, capW, plaqueH), StardewUiAtlas.MineLevelPlaqueLeft);

        // Draw Plaque Mid Repeating
        StardewUiAtlas.DrawCursorsSprite(new Rect(x + capW, y, midW, plaqueH), StardewUiAtlas.MineLevelPlaqueMid);

        // Draw Plaque Right Cap
        StardewUiAtlas.DrawCursorsSprite(new Rect(x + capW + midW, y, capW, plaqueH), StardewUiAtlas.MineLevelPlaqueRight);

        // Draw Red Floor Number inside plaque
        Color oldColor = GUI.color;
        GUI.color = new Color(0.88f, 0.12f, 0.12f, 1f); // Vibrant red matching screenshot
        float tx = x + (totalW - textW) * 0.5f;
        float ty = y + (plaqueH - textH) * 0.5f - 2f;
        NpcBitmapText.Draw(font, new Rect(tx, ty, textW + 8f, textH + 4f), floorText, textScale);
        GUI.color = oldColor;
    }
}
