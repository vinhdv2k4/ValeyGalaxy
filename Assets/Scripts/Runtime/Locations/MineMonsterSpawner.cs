using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Spawns configured monster prefabs over clear floor tiles in a mine level.
/// Loads the built-in Resources/Monsters prefabs by MineMonsterKind, with an
/// Inspector override available for custom variants.
/// </summary>
public sealed class MineMonsterSpawner : MonoBehaviour
{
    public enum MineMonsterKind
    {
        GreenSlime, RockCrab, Bug, Grub, RockGolem, Bat, FrostBat, DustSpirit,
        Skeleton, Ghost, RedSlime, LavaCrab, MetalHead, ShadowBrute,
        ShadowShaman, SquidKid, Fly
    }

    [Serializable]
    private struct MonsterPrefab
    {
        public MineMonsterKind kind;
        public GameObject prefab;
    }

    [Header("Monster prefabs")]
    [SerializeField] private MonsterPrefab[] monsterPrefabs = Array.Empty<MonsterPrefab>();
    [SerializeField] private Transform monsterParent;

    [Header("Floor population")]
    [SerializeField, Range(0f, 1f)] private float infestedFloorChance = 0.022f;
    [SerializeField] private bool forceInfestedFloor;
    [SerializeField] private int randomSeed = -1;

    [Header("Flying monster event")]
    [SerializeField, Min(1f)] private float eventCheckSeconds = 120f;
    [SerializeField, Range(0f, 1f)] private float flyingEventChance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float fogEventChance = 0.11f;
    [SerializeField, Min(1f)] private float fogDurationSeconds = 35f;
    [SerializeField, Min(0.1f)] private float fogSpawnInterval = 4f;
    [SerializeField, Min(0f)] private float flySpeed = 4f;
    [SerializeField, Min(0.1f)] private float cameraEdgePadding = 1.5f;

    private static readonly Vector3Int[] Neighbors =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

    private readonly Dictionary<MineMonsterKind, GameObject> prefabCache = new();
    private readonly HashSet<MineMonsterKind> missingPrefabWarnings = new();
    private readonly HashSet<Vector3Int> occupiedCells = new();
    private readonly HashSet<Vector3Int> rockCells = new();
    private readonly HashSet<GameObject> spawnedMonsters = new();
    private readonly Collider2D[] movementHits = new Collider2D[16];

    private Tilemap back;
    private Tilemap buildings;
    private Tilemap front;
    private Tilemap collision;
    private GameLocation location;
    private System.Random random;
    private Coroutine flyingEventRoutine;
    private int mineLevel;
    private Vector3 startLadderPos;
    private bool prepared;
    private bool populated;
    private bool infestedFloor;
    private bool ghostSpawned;
    private bool fogActive;

    public int MineLevel => mineLevel;
    public bool IsInfestedFloor => infestedFloor;
    public bool IsFogActive => fogActive;
    public IReadOnlyCollection<GameObject> SpawnedMonsters => spawnedMonsters;
    public event Action<bool> FogStateChanged;

