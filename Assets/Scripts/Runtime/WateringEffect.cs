using UnityEngine;

public sealed class WateringEffect : MonoBehaviour
{
    private const int FrameCount = 10;
    private const float FrameDuration = 0.07f;
    private static readonly System.Collections.Generic.Dictionary<Texture2D, Sprite[]> Frames = new();
    private float elapsed;
    private SpriteRenderer visual;
    private Sprite[] animationFrames;

    public static void Play(Texture2D atlas, Vector3 soil)
    {
        if (atlas == null) return;
        if (!Frames.TryGetValue(atlas, out var frames))
        {
            frames = new Sprite[FrameCount];
            // Unity can import the source 640x3328 atlas at a smaller size.
            // Convert the source atlas coordinates to imported pixels.
            float scaleX = atlas.width / 640f;
            float scaleY = atlas.height / 3328f;
            int yTop = Mathf.RoundToInt(832f * scaleY);
            int yBottom = Mathf.RoundToInt(896f * scaleY);
            for (int i = 0; i < FrameCount; i++)
            {
                int xLeft = Mathf.RoundToInt(i * 64f * scaleX);
                int xRight = Mathf.RoundToInt((i + 1) * 64f * scaleX);
                frames[i] = Sprite.Create(atlas,
                    new Rect(xLeft, atlas.height - yBottom, xRight - xLeft, yBottom - yTop),
                    new Vector2(0.5f, 0.5f), 64f * scaleY);
            }
            Frames.Add(atlas, frames);
        }

        WateringEffect effect = Take();
        effect.transform.position = soil;
        effect.elapsed = 0f;
        effect.visual.sprite = frames[0];
        effect.visual.sortingOrder = WorldSortOrder.GetOrder(soil.y) + 5;
        effect.animationFrames = frames;
        effect.gameObject.SetActive(true);
    }

    // A charged can splashes up to 18 tiles at once: finished splashes are reused.
    private static readonly System.Collections.Generic.Stack<WateringEffect> Pool = new();

    private static WateringEffect Take()
    {
        // Pooled splashes die with their scene; skip those.
        while (Pool.Count > 0)
        {
            WateringEffect pooled = Pool.Pop();
            if (pooled != null) return pooled;
        }
        var splash = new GameObject("Watering Splash");
        var effect = splash.AddComponent<WateringEffect>();
        effect.visual = splash.AddComponent<SpriteRenderer>();
        return effect;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed / FrameDuration);
        if (frame >= FrameCount)
        {
            gameObject.SetActive(false);
            Pool.Push(this);
            return;
        }
        visual.sprite = animationFrames[frame];
    }
}
