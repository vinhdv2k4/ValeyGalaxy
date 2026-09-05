using UnityEngine;

public sealed class PlayerFarmingInteractor : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CropPlot selectedCrop;
    [Min(0.1f)] public float interactionRadius = 1.2f;
    public LayerMask plotLayer;

    public bool TryHoe(Vector2 facing) => FindPlot(facing)?.TryHoe() == true;
    public bool TryPlant(Vector2 facing) => FindPlot(facing)?.TryPlant() == true;

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
