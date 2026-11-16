using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground effects left by footsteps: two khaki dust puffs on sand and a fading print on snow.
/// Renderers are pooled; one shared instance lives in the scene.
/// </summary>
internal sealed class FootstepEffects : MonoBehaviour
{
    private const float TileSize = 64f;               // screen pixels per tile in the source art
    private const float DustFrameSeconds = 0.08f;     // 8 frames of 80 ms
    private const float DustFadePerSecond = 0.03f * 60f;
    private const float SnowFadePerSecond = 0.01f * 60f;
    private static readonly Color Khaki = new(240 / 255f, 230 / 255f, 140 / 255f, 0.45f);

    private static FootstepEffects instance;
    private readonly List<Effect> live = new();
    private readonly Stack<SpriteRenderer> pool = new();
    private Sprite[] dustFrames;
    private Sprite snowPrint;

    private sealed class Effect
    {
        public SpriteRenderer Renderer;
        public bool Dust;
        public float Delay, Age, Alpha = 1f;
    }

    internal static void Spawn(string cue, Vector3 feet, Vector2 facing)
    {
        if (cue != "sandyStep" && cue != "snowyStep") return;
        if (instance == null) instance = new GameObject("Footstep Effects").AddComponent<FootstepEffects>();
        if (cue == "sandyStep") instance.SpawnDust(feet);
        else instance.SpawnSnowPrint(feet, facing);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Two puffs just behind the farmer's feet, the second smaller and 20 ms later.
    private void SpawnDust(Vector3 feet)
    {
        if (!EnsureDust()) return;
        Add(feet + Pixels(8 + Random.Range(-8, 8), 2), 0.75f + Random.Range(-3, 4) * 0.05f, 0f);
        Add(feet + Pixels(2 + Random.Range(-4, 4), 8), 0.55f + Random.Range(-3, 4) * 0.05f, 0.02f);

        void Add(Vector3 position, float scale, float delay)
        {
            SpriteRenderer renderer = Take(dustFrames[0], position, scale);
            renderer.color = Khaki;
            renderer.sortingOrder = WorldSortOrder.GetOrder(feet.y, 0.05f, -1);
            renderer.enabled = delay <= 0f;
            live.Add(new Effect { Renderer = renderer, Dust = true, Delay = delay });
        }
    }

    // A 6x6 print, scaled 3-4x, turned 45 degrees when walking sideways.
    private void SpawnSnowPrint(Vector3 feet, Vector2 facing)
    {
        if (snowPrint == null)
        {
            Texture2D cursors = StardewUiAtlas.Cursors;
            if (cursors == null) return;
            snowPrint = Sprite.Create(cursors, new Rect(247, cursors.height - 413, 6, 6),
                new Vector2(0.5f, 0.5f), TileSize, 0, SpriteMeshType.FullRect);
        }
        Vector3 position = feet + Pixels(2 + Random.Range(-4, 4) * 4, -2 - Random.Range(-4, 4) * 4);
        SpriteRenderer renderer = Take(snowPrint, position, 3f + Random.value);
        renderer.color = Color.white;
        renderer.sortingOrder = WorldSortOrder.LayerPaths + 1;
        bool sideways = Mathf.Abs(facing.x) > Mathf.Abs(facing.y);
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, sideways ? 45f : 0f);
        live.Add(new Effect { Renderer = renderer, Dust = false });
    }

    private bool EnsureDust()
    {
        if (dustFrames != null) return true;
        Texture2D sheet = Resources.Load<NpcCatalog>("NpcCatalog")?.animationsAtlas;
        if (sheet == null) return false;
        dustFrames = new Sprite[8];
        for (int i = 0; i < dustFrames.Length; i++)
            dustFrames[i] = Sprite.Create(sheet, new Rect(128 + i * 64, sheet.height - 2948 - 64, 64, 64),
                new Vector2(0.5f, 0.5f), TileSize, 0, SpriteMeshType.FullRect);
        return true;
    }

    private static Vector3 Pixels(float right, float up) => new(right / TileSize, up / TileSize, 0f);

    private SpriteRenderer Take(Sprite sprite, Vector3 position, float scale)
    {
        SpriteRenderer renderer = null;
        while (renderer == null && pool.Count > 0) renderer = pool.Pop();
        if (renderer == null)
        {
            var go = new GameObject("Footstep Effect");
            go.transform.SetParent(transform, false);
            renderer = go.AddComponent<SpriteRenderer>();
        }
        // Effects belong to the map, so they stay behind when the farmer warps away.
        Transform map = LocationManager.Instance?.CurrentLocation?.transform;
        renderer.transform.SetParent(map != null ? map : transform, false);
        renderer.gameObject.SetActive(true);
        renderer.sprite = sprite;
        renderer.transform.position = position;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = Vector3.one * scale;
        return renderer;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Effect effect = live[i];
            if (effect.Renderer == null)
            {
                // The map that held it was destroyed (e.g. a rebuilt mine floor).
                live.RemoveAt(i);
                continue;
            }
            if (effect.Delay > 0f)
            {
                effect.Delay -= dt;
                if (effect.Delay > 0f) continue;
                effect.Renderer.enabled = true;
            }
            effect.Age += dt;
            effect.Alpha -= (effect.Dust ? DustFadePerSecond : SnowFadePerSecond) * dt;
            bool done = effect.Alpha <= 0f;
            if (effect.Dust)
            {
                int frame = Mathf.FloorToInt(effect.Age / DustFrameSeconds);
                done |= frame >= dustFrames.Length;
                if (!done) effect.Renderer.sprite = dustFrames[frame];
            }
            if (done)
            {
                effect.Renderer.gameObject.SetActive(false);
                pool.Push(effect.Renderer);
                live.RemoveAt(i);
                continue;
            }
            Color color = effect.Renderer.color;
            color.a = (effect.Dust ? Khaki.a : 1f) * effect.Alpha;
            effect.Renderer.color = color;
        }
    }
}
