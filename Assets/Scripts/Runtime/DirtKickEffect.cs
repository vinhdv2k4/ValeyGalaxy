using UnityEngine;

/// <summary>
/// The puff of dirt kicked up where a hoe digs: row 12 of the shared animations sheet, eight
/// 64-pixel frames at 50 ms each, randomly mirrored.
/// </summary>
internal sealed class DirtKickEffect : MonoBehaviour
{
    private const int Row = 12, Frames = 8;
    private const float FrameSeconds = 0.05f;

    private static Sprite[] frames;
    // Every hoe swing kicks dirt: finished puffs are parked here instead of destroyed.
    private static readonly System.Collections.Generic.Stack<DirtKickEffect> Pool = new();
    private SpriteRenderer body;
    private float started;
    private float delay;

    internal static void Spawn(Vector3 tileCenter, float delaySeconds = 0f)
    {
        if (frames == null)
        {
            Texture2D sheet = Resources.Load<NpcCatalog>("NpcCatalog")?.animationsAtlas;
            if (sheet == null) return;
            frames = new Sprite[Frames];
            for (int i = 0; i < Frames; i++)
                frames[i] = AtlasSprites.FromTop(sheet, i * 64, Row * 64, 64, 64, new Vector2(0.5f, 0.5f), 64f);
        }
        DirtKickEffect effect = Take();
        effect.transform.position = tileCenter;
        effect.body.flipX = Random.value < 0.5f;
        effect.body.sortingOrder = WorldSortOrder.GetOrder(tileCenter.y - 0.5f) + 5;
        effect.body.enabled = false;
        effect.delay = delaySeconds;
        effect.started = Time.time;
        effect.gameObject.SetActive(true);
    }

    private static DirtKickEffect Take()
    {
        // Pooled puffs die with their scene; skip those.
        while (Pool.Count > 0)
        {
            DirtKickEffect pooled = Pool.Pop();
            if (pooled != null) return pooled;
        }
        var go = new GameObject("Dirt Kick");
        var effect = go.AddComponent<DirtKickEffect>();
        effect.body = go.AddComponent<SpriteRenderer>();
        return effect;
    }

    private void Update()
    {
        float elapsed = Time.time - started - delay;
        if (elapsed < 0f) return;
        int frame = Mathf.FloorToInt(elapsed / FrameSeconds);
        if (frame >= Frames)
        {
            body.enabled = false;
            gameObject.SetActive(false);
            Pool.Push(this);
            return;
        }
        body.enabled = true;
        body.sprite = frames[frame];
    }
}
