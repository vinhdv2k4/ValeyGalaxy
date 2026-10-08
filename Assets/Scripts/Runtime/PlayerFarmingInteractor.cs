using UnityEngine;

public sealed class PlayerFarmingInteractor : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CropPlot selectedCrop;
    [Min(0.1f)] public float interactionRadius = 1.2f;
    public LayerMask plotLayer;

    public bool TryHoe(Vector2 facing) => FindPlot(facing)?.TryHoe() == true;
    public bool TryPlant(Vector2 facing) => FindPlot(facing)?.TryPlant() == true;
    public bool TryWater(Vector2 facing) => FindPlot(facing)?.TryWater() == true;

    public bool TryHitResource(Vector2 facing, ItemUseContext context)
    {
        var resource = FindResource(facing, context);
        if (resource != null)
        {
            return resource.Hit(context);
        }
        return false;
    }

    public WorldResource FindResource(Vector2 facing)
        => FindResource(facing, null);

    private WorldResource FindResource(Vector2 facing, ItemUseContext? context)
    {
        Vector2 dir = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
        Vector2 center = (Vector2)transform.position + dir * (interactionRadius * 0.5f);
        var hits = Physics2D.OverlapCircleAll(center, interactionRadius);
        WorldResource nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            var res = hit.GetComponentInParent<WorldResource>();
            if (res == null || res.IsBroken) continue;
            if (context.HasValue)
            {
                InventorySlot slot = context.Value.Item;
                PlayerItemKind kind = slot != null ? slot.kind : PlayerItemKind.Material;
                if (!res.CanHarvest(slot?.itemData, kind)) continue;
            }
            Vector2 target = hit.ClosestPoint(center);
            if (Vector2.Dot((Vector2)hit.bounds.center - (Vector2)transform.position, dir) < -0.1f) continue;
            float distance = (target - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = res;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private CropPlot FindPlot(Vector2 facing)
    {
        if (selectedCrop != null && IsInRange(selectedCrop.transform.position))
            return selectedCrop;

        var center = (Vector2)transform.position + facing.normalized * (interactionRadius * 0.5f);
        var mask = plotLayer.value == 0 ? Physics2D.DefaultRaycastLayers : plotLayer.value;
        var hits = Physics2D.OverlapCircleAll(center, interactionRadius, mask);
        CropPlot nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            var plot = hit.GetComponent<CropPlot>();
            if (plot == null) continue;
            var distance = ((Vector2)plot.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearest = plot;
            nearestDistance = distance;
        }
        return nearest;
    }

    private bool IsInRange(Vector3 target) => ((Vector2)(target - transform.position)).sqrMagnitude <= interactionRadius * interactionRadius;
}
