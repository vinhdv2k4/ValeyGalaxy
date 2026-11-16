using UnityEngine;

internal sealed class FestivalJellyShow : MonoBehaviour
{
    private readonly Sprite[] frames = new Sprite[3];
    private readonly SpriteRenderer[] jellies = new SpriteRenderer[9];
    private float started;

    internal static void StartAt(GameLocation map)
    {
        if (map.GetComponentInChildren<FestivalJellyShow>() != null) return;
        var texture = Resources.Load<FestivalPortraits>("DailyEvents/FestivalPortraits")?.FestivalTiles;
        if (texture == null || texture.width < 304 || texture.height < 32) return;
        var show = new GameObject("Moonlight Jellies");
        show.transform.SetParent(map.transform, false);
        show.AddComponent<FestivalJellyShow>().Initialize(map, texture);
    }

    private void Initialize(GameLocation map, Texture2D texture)
    {
        started = Time.time;
        for (int frame = 0; frame < frames.Length; frame++)
            frames[frame] = Sprite.Create(texture, new Rect(256 + 16 * frame, texture.height - 32, 16, 16),
                Vector2.one * 0.5f, 16);
        for (int i = 0; i < jellies.Length; i++)
        {
            var jelly = new GameObject("Jelly");
            jelly.transform.SetParent(transform, false);
            jelly.transform.position = map.TileCenter(24 + i, 49 + i % 3);
            var renderer = jelly.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = i == jellies.Length - 1
                ? new Color(1f, 0.56f, 0.76f) : new Color(0.65f, 1f, 0.92f);
            renderer.sortingOrder = WorldSortOrder.GetOrder(jelly.transform.position.y);
            jellies[i] = renderer;
        }
    }

    private void Update()
    {
        float elapsed = Time.time - started;
        for (int i = 0; i < jellies.Length; i++)
        {
            var jelly = jellies[i];
            jelly.sprite = frames[(Mathf.FloorToInt(elapsed * 4f + i) % 4) switch { 3 => 1, var frame => frame }];
            if (elapsed < 19f)
                jelly.transform.position += Time.deltaTime * new Vector3(Mathf.Cos(elapsed + i) * 0.35f, 0.42f);
        }
    }

    private void OnDestroy()
    {
        foreach (var frame in frames)
            if (frame != null) Destroy(frame);
    }
}
