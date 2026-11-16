using System.Collections.Generic;
using UnityEngine;

/// <summary>A farm bush component.</summary>
public sealed class FarmBush : MonoBehaviour
{
    private static readonly Dictionary<(Texture2D, int, int), Sprite> Cache = new();
    private SpriteRenderer visual;
    private Texture2D sheet;
    private int size, shownSeason = -1;
    private bool shownBerries;

    /// <summary>Medium bushes (size 1) are the berry bushes.</summary>
    internal bool CanBearBerries => size == 1 && sheet != null;
    internal bool HasBerries => shownBerries;

    public void Setup(Sprite sprite, int size)
    {
        this.size = size;
        if (sprite == null) return;
        var visualObject = new GameObject("BushVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = new Vector3(
            (sprite.rect.width / 16f - 1f) * 0.5f,
            (sprite.rect.height / 16f - 1f) * 0.5f,
            0f);
        visual = visualObject.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(size == 0 ? 0.8f : size == 2 ? 2.8f : 1.8f, 0.65f);
        collider.offset = new Vector2((sprite.rect.width / 16f - 1f) * 0.5f, 0f);
    }

    /// <summary>
    /// The bush sways from its base, starting at maxShake (pi / 128) and losing
    /// shakeDecayRate each frame while swinging at shakeRate; "leafrustle" plays 100 ms in.
    /// </summary>
    internal void Shake(bool towardLeft)
    {
        if (visual == null) return;
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(towardLeft));
    }

    private System.Collections.IEnumerator ShakeRoutine(bool towardLeft)
    {
        const float maxShake = Mathf.PI / 128f, shakeRate = 0.01570796f, decay = 0.003067962f;
        Transform t = visual.transform;
        Vector3 restPosition = t.localPosition;
        // Rotate around the bottom centre of the sprite.
        Vector3 pivot = restPosition + Vector3.down * (visual.sprite.bounds.size.y * 0.5f);
        float shake = maxShake, rotation = 0f;
        bool left = towardLeft, rustled = false;
        float elapsed = 0f;
        while (shake > 0f)
        {
            elapsed += Time.deltaTime;
            if (!rustled && elapsed >= 0.1f) { rustled = true; GameAudio.Play("leafrustle"); }
            float frames = Time.deltaTime * 60f;
            rotation += (left ? -shakeRate : shakeRate) * frames;
            if (Mathf.Abs(rotation) >= shake) { rotation = Mathf.Sign(rotation) * shake; left = !left; }
            shake = Mathf.Max(0f, shake - decay * frames);
            Quaternion turn = Quaternion.Euler(0f, 0f, -rotation * Mathf.Rad2Deg);
            t.localRotation = turn;
            t.localPosition = pivot + turn * (restPosition - pivot);
            yield return null;
        }
        t.localRotation = Quaternion.identity;
        t.localPosition = restPosition;
    }

    /// <summary>Lets a medium bush follow the season and show berries.</summary>
    internal void UseSeasonalSheet(Texture2D bushes) => sheet = bushes;

    /// <summary>
    /// Setup source rect for size 1: (season * 64 + tileSheetOffset * 32) along the sheet, 32x48,
    /// where tileSheetOffset 1 is the bush covered in berries.
    /// </summary>
    internal void Show(int season, bool berries)
    {
        if (!CanBearBerries || visual == null || season == shownSeason && berries == shownBerries) return;
        shownSeason = season;
        shownBerries = berries;
        int offset = season * 64 + (berries ? 32 : 0);
        int x = offset % sheet.width, y = offset / sheet.width * 48;
        if (!Cache.TryGetValue((sheet, x, y), out var sprite) || sprite == null)
        {
            if (y + 48 > sheet.height) return;
            sprite = Sprite.Create(sheet, new Rect(x, sheet.height - y - 48, 32, 48), new Vector2(0.5f, 0.5f), 16f);
            Cache[(sheet, x, y)] = sprite;
        }
        visual.sprite = sprite;
    }
}