    public static int DamageMonstersInFront(CharacterMotor attacker, ItemData weapon,
        NetworkCharacterStats stats)
    {
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        if (attacker == null || location == null || !IsMineLocation(location)) return 0;

        Vector2 facing = attacker.Facing.sqrMagnitude > 0.01f ? attacker.Facing.normalized : Vector2.down;
        Vector2 origin = (Vector2)attacker.StandingPosition + facing * 0.45f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, 0.85f);
        var struck = new HashSet<MineMonsterInstance>();
        foreach (Collider2D hit in hits)
        {
            MineMonsterInstance monster = hit != null ? hit.GetComponentInParent<MineMonsterInstance>() : null;
            if (monster == null || !monster.isActiveAndEnabled ||
                !monster.transform.IsChildOf(location.transform) || struck.Contains(monster)) continue;

            Vector2 delta = (Vector2)monster.transform.position - (Vector2)attacker.StandingPosition;
            if (delta.magnitude > 1.4f || Vector2.Dot(facing, delta.normalized) < 0.15f) continue;
            struck.Add(monster);
            int baseDamage = weapon is MeleeWeaponData melee
                ? UnityEngine.Random.Range(melee.minDamage, melee.maxDamage + 1)
                : 5;
            monster.TakeDamage(Mathf.Max(1, baseDamage + (stats != null ? stats.AttackBonus : 0)), facing);
        }
        return struck.Count;
    }

    private static bool IsMineLocation(GameLocation location) =>
        location != null && (MineManager.IsMineFloor(location.LocationName) ||
            string.Equals(location.LocationName, "Mine", System.StringComparison.OrdinalIgnoreCase));

    /// <summary>Initialize deterministic per-level state before resource population.</summary>
    public void PrepareFloor(int level, Vector3 ladderPosition)
    {
        ResolveReferences();
        if (prepared && mineLevel == level)
        {
            startLadderPos = ladderPosition;
            return;
        }

        mineLevel = level;
        startLadderPos = ladderPosition;
        int day = FarmDayClock.Instance != null ? FarmDayClock.Instance.Day : 1;
        int seed = randomSeed >= 0 ? randomSeed : unchecked(level * 73856093 ^ day * 19349663);
        random = new System.Random(seed);
        infestedFloor = forceInfestedFloor || (level > 1 && level % 10 != 0 && random.NextDouble() < infestedFloorChance);
        prepared = true;
    }

    /// <summary>Call after the player has arrived at startLadderPos.</summary>
    public void PopulateFloor()
    {
        if (populated || !prepared || !IsMineLocation(location)) return;
        ResolveReferences();
        if (back == null)
        {
            Debug.LogWarning($"[MineMonsterSpawner] {name}: missing Back Tilemap; cannot populate mine floor.", this);
            return;
        }

        populated = true;
        occupiedCells.Clear();
        rockCells.Clear();
        CacheExistingRocks();

        // Monster spawn chance is zero on floor 1 and elevator/chest floors.
        float monsterChance = mineLevel == 1 || mineLevel % 10 == 0
            ? 0f
            : infestedFloor ? 0.025f : 0.002f + random.Next(200) / 10000f;
        if (monsterChance <= 0f) return;

        foreach (Vector3Int cell in back.cellBounds.allPositionsWithin)
        {
            if (!IsEligibleCell(cell, startLadderPos)) continue;
            if (random.NextDouble() >= monsterChance) continue;

            MineMonsterKind kind = ChooseMonster(cell);
            if (!TrySpawn(kind, cell)) continue;

            if (kind == MineMonsterKind.Grub) TrySpawnCluster(kind, cell, 0.4);
            else if (kind == MineMonsterKind.DustSpirit) TrySpawnCluster(kind, cell, 0.6);
        }

        if (flyingEventRoutine == null)
            flyingEventRoutine = StartCoroutine(FlyingMonsterEvents());
    }

    private MineMonsterKind ChooseMonster(Vector3Int cell)
    {
        float distance = Vector3.Distance(back.GetCellCenterWorld(cell), startLadderPos);

        if (mineLevel <= 29)
        {
            if (!infestedFloor && random.NextDouble() < 0.25) return MineMonsterKind.Bug;
            if (random.NextDouble() < 0.15) return MineMonsterKind.RockCrab;
            if (mineLevel >= 15 && random.NextDouble() >= 0.45) return MineMonsterKind.Grub;
            return MineMonsterKind.GreenSlime;
        }

        if (mineLevel <= 39)
            return distance > 10f && random.NextDouble() < 0.1 ? MineMonsterKind.Bat : MineMonsterKind.RockGolem;

        if (mineLevel <= 79)
        {
            // Above floor 70 the original level selector gives skeletons priority.
            if (mineLevel >= 70 && random.NextDouble() < 0.75) return MineMonsterKind.Skeleton;
            if (random.NextDouble() < 0.3) return MineMonsterKind.DustSpirit;
            if (distance > 10f && random.NextDouble() < 0.3) return MineMonsterKind.FrostBat;
            if (!ghostSpawned && mineLevel >= 50 && distance > 10f && random.NextDouble() < 0.3)
            {
                ghostSpawned = true;
                return MineMonsterKind.Ghost;
            }
            return mineLevel >= 70 ? MineMonsterKind.Skeleton : MineMonsterKind.GreenSlime;
        }

        // Keep the selector's ordered checks: listed percentages are
        // sequential chances, not mutually exclusive percentages summing to 100.
        if (random.NextDouble() < 0.15) return MineMonsterKind.RedSlime;
        if (random.NextDouble() < 0.15) return MineMonsterKind.MetalHead;
        if (random.NextDouble() < 0.25) return MineMonsterKind.ShadowBrute;
        if (random.NextDouble() < 0.25) return MineMonsterKind.ShadowShaman;
        if (random.NextDouble() < 0.25) return MineMonsterKind.LavaCrab;
        if (mineLevel >= 90 && distance > 8f && random.NextDouble() < 0.2)
            return MineMonsterKind.SquidKid;
        return MineMonsterKind.RedSlime;
    }

    private void TrySpawnCluster(MineMonsterKind kind, Vector3Int center, double chancePerNeighbor)
    {
        foreach (Vector3Int direction in Neighbors)
        {
            if (random.NextDouble() >= chancePerNeighbor) continue;
            Vector3Int neighbor = center + direction;
            TrySpawn(kind, neighbor);
        }
    }

    private bool IsEligibleCell(Vector3Int cell, Vector3 referencePosition)
    {
        if (!back.HasTile(cell) || occupiedCells.Contains(cell) || rockCells.Contains(cell)) return false;
        if (buildings != null && buildings.HasTile(cell)) return false;
        if (front != null && front.HasTile(cell)) return false;
        if (collision != null && collision.HasTile(cell)) return false;
        if (Vector3.Distance(back.GetCellCenterWorld(cell), referencePosition) <= 5f) return false;

        // Existing colliders/characters can be placed on maps without a tile collision layer.
        Vector2 center = back.GetCellCenterWorld(cell);
        return Physics2D.OverlapCircle(center, 0.25f) == null;
    }

    internal bool CanMonsterOccupy(Vector2 position, Vector2 footprint)
    {
        Vector3Int cell = back.WorldToCell(position);
        if (!back.HasTile(cell) || (collision != null && collision.HasTile(cell))) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        int count = Physics2D.OverlapBox(position, footprint, 0f, filter, movementHits);
        if (count == movementHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (movementHits[i].transform.IsChildOf(transform)) return false;
        return true;
    }

    private bool TrySpawn(MineMonsterKind kind, Vector3Int cell)
    {
        if (!IsEligibleCell(cell, startLadderPos)) return false;
        GameObject prefab = GetPrefab(kind);
        if (prefab == null) return false;

        Vector3 position = back.GetCellCenterWorld(cell);
        GameObject monster = Instantiate(prefab, position, Quaternion.identity, monsterParent);
        monster.name = $"{kind}_{mineLevel}_{cell.x}_{cell.y}";
        bool hasSpecialItem = random.NextDouble() < 0.00175;
        MineMonsterInstance marker = monster.GetComponent<MineMonsterInstance>();
        if (marker == null) marker = monster.AddComponent<MineMonsterInstance>();
        marker.Initialize(this, mineLevel, kind.ToString(), hasSpecialItem, FindPlayer());

        occupiedCells.Add(cell);
        spawnedMonsters.Add(monster);
        return true;
    }

    private GameObject GetPrefab(MineMonsterKind kind)
    {
        if (prefabCache.TryGetValue(kind, out GameObject cached)) return cached;
        GameObject prefab = null;
        foreach (MonsterPrefab entry in monsterPrefabs)
            if (entry.kind == kind && entry.prefab != null) { prefab = entry.prefab; break; }
        if (prefab == null) prefab = Resources.Load<GameObject>($"Monsters/{kind}");

        prefabCache[kind] = prefab;
        if (prefab == null && missingPrefabWarnings.Add(kind))
            Debug.LogWarning($"[MineMonsterSpawner] No prefab for {kind}. Assign it in the Inspector or add Resources/Monsters/{kind}.prefab.", this);
        return prefab;
    }

    private IEnumerator FlyingMonsterEvents()
    {
        while (isActiveAndEnabled && populated)
        {
            yield return new WaitForSeconds(eventCheckSeconds);
            if (!isActiveAndEnabled || !populated || random.NextDouble() >= flyingEventChance) continue;

            if (mineLevel > 10 && !infestedFloor && random.NextDouble() < fogEventChance)
            {
                SetFogActive(true);
                float until = Time.time + fogDurationSeconds;
                while (Time.time < until && isActiveAndEnabled)
                {
                    SpawnFlyingMonsterFromCameraEdge();
                    yield return new WaitForSeconds(fogSpawnInterval);
                }
                SetFogActive(false);
            }
            else
            {
                SpawnFlyingMonsterFromCameraEdge();
            }
        }
        SetFogActive(false);
        flyingEventRoutine = null;
    }

    private void SetFogActive(bool value)
    {
        if (fogActive == value) return;
        fogActive = value;
        FogStateChanged?.Invoke(value);
    }

    private void SpawnFlyingMonsterFromCameraEdge()
    {
        CharacterMotor player = FindPlayer();
        if (player == null) return;
        Vector3 spawnPosition = GetCameraEdgePosition(player.transform.position);
        Vector3Int virtualCell = back.WorldToCell(spawnPosition);
        // The event deliberately starts outside the level bounds, so it bypasses floor-tile checks.
        GameObject prefab = GetPrefab(MineMonsterKind.Fly);
        if (prefab == null) prefab = GetPrefab(MineMonsterKind.Bat);
        if (prefab == null) return;

        GameObject monster = Instantiate(prefab, spawnPosition, Quaternion.identity, monsterParent);
        monster.name = $"FlyingMonster_{mineLevel}_{virtualCell.x}_{virtualCell.y}";
        MineMonsterInstance marker = monster.GetComponent<MineMonsterInstance>();
        if (marker == null) marker = monster.AddComponent<MineMonsterInstance>();
        marker.Initialize(this, mineLevel, "Fly", random.NextDouble() < 0.00175, player, flySpeed);
        spawnedMonsters.Add(monster);
    }

    private Vector3 GetCameraEdgePosition(Vector3 target)
    {
        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : 5f;
        float halfWidth = camera != null && camera.orthographic
            ? halfHeight * camera.aspect
            : 5f * (camera != null ? camera.aspect : 1.78f);
        int edge = random.Next(4);
        Vector3 position = target;
        switch (edge)
        {
            case 0: position.x -= halfWidth + cameraEdgePadding; break;
            case 1: position.x += halfWidth + cameraEdgePadding; break;
            case 2: position.y -= halfHeight + cameraEdgePadding; break;
            default: position.y += halfHeight + cameraEdgePadding; break;
        }
        position.z = target.z;
        return position;
    }

    private void CacheExistingRocks()
    {
        if (back == null) return;
        foreach (MineRock rock in GetComponentsInChildren<MineRock>(true))
            rockCells.Add(back.WorldToCell(rock.transform.position));
    }

    private void ResolveReferences()
    {
        if (back == null) back = transform.Find("Back")?.GetComponent<Tilemap>();
        if (buildings == null) buildings = transform.Find("Buildings")?.GetComponent<Tilemap>();
        if (front == null) front = transform.Find("Front")?.GetComponent<Tilemap>();
        if (collision == null) collision = transform.Find("MapCollision")?.GetComponent<Tilemap>();
        if (location == null) location = GetComponent<GameLocation>();
        if (monsterParent == null)
        {
            Transform existing = transform.Find("MineMonsters");
            if (existing != null) monsterParent = existing;
            else
            {
                GameObject root = new("MineMonsters");
                root.transform.SetParent(transform, false);
                monsterParent = root.transform;
            }
        }
    }

    private static CharacterMotor FindPlayer() => LocationManager.Instance?.LocalFarmer;

    private void OnDisable()
    {
        if (flyingEventRoutine != null)
        {
            StopCoroutine(flyingEventRoutine);
            flyingEventRoutine = null;
        }
        SetFogActive(false);
    }

    private void OnDestroy()
    {
        foreach (GameObject monster in spawnedMonsters)
            if (monster != null) Destroy(monster);
        spawnedMonsters.Clear();
    }
}

