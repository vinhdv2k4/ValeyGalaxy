using System.Collections;
using UnityEngine;

public sealed class NetworkPlayerController : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CharacterMotor motor;
    [SerializeField] private PlayerFarmingInteractor farmingInteractor;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField, Min(0.05f)] private float toolActionDuration = 0.28f;

    private bool isUsingTool;

    private void Awake()
    {
        motor = motor == null ? GetComponent<CharacterMotor>() : motor;
        farmingInteractor = farmingInteractor == null ? GetComponent<PlayerFarmingInteractor>() : farmingInteractor;
        inventory = inventory == null ? GetComponent<PlayerInventory>() : inventory;
        if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (inputSourceBehaviour == null || motor == null) return;

        if (inputSourceBehaviour.ConsumeInventory())
            inventory.Toggle();

        motor.SetMovement(inventory.IsOpen || isUsingTool ? Vector2.zero : inputSourceBehaviour.move);
        if (!inventory.IsOpen && !isUsingTool && (inputSourceBehaviour.ConsumeAttack() || inputSourceBehaviour.ConsumeInteract()))
            UseSelectedItem();
    }

    private void UseSelectedItem()
    {
        var item = inventory.SelectedItem;
        if (item == null || item.amount <= 0 || farmingInteractor == null) return;

        switch (item.kind)
        {
            case PlayerItemKind.Hoe:
                StartCoroutine(UseHoe());
                break;
            case PlayerItemKind.Seeds:
                if (farmingInteractor.TryPlant(motor.Facing))
                    inventory.ConsumeSelected(PlayerItemKind.Seeds);
                break;
        }
    }

    private IEnumerator UseHoe()
    {
        isUsingTool = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        motor.PlayToolAnimation();

        yield return new WaitForSeconds(toolActionDuration * 0.5f);
        farmingInteractor.TryHoe(motor.Facing);
        yield return new WaitForSeconds(toolActionDuration * 0.5f);

        motor.IsMovementEnabled = true;
        isUsingTool = false;
    }
}
