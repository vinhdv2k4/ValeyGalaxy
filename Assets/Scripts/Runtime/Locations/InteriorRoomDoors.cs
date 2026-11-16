using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class MapInteriorRoomDoor
{
    public string map;
    public int x, y, tileIndex;
    public string[] owners;
}

[Serializable]
public sealed class MapInteriorRoomDoorData
{
    public MapInteriorRoomDoor[] doors;
}

internal sealed class InteriorRoomDoors : MonoBehaviour
{
    private const int RequiredFriendship = 500;
    private const string UnlockFlag = "doorUnlock";
    private static MapInteriorRoomDoor[] catalog;
    private readonly List<RoomDoor> doors = new();
    private GameLocation location;
    private Tilemap buildings, front, collision;
    private TilemapCollider2D collisionShape;

    private sealed class RoomDoor
    {
        internal MapInteriorRoomDoor Data;
        internal Vector3Int Cell;
        internal TileBase BuildingTile, FrontMiddle, FrontTop, CollisionTile;
        internal SpriteRenderer Visual;
        internal Sprite[] Frames;
        internal bool IsOpen;
        internal bool UnlockPending;
    }

    internal static InteriorRoomDoors Attach(GameLocation location)
    {
        if (catalog == null)
        {
            TextAsset source = Resources.Load<TextAsset>("InteriorRoomDoors");
            catalog = (source == null ? null : JsonUtility.FromJson<MapInteriorRoomDoorData>(source.text)?.doors)
                ?? Array.Empty<MapInteriorRoomDoor>();
        }
        if (!Array.Exists(catalog, door => string.Equals(door.map, location.ContentMapName,
            StringComparison.OrdinalIgnoreCase))) return null;
        Tilemap buildings = location.transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (buildings == null) return null;

        var view = location.gameObject.AddComponent<InteriorRoomDoors>();
        view.location = location;
        view.buildings = buildings;
        view.front = location.transform.Find("Front")?.GetComponent<Tilemap>();
        view.collision = location.transform.Find("MapCollision")?.GetComponent<Tilemap>();
        view.collisionShape = view.collision?.GetComponent<TilemapCollider2D>();
        foreach (MapInteriorRoomDoor data in catalog)
        {
            if (!string.Equals(data.map, location.ContentMapName, StringComparison.OrdinalIgnoreCase)) continue;
            var cell = new Vector3Int(data.x, -data.y, 0);
            TileBase tile = buildings.GetTile(cell);
            if (tile == null) continue;
            view.doors.Add(new RoomDoor
            {
                Data = data,
                Cell = cell,
                BuildingTile = tile,
                FrontMiddle = view.front?.GetTile(cell + Vector3Int.up),
                FrontTop = view.front?.GetTile(cell + Vector3Int.up * 2),
                CollisionTile = view.collision?.GetTile(cell)
            });
            view.CreateDoorSprite(view.doors[view.doors.Count - 1]);
        }
        return view;
    }

    internal bool TryUse(Vector2? pointer, Vector2 standing, Vector2 facing, OnlineGameState online)
    {
        RoomDoor closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (RoomDoor door in doors)
        {
            if (door.IsOpen) continue;
            Vector2 offset = (Vector2)buildings.GetCellCenterWorld(door.Cell) - standing;
            if (Mathf.Abs(offset.x) > (pointer.HasValue ? 1.1f : 0.6f) || Mathf.Abs(offset.y) > 1.35f) continue;
            if (pointer.HasValue)
            {
                Vector3Int clicked = buildings.WorldToCell(pointer.Value);
                if (clicked.x != door.Cell.x || clicked.y < door.Cell.y || clicked.y > door.Cell.y + 2) continue;
            }
            else if (Vector2.Dot(offset, facing) <= 0.2f) continue;
            if (offset.sqrMagnitude >= closestDistance) continue;
            closest = door;
            closestDistance = offset.sqrMagnitude;
        }
        if (closest == null) return false;
        if (!closest.UnlockPending) TryOpen(closest, online);
        return true;
    }

    private float nextNpcScan;
    private float nextLockedNotice;
    private NpcActor[] npcs = Array.Empty<NpcActor>();
    private float nextNpcRefresh;

    private void Update()
    {
        if (doors.Count == 0) return;
        if (Time.time >= nextNpcScan)
        {
            nextNpcScan = Time.time + 0.1f;
            OpenForPassingNpcs();
        }
        BlockLockedRoom();
    }

    // An NPC walking onto a door tile opens it; the door stays open until the location resets.
    private void OpenForPassingNpcs()
    {
        if (Time.time >= nextNpcRefresh)
        {
            nextNpcRefresh = Time.time + 2f;
            npcs = FindObjectsByType<NpcActor>(FindObjectsSortMode.None);
        }
        foreach (NpcActor npc in npcs)
        {
            if (npc == null || !npc.IsVisible ||
                !string.Equals(npc.LocationName, location.LocationName, StringComparison.Ordinal)) continue;
            Vector3Int cell = buildings.WorldToCell(npc.transform.position);
            foreach (RoomDoor door in doors)
                if (!door.IsOpen && cell.x == door.Cell.x &&
                    cell.y >= door.Cell.y - 1 && cell.y <= door.Cell.y + 1)
                    StartCoroutine(Open(door));
        }
    }

