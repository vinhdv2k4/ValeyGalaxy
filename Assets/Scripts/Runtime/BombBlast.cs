using UnityEngine;

/// <summary>
/// A lit bomb: shakes for a moment with a burning fuse, then explodes. The blast breaks stones,
/// ore and boulders in its radius, hurts monsters and hurts the farmer if they stand too close.
/// </summary>
internal sealed class BombBlast : MonoBehaviour
{
    private const float FuseSeconds = 2.4f;

    private int radius;
    private Transform farmer;
    private SpriteRenderer body;
    private SpriteRenderer spark;
    private float litAt;
    private bool exploded;
    private Sprite[] blastFrames;
    private SpriteRenderer blast;
    private float blastStarted;

    internal static void Spawn(GameLocation map, Vector3 center, int objectIndex, int radius, Transform farmer)
    {
        var go = new GameObject("Bomb");
        go.transform.SetParent(map.transform, true);
        go.transform.position = center;
        var bomb = go.AddComponent<BombBlast>();
        bomb.radius = radius;
        bomb.farmer = farmer;
        bomb.litAt = Time.time;
        bomb.body = go.AddComponent<SpriteRenderer>();
        bomb.body.sprite = AtlasSprites.Cell(Resources.Load<Texture2D>("Maps/springobjects"), objectIndex);
        bomb.body.sortingOrder = WorldSortOrder.GetOrder(center.y - 0.4f);
        var spark = new GameObject("Fuse");
        spark.transform.SetParent(go.transform, false);
        spark.transform.localPosition = new Vector3(0.2f, 0.35f, 0f);
        bomb.spark = spark.AddComponent<SpriteRenderer>();
        bomb.spark.sprite = AtlasSprites.FromTop(StardewUiAtlas.Cursors, 598, 1279, 3, 4, new Vector2(0.5f, 0.5f), 16f);
        bomb.spark.color = Color.yellow;
        bomb.spark.sortingOrder = bomb.body.sortingOrder + 1;
        GameAudio.Play("thudStep");
        GameAudio.Play("fuse");
    }

    private void Update()
    {
        if (!exploded)
        {
            float elapsed = Time.time - litAt;
            float shake = 0.02f + elapsed * 0.03f;
            body.transform.localPosition = new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);
            spark.color = Mathf.FloorToInt(Time.time * 12f) % 2 == 0 ? Color.yellow : new Color(1f, 0.55f, 0.1f);
            if (elapsed >= FuseSeconds) Explode();
            return;
        }
        int frame = Mathf.FloorToInt((Time.time - blastStarted) / 0.07f);
        if (blastFrames == null || frame >= blastFrames.Length) { Destroy(gameObject); return; }
        blast.sprite = blastFrames[frame];
    }

    private void Explode()
    {
        exploded = true;
        body.enabled = false;
        spark.enabled = false;
        GameAudio.Play("explosion");
        Vector2 center = transform.position;

        foreach (WorldResource resource in FindObjectsByType<WorldResource>(FindObjectsSortMode.None))
        {
            if (!resource.isActiveAndEnabled) continue;
            Vector2 offset = (Vector2)resource.transform.position - center;
            if (offset.magnitude <= radius) resource.Explode(farmer, offset.normalized);
        }
        foreach (MineMonsterInstance monster in FindObjectsByType<MineMonsterInstance>(FindObjectsSortMode.None))
        {
            Vector2 offset = (Vector2)monster.transform.position - center;
            if (offset.magnitude <= radius)
                monster.TakeDamage(radius * 6 + Random.Range(0, radius * 3), offset.normalized);
        }
        if (farmer != null && ((Vector2)farmer.position - center).magnitude <= radius - 1)
            farmer.GetComponent<NetworkCharacterStats>()?.TakeDamage(radius * 3);

        // Animation row 23 of the shared animations sheet is the explosion.
        Texture2D sheet = Resources.Load<NpcCatalog>("NpcCatalog")?.animationsAtlas;
        if (sheet == null) { Destroy(gameObject, 0.1f); return; }
        blastFrames = new Sprite[8];
        for (int i = 0; i < blastFrames.Length; i++)
            blastFrames[i] = AtlasSprites.FromTop(sheet, i * 64, 23 * 64, 64, 64, new Vector2(0.5f, 0.5f), 16f);
        var go = new GameObject("Blast");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * Mathf.Max(1f, radius / 2.5f);
        blast = go.AddComponent<SpriteRenderer>();
        blast.sortingOrder = WorldSortOrder.GetOrder(center.y - 1f) + 50;
        blastStarted = Time.time;
    }
}
