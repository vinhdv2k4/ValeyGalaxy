using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0f)] private float smoothSpeed = 8f;
    [SerializeField, Min(0f)] private float snapDistanceThreshold = 10f;
    [SerializeField] private bool clampToMapBounds = true;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private bool useManualBounds;
    [SerializeField] private Bounds manualBounds = new Bounds(Vector3.zero, new Vector3(80f, 80f, 0f));
    [SerializeField] private bool isFrozen;

    private Camera cameraComponent;
    private Bounds mapBounds;
    private bool hasMapBounds;
    private float defaultOrthographicSize;
    private Color defaultBackgroundColor;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        defaultOrthographicSize = cameraComponent.orthographicSize;
        defaultBackgroundColor = cameraComponent.backgroundColor;
        RefreshMapBounds();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (isFrozen || target == null) return;
        GameLocation activeLocation = LocationManager.Instance?.CurrentLocation;
        if (clampToMapBounds && !useManualBounds && (mapRoot == null || !mapRoot.gameObject.activeInHierarchy ||
            mapRoot.gameObject.scene != target.gameObject.scene ||
            (activeLocation != null && mapRoot != activeLocation.transform)))
            RefreshMapBounds();

        // The visible width changes when the game window changes aspect ratio.
        ApplyMapZoom();

        Vector3 desired = ClampToMap(target.position + offset);
        Vector3 current = transform.position;
        float distance = Vector2.Distance(current, desired);
        float blend = distance >= snapDistanceThreshold ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = SnapToScreenPixelGrid(ClampToMap(Vector3.Lerp(current, desired, blend)));
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        mapRoot = null;
        RefreshMapBounds();
        if (snap) SnapToTarget();
    }

    public void SetFrozen(bool frozen) => isFrozen = frozen;

    public void SnapToTarget()
    {
        if (target != null)
            transform.position = SnapToScreenPixelGrid(ClampToMap(target.position + offset));
    }

    public void RefreshMapBounds()
    {
        hasMapBounds = false;
        GameLocation activeLocation = LocationManager.Instance?.CurrentLocation;
        // Empty cave/interior cells must blend into darkness instead of the outdoor sky color.
        if (cameraComponent != null)
            cameraComponent.backgroundColor = activeLocation != null && !activeLocation.IsOutdoors
                ? Color.black : defaultBackgroundColor;
        if (useManualBounds)
        {
            mapBounds = manualBounds;
            hasMapBounds = true;
            ApplyMapZoom();
            return;
        }

        mapRoot = activeLocation != null && activeLocation.gameObject.activeInHierarchy
            ? activeLocation.transform : FindCurrentMapRoot();
        if (mapRoot == null)
        {
            ApplyMapZoom();
            return;
        }

        Tilemap[] tilemaps = mapRoot.GetComponentsInChildren<Tilemap>();
        // Decorative layers can extend beyond the walkable map. Prefer its ground layer.
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.name == "Back" && TryGetTilemapBounds(tilemap, out mapBounds))
            {
                hasMapBounds = true;
                break;
            }
        }

        if (!hasMapBounds)
        {
            foreach (Tilemap tilemap in tilemaps)
            {
                if (!TryGetTilemapBounds(tilemap, out Bounds tileBounds)) continue;
                if (!hasMapBounds) { mapBounds = tileBounds; hasMapBounds = true; }
                else mapBounds.Encapsulate(tileBounds);
            }
        }
        ApplyMapZoom();
    }

    private static bool TryGetTilemapBounds(Tilemap tilemap, out Bounds bounds)
    {
        BoundsInt cells = tilemap.cellBounds;
        if (cells.size.x <= 0 || cells.size.y <= 0)
        {
            bounds = default;
            return false;
        }

        Vector3 a = tilemap.CellToWorld(cells.min);
        Vector3 b = tilemap.CellToWorld(cells.max);
        bounds = new Bounds((a + b) * 0.5f,
            new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
        return true;
    }

    private void ApplyMapZoom()
    {
        if (cameraComponent == null || !cameraComponent.orthographic) return;
        if (!hasMapBounds)
        {
            cameraComponent.orthographicSize = defaultOrthographicSize;
            return;
        }
        float halfWidthAsHeight = mapBounds.size.x / (2f * Mathf.Max(0.1f, cameraComponent.aspect));
        cameraComponent.orthographicSize = Mathf.Max(0.1f,
            Mathf.Min(defaultOrthographicSize, mapBounds.size.y * 0.5f, halfWidthAsHeight));
    }

    private Transform FindCurrentMapRoot()
    {
        if (target == null) return null;
        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (GameObject root in target.gameObject.scene.GetRootGameObjects())
        {
            Tilemap ground = null;
            foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>())
            {
                if (tilemap.name == "Back") { ground = tilemap; break; }
                if (ground == null) ground = tilemap;
            }
            if (ground == null || ground.cellBounds.size.x == 0 || ground.cellBounds.size.y == 0) continue;
            BoundsInt cells = ground.cellBounds;
            Vector3 a = ground.CellToWorld(cells.min);
            Vector3 b = ground.CellToWorld(cells.max);
            float distance = Mathf.Max(Mathf.Min(a.x, b.x) - target.position.x, 0f, target.position.x - Mathf.Max(a.x, b.x)) +
                Mathf.Max(Mathf.Min(a.y, b.y) - target.position.y, 0f, target.position.y - Mathf.Max(a.y, b.y));
            if (distance < nearestDistance) { nearestDistance = distance; nearest = root.transform; }
            if (distance == 0f) break;
        }
        return nearest;
    }

    private Vector3 ClampToMap(Vector3 position)
    {
        if (!clampToMapBounds || !hasMapBounds || !cameraComponent.orthographic) return position;

        float halfHeight = cameraComponent.orthographicSize;
        float halfWidth = halfHeight * cameraComponent.aspect;
        position.x = ClampAxis(position.x, mapBounds.min.x, mapBounds.max.x, halfWidth);
        position.y = ClampAxis(position.y, mapBounds.min.y, mapBounds.max.y, halfHeight);
        return position;
    }

    private static float ClampAxis(float value, float min, float max, float halfView)
    {
        if (max - min <= 2f * halfView) return (min + max) * 0.5f;
        return Mathf.Clamp(value, min + halfView, max - halfView);
    }

    private Vector3 SnapToScreenPixelGrid(Vector3 position)
    {
        if (cameraComponent == null || !cameraComponent.orthographic || Screen.height <= 0) return position;

        // Keep the camera aligned to output pixels while retaining the current zoom.
        // Fractional camera positions can expose seams between otherwise adjacent tiles.
        float worldUnitsPerPixel = 2f * cameraComponent.orthographicSize / Screen.height;
        position.x = Mathf.Round(position.x / worldUnitsPerPixel) * worldUnitsPerPixel;
        position.y = Mathf.Round(position.y / worldUnitsPerPixel) * worldUnitsPerPixel;
        return position;
    }
}
