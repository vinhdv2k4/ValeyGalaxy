using UnityEngine;

/// <summary>A farm bush from the original bushes sheet.</summary>
public sealed class FarmBush : MonoBehaviour
{
    public void Setup(Sprite sprite, int size)
    {
        if (sprite == null) return;
        var visual = new GameObject("BushVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(
            (sprite.rect.width / 16f - 1f) * 0.5f,
            (sprite.rect.height / 16f - 1f) * 0.5f,
            0f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 23;

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(size == 0 ? 0.8f : size == 2 ? 2.8f : 1.8f, 0.65f);
        collider.offset = new Vector2((sprite.rect.width / 16f - 1f) * 0.5f, 0f);
    }
}
