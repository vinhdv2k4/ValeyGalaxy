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

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        RefreshMapBounds();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (isFrozen || target == null) return;

        Vector3 desired = ClampToMap(target.position + offset);
        Vector3 current = transform.position;
        float distance = Vector2.Distance(current, desired);
        float blend = distance >= snapDistanceThreshold ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(current, desired, blend);
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        if (snap) SnapToTarget();
    }

    public void SetFrozen(bool frozen) => isFrozen = frozen;

    public void SnapToTarget()
    {
        if (target != null)
            transform.position = ClampToMap(target.position + offset);
    }

    public void RefreshMapBounds()
    {
        hasMapBounds = false;
        if (useManualBounds)
        {
            mapBounds = manualBounds;
            hasMapBounds = true;
            return;
        }

        if (mapRoot == null) return;
        foreach (Tilemap tilemap in mapRoot.GetComponentsInChildren<Tilemap>())
        {
            BoundsInt cells = tilemap.cellBounds;
            if (cells.size.x == 0 || cells.size.y == 0) continue;

            Vector3 a = tilemap.CellToWorld(cells.min);
            Vector3 b = tilemap.CellToWorld(cells.max);
            Bounds tileBounds = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
            if (!hasMapBounds)
            {
                mapBounds = tileBounds;
                hasMapBounds = true;
            }
            else mapBounds.Encapsulate(tileBounds);
        }
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
}
