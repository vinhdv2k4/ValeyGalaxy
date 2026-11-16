using UnityEngine;

/// <summary>
/// Robin at work on a building site: the hammer loop (two quick wind-up frames, the strike with its
/// chop or clank, then a pause of one to four seconds), heard louder the closer the camera is.
/// </summary>
internal sealed class RobinConstructionView : MonoBehaviour
{
    private const int FirstFrame = 24;

    private SpriteRenderer body;
    private Sprite[] frames;
    private int step;
    private float stepEnds;

    /// <summary>Robin standing with her feet at <paramref name="feet"/>; null when her sheet is missing.</summary>
    internal static RobinConstructionView Spawn(Transform parent, Vector3 feet)
    {
        Texture2D sheet = RobinSheet();
        if (sheet == null) return null;
        int columns = sheet.width / 16;
        var frames = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            int index = FirstFrame + i;
            frames[i] = AtlasSprites.FromTop(sheet, index % columns * 16, index / columns * 32, 16, 32, new Vector2(0.5f, 0f));
            if (frames[i] == null) return null;
        }
        var go = new GameObject("Robin (building)");
        go.transform.SetParent(parent, false);
        go.transform.position = feet;
        var view = go.AddComponent<RobinConstructionView>();
        view.frames = frames;
        view.body = go.AddComponent<SpriteRenderer>();
        view.body.sortingOrder = WorldSortOrder.GetOrder(feet.y, 0f, 1);
        view.step = 3;
        view.stepEnds = Time.time + Random.Range(0.2f, 1.5f);
        view.body.sprite = frames[3];
        return view;
    }

    private static Texture2D RobinSheet()
    {
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (catalog == null) return null;
        foreach (NpcDefinition npc in catalog.characters)
        {
            if (npc == null || npc.name != "Robin" || npc.animationFrames == null) continue;
            foreach (Sprite frame in npc.animationFrames)
                if (frame != null) return frame.texture;
        }
        return null;
    }

    private void Update()
    {
        if (Time.time < stepEnds) return;
        step = (step + 1) % 4;
        body.sprite = frames[step];
        stepEnds = Time.time + step switch
        {
            0 or 1 => 0.075f,
            2 => 0.3f,
            _ => Random.Range(1f, 4f)
        };
        if (step == 2) Strike();
    }

    private void Strike()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        float distance = Vector2.Distance(camera.transform.position, transform.position);
        float volume = Mathf.Clamp01(1f - distance / 14f);
        if (volume <= 0.05f) return;
        GameAudio.Play(Random.value < 0.1f ? "clank" : "axchop", volume);
    }
}
