using UnityEngine;

/// <summary>Three spring grass tufts from a Paths marker, as in Grass(1, 3).</summary>
public sealed class FarmGrass : WorldResource
{
    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Material;
        maxHealth = health = 1;
    }

    protected override void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;
        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.9f, 0.8f);
        collider.isTrigger = true;
    }

    public void Setup(Sprite[] variants, System.Random random)
    {
        if (variants == null || variants.Length == 0) return;
        for (int i = 0; i < 3; i++)
        {
            var tuft = new GameObject("Tuft");
            tuft.transform.SetParent(transform, false);
            tuft.transform.localPosition = new Vector3(
                (i % 2 == 0 ? -0.18f : 0.18f) + (float)(random.NextDouble() - 0.5) * 0.15f,
                (i == 2 ? -0.16f : 0.08f) + (float)(random.NextDouble() - 0.5) * 0.1f,
                0f);
            var renderer = tuft.AddComponent<SpriteRenderer>();
            renderer.sprite = variants[random.Next(variants.Length)];
            renderer.sortingOrder = 22;
        }
    }
}
