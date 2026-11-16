using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>GameLocation.drawWaterTile using the tBIN Back layer's Water flags.</summary>
public sealed class MapWaterView : MonoBehaviour
{
    private readonly List<(Vector3 origin, bool odd, bool topEdge, bool bottomEdge)> cells = new();
    private Mesh mesh;
    private Material material;
    private Vector3 cellSize;
    private Vector3[] vertices;
    private Vector2[] uv;
    private Texture2D atlas;
    private bool farm;
    private float animationTime, waterPosition;
    private bool flip;
    private int lastFrame = -1, lastOffset = -1;

    internal void Initialize(Tilemap tilemap, MapWaterMask mask, bool onFarm, MapDoorCatalog catalog)
    {
        atlas = catalog != null && catalog.waterRippleAtlas != null ? catalog.waterRippleAtlas : StardewUiAtlas.Cursors;
        Shader shader = catalog != null && catalog.waterRippleShader != null
            ? catalog.waterRippleShader : Resources.Load<Shader>("MapWaterOverlay");
        if (atlas == null || shader == null || !shader.isSupported)
        {
            Debug.LogWarning($"Cannot render water ripples on {mask.map}: missing atlas or unsupported shader.", this);
            return;
        }
        cellSize = tilemap.layoutGrid.cellSize;
        farm = onFarm;
        for (int y = 0; y < mask.height; y++)
        for (int x = 0; x < mask.width; x++)
        {
            var cell = new Vector3Int(x, -y, 0);
            if (!mask.IsVisible(x, y) || !tilemap.HasTile(cell)) continue;
            cells.Add((tilemap.CellToLocal(cell), ((x + y) & 1) != 0,
                !mask.Contains(x, y - 1), !mask.Contains(x, y + 1)));
        }
        if (cells.Count == 0) return;

        var view = new GameObject("Water ripples", typeof(MeshFilter), typeof(MeshRenderer));
        view.layer = tilemap.gameObject.layer;
        view.transform.SetParent(tilemap.transform, false);
        mesh = new Mesh { name = "Water atlas strips", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        vertices = new Vector3[cells.Count * 8];
        uv = new Vector2[vertices.Length];
        var indices = new int[cells.Count * 12];
        for (int quad = 0; quad < cells.Count * 2; quad++)
        {
            int v = quad * 4;
            int i = quad * 6;
            indices[i] = v;
            indices[i + 1] = v + 1;
            indices[i + 2] = v + 2;
            indices[i + 3] = v;
            indices[i + 4] = v + 2;
            indices[i + 5] = v + 3;
        }
        // Keep the softer farm look in Linear. The brighter open-water tiles
        // need more of the atlas contrast for the ripple frames to read.
        float opacity = 0.33f;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            opacity *= farm ? 0.60f : 0.85f;
        material = new Material(shader) { mainTexture = atlas, color = new Color(1, 1, 1, opacity) };
        view.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = view.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var backRenderer = tilemap.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = backRenderer.sortingLayerID;
        renderer.sortingOrder = backRenderer.sortingOrder + 1;
        UpdateMesh(0, 0);
        mesh.triangles = indices;
        mesh.RecalculateBounds();
    }

    private void Update()
    {
        if (mesh == null || Time.timeScale <= 0f) return;
        float elapsed = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
        animationTime = (animationTime + elapsed) % 2f;
        waterPosition += elapsed * (farm ? 6f : (Mathf.Sin(animationTime % 1f) + 1f) * 9f);
        if (waterPosition >= 64f)
        {
            waterPosition -= 64f;
            flip = !flip;
        }
        UpdateMesh((int)(animationTime * 5f) % 10, (int)waterPosition);
    }

    private void UpdateMesh(int frame, int offset)
    {
        if (frame == lastFrame && offset == lastOffset) return;
        lastFrame = frame;
        lastOffset = offset;
        float pixelHeight = cellSize.y / 64f;
        for (int i = 0; i < cells.Count; i++)
        {
            var (origin, odd, topEdge, bottomEdge) = cells[i];
            bool secondStrip = odd != flip;
            int stripY = secondStrip ? 2192 : 2064;
            int nextStripY = secondStrip ? 2064 : 2192;
            int crop = topEdge ? offset : 0;
            SetQuad(i * 8, origin + Vector3.up * (offset * pixelHeight), cellSize.x,
                (64 - crop) * pixelHeight, new Rect(frame * 64, stripY + crop, 64, 64 - crop));

            int patchHeight = bottomEdge ? Mathf.Max(0, offset - 1) : 0;
            SetQuad(i * 8 + 4, origin + Vector3.up * pixelHeight, cellSize.x,
                patchHeight * pixelHeight, new Rect(frame * 64, nextStripY, 64, patchHeight));
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
    }

    private void SetQuad(int i, Vector3 bottomLeft, float width, float height, Rect source)
    {
        Rect rect = StardewUiAtlas.ToUv(source, atlas.width, atlas.height);
        vertices[i] = bottomLeft;
        vertices[i + 1] = bottomLeft + Vector3.up * height;
        vertices[i + 2] = bottomLeft + new Vector3(width, height);
        vertices[i + 3] = bottomLeft + Vector3.right * width;
        uv[i] = new Vector2(rect.xMin, rect.yMin);
        uv[i + 1] = new Vector2(rect.xMin, rect.yMax);
        uv[i + 2] = new Vector2(rect.xMax, rect.yMax);
        uv[i + 3] = new Vector2(rect.xMax, rect.yMin);
    }

    private void OnDestroy()
    {
        Destroy(mesh);
        Destroy(material);
    }
}
