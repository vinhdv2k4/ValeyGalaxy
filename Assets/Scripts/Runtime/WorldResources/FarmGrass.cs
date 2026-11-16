using System.Collections;
using UnityEngine;

/// <summary>Three spring grass tufts from a Paths marker.</summary>
public sealed class FarmGrass : WorldResource
{
    protected override void Awake()
    {
        base.Awake();
        requiredToolKind = PlayerItemKind.Material;
        maxHealth = health = 3;
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
            renderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y, -0.5f);
        }
    }

    public override bool Hit(ItemUseContext context)
    {
        if (isBroken || !CanHarvest(context.Item?.itemData, context.Item?.kind ?? PlayerItemKind.Material)) return false;
        // A scythe or blade slicing grass: daggerswipe 50 ms after the hit (a dagger itself plays swordswipe).
        bool dagger = context.Item?.itemData is MeleeWeaponData weapon && weapon.weaponType == MeleeWeaponType.Dagger;
        if (dagger) GameAudio.Play("swordswipe");
        else StartCoroutine(PlayAfter("daggerswipe", 0.05f));
        if (health == 1)
        {
            FinishRemoval(context.User, false, health, () => OnBreak(context.Facing));
            return true;
        }
        int index = --health;
        if (index < transform.childCount) transform.GetChild(index).gameObject.SetActive(false);
        Shake(0.3f, 0.35f);
        return true;
    }

    private static IEnumerator PlayAfter(string cue, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameAudio.Play(cue);
    }

    public override bool CanHarvest(ItemData tool, PlayerItemKind kind)
        => tool is MeleeWeaponData || kind == PlayerItemKind.Sword;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isBroken && other.GetComponentInParent<CharacterMotor>() != null)
            Shake(0.2f, 0.3f);
    }

    public override void Shake(float intensity, float duration)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeTufts(intensity, duration));
    }

    private IEnumerator ShakeTufts(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Sin(elapsed * 35f) * intensity * (1f - elapsed / duration) * Mathf.Rad2Deg;
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).localRotation = Quaternion.identity;
        shakeCoroutine = null;
    }
}
