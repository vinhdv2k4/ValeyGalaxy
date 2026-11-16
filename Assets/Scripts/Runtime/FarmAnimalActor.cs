using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A farm animal wandering inside its building or around it on the farm. The sheet holds four
/// walking frames per row: down, right, up, left.
/// </summary>
internal sealed class FarmAnimalActor : MonoBehaviour
{
    private const float WalkSpeed = 1.1f;

    internal OnlineRanchAnimal Animal { get; private set; }
    internal RanchOfferView Species { get; private set; }
    internal Bounds Bounds => body != null ? body.bounds : new Bounds(transform.position, Vector3.one);

    private GameLocation map;
    private Texture2D sheet;
    private int frameSize;
    private RectInt area;
    private bool outside;
    private SpriteRenderer body, heart;
    private System.Random random;
    private Vector3 target;
    private int row;
    private float walkTime, idleUntil, heartUntil, eatUntil, nextGrassSearch, nextDig;
    private bool walking, grazed, requestPending;
    private int dug;
    private FarmGrass grassTarget;
    private static FarmGrass[] grassCache = System.Array.Empty<FarmGrass>();
    private static float grassCacheTime = -10f;

    internal void Setup(GameLocation location, OnlineRanchAnimal animal, RanchOfferView species, Texture2D art,
        int size, RectInt roamArea, int seed, bool grazing)
    {
        map = location;
        Animal = animal;
        Species = species;
        sheet = art;
        frameSize = size;
        area = roamArea;
        outside = grazing;
        random = new System.Random(seed);
        grazed = animal.grazed;
        dug = animal.dug;
        nextGrassSearch = Time.time + 2f + (float)random.NextDouble() * 6f;
        nextDig = Time.time + 15f + (float)random.NextDouble() * 30f;
        body = gameObject.AddComponent<SpriteRenderer>();
        Vector2Int start = RandomTile(20) ?? new Vector2Int(area.x + area.width / 2, area.y + area.height / 2);
        transform.position = Standing(start);
        target = transform.position;
        row = 0;
        idleUntil = Time.time + (float)random.NextDouble() * 2f;
        ApplyFrame(0);
    }

    private Vector3 Standing(Vector2Int tile) => map.TileCenter(tile.x, tile.y) + new Vector3(0f, -0.4f, 0f);

