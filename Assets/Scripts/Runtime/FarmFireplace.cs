using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A fireplace furniture piece. It starts unlit; using it toggles two animated flames, an orange
/// glow and the crackling fire loop.
/// </summary>
internal sealed class FarmFireplace : MonoBehaviour
{
    // Orange light (the lightmap colour 0,80,160 inverted), radius 2.5 x the 4-tile sconce mask.
    private static readonly Color FireGlow = new(1f, 175f / 255f, 95f / 255f, 1f);
    private const float GlowRadius = 10f;

    private static readonly List<FarmFireplace> Instances = new();
    private Transform space;           // house root: local (x, -y) = map pixels / 64
    private int tileX, tileY, width;
    private SpriteRenderer[] flames;
    private Sprite[] frames;
    private Light2D glow;
    private bool lit;

    /// <param name="space">Transform whose local units are map tiles with y flipped.</param>
    internal void Setup(Transform space, int tileX, int tileY, int width, int sortingOrder)
    {
        this.space = space;
        this.tileX = tileX;
        this.tileY = tileY;
        this.width = width;
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = AtlasSprites.FromTop(cursors, 276 + i * 12, 1985, 12, 11, Vector2.up);
        }
        // Two flames 12 px apart, 0.5 tile above the footprint's top edge.
        flames = new[] { Flame(tileX + 0.8125f), Flame(tileX + 0.4375f) };
        glow = WorldLightingController.CreateFurnitureLight(space, "Fireplace Glow", FireGlow, GlowRadius);
        glow.transform.localPosition = new Vector3(tileX + 0.5f, -(tileY - 1f), 0f);
        SetLit(false, false);

        SpriteRenderer Flame(float x)
        {
            var go = new GameObject("Fireplace Flame");
            go.transform.SetParent(space, false);
            go.transform.localPosition = new Vector3(x, -(tileY - 0.5f), 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = sortingOrder + 1;
            return renderer;
        }
    }

    private void OnEnable() => Instances.Add(this);
    private void OnDisable() => Instances.Remove(this);

    /// <summary>Action button on the fireplace in the current location.</summary>
    internal static bool TryInteract(Vector2? pointer, Vector3 standing, Vector2 facing)
    {
        foreach (FarmFireplace fireplace in Instances)
        {
            if (fireplace == null || fireplace.space == null || !fireplace.isActiveAndEnabled) continue;
            Vector3 target = pointer ?? standing + (Vector3)(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down);
            Vector3 local = fireplace.space.InverseTransformPoint(target);
            int x = Mathf.FloorToInt(local.x), y = Mathf.FloorToInt(-local.y);
            if (y != fireplace.tileY || x < fireplace.tileX || x >= fireplace.tileX + fireplace.width) continue;
            Vector3 center = fireplace.space.TransformPoint(new Vector3(x + 0.5f, -(y + 0.5f), 0f));
            if (((Vector2)(center - standing)).sqrMagnitude > 1.8f * 1.8f) continue;
            fireplace.SetLit(!fireplace.lit, true);
            return true;
        }
        return false;
    }

    private void SetLit(bool on, bool playSound)
    {
        lit = on;
        if (playSound) GameAudio.Play("fireball");
        if (flames != null)
            foreach (SpriteRenderer flame in flames) flame.enabled = on;
        if (glow != null) glow.enabled = on;
        if (!on && playSound) GameAudio.PlayAmbience(null);
    }

    private void Update()
    {
        if (!lit || flames == null) return;
        // Each flame runs four 100 ms frames, offset by its position.
        double ms = Time.time * 1000.0;
        flames[0].sprite = frames[(int)((ms + tileX * 3047 + tileY * 88) % 400 / 100)];
        flames[1].sprite = frames[(int)((ms + tileX * 2047 + tileY * 98) % 400 / 100)];
        if (glow != null) glow.intensity = WorldLightingController.AmbientDarkness * 0.9f;
        GameAudio.PlayAmbience("cracklingFire");
    }
}