    // Once an NPC has opened a door, a farmer under 2 hearts who walks onto it is pushed back.
    private void BlockLockedRoom()
    {
        CharacterMotor farmer = LocationManager.Instance?.LocalFarmer;
        if (farmer == null) return;
        OnlineGameState online = farmer.GetComponent<OnlineGameState>();
        Vector3 standing = farmer.StandingPosition;
        Vector3Int cell = buildings.WorldToCell(standing);
        foreach (RoomDoor door in doors)
        {
            if (!door.IsOpen || cell != door.Cell || CanEnter(door, online)) continue;
            Vector3 center = buildings.GetCellCenterWorld(door.Cell);
            float side = standing.y < center.y ? -0.75f : 0.75f;
            farmer.SetMovement(Vector2.zero);
            farmer.transform.position += new Vector3(0f, center.y + side - standing.y, 0f);
            if (Time.time >= nextLockedNotice)
            {
                nextLockedNotice = Time.time + 1.5f;
                NpcConversation.ShowNotice(LockedMessage(door));
            }
            return;
        }
    }

    private static bool CanEnter(RoomDoor door, OnlineGameState online)
    {
        string[] owners = door.Data.owners ?? Array.Empty<string>();
        OnlineGameSnapshot snapshot = online?.Snapshot;
        return owners.Length == 0 ||
            Array.Exists(owners, npc => snapshot?.milestones?.HasSeen(UnlockFlag + npc) == true) ||
            Array.Exists(owners, npc => online != null && online.GetNpcFriendshipPoints(npc) >= RequiredFriendship);
    }

    private static string LockedMessage(RoomDoor door) =>
        $"Bạn cần ít nhất 2 tim với {string.Join(" hoặc ", door.Data.owners ?? Array.Empty<string>())} để mở cửa phòng.";

    private void TryOpen(RoomDoor door, OnlineGameState online)
    {
        string[] owners = door.Data.owners ?? Array.Empty<string>();
        OnlineGameSnapshot snapshot = online?.Snapshot;
        if (owners.Length == 0 || Array.Exists(owners, npc => snapshot?.milestones?.HasSeen(UnlockFlag + npc) == true))
        {
            StartCoroutine(Open(door));
            return;
        }
        string owner = Array.Find(owners, npc => online != null && online.GetNpcFriendshipPoints(npc) >= RequiredFriendship);
        if (owner == null || snapshot == null)
        {
            NpcConversation.ShowNotice(LockedMessage(door));
            return;
        }

        door.UnlockPending = true;
        online.ActOnMilestone(new StartupApiClient.MilestoneAction
        {
            action = "unlock_door", id = owner, location = location.LocationName,
            day = snapshot.gameDay, time = FarmDayClock.Instance?.TimeOfDay ?? 600
        }, error =>
        {
            door.UnlockPending = false;
            if (this == null || !isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(error)) StartCoroutine(Open(door));
            else NpcConversation.ShowNotice(error);
        });
    }

    // The map tiles under a door are only a faint shadow; the door itself is a 16x48 Cursors
    // sprite at (x, y - 2) that is always drawn and stays on frame 0 while closed.
    private void CreateDoorSprite(RoomDoor door)
    {
        int tile = door.Data.tileIndex;
        int atlasX = tile switch { 120 => 512, 824 or 825 => 640, 838 => 576, _ => -1 };
        if (atlasX < 0) return;
        door.Frames = new Sprite[4];
        for (int frame = 0; frame < door.Frames.Length; frame++)
            door.Frames[frame] = StartupCharacterScreen.CropCursors(atlasX + frame * 16, 144, 16, 48);
        Vector3 center = buildings.GetCellCenterWorld(door.Cell);
        var visual = new GameObject("Room Door " + door.Data.x + "," + door.Data.y);
        visual.transform.SetParent(transform, false);
        visual.transform.position = center + Vector3.up;
        door.Visual = visual.AddComponent<SpriteRenderer>();
        door.Visual.sprite = door.Frames[0];
        // Sorts just above the door tile's bottom edge.
        door.Visual.sortingOrder = WorldSortOrder.GetOrder(center.y - 5f / 16f);
        door.Visual.flipX = tile == 825 || (tile == 838 && door.Data.x == 10 && door.Data.y == 5);
    }

    private IEnumerator Open(RoomDoor door)
    {
        if (door.IsOpen) yield break;
        door.IsOpen = true;
        buildings.SetTile(door.Cell, null);
        front?.SetTile(door.Cell + Vector3Int.up, null);
        front?.SetTile(door.Cell + Vector3Int.up * 2, null);
        GameAudio.Play(door.Data.tileIndex == 120 ? "doorOpen" : "doorCreak");

        // The sprite plays its four frames at 100 ms and holds the last one; the
        // temporary barrier is lifted after 400 ms.
        var interval = new WaitForSeconds(0.1f);
        for (int frame = 0; frame < 4; frame++)
        {
            if (door.Visual != null) door.Visual.sprite = door.Frames[frame];
            yield return interval;
        }
        collision?.SetTile(door.Cell, null);
        collisionShape?.ProcessTilemapChanges();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (buildings == null) return;
        foreach (RoomDoor door in doors)
        {
            if (door.Visual != null) door.Visual.sprite = door.Frames[0];
            if (!door.IsOpen) continue;
            buildings.SetTile(door.Cell, door.BuildingTile);
            front?.SetTile(door.Cell + Vector3Int.up, door.FrontMiddle);
            front?.SetTile(door.Cell + Vector3Int.up * 2, door.FrontTop);
            collision?.SetTile(door.Cell, door.CollisionTile);
            door.IsOpen = false;
        }
        collisionShape?.ProcessTilemapChanges();
    }
}
