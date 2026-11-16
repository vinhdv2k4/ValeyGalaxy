using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class WaterDetector : MonoBehaviour
{
    [SerializeField] private Tilemap ground;

    private void ResolveGround()
    {
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        if (location == null) { ground = null; return; }
        if (ground != null && ground.transform.IsChildOf(location.transform)) return;
        ground = null;
        foreach (Tilemap candidate in location.GetComponentsInChildren<Tilemap>(true))
        {
            if (candidate.name != "Back") continue;
            ground = candidate;
            return;
        }
    }

    public bool IsWater(Vector3 worldPosition)
    {
        ResolveGround();
        if (ground == null) return false;
        Vector3Int cell = ground.WorldToCell(worldPosition);
        return LocationManager.Instance?.CurrentLocation?.WaterMask?.Contains(cell.x, -cell.y)
            ?? IsWaterCell(cell);
    }

    private bool IsWaterCell(Vector3Int cell)
    {
        Sprite sprite = ground.GetSprite(cell);
        if (sprite == null) return false;

        // The map builder creates plain Unity Tiles and does not copy tBIN's
        // Water property. Sample the centre of its existing water tile sprite.
        try
        {
            Rect rect = sprite.textureRect;
            int blueSamples = 0;
            foreach (var point in new[] {
                rect.center,
                new Vector2(rect.xMin + rect.width * 0.3f, rect.yMin + rect.height * 0.3f),
                new Vector2(rect.xMin + rect.width * 0.7f, rect.yMin + rect.height * 0.3f),
                new Vector2(rect.xMin + rect.width * 0.3f, rect.yMin + rect.height * 0.7f),
                new Vector2(rect.xMin + rect.width * 0.7f, rect.yMin + rect.height * 0.7f) })
            {
                Color color = sprite.texture.GetPixel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
                if (color.a > 0.5f && color.b > color.r * 1.08f && color.b >= color.g * 0.75f)
                    blueSamples++;
            }
            return blueSamples >= 3;
        }
        catch (UnityException)
        {
            return false;
        }
    }
}
