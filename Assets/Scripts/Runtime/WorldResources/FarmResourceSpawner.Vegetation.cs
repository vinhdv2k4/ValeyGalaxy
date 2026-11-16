using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FarmResourceSpawner
{
    private enum VegetationStage { Uninitialized, Cleared, Stump, Sapling, Mature }

    private sealed class VegetationMarker
    {
        public Vector3 Position;
        public Transform Parent;
        public string Kind;
        public int Seed;
        public FarmTree.TreeType TreeType;
        public Texture2D TreeSheet;
        public VegetationStage Stage;
        public WorldResource Resource;
    }

    private readonly Dictionary<GameLocation, Dictionary<Vector2Int, VegetationMarker>> vegetation = new();

    private bool RegisterVegetation(GameLocation location, Vector3 position, int index, System.Random random)
    {
        string kind = index switch
        {
            9 or 10 or 11 or 12 or 23 or 31 or 32 => "tree",
            13 or 14 or 15 => "weed",
            22 => "grass",
            _ => null
        };
        if (kind == null) return false;
        if (location == null) return true;

        var tile = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(-position.y));
        if (!vegetation.TryGetValue(location, out var markers))
            vegetation.Add(location, markers = new());
        if (markers.ContainsKey(tile)) return true;

        var species = index switch
        {
            9 => FarmTree.TreeType.Maple,
            11 or 12 => FarmTree.TreeType.Pine,
            23 when random.Next(2) == 0 => FarmTree.TreeType.Maple,
            _ => FarmTree.TreeType.Oak
        };
        Texture2D sheet = species switch
        {
            FarmTree.TreeType.Maple => mapleSheet,
            FarmTree.TreeType.Pine => pineSheet != null ? pineSheet : mapleSheet,
            _ => oakSheet != null ? oakSheet : mapleSheet
        };
        markers.Add(tile, new VegetationMarker
        {
            Parent = resourceParent, Position = position, Kind = kind, Seed = random.Next(),
            TreeType = species, TreeSheet = sheet
        });
        return true;
    }

    internal void RestoreVegetation(GameLocation location, OnlineGameSnapshot snapshot, bool cropsLoaded = false)
    {
        if (location == null || !vegetation.TryGetValue(location, out var markers)) return;
        var changes = new Dictionary<Vector2Int, OnlineNaturalResource>();
        foreach (var saved in snapshot?.naturalResources ?? Array.Empty<OnlineNaturalResource>())
            if (saved != null && saved.location == location.LocationName)
                changes[new Vector2Int(saved.tileX, saved.tileY)] = saved;

        bool farm = location.LocationName == "Farm";
        bool physicsSynced = false;
        foreach (var pair in markers)
        {
            var marker = pair.Value;
            if (snapshot == null && marker.Stage != VegetationStage.Uninitialized) continue;
            var change = changes.GetValueOrDefault(pair.Key);
            var stage = VegetationStage.Mature;
            bool regrowing = change != null && change.kind == marker.Kind && !change.stump;
            if (change != null && change.kind == marker.Kind)
            {
                if (change.stump) stage = VegetationStage.Stump;
                else if (snapshot.gameDay < change.regrowDay)
                    stage = change.saplingDay > 0 && snapshot.gameDay >= change.saplingDay
                        ? VegetationStage.Sapling : VegetationStage.Cleared;
            }
            if (stage == marker.Stage) continue;

            if (regrowing && stage != VegetationStage.Cleared)
            {
                bool deferRegrowth = farm && !cropsLoaded;
                if (!deferRegrowth)
                {
                    if (!physicsSynced)
                    {
                        Physics2D.SyncTransforms();
                        physicsSynced = true;
                    }
                    deferRegrowth = IsRegrowthBlocked(marker);
                }
                if (deferRegrowth)
                    stage = marker.Stage == VegetationStage.Sapling ? VegetationStage.Sapling : VegetationStage.Cleared;
            }
            if (stage != marker.Stage) SetVegetationStage(location, pair.Key, marker, stage);
        }
    }

    private static bool IsRegrowthBlocked(VegetationMarker marker)
    {
        foreach (var hit in Physics2D.OverlapBoxAll(marker.Position, new Vector2(.8f, .65f), 0f))
        {
            if (marker.Resource != null && hit.transform.IsChildOf(marker.Resource.transform)) continue;
            if (!hit.isTrigger || hit.GetComponentInParent<CropPlot>()?.IsTilled == true ||
                hit.GetComponentInParent<PlacedPlayerChest>() != null ||
                hit.GetComponentInParent<CharacterMotor>() != null)
                return true;
        }
        return false;
    }

    private void SetVegetationStage(GameLocation location, Vector2Int tile, VegetationMarker marker,
        VegetationStage stage)
    {
        if (marker.Resource != null)
        {
            marker.Resource.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(marker.Resource.gameObject);
            else DestroyImmediate(marker.Resource.gameObject);
        }
        marker.Stage = stage;
        marker.Resource = null;
        if (stage == VegetationStage.Cleared) return;

        var random = new System.Random(marker.Seed);
        marker.Resource = marker.Kind switch
        {
            "grass" => SpawnGrass(marker.Parent, marker.Position, random),
            "weed" => SpawnWeed(marker.Parent, marker.Position, random),
            _ => SpawnTree(marker.Parent, marker.Position, marker.TreeType, marker.TreeSheet, stage)
        };
        marker.Resource?.BindNaturalRemoval(location, (user, stump, done) =>
        {
            void Apply(bool saved)
            {
                if (saved) marker.Stage = stump ? VegetationStage.Stump : VegetationStage.Cleared;
                done(saved);
            }

            var online = user != null ? user.GetComponent<OnlineGameState>() : null;
            if (online == null) Apply(true);
            else online.ClearNaturalResource(location.LocationName, tile.x, tile.y, marker.Kind, stump, Apply);
        });
    }
}
