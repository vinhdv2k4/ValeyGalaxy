using UnityEngine;
using UnityEngine.Events;

public sealed class CropPlot : MonoBehaviour
{
    public Transform cropAnchor;
    [SerializeField] private bool tilled;
    [SerializeField] private bool planted;
    [SerializeField] private bool watered;
    [SerializeField] private UnityEvent onTilled = new();
    [SerializeField] private UnityEvent onPlanted = new();
    [SerializeField] private UnityEvent onWatered = new();

    public bool IsTilled => tilled;
    public bool IsPlanted => planted;
    public bool IsWatered => watered;

    public bool TryHoe()
    {
        if (tilled) return false;
        tilled = true;
        onTilled?.Invoke();
        return true;
    }

    public bool TryPlant()
    {
        if (!tilled || planted) return false;
        planted = true;
        onPlanted?.Invoke();
        return true;
    }

    public bool TryWater()
    {
        if (!tilled || watered) return false;
        watered = true;
        onWatered?.Invoke();
        return true;
    }
}
