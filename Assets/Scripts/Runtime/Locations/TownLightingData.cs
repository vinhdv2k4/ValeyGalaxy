using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class TownLightingData : ScriptableObject
{
    [Serializable]
    internal struct Lamp
    {
        public int x, y, tilePair;
    }

    [Serializable]
    private struct TilePair
    {
        public string layer;
        public Tile day, night;
    }

    [SerializeField] private Lamp[] lamps = Array.Empty<Lamp>();
    [SerializeField] private TilePair[] tilePairs = Array.Empty<TilePair>();

    internal IReadOnlyList<Lamp> Lamps => lamps;

    internal void ApplyTiles(GameLocation town, bool night)
    {
        foreach (Lamp lamp in lamps)
        {
            TilePair pair = tilePairs[lamp.tilePair];
            if (pair.day == null || pair.night == null) continue;
            var layer = town.transform.Find(pair.layer)?.GetComponent<Tilemap>();
            var cell = new Vector3Int(lamp.x, -lamp.y, 0);
            if (layer == null || layer.GetTile(cell) is not Tile current) continue;

            Tile desired = night ? pair.night : pair.day;
            // A map update can replace the building tile at the same coordinates.
            if (current != desired && (current.sprite == pair.day.sprite || current.sprite == pair.night.sprite))
                layer.SetTile(cell, desired);
        }
    }
}
