using System;
using UnityEngine;

internal sealed class MilestoneSprites : ScriptableObject
{
    [Serializable] private struct Actor
    {
        public string id;
        public Sprite sprite;
        public Sprite portrait;
    }

    [SerializeField] private Actor[] actors;
    [SerializeField] internal Texture2D emotes;
    [SerializeField] internal Texture2D cursors;
    internal Sprite SpriteFor(string id)
    {
        foreach (var actor in actors)
            if (actor.id == id) return NpcSprites.AtWorldScale(actor.sprite);
        return null;
    }
    internal Sprite PortraitFor(string id)
    {
        foreach (var actor in actors)
            if (actor.id == id) return actor.portrait;
        return null;
    }
}
