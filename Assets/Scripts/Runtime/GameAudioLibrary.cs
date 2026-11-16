using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio Cue Library")]
public sealed class GameAudioLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string cue;
        public AudioClip clip;
    }

    public List<Entry> entries = new();
}
