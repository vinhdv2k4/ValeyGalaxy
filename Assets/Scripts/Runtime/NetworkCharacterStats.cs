using UnityEngine;

public sealed class NetworkCharacterStats : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxStamina = 100;
    [SerializeField, Min(0)] private int stamina = 100;

    public int Stamina => stamina;
    public bool TrySpendStamina(int amount)
    {
        if (amount < 0 || stamina < amount) return false;
        stamina -= amount;
        return true;
    }

    public void RestoreStamina(int amount) => stamina = Mathf.Clamp(stamina + Mathf.Max(0, amount), 0, maxStamina);
}
