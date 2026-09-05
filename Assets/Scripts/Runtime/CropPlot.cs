using UnityEngine;
using UnityEngine.Events;

public sealed class CropPlot : MonoBehaviour
{
    public Transform cropAnchor;
    [SerializeField] private bool tilled;
    [SerializeField] private bool planted;
    [SerializeField] private UnityEvent onTilled = new();
    [SerializeField] private UnityEvent onPlanted = new();

    public bool IsTilled => tilled;
    public bool IsPlanted => planted;

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
}
