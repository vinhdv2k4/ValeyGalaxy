using System.Collections;
using UnityEngine;

public sealed class NetworkPlayerController : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CharacterMotor motor;
    [SerializeField] private PlayerFarmingInteractor farmingInteractor;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerAnimationController playerAnimation;
    [SerializeField, Min(0.05f)] private float toolActionDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float toolSwitchCooldownDuration = 0.25f;

    private bool isUsingItem;
    private float toolSwitchCooldownTimer;

    private void Awake()
    {
        motor = motor == null ? GetComponent<CharacterMotor>() : motor;
        farmingInteractor = farmingInteractor == null ? GetComponent<PlayerFarmingInteractor>() : farmingInteractor;
        inventory = inventory == null ? GetComponent<PlayerInventory>() : inventory;
        playerAnimation = playerAnimation == null ? GetComponent<PlayerAnimationController>() : playerAnimation;
        if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.SelectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged(int newIndex)
    {
        toolSwitchCooldownTimer = toolSwitchCooldownDuration;
    }

    private void Update()
    {
        if (inputSourceBehaviour == null || motor == null) return;

        if (inputSourceBehaviour.ConsumeInventory())
            inventory.Toggle();

        if (toolSwitchCooldownTimer > 0f)
        {
            toolSwitchCooldownTimer -= Time.deltaTime;
            // Clear any buffered attack or interact during switch cooldown
            inputSourceBehaviour.ConsumeAttack();
            inputSourceBehaviour.ConsumeInteract();
        }

        motor.SetMovement(inventory.IsOpen || isUsingItem ? Vector2.zero : inputSourceBehaviour.move);
        if (!inventory.IsOpen && !isUsingItem && toolSwitchCooldownTimer <= 0f && (inputSourceBehaviour.ConsumeAttack() || inputSourceBehaviour.ConsumeInteract()))
            UseSelectedItem();
    }

    private void UseSelectedItem()
    {
        var item = inventory.SelectedItem;
        if (item == null || item.amount <= 0 || !item.hasUseAnimation) return;
        IItemUseBehavior behavior = ItemUseCatalog.Resolve(item);
        if (behavior != null) StartCoroutine(UseItem(behavior, item));
    }

    private IEnumerator UseItem(IItemUseBehavior behavior, InventorySlot item)
    {
        isUsingItem = true;
        inventory.IsLocked = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        float duration = Mathf.Max(toolActionDuration,
            playerAnimation == null ? 0f : playerAnimation.Play(behavior.UseAnimation));

        yield return new WaitForSeconds(duration * behavior.ImpactFraction);
        behavior.Apply(new ItemUseContext(transform, motor.Facing, item, inventory, farmingInteractor));
        yield return new WaitForSeconds(duration * (1f - behavior.ImpactFraction));

        motor.IsMovementEnabled = true;
        inventory.IsLocked = false;
        isUsingItem = false;
    }
}
