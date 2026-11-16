using System;
using UnityEngine;

internal sealed class FestivalPortraits : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public string name;
        public Sprite sprite;
    }

    [SerializeField] private Entry[] actors;
    [SerializeField] private Texture2D festivalTiles;
    internal Texture2D FestivalTiles => festivalTiles;

    internal Sprite Find(string name)
    {
        foreach (var actor in actors)
            if (actor.name == name) return actor.sprite;
        return null;
    }
}
