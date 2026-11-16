using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FarmingVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float frameDuration = 0.07f;
    private static readonly Dictionary<Texture2D, Sprite[]> WaterFrames = new();
    private Sprite[] frames;
    private Action finished;
    private float elapsed;

    public void Play(Texture2D waterAtlas, Action onFinished)
    {
        visual = visual == null ? GetComponent<SpriteRenderer>() : visual;
        frames = GetWaterFrames(waterAtlas);
        visual.sprite = frames[0];
        visual.color = Color.white;
        visual.sortingOrder = WorldSortOrder.GetOrder(transform.position.y) + 5;
        transform.localScale = Vector3.one;
        elapsed = 0f;
        finished = onFinished;
    }

    private void Update()
    {
        if (finished == null) return;
        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed / frameDuration);
        if (frame >= frames.Length) { Finish(); return; }
        visual.sprite = frames[frame];
    }

    private void Finish()
    {
        Action callback = finished;
        finished = null;
        callback?.Invoke();
    }

    private static Sprite[] GetWaterFrames(Texture2D atlas)
    {
        if (WaterFrames.TryGetValue(atlas, out var cached)) return cached;
        var result = new Sprite[10];
        float scaleX = atlas.width / 640f;
        float scaleY = atlas.height / 3328f;
        int top = Mathf.RoundToInt(832f * scaleY);
        int bottom = Mathf.RoundToInt(896f * scaleY);
        for (int i = 0; i < result.Length; i++)
        {
            int left = Mathf.RoundToInt(i * 64f * scaleX);
            int right = Mathf.RoundToInt((i + 1) * 64f * scaleX);
            result[i] = Sprite.Create(atlas,
                new Rect(left, atlas.height - bottom, right - left, bottom - top),
                new Vector2(0.5f, 0.5f), 64f * scaleY);
        }
        WaterFrames.Add(atlas, result);
        return result;
    }
}
