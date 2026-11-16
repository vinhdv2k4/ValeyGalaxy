using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Draws animal buildings on the farm and everything inside them: animal doors, hay in the
/// troughs, produce lying on the floor and the animals themselves. Animals go outside to graze
/// while their door is open on a dry, non-winter day between 9:00 and 18:00.
/// </summary>
internal sealed class FarmRanchView : MonoBehaviour
{
    private const string HayId = "object_178";
    private const int OutsideFrom = 900, OutsideUntil = 1800;

    private readonly List<LocationWarp> entries = new();
    private readonly List<AnimalDoorView> doors = new();
    private readonly List<(OnlineRanchProduct Product, SpriteRenderer Renderer)> products = new();
    private readonly List<FarmAnimalActor> animals = new();
    private Transform display;
    private string displayedState;
    private GameLocation map;
    private float nextOutsideCheck;

    private static OnlineRanchHome[] knownHomes = Array.Empty<OnlineRanchHome>();
    private static OnlineGameSnapshot lastSnapshot;
    private static bool requestPending;

    internal static IReadOnlyList<OnlineRanchHome> Homes => knownHomes;

    internal static bool ReservesCell(int x, int y) => Array.Exists(knownHomes, home => home.Reserves(x, y));

    /// <summary>True while a building's animals are out grazing on the farm.</summary>
    internal static bool AnimalsOutside(OnlineRanchHome home)
    {
        var clock = FarmDayClock.Instance;
        if (home == null || !home.animalDoorOpen || home.level < 1 || clock == null) return false;
        if (clock.IsRainingToday || clock.CurrentSeason == "winter") return false;
        int time = clock.TimeOfDay;
        return time >= OutsideFrom && time < OutsideUntil;
    }

