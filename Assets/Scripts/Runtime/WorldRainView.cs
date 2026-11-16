using UnityEngine;

/// <summary>Viewport rain rendering.</summary>
internal sealed class WorldRainView : MonoBehaviour
{
    private const int DropCount = 70;
    private const float StepSeconds = 0.07f;
    private const float OriginalPixelsPerTile = 64f;
    private const int AtlasCell = 16;

    private struct Drop
    {
        public Vector2 Position;
        public float Elapsed;
        public int Frame;
        public SpriteRenderer Renderer;
    }

    private readonly Drop[] drops = new Drop[DropCount];
    private readonly Sprite[] frames = new Sprite[4];
    private readonly System.Random random = new();
    private FarmDayClock clock;
    private Transform pool;
    private Material unlit;
    private Camera lastCamera;
    private GameLocation lastMap;
    private Vector3 previousCameraPosition;

    private void Awake() => clock = GetComponent<FarmDayClock>();

    private void LateUpdate()
    {
        var location = LocationManager.Instance?.CurrentLocation;
        var camera = Camera.main;
        if (!clock.IsRainingOutdoors(location) || camera == null || !camera.orthographic)
        {
            if (pool != null) pool.gameObject.SetActive(false);
            lastMap = null;
            return;
        }

        if (pool == null && !CreatePool()) return;
        pool.gameObject.SetActive(true);
        Vector3 cameraPosition = camera.transform.position;
        float height = 2f * camera.orthographicSize;
        float width = height * camera.aspect;
        bool reset = location != lastMap || camera != lastCamera;
        Vector3 cameraMovement = reset ? Vector3.zero : cameraPosition - previousCameraPosition;
        previousCameraPosition = cameraPosition;
        lastCamera = camera;
        lastMap = location;

        float elapsed = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
        Vector3 topLeft = cameraPosition + new Vector3(-width * 0.5f, height * 0.5f,
            camera.nearClipPlane + 0.5f);
        for (int index = 0; index < drops.Length; index++)
        {
            ref Drop drop = ref drops[index];
            if (reset)
            {
                drop.Position = SamplePosition(width, height);
                drop.Frame = random.Next(frames.Length);
                drop.Elapsed = (float)random.NextDouble() * StepSeconds;
            }

            drop.Position.x -= cameraMovement.x;
            drop.Position.y += cameraMovement.y;
            drop.Elapsed += elapsed;
            while (drop.Elapsed >= StepSeconds)
            {
                drop.Elapsed -= StepSeconds;
                if (drop.Frame == 0)
                {
                    int stagger = index * 8 / drops.Length;
                    drop.Position += new Vector2(stagger - 16, 32 - stagger) / OriginalPixelsPerTile;
                    if (random.NextDouble() < 0.1) drop.Frame = 1;
                }
                else if ((drop.Frame = (drop.Frame + 1) % frames.Length) == 0)
                {
                    drop.Position = SamplePosition(width, height);
                }
            }
            drop.Position.x = Wrap(drop.Position.x, width);
            drop.Position.y = Wrap(drop.Position.y, height);
            drop.Renderer.sprite = frames[drop.Frame];
            drop.Renderer.transform.position = topLeft + new Vector3(drop.Position.x, -drop.Position.y);
        }
    }

    private bool CreatePool()
    {
        var atlas = Resources.Load<Texture2D>("Maps/rain");
        if (atlas == null || atlas.width < AtlasCell * frames.Length || atlas.height < AtlasCell)
        {
            enabled = false;
            return false;
        }

        // The imported sheet uses 16px cells, drawn as 64px cells.
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Sprite.Create(atlas, new Rect(i * AtlasCell, atlas.height - AtlasCell, AtlasCell, AtlasCell),
                Vector2.up, AtlasCell, 0, SpriteMeshType.FullRect);

        pool = new GameObject("Rain drops").transform;
        pool.SetParent(transform, false);
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null) unlit = new Material(shader);
        for (int i = 0; i < drops.Length; i++)
        {
            var renderer = new GameObject("Rain drop").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(pool, false);
            renderer.sortingOrder = short.MaxValue - 1;
            if (unlit != null) renderer.sharedMaterial = unlit;
            drops[i].Renderer = renderer;
        }
        return true;
    }

    private Vector2 SamplePosition(float width, float height) =>
        new((float)random.NextDouble() * width, (float)random.NextDouble() * height);

    private static float Wrap(float position, float size) =>
        position < -1f ? size : position > size + 1f ? -1f : position;

    private void OnDestroy()
    {
        foreach (var frame in frames) if (frame != null) Destroy(frame);
        if (unlit != null) Destroy(unlit);
        if (pool != null) Destroy(pool.gameObject);
    }
}
