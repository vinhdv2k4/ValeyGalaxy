using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the local farmer's input into actions. Split by concern: this file reads input and
/// decides what may happen this frame; NetworkPlayerController.ItemUse uses tools, seeds, food
/// and rewards; NetworkPlayerController.Night handles sleeping, passing out and mine knockouts.
/// </summary>
public sealed partial class NetworkPlayerController : MonoBehaviour
{
    public InputController inputSourceBehaviour;
    public CharacterMotor motor;
    [SerializeField] private PlayerFarmingInteractor farmingInteractor;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerAnimationController playerAnimation;
    [SerializeField] private FishingController fishingController;
    [SerializeField] private NetworkCharacterStats characterStats;
    [SerializeField] private Texture2D fishingCursorAtlas;
    [SerializeField] private Texture2D fishingAnimationsAtlas;
    [SerializeField, Min(0.05f)] private float toolActionDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float toolSwitchCooldownDuration = 0.25f;

    private bool isUsingItem;
    private bool farmActionPending;
    private bool isFishing => fishingController != null && fishingController.IsFishing;
    private float toolSwitchCooldownTimer;
    private OnlineGameState onlineGame;
    private DailyEventPlayer dailyEvents;
    private CutsceneDirector cutscenes;
    private bool sleepPrompt;
    private bool endingDay;
    private bool wasOnBed;
    private NightResultsMenu nightResults;

    private void Awake()
    {
        motor = motor == null ? GetComponent<CharacterMotor>() : motor;
        farmingInteractor = farmingInteractor == null ? GetComponent<PlayerFarmingInteractor>() : farmingInteractor;
        inventory = inventory == null ? GetComponent<PlayerInventory>() : inventory;
        playerAnimation = playerAnimation == null ? GetComponent<PlayerAnimationController>() : playerAnimation;
        characterStats = characterStats == null ? GetComponent<NetworkCharacterStats>() : characterStats;
        if (fishingController == null)
            fishingController = GetComponent<FishingController>() ?? gameObject.AddComponent<FishingController>();
        fishingController.Configure(fishingCursorAtlas, fishingAnimationsAtlas);
        if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.SelectionChanged += OnSelectionChanged;
        if (characterStats != null) characterStats.PassedOut += PassOutFromExhaustion;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.SelectionChanged -= OnSelectionChanged;
        if (characterStats != null) characterStats.PassedOut -= PassOutFromExhaustion;
    }

    private void OnSelectionChanged(int newIndex)
    {
        toolSwitchCooldownTimer = toolSwitchCooldownDuration;
        GameAudio.Play("toolSwap");
    }

    private void Update()
    {
        if (inputSourceBehaviour == null || motor == null) return;
        if (StartupSession.IsOpening)
        {
            inputSourceBehaviour.SuppressUseUntilReleased();
            return;
        }
#if UNITY_EDITOR
        var qa = EventQaPreview.For(motor);
        if (qa != null)
        {
            qa.HandleInput(inputSourceBehaviour);
            return;
        }
#endif
        if (onlineGame == null) onlineGame = GetComponent<OnlineGameState>();
        if (cutscenes == null && onlineGame != null) cutscenes = GetComponent<CutsceneDirector>();
        if (cutscenes?.Service.IsBusy == true)
        {
            inputSourceBehaviour.SuppressUseUntilReleased();
            cutscenes.HandleSkipInput();
            return;
        }
        if (endingDay) return;
        var location = LocationManager.Instance;
        if (dailyEvents == null && onlineGame != null && location?.IsLocalFarmer(motor) == true)
            dailyEvents = new DailyEventPlayer(onlineGame, inventory, motor, fishingController);
        bool showingCatch = fishingController?.IsShowingCatch == true;
        if (location?.IsWarping != true && !isUsingItem && !farmActionPending && !showingCatch &&
            FarmDayClock.Instance?.TimeOfDay >= 2600)
        {
            StartNight(true);
            return;
        }
        if (sleepPrompt)
        {
            motor.SetMovement(Vector2.zero);
            inputSourceBehaviour.SuppressUseUntilReleased();
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true) sleepPrompt = false;
            else if (Keyboard.current?.enterKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonSouth.wasPressedThisFrame == true) StartNight(false);
            return;
        }
        // A farmhouse chest or any modal menu owns input: stand still and ignore world clicks.
        if ((onlineGame != null && onlineGame.IsChestInteractionOpen) || GameUi.IsModalOpen)
        {
            motor.SetMovement(Vector2.zero);
            inputSourceBehaviour.SuppressUseUntilReleased();
            return;
        }

        if (inputSourceBehaviour.ConsumeInventory() && !showingCatch)
        {
            inventory.Toggle();
            inputSourceBehaviour.SuppressUseUntilReleased();
            return;
        }

        if (inventory.IsOpen || FullInventoryMenuUI.IsOpen)
        {
            motor.SetMovement(Vector2.zero);
            inputSourceBehaviour.SuppressUseUntilReleased();
            return;
        }

        if (location?.IsWarping == true) return;
        if (dailyEvents?.IsBusy == true)
        {
            motor.SetMovement(Vector2.zero);
            inputSourceBehaviour.SuppressUseUntilReleased();
            return;
        }
        if (!this.isFishing && !isUsingItem && !farmActionPending && dailyEvents != null)
        {
            if (cutscenes?.TryStart() == true) return;
            if (Keyboard.current?.jKey.wasPressedThisFrame == true)
            {
                dailyEvents.OpenJournal();
                motor.SetMovement(Vector2.zero);
                return;
            }
            if (dailyEvents.Tick()) return;
        }
        bool onBed = FarmHouseFurnishings.IsBedRow(location?.CurrentLocation, motor.StandingPosition);
        if (onBed && !wasOnBed && inputSourceBehaviour.move.sqrMagnitude > 0.01f &&
            !isUsingItem && !farmActionPending)
        {
            wasOnBed = true;
            OpenSleepPrompt();
            return;
        }
        wasOnBed = onBed;

        toolSwitchCooldownTimer = Mathf.Max(0f, toolSwitchCooldownTimer - Time.deltaTime);

        bool isFishing = fishingController != null && fishingController.State != FishingController.FishingState.Inactive;
        bool isUiBlocking = inventory.IsOpen || isUsingItem || farmActionPending || isFishing ||
            GameUi.IsModalOpen;
        motor.SetMovement(isUiBlocking ? Vector2.zero : inputSourceBehaviour.move);
        InventorySlot selectedItem = inventory.SelectedItem;
        playerAnimation?.UpdateCarryLocomotion(motor.Movement,
            selectedItem != null && selectedItem.amount > 0 && !PlayerAppearance.IsTool(selectedItem));
        if (isFishing)
        {
            if (inputSourceBehaviour.ConsumeUse(out _) &&
                fishingController.State != FishingController.FishingState.Charging)
                fishingController.NotifyActionPressed();
            return;
        }
        if (!isUiBlocking &&
            inputSourceBehaviour.ConsumeUse(out var pointer, out bool secondary, out bool interaction))
            UseSelectedItem(pointer, secondary, interaction);
        else if (!isUiBlocking && !plantPending && selectedItem != null && selectedItem.kind == PlayerItemKind.Seeds &&
                 inputSourceBehaviour.IsUseHeld)
            PlantWhileHeld();
    }

    internal void MarkMorningArrival() => wasOnBed = true;
}
