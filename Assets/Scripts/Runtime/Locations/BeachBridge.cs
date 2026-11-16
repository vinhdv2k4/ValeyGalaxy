using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The broken bridge east of Elliott's cabin. Using it asks to rebuild it with 300 wood; once
/// fixed (saved on the server as "beachBridgeFixed") the planks are laid and the deck is walkable.
/// </summary>
internal sealed class BeachBridge : MonoBehaviour
{
    private const string FixedFlag = "beachBridgeFixed";
    private const int WoodNeeded = 300;
    private const int DeckY = 13, ShadowY = 14, FirstX = 58, LastX = 61;
    private const int PlankTile = 301, ShadowTile = 336;

    private static BeachBridge active;
    private GameLocation location;
    private Tilemap back, buildings, collision;
    private bool applied, busy;

    internal static void Attach(GameLocation location)
    {
        if (location.ContentMapName != "Beach" || location.GetComponent<BeachBridge>() != null) return;
        var bridge = location.gameObject.AddComponent<BeachBridge>();
        bridge.location = location;
        bridge.back = location.transform.Find("Back")?.GetComponent<Tilemap>();
        bridge.buildings = location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        bridge.collision = location.transform.Find("MapCollision")?.GetComponent<Tilemap>();
    }

    private void OnEnable() => active = this;
    private void OnDisable() { if (active == this) active = null; }

    private static OnlineGameState Online => LocationManager.Instance?.LocalFarmer?.GetComponent<OnlineGameState>();

    private void Update()
    {
        if (!applied && Online?.Snapshot?.milestones?.HasSeen(FixedFlag) == true) Apply();
    }

    /// <summary>Action button on either broken end of the bridge.</summary>
    internal static bool TryInteract(Vector2? pointer, Vector3 standing, Vector2 facing)
    {
        if (active == null || active.applied || active.busy || active.buildings == null) return false;
        Vector3 target = pointer ?? standing + (Vector3)(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down);
        Vector3Int cell = active.buildings.WorldToCell(target);
        int tileX = cell.x, tileY = -cell.y;
        // Either broken end of the bridge, from the deck row or the rows just above and below it.
        if (tileY < DeckY - 1 || tileY > DeckY + 1 || tileX < FirstX - 1 || tileX > LastX + 1 ||
            tileX > FirstX && tileX < LastX && tileY != DeckY) return false;
        Vector3 center = active.buildings.GetCellCenterWorld(new Vector3Int(tileX <= (FirstX + LastX) / 2 ? FirstX : LastX, -DeckY, 0));
        if (((Vector2)(center - standing)).sqrMagnitude > 2.2f * 2.2f) return false;
        active.Ask();
        return true;
    }

    private static int WoodCarried()
    {
        int wood = 0;
        var inventory = Online?.Snapshot?.inventory;
        if (inventory != null)
            foreach (var stack in inventory)
                if (stack != null && stack.itemId == "wood") wood += stack.amount;
        return wood;
    }

    private void Ask()
    {
        int wood = WoodCarried();
        string count = $"Gỗ trong túi: {wood}/{WoodNeeded}";
        if (wood < WoodNeeded)
        {
            NpcConversation.ShowNotice($"Hmm... Với {WoodNeeded} gỗ thì có thể sửa được cây cầu này.\n{count}");
            return;
        }
        NpcConversation.ShowChoices($"Dùng {WoodNeeded} gỗ để sửa cây cầu?\n{count}", new (string, System.Action)[]
        {
            ("Có", Fix),
            ("Không", () => { })
        });
    }

    private void Fix()
    {
        var online = Online;
        var snapshot = online?.Snapshot;
        if (online == null || snapshot == null) return;
        busy = true;
        online.ActOnMilestone(new StartupApiClient.MilestoneAction
        {
            action = "fix_beach_bridge", id = "beach_bridge", location = location.LocationName,
            day = snapshot.gameDay, time = FarmDayClock.Instance?.TimeOfDay ?? 600
        }, error =>
        {
            if (this == null) return;
            if (!string.IsNullOrEmpty(error))
            {
                busy = false;
                NpcConversation.ShowNotice(error);
                return;
            }
            StartCoroutine(RepairScene());
        });
    }

    // Fade to black, hammering while the bridge is rebuilt, then fade back in.
    private IEnumerator RepairScene()
    {
        var locations = LocationManager.Instance;
        var farmer = locations?.LocalFarmer;
        if (farmer != null)
        {
            farmer.IsMovementEnabled = false;
            farmer.SetMovement(Vector2.zero);
        }
        yield return Fade(0f, 1f, 0.5f);
        Apply();
        float[] crafting = { 1f, 1.5f, 2f, 2.5f };
        float elapsed = 0f;
        int next = 0;
        bool chopped = false, shipped = false;
        while (elapsed < 4f)
        {
            if (next < crafting.Length && elapsed >= crafting[next]) { GameAudio.Play("crafting"); next++; }
            if (!chopped && elapsed >= 3f) { GameAudio.Play("axchop"); chopped = true; }
            if (!shipped && elapsed >= 3.2f) { GameAudio.Play("Ship"); shipped = true; }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return Fade(1f, 0f, 0.5f);
        if (farmer != null) farmer.IsMovementEnabled = true;
        busy = false;
    }

    private static IEnumerator Fade(float from, float to, float seconds)
    {
        var locations = LocationManager.Instance;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            locations?.SetCutsceneFade(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        locations?.SetCutsceneFade(to);
    }

    private void Apply()
    {
        applied = true;
        if (buildings == null || back == null) return;
        TileBase plank = MapLightingData.TileFromSameSheet(buildings.GetTile(Cell(FirstX, DeckY)), PlankTile);
        TileBase shadow = MapLightingData.TileFromSameSheet(back.GetTile(Cell(FirstX, ShadowY)), ShadowTile);
        for (int x = FirstX; x <= LastX; x++)
        {
            if (plank != null) buildings.SetTile(Cell(x, DeckY), plank);
            if (shadow != null) back.SetTile(Cell(x, ShadowY), shadow);
        }
        // The deck row becomes walkable; rows 12 and 14 keep the river blocked on either side.
        if (collision == null) return;
        collision.SetTile(Cell(FirstX, DeckY), null);
        collision.SetTile(Cell(LastX, DeckY), null);
        collision.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        collision.GetComponent<CompositeCollider2D>()?.GenerateGeometry();
    }

    private static Vector3Int Cell(int x, int y) => new(x, -y, 0);
}
