using System.Collections.Generic;
using UnityEngine;

/// <summary>An animal door on the farm that slides open and closed.</summary>
internal sealed class AnimalDoorView : MonoBehaviour
{
    internal OnlineRanchHome Home { get; private set; }
    internal int TileX { get; private set; }
    internal int TileY { get; private set; }
    private static readonly Dictionary<string, float> LastOpenness = new();
    private List<(Transform Panel, Vector3 Closed)> panels;
    private float travel, openness;

    internal void Setup(OnlineRanchHome home, int tileX, int tileY, List<(Transform, Vector3)> doorPanels, float distance)
    {
        Home = home;
        TileX = tileX;
        TileY = tileY;
        panels = doorPanels;
        travel = distance;
        openness = LastOpenness.TryGetValue(home.id, out float previous) ? previous : home.animalDoorOpen ? 1f : 0f;
        Apply();
    }

    private void Update()
    {
        float target = Home.animalDoorOpen ? 1f : 0f;
        if (Mathf.Approximately(openness, target)) return;
        openness = Mathf.MoveTowards(openness, target, Time.deltaTime * 2.5f);
        LastOpenness[Home.id] = openness;
        Apply();
    }

    private void Apply()
    {
        foreach (var (panel, closed) in panels)
            if (panel != null) panel.localPosition = closed + Vector3.up * (travel * openness);
    }
}