    private Vector2Int? RandomTile(int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            var tile = new Vector2Int(area.x + random.Next(Mathf.Max(1, area.width)),
                area.y + random.Next(Mathf.Max(1, area.height)));
            if (Walkable(tile)) return tile;
        }
        return null;
    }

    private bool Walkable(Vector2Int tile)
    {
        var cell = new Vector3Int(tile.x, -tile.y, 0);
        Tilemap Layer(string name) => map.transform.Find(name)?.GetComponent<Tilemap>();
        Tilemap back = Layer("Back");
        if (back == null || !back.HasTile(cell)) return false;
        if (Layer("MapCollision")?.HasTile(cell) == true || Layer("Buildings")?.HasTile(cell) == true) return false;
        if (!outside) return true;
        if (FarmRanchView.ReservesCell(tile.x, tile.y)) return false;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(map.TileCenter(tile.x, tile.y), new Vector2(0.8f, 0.8f), 0f))
            if (!hit.isTrigger && hit is not TilemapCollider2D && hit is not CompositeCollider2D &&
                hit.GetComponentInParent<CharacterMotor>() == null)
                return false;
        return true;
    }

    private void Update()
    {
        if (body == null) return;
        if (heart != null)
        {
            heart.enabled = Time.time < heartUntil;
            heart.transform.localPosition = new Vector3(0f, frameSize / 16f + 0.2f + Mathf.Sin(Time.time * 6f) * 0.05f, 0f);
        }
        if (outside && UpdateFieldWork()) return;
        if (!walking)
        {
            if (Time.time < idleUntil) { ApplyFrame(0); return; }
            Vector2Int here = NpcPathfinder.WorldToTile(transform.position + Vector3.up * 0.4f);
            Vector2Int step = random.Next(4) switch { 0 => Vector2Int.up, 1 => Vector2Int.right, 2 => Vector2Int.down, _ => Vector2Int.left };
            int distance = 1 + random.Next(3);
            Vector2Int goal = here;
            for (int i = 0; i < distance; i++)
            {
                Vector2Int next = goal + step;
                if (!area.Contains(next) || !Walkable(next)) break;
                goal = next;
            }
            row = step == Vector2Int.down ? 2 : step == Vector2Int.right ? 1 : step == Vector2Int.up ? 0 : 3;
            if (goal == here) { idleUntil = Time.time + 0.5f + (float)random.NextDouble() * 2f; return; }
            target = Standing(goal);
            walking = true;
        }
        transform.position = Vector3.MoveTowards(transform.position, target, WalkSpeed * Time.deltaTime);
        walkTime += Time.deltaTime;
        ApplyFrame(Mathf.FloorToInt(walkTime * 6f) % 4);
        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            walking = false;
            idleUntil = Time.time + 1f + (float)random.NextDouble() * 4f;
        }
    }

    private static FarmGrass[] Grass()
    {
        if (Time.time - grassCacheTime > 3f)
        {
            grassCache = FindObjectsByType<FarmGrass>(FindObjectsSortMode.None);
            grassCacheTime = Time.time;
        }
        return grassCache;
    }

    /// <summary>Walks to a nearby grass tuft and eats it; grown pigs sometimes root up truffles.</summary>
    private bool UpdateFieldWork()
    {
        if (requestPending) { ApplyFrame(0); return true; }
        if (Time.time < eatUntil)
        {
            row = 0;
            ApplyFrame(Mathf.FloorToInt(Time.time * 4f) % 2 == 0 ? 0 : 2);
            return true;
        }
        bool adult = Animal.age >= Species.mature;
        if (Species.harvest == "dig" && adult && dug < Mathf.Min(5, 1 + Animal.friendship / 250) &&
            !walking && Time.time >= nextDig)
        {
            nextDig = Time.time + 25f + (float)random.NextDouble() * 50f;
            Vector2Int tile = NpcPathfinder.WorldToTile(transform.position + Vector3.up * 0.4f);
            requestPending = true;
            eatUntil = Time.time + 1.2f;
            FarmRanchView.RequestDig(this, tile, ok =>
            {
                requestPending = false;
                if (ok) { dug++; GameAudio.Play("dirtyHit"); }
            });
            return true;
        }
        if (grazed) return false;
        if (grassTarget == null && Time.time >= nextGrassSearch)
        {
            nextGrassSearch = Time.time + 4f + (float)random.NextDouble() * 4f;
            float best = 7f * 7f;
            foreach (FarmGrass grass in Grass())
            {
                if (grass == null || !grass.isActiveAndEnabled) continue;
                float distance = ((Vector2)(grass.transform.position - transform.position)).sqrMagnitude;
                if (distance < best) { best = distance; grassTarget = grass; }
            }
        }
        if (grassTarget == null) return false;
        if (!grassTarget.isActiveAndEnabled) { grassTarget = null; return false; }
        Vector3 goal = grassTarget.transform.position + new Vector3(0f, -0.4f, 0f);
        Vector3 delta = goal - transform.position;
        if (delta.sqrMagnitude > 0.04f)
        {
            row = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? 1 : 3) : (delta.y > 0 ? 2 : 0);
            transform.position = Vector3.MoveTowards(transform.position, goal, WalkSpeed * Time.deltaTime);
            walkTime += Time.deltaTime;
            ApplyFrame(Mathf.FloorToInt(walkTime * 6f) % 4);
            return true;
        }
        Vector3 tuft = grassTarget.transform.position;
        var vegetationTile = new Vector2Int(Mathf.FloorToInt(tuft.x), Mathf.FloorToInt(-tuft.y));
        grassTarget = null;
        requestPending = true;
        eatUntil = Time.time + 2f;
        FarmRanchView.RequestGraze(this, vegetationTile, ok =>
        {
            requestPending = false;
            if (ok) grazed = true;
        });
        return true;
    }

    private void ApplyFrame(int frame)
    {
        int columns = Mathf.Max(1, sheet.width / frameSize);
        body.sprite = AtlasSprites.FromTop(sheet, frame % columns * frameSize, row * frameSize, frameSize, frameSize,
            new Vector2(0.5f, 0f), 16f, false);
        body.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
    }

    /// <summary>Petting or harvesting: a heart floats above the animal for a moment.</summary>
    internal void ShowHeart()
    {
        if (heart == null)
        {
            var emotes = Resources.Load<MilestoneSprites>("Cutscenes/MilestoneSprites")?.emotes;
            if (emotes == null) return;
            var go = new GameObject("Heart");
            go.transform.SetParent(transform, false);
            heart = go.AddComponent<SpriteRenderer>();
            heart.sprite = AtlasSprites.Cell(emotes, 20, pivot: new Vector2(0.5f, 0f));
        }
        heart.sortingOrder = body.sortingOrder + 5;
        heartUntil = Time.time + 1.5f;
        walking = false;
        idleUntil = Time.time + 1.5f;
    }

    internal void PlaySound()
    {
        GameAudio.Play(Species.id switch
        {
            "chicken" => "cluck", "duck" => "Duck", "rabbit" => "rabbit", "cow" => "cow",
            "goat" => "goat", "sheep" => "sheep", "pig" => "pig", _ => "cluck"
        });
    }
}
