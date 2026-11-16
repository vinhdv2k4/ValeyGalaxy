using UnityEngine;

/// <summary>Loads the authored startup prefab from the boot scene.</summary>
public sealed class StartupTitleMenu : MonoBehaviour
{
    [SerializeField] private GameObject startupPrefab;

    private void Awake()
    {
        if (startupPrefab == null)
        {
            Debug.LogError("Assign the StartupMenu prefab to the Main Menu camera.");
            return;
        }
        Instantiate(startupPrefab);
    }
}