    internal static void Refresh(GameLocation location, OnlineGameSnapshot snapshot)
    {
        if (snapshot != null)
        {
            knownHomes = snapshot.ranchHomes ?? Array.Empty<OnlineRanchHome>();
            lastSnapshot = snapshot;
        }
        if (location == null || lastSnapshot == null || FarmRanchAssets.Data == null) return;
        bool farm = location.LocationName == "Farm";
        string buildingId = FarmRanchAssets.BuildingId(location.LocationName);
        if (!farm && buildingId == null) return;
        var view = location.GetComponent<FarmRanchView>();
        if (view == null) view = location.gameObject.AddComponent<FarmRanchView>();
        view.map = location;
        view.Redraw(farm, buildingId);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextOutsideCheck || map == null || lastSnapshot == null) return;
        nextOutsideCheck = Time.unscaledTime + 1f;
        Redraw(map.LocationName == "Farm", FarmRanchAssets.BuildingId(map.LocationName));
    }

    private string Signature(bool farm, string buildingId)
    {
        var snapshot = lastSnapshot;
        var homes = knownHomes;
        var animalList = snapshot.ranchAnimals ?? Array.Empty<OnlineRanchAnimal>();
        var productList = snapshot.ranchProducts ?? Array.Empty<OnlineRanchProduct>();
        string location = farm ? "Farm" : buildingId;
        string AnimalKey(OnlineRanchAnimal a) => $"{a.id}:{a.age}:{a.produce}:{a.petted}";
        string items = string.Join(",", productList.Where(p => p.location == location).Select(p => p.id));
        if (farm)
        {
            return string.Join(";", homes.Select(home => $"{home.id}:{home.x},{home.y}:{home.level}>{home.targetLevel}@{home.readyDay}/{snapshot.gameDay}:" +
                string.Join(",", (home.paint ?? Array.Empty<OnlineRanchPaint>()).Select(p => p.custom ? $"{p.hue}/{p.saturation}/{p.lightness}" : "-")) +
                $":{home.animalDoorOpen}:{AnimalsOutside(home)}:" +
                (AnimalsOutside(home) ? string.Join(",", animalList.Where(a => a.home == home.id).Select(AnimalKey)) : ""))) + "|" + items;
        }
        OnlineRanchHome room = Array.Find(homes, home => home.id == buildingId);
        bool outside = AnimalsOutside(room);
        return $"{room?.level}:{string.Join(",", room?.hay ?? Array.Empty<int>())}:{outside}:{snapshot.piecesOfHay > 0}:" +
            (outside ? "" : string.Join(",", animalList.Where(a => a.home == buildingId).Select(AnimalKey))) + "|" + items;
    }

    private void Redraw(bool farm, string buildingId)
    {
        string signature = Signature(farm, buildingId);
        if (displayedState == signature) return;
        displayedState = signature;
        foreach (LocationWarp entry in entries) map.RemoveWarp(entry);
        entries.Clear();
        doors.Clear();
        products.Clear();
        animals.Clear();
        if (display != null)
        {
            display.gameObject.SetActive(false);
            Destroy(display.gameObject);
        }
        display = new GameObject("Ranch").transform;
        display.SetParent(map.transform, false);
        var animalList = lastSnapshot.ranchAnimals ?? Array.Empty<OnlineRanchAnimal>();
        if (farm)
        {
            DrawBuildings(lastSnapshot.gameDay);
            foreach (OnlineRanchHome home in knownHomes)
                if (AnimalsOutside(home))
                    SpawnAnimals(home, animalList.Where(a => a.home == home.id), true);
            DrawProducts("Farm");
            return;
        }
        OnlineRanchHome room = Array.Find(knownHomes, home => home.id == buildingId);
        if (room == null) return;
        if (room.targetLevel > room.level && room.readyDay > lastSnapshot.gameDay)
            RobinConstructionView.Spawn(display, map.TileCenter(1, 5) + new Vector3(0f, -0.5f, 0f));
        DrawHay(room);
        DrawHopper(room);
        DrawProducts(buildingId);
        if (!AnimalsOutside(room)) SpawnAnimals(room, animalList.Where(a => a.home == buildingId), false);
    }

    // ── Buildings ────────────────────────────────────────────────────────────────────────────

    /// <summary>Building art for a level without the animal-door strip at the bottom of the sheet.</summary>
    internal static Sprite BuildingSprite(string kind, int level)
    {
        Texture2D art = FarmRanchAssets.Art(FarmRanchAssets.Plan(kind, Mathf.Max(1, level))?.name);
        if (art == null) return null;
        int strip = FarmRanchAssets.Footprint(kind)?.HasAnimalDoor == true ? 16 : 0;
        return AtlasSprites.Get(art, new Rect(0, strip, art.width, art.height - strip), Vector2.zero);
    }

    /// <summary>The sprite drawn for a finished building, including its paint job.</summary>
    internal static Sprite HomeSprite(OnlineRanchHome home, OnlineRanchPaint[] paintOverride = null)
    {
        int level = Mathf.Max(1, home.level);
        string planName = FarmRanchAssets.Plan(home.kind, level)?.name;
        OnlineRanchPaint[] paint = paintOverride ?? home.paint;
        if (RanchPainter.HasPaint(paint))
        {
            Sprite painted = RanchPainter.PaintedSprite(planName, FarmRanchAssets.Art(planName), paint);
            if (painted != null) return painted;
        }
        return BuildingSprite(home.kind, level);
    }

    internal static Vector3 FootprintOrigin(GameLocation map, int x, int y, int height) =>
        map.TileCenter(x, y + height - 1) + new Vector3(-0.5f, -0.5f, 0f);

    private static Vector3 TileTopLeft(GameLocation map, int x, int y) => map.TileCenter(x, y) + new Vector3(-0.5f, 0.5f, 0f);

    private void DrawBuildings(int day)
    {
        foreach (OnlineRanchHome home in knownHomes)
        {
            bool newSite = home.level == 0;
            Vector3 origin = FootprintOrigin(map, home.x, home.y, home.height);
            var visual = new GameObject("RanchBuilding:" + home.id);
            visual.transform.SetParent(display, false);
            visual.transform.position = origin;
            var collider = visual.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(home.width, home.height);
            collider.offset = collider.size * 0.5f;
            if (newSite)
            {
                DrawScaffold(home, home.readyDay - day <= 1);
                RobinConstructionView.Spawn(display, TileTopLeft(map, home.x + home.width / 2, home.y + home.height / 2) +
                    new Vector3(0.75f, -0.25f, 0f));
                continue;
            }
            Sprite sprite = HomeSprite(home);
            if (sprite == null) continue;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = WorldSortOrder.GetOrder(origin.y);
            if (home.targetLevel > home.level && home.readyDay > day) DrawUpgradeSign(home, renderer.sortingOrder);

            DrawAnimalDoor(home, renderer.sortingOrder);
            string roomName = FarmRanchAssets.RoomName(home);
            GameObject room = FarmRanchAssets.Interior(roomName);
            LocationWarp exit = room?.GetComponent<GameLocation>()?.Warps.FirstOrDefault(warp => warp.targetName == "Farm");
            if (exit == null) continue;
            var entry = new LocationWarp(home.DoorX, home.DoorY, roomName, exit.x, exit.y - 1)
                { isDoor = true, fromMapAction = true };
            map.AddWarp(entry);
            entries.Add(entry);
        }
    }

    /// <summary>
    /// Wooden scaffolding over every tile of a site: corner, edge and centre pieces, with the
    /// finishing planks laid on the ground the day before Robin is done.
    /// </summary>
    private void DrawScaffold(OnlineRanchHome home, bool lastDay)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        int left = home.x, top = home.y, right = home.x + home.width - 1, bottom = home.y + home.height - 1;
        int middle = home.x + home.width / 2;
        for (int x = left; x <= right; x++)
        for (int y = top; y <= bottom; y++)
        {
            (int sx, int sy)? piece;
            (int sx, int sy) ground;
            bool sortsLow = false;
            if (x == middle && y == bottom) { piece = (367, 309); ground = (367, 277); sortsLow = true; }
            else if (x == left && y == top) { piece = (351, 293); ground = (351, 261); sortsLow = true; }
            else if (x == right && y == top) { piece = (383, 293); ground = (383, 261); sortsLow = true; }
            else if (x == right && y == bottom) { piece = (383, 325); ground = (383, 277); }
            else if (x == left && y == bottom) { piece = (351, 325); ground = (351, 277); }
            else if (x == right) { piece = (383, 309); ground = (383, 261); }
            else if (y == bottom) { piece = (367, 325); ground = (367, 277); }
            else if (x == left) { piece = (351, 309); ground = (351, 261); }
            else if (y == top) { piece = (367, 293); ground = (367, 261); }
            else { piece = null; ground = (367, 261); }

            Vector3 tile = TileTopLeft(map, x, y);
            if (lastDay)
                AddPiece(cursors, ground, tile + new Vector3(0f, -0.25f, 0f), WorldSortOrder.LayerPaths + 1);
            if (piece != null)
            {
                float sortY = sortsLow ? tile.y - 1f : tile.y;
                AddPiece(cursors, piece.Value, tile, WorldSortOrder.GetOrder(sortY));
            }
        }
    }

    private void AddPiece(Texture2D cursors, (int x, int y) source, Vector3 topLeft, int order)
    {
        var go = new GameObject("Scaffold");
        go.transform.SetParent(display, false);
        go.transform.position = topLeft;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(cursors, source.x, source.y, 16, 16, Vector2.up);
        renderer.sortingOrder = order;
    }

    /// <summary>The little "under renovation" sign hung on a building while Robin upgrades it.</summary>
    private void DrawUpgradeSign(OnlineRanchHome home, int buildingOrder)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        var go = new GameObject("Upgrade Sign");
        go.transform.SetParent(display, false);
        go.transform.position = TileTopLeft(map, home.x, home.y) + new Vector3(0.5f, 0.5f, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(cursors, 367, 309, 16, 15, Vector2.up);
        renderer.sortingOrder = buildingOrder + 1;
    }

    private void DrawAnimalDoor(OnlineRanchHome home, int buildingOrder)
    {
        RanchFootprintView footprint = FarmRanchAssets.Footprint(home.kind);
        Texture2D art = FarmRanchAssets.Art(FarmRanchAssets.Plan(home.kind, home.level)?.name);
        if (footprint == null || art == null || !footprint.HasAnimalDoor) return;
        int tileX = home.x + footprint.animalDoorX, tileY = home.y + footprint.animalDoorY;
        Vector3 topLeft = TileTopLeft(map, tileX, tileY);
        var root = new GameObject("Animal Door").transform;
        root.SetParent(display, false);
        root.position = topLeft;
        bool barn = home.kind == "Barn";
        var panels = new List<(Transform, Vector3)>();
        int top = art.height - 16;

        SpriteRenderer Piece(int x, int width, int height, Vector3 local, int order)
        {
            var go = new GameObject("Door Piece");
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AtlasSprites.FromTop(art, x, top, width, height, Vector2.up);
            renderer.sortingOrder = order;
            return renderer;
        }

        if (barn)
        {
            Piece(32, 32, 16, Vector3.up, buildingOrder - 3);
            Piece(64, 32, 16, Vector3.zero, buildingOrder - 3);
            panels.Add((Piece(0, 32, 12, new Vector3(0f, 0.75f), buildingOrder - 2).transform, new Vector3(0f, 0.75f)));
            panels.Add((Piece(0, 32, 16, Vector3.zero, buildingOrder - 2).transform, Vector3.zero));
        }
        else
        {
            Piece(16, 16, 16, Vector3.zero, buildingOrder - 3);
            panels.Add((Piece(0, 16, 16, Vector3.zero, buildingOrder - 2).transform, Vector3.zero));
        }
        var door = root.gameObject.AddComponent<AnimalDoorView>();
        door.Setup(home, tileX, tileY, panels, barn ? 76f / 64f : 52f / 64f);
        doors.Add(door);
    }

    // ── Room contents ────────────────────────────────────────────────────────────────────────

    private void DrawHay(OnlineRanchHome room)
    {
        RanchFootprintView footprint = FarmRanchAssets.Footprint(room.kind);
        Texture2D objects = Resources.Load<Texture2D>("Maps/springobjects");
        if (footprint == null || room.hay == null || objects == null) return;
        Sprite hay = AtlasSprites.Cell(objects, 178, pivot: Vector2.zero);
        foreach (int trough in room.hay)
        {
            Vector3 position = TileTopLeft(map, footprint.troughX + trough, footprint.troughY) + Vector3.down;
            var go = new GameObject("Hay");
            go.transform.SetParent(display, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = hay;
            renderer.sortingOrder = WorldSortOrder.GetOrder(position.y) + 1;
        }
    }

    private void DrawHopper(OnlineRanchHome room)
    {
        RanchFootprintView footprint = FarmRanchAssets.Footprint(room.kind);
        Texture2D craftables = Resources.Load<NpcCatalog>("NpcCatalog")?.craftablesAtlas;
        if (footprint == null || footprint.hopperX < 0 || craftables == null) return;
        int index = lastSnapshot.piecesOfHay > 0 ? 100 : 99;
        Vector3 position = TileTopLeft(map, footprint.hopperX, footprint.hopperY) + Vector3.down;
        var go = new GameObject("Feed Hopper");
        go.transform.SetParent(display, false);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = AtlasSprites.FromTop(craftables, index % 8 * 16, index / 8 * 32, 16, 32, Vector2.zero, 16f, false);
        renderer.sortingOrder = WorldSortOrder.GetOrder(position.y);
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.offset = new Vector2(0.5f, 0.5f);
    }

    private void DrawProducts(string location)
    {
        Texture2D objects = Resources.Load<Texture2D>("Maps/springobjects");
        if (objects == null || lastSnapshot.ranchProducts == null) return;
        foreach (OnlineRanchProduct product in lastSnapshot.ranchProducts)
        {
            if (product.location != location) continue;
            if (!int.TryParse(ItemQuality.BaseId(product.itemId).Replace("object_", ""), out int index)) continue;
            Vector3 position = TileTopLeft(map, product.x, product.y) + Vector3.down;
            var go = new GameObject("Produce " + product.itemId);
            go.transform.SetParent(display, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AtlasSprites.Cell(objects, index, pivot: Vector2.zero);
            renderer.sortingOrder = WorldSortOrder.GetOrder(position.y + 0.1f);
            products.Add((product, renderer));
        }
    }

    private void SpawnAnimals(OnlineRanchHome home, IEnumerable<OnlineRanchAnimal> list, bool outside)
    {
        RectInt area;
        if (outside)
        {
            RanchFootprintView footprint = FarmRanchAssets.Footprint(home.kind);
            int doorX = home.x + (footprint?.animalDoorX ?? 1);
            area = new RectInt(doorX - 5, home.y + home.height, 11, 7);
        }
        else
        {
            int[] produceArea = FarmRanchAssets.Plan(home.kind, home.level)?.produceArea;
            area = produceArea is { Length: 4 }
                ? new RectInt(produceArea[0], produceArea[1], produceArea[2], produceArea[3])
                : new RectInt(2, 4, 8, 4);
        }
        int seed = 0;
        foreach (OnlineRanchAnimal animal in list)
        {
            RanchOfferView species = FarmRanchAssets.Species(animal.species);
            Texture2D art = FarmRanchAssets.AnimalArt(species?.ArtFor(animal));
            if (species == null || art == null) continue;
            var go = new GameObject("Animal " + animal.name);
            go.transform.SetParent(display, false);
            var actor = go.AddComponent<FarmAnimalActor>();
            actor.Setup(map, animal, species, art, home.kind == "Barn" ? 32 : 16, area,
                animal.id.GetHashCode() + seed++, outside);
            animals.Add(actor);
        }
    }

    // ── Interaction ──────────────────────────────────────────────────────────────────────────

    /// <summary>Action button inside a ranch building or on the farm: produce, hay, animal door, animals.</summary>
    internal static bool TryInteract(Vector2? pointer, Vector2 standing, Vector2 facing, PlayerInventory inventory)
    {
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        var view = location == null ? null : location.GetComponent<FarmRanchView>();
        if (view == null || view.display == null) return false;
        var online = inventory?.GetComponent<OnlineGameState>();
        if (online == null) return false;
        Vector2 target = pointer ?? standing + (facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down);
        Vector2Int tile = NpcPathfinder.WorldToTile(target);
        bool InReach(Vector2 point) => (point - standing).sqrMagnitude <= 1.8f * 1.8f;

        foreach (var (product, renderer) in view.products)
        {
            if (renderer == null || !InReach(renderer.bounds.center)) continue;
            if (!(pointer.HasValue ? renderer.bounds.Contains(new Vector3(target.x, target.y, renderer.bounds.center.z))
                    : new Vector2Int(product.x, product.y) == tile)) continue;
            if (requestPending) return true;
            requestPending = true;
            online.CollectRanchProduct(product.id, error =>
            {
                requestPending = false;
                if (error != null) NpcConversation.ShowNotice(error);
                else GameAudio.Play("pickUpItem");
            });
            return true;
        }

        foreach (AnimalDoorView door in view.doors)
        {
            if (door == null || tile.x != door.TileX || tile.y != door.TileY) continue;
            if (!InReach(view.map.TileCenter(door.TileX, door.TileY))) continue;
            if (requestPending) return true;
            requestPending = true;
            bool opening = !door.Home.animalDoorOpen;
            online.ToggleAnimalDoor(door.Home.id, error =>
            {
                requestPending = false;
                if (error != null) NpcConversation.ShowNotice(error);
                else GameAudio.Play(opening ? "doorCreak" : "doorCreakReverse");
            });
            return true;
        }

        string roomId = FarmRanchAssets.BuildingId(location.LocationName);
        OnlineRanchHome room = roomId == null ? null : Array.Find(knownHomes, home => home.id == roomId);
        RanchFootprintView footprint = room == null ? null : FarmRanchAssets.Footprint(room.kind);

        if (footprint != null && footprint.hopperX >= 0 && tile.x == footprint.hopperX &&
            (tile.y == footprint.hopperY || tile.y == footprint.hopperY - 1) &&
            InReach(view.map.TileCenter(footprint.hopperX, footprint.hopperY)))
        {
            if (requestPending) return true;
            requestPending = true;
            online.TakeHayFromHopper(room.id, error =>
            {
                requestPending = false;
                if (error != null) NpcConversation.ShowNotice(error);
                else GameAudio.Play("shwip");
            });
            return true;
        }

        if (location.LocationName == "Farm")
        {
            OnlineRanchHome silo = Array.Find(knownHomes, home => home.IsSilo && home.level >= 1 && home.Contains(tile.x, tile.y));
            if (silo != null && InReach(view.map.TileCenter(tile.x, tile.y)))
            {
                NpcConversation.ShowNotice($"Cỏ khô trong Silo: {lastSnapshot.piecesOfHay}/{lastSnapshot.hayCapacity}");
                return true;
            }
        }
        if (footprint != null && tile.y == footprint.troughY && tile.x >= footprint.troughX &&
            tile.x < footprint.troughX + room.capacity && InReach(view.map.TileCenter(tile.x, tile.y)) &&
            inventory.SelectedItem?.itemData?.itemId == HayId)
        {
            int trough = tile.x - footprint.troughX;
            if (room.hay != null && Array.IndexOf(room.hay, trough) >= 0) return true;
            if (requestPending) return true;
            requestPending = true;
            online.PutHayInTrough(room.id, trough, error =>
            {
                requestPending = false;
                if (error != null) NpcConversation.ShowNotice(error);
                else GameAudio.Play("coin");
            });
            return true;
        }

        FarmAnimalActor best = null;
        float bestDistance = float.MaxValue;
        foreach (FarmAnimalActor actor in view.animals)
        {
            if (actor == null) continue;
            Vector2 center = actor.Bounds.center;
            float distance = (center - standing).sqrMagnitude;
            if (distance > 2f * 2f) continue;
            bool aimed = pointer.HasValue ? actor.Bounds.Contains(new Vector3(target.x, target.y, actor.Bounds.center.z))
                : Vector2.Dot((center - standing).normalized, facing) >= 0.3f;
            if (!aimed || distance >= bestDistance) continue;
            best = actor;
            bestDistance = distance;
        }
        if (best == null) return false;
        UseOnAnimal(best, inventory, online);
        return true;
    }

    private static void UseOnAnimal(FarmAnimalActor actor, PlayerInventory inventory, OnlineGameState online)
    {
        OnlineRanchAnimal animal = actor.Animal;
        string held = inventory.SelectedItem?.itemData?.itemId;
        string tool = actor.Species.harvest switch { "pail" => "tool_MilkPail", "shears" => "tool_Shears", _ => null };
        if (requestPending) return;
        if (tool != null && held == tool)
        {
            if (string.IsNullOrEmpty(animal.produce) || animal.age < actor.Species.mature)
            {
                NpcConversation.ShowNotice($"{animal.name} chưa có gì để thu hoạch.");
                return;
            }
            requestPending = true;
            online.HarvestRanchAnimal(animal.id, error =>
            {
                requestPending = false;
                if (error != null) { NpcConversation.ShowNotice(error); return; }
                GameAudio.Play(tool == "tool_MilkPail" ? "Milking" : "scissors");
                actor.ShowHeart();
            });
            return;
        }
        if (!animal.petted)
        {
            requestPending = true;
            online.PetRanchAnimal(animal.id, error =>
            {
                requestPending = false;
                if (error != null) { NpcConversation.ShowNotice(error); return; }
                actor.PlaySound();
                actor.ShowHeart();
            });
            return;
        }
        NpcConversation.ShowNotice(Describe(animal, actor.Species));
    }

    private static string Describe(OnlineRanchAnimal animal, RanchOfferView species)
    {
        int hearts = Mathf.Clamp(animal.friendship / 200, 0, 5);
        string stage = animal.age < species.mature
            ? $"Con non, còn {species.mature - animal.age} ngày nữa trưởng thành"
            : "Trưởng thành";
        string mood = animal.hungry ? "đang đói" : animal.happiness >= 200 ? "rất vui" :
            animal.happiness >= 100 ? "khá vui" : animal.happiness >= 30 ? "hơi buồn" : "buồn bã";
        return $"{animal.name} ({species.name}) – {animal.age} ngày tuổi. {stage}. " +
               $"Tình cảm: {new string('♥', hearts)}{new string('♡', 5 - hearts)}. Tâm trạng: {mood}.";
    }

    internal bool TryInspect(Vector2? pointer, Vector2 standing, Vector2 facing) => false;

    // ── Grazing and truffles ─────────────────────────────────────────────────────────────────

    private static OnlineGameState LocalOnline =>
        LocationManager.Instance?.LocalFarmer != null ? LocationManager.Instance.LocalFarmer.GetComponent<OnlineGameState>() : null;

    /// <summary>An animal outside eats the grass tuft at the vegetation tile; the tuft disappears.</summary>
    internal static void RequestGraze(FarmAnimalActor actor, Vector2Int vegetationTile, Action<bool> done)
    {
        OnlineGameState online = LocalOnline;
        if (online == null) { done(false); return; }
        online.GrazeAnimal(actor.Animal.id, vegetationTile.x, vegetationTile.y, error =>
        {
            if (error == null)
            {
                GameLocation farm = LocationManager.Instance?.CurrentLocation;
                if (farm != null && farm.LocationName == "Farm")
                    FarmResourceSpawner.Instance?.RestoreVegetation(farm, online.Snapshot);
            }
            done(error == null);
        });
    }

    internal static void RequestDig(FarmAnimalActor actor, Vector2Int tile, Action<bool> done)
    {
        OnlineGameState online = LocalOnline;
        if (online == null) { done(false); return; }
        online.DigTruffle(actor.Animal.id, tile.x, tile.y, error => done(error == null));
    }
}
