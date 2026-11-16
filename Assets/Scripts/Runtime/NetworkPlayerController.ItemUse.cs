using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Using the selected item: tools and charging, seeds, food, harvest and reward poses.</summary>
public sealed partial class NetworkPlayerController
{
    private bool plantPending;

    // Seeds go straight into the tilled tile with a dirtyHit and no farmer animation; walking
    // on or dragging the cursor while the button is held keeps planting each empty tile.
    private bool TryPlantSelectedSeed()
    {
        var item = inventory.SelectedItem;
        var seed = item?.itemData;
        if (plantPending || item == null || item.amount <= 0 || item.kind != PlayerItemKind.Seeds ||
            seed == null || onlineGame == null) return false;
        var plot = farmingInteractor?.PlantTarget(motor.Facing);
        if (plot == null || !plot.IsTilled || plot.IsPlanted) return false;
        plantPending = true;
        GameAudio.Play("dirtyHit");
        onlineGame.PlantFarmCrop(Mathf.FloorToInt(plot.transform.position.x),
            Mathf.FloorToInt(-plot.transform.position.y), inventory.SelectedIndex, seed.itemId, error =>
            {
                plantPending = false;
                if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);
            });
        return true;
    }

    private void PlantWhileHeld()
    {
        Vector2? pointer = null;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed && Camera.main != null)
            pointer = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        farmingInteractor?.SetPointerTarget(pointer);
        TryPlantSelectedSeed();
        farmingInteractor?.SetPointerTarget(null);
    }

    private void UseSelectedItem(Vector2? pointer, bool secondary, bool interaction)
    {
        if (GameUi.IsModalOpen) return;
        if (interaction && cutscenes?.TryInteract(pointer, inventory.SelectedItem?.itemData?.itemId) == true) return;
        if (dailyEvents?.TryInteract(pointer, interaction) == true) return;
        var house = LocationManager.Instance?.CurrentLocation;
        if (house?.LocationName == "FarmHouse")
        {
            var bed = FarmHouseFurnishings.BedSpot(house);
            Vector2 near = house.TileCenter(bed.x, bed.y);
            if (FarmHouseFurnishings.IsBedRow(house, motor.StandingPosition) ||
                (pointer.HasValue && FarmHouseFurnishings.IsBedRow(house, pointer.Value) &&
                    Vector2.Distance(motor.StandingPosition, near) <= 2f))
            {
                OpenSleepPrompt();
                return;
            }
        }
        if (PlacedObjectsView.TryHandle(pointer, interaction, motor.StandingPosition, motor.Facing, inventory))
        {
            farmingInteractor?.SetPointerTarget(null);
            return;
        }
        bool pickaxeSwing = !interaction && inventory.SelectedItem?.kind == PlayerItemKind.Pickaxe;
        if (!pickaxeSwing && MineManager.TryInteract(pointer, motor.StandingPosition, motor.Facing)) return;
        if (FarmShippingBin.TryInteract(pointer, motor.StandingPosition, motor.Facing,
                onlineGame, inventory)) return;
        if ((interaction || PlayerAppearance.IsTool(inventory.SelectedItem) == false) &&
            BeachBridge.TryInteract(pointer, motor.StandingPosition, motor.Facing))
        {
            motor.SetMovement(Vector2.zero);
            return;
        }
        if (!interaction && toolSwitchCooldownTimer > 0f && PlayerAppearance.IsTool(inventory.SelectedItem))
            return;
        if (interaction && FarmFireplace.TryInteract(pointer, motor.StandingPosition, motor.Facing))
            return;
        if (InteractableTV.TryInteract(pointer, motor.StandingPosition, motor.Facing))
            return;
        if (NpcConversation.TryGuildBoard(pointer, motor.StandingPosition, motor.Facing))
            return;
        if (ForageView.TryPick(pointer, motor.StandingPosition, motor.Facing))
        {
            motor.SetMovement(Vector2.zero);
            return;
        }
        if (JojaMorrisDesk.TryInteract(pointer, motor.StandingPosition, motor.Facing))
        {
            motor.SetMovement(Vector2.zero);
            return;
        }
        if (CalendarBillboard.TryOpen(pointer, motor.StandingPosition, motor.Facing))
        {
            motor.SetMovement(Vector2.zero);
            return;
        }
        // Farmer.checkAction on another farmer with an object in hand: offer it as a gift.
        if (!secondary && WorldPlayerGifts.TryOffer(pointer, motor, inventory))
            return;
        if (!secondary && NpcConversation.TryGiveGift(pointer, motor.StandingPosition,
                motor.Facing, motor, inventory, onlineGame))
            return;
        if ((secondary || !pointer.HasValue) &&
            NpcShop.TryOpen(pointer, motor.StandingPosition, motor.Facing, inventory))
            return;
        if (onlineGame != null && onlineGame.TryOpenChest(pointer, motor.StandingPosition))
            return;
        if ((secondary || !pointer.HasValue) && LocationManager.Instance != null &&
            LocationManager.Instance.TryUseDoor(pointer, motor.StandingPosition, motor.Facing))
            return;
        if ((secondary || !pointer.HasValue || IsAnimalTool(inventory.SelectedItem)) &&
            FarmRanchView.TryInteract(pointer, motor.StandingPosition, motor.Facing, inventory))
            return;
        if (NpcConversation.TryOpen(pointer, motor.StandingPosition, motor.Facing, motor))
            return;
        farmingInteractor?.SetPointerTarget(pointer);
        if (pointer.HasValue)
            motor.FaceTowards(pointer.Value - (Vector2)motor.StandingPosition);
        ItemData harvestItem = farmingInteractor == null ? null : farmingInteractor.HarvestItemAt(motor.Facing);
        if (harvestItem != null && farmingInteractor.RequestHarvest(motor.Facing, false, success =>
        {
            farmActionPending = false;
            if (success) StartCoroutine(ShowHarvest(harvestItem));
        }))
        {
            farmActionPending = true;
            farmingInteractor.SetPointerTarget(null);
            return;
        }
        // Right-click places the held object (or chest) once nothing on the tile answered.
        if (interaction && PlacedObjectsView.TryPlaceHeld(pointer, motor.StandingPosition, motor.Facing, inventory))
        {
            farmingInteractor?.SetPointerTarget(null);
            return;
        }
        var item = inventory.SelectedItem;
        if (item != null && item.amount > 0 && item.kind == PlayerItemKind.Seeds)
        {
            TryPlantSelectedSeed();
            farmingInteractor?.SetPointerTarget(null);
            return;
        }
        if (item != null && item.amount > 0 && item.itemData != null &&
            item.itemData.IsEdible && (secondary || !pointer.HasValue))
        {
            if (!inventory.IsLocked) StartCoroutine(ConsumeFood(item.itemData, inventory.SelectedIndex));
            farmingInteractor?.SetPointerTarget(null);
            return;
        }
        if (item == null || item.amount <= 0 || !item.hasUseAnimation)
        {
            farmingInteractor?.SetPointerTarget(null);
            return;
        }
        if (item.kind == PlayerItemKind.WateringCan && farmingInteractor != null &&
            !farmingInteractor.CanUseWateringCan(motor.Facing))
        {
            farmingInteractor.SetPointerTarget(null);
            return;
        }
        IItemUseBehavior behavior = ItemUseCatalog.Resolve(item);
        if (behavior != null)
        {
            Vector2 facingBeforeUse = motor.Facing;
            if (item.kind == PlayerItemKind.FishingRod)
            {
                if (fishingController != null)
                    fishingController.StartCharging(pointer, facingBeforeUse);
            }
            else
            {
                if (item.kind == PlayerItemKind.WateringCan)
                    farmingInteractor?.SyncWateringCan(item.itemData as ToolData);
                if (TryStartCharge(behavior, item)) return;
                float cost = item.itemData is ToolData tool
                    ? NetworkCharacterStats.ToolStaminaCost(tool.staminaCost, ToolSkillLevel(item.kind))
                    : item.kind == PlayerItemKind.Sword ? 0f : 2f;
                if (characterStats != null && !characterStats.TrySpendStamina(cost))
                {
                    NpcConversation.ShowNotice("Too exhausted to use this tool.");
                    farmingInteractor?.SetPointerTarget(null);
                    return;
                }
                StartCoroutine(UseItem(behavior, item));
            }
        }
        else farmingInteractor?.SetPointerTarget(null);
    }

    private ToolData chargingTool;
    private int chargePower;
    private float chargeProgress;

    // Only the Hoe and Watering Can charge: one level per 600 ms of holding (scaled by
    // AnimationSpeedModifier), up to the upgrade level.
    private bool TryStartCharge(IItemUseBehavior behavior, InventorySlot item)
    {
        if (item.kind is not (PlayerItemKind.Hoe or PlayerItemKind.WateringCan) ||
            item.itemData is not ToolData tool || tool.upgradeLevel < 1 || farmingInteractor == null ||
            inputSourceBehaviour == null || !inputSourceBehaviour.IsUseHeld) return false;
        StartCoroutine(ChargeAndUse(behavior, item, tool));
        return true;
    }

    private IEnumerator ChargeAndUse(IItemUseBehavior behavior, InventorySlot item, ToolData tool)
    {
        isUsingItem = true;
        inventory.IsLocked = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        chargingTool = tool;
        chargePower = 0;
        chargeProgress = 0f;
        // The farmer holds the tool raised (frame 0) while it charges, sinking with each level.
        if (playerAnimation != null)
        {
            playerAnimation.ToolPower = 0;
            playerAnimation.HoldUseStart(behavior.UseAnimation);
        }
        float perLevel = ToolArea.HoldPerPowerLevelSeconds * Mathf.Max(0.1f, tool.animationSpeedModifier);
        while (inputSourceBehaviour != null && inputSourceBehaviour.IsUseHeld)
        {
            bool canCharge = chargePower < tool.upgradeLevel &&
                (characterStats == null || characterStats.ExactStamina > 0f);
            if (canCharge)
            {
                chargeProgress += Time.deltaTime / perLevel;
                if (chargeProgress >= 1f)
                {
                    chargeProgress = 0f;
                    chargePower++;
                    if (playerAnimation != null) playerAnimation.ToolPower = chargePower;
                    GameAudio.Play("toolCharge");
                }
            }
            else chargeProgress = 0f;
            yield return null;
        }
        int power = chargePower;
        chargingTool = null;
        motor.IsMovementEnabled = true;
        inventory.IsLocked = false;
        isUsingItem = false;

        if (characterStats != null && !characterStats.TrySpendStamina(
                NetworkCharacterStats.ToolStaminaCost(ToolArea.StaminaCost(power), ToolSkillLevel(item.kind))))
        {
            if (playerAnimation != null)
            {
                playerAnimation.ToolPower = 0;
                playerAnimation.StopAction();
            }
            NpcConversation.ShowNotice("Too exhausted to use this tool.");
            farmingInteractor?.SetPointerTarget(null);
            yield break;
        }
        farmingInteractor.ChargePower = power;
        StartCoroutine(UseItem(behavior, item));
    }

    // Charge gauge above the farmer, coloured by the next level's metal.
    private void DrawChargeGauge()
    {
        if (chargingTool == null || chargePower >= chargingTool.upgradeLevel || Event.current.type != EventType.Repaint)
            return;
        Camera view = Camera.main;
        if (view == null) return;
        Vector3 head = view.WorldToScreenPoint(transform.position + Vector3.up * 2.1f);
        float unit = Mathf.Abs(view.WorldToScreenPoint(transform.position + Vector3.right).x -
            view.WorldToScreenPoint(transform.position).x);
        float width = unit * 0.75f, height = Mathf.Max(3f, unit * 0.125f);
        var frame = new Rect(head.x - width * 0.5f - 2f, Screen.height - head.y - 2f, width + 4f, height + 4f);
        Color previous = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(frame, Texture2D.whiteTexture);
        GUI.color = ToolArea.PowerColors[Mathf.Clamp(chargePower + 1, 1, 4)];
        GUI.DrawTexture(new Rect(frame.x + 2f, frame.y + 2f, width * chargeProgress, height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>The skill whose level lowers a tool's energy cost (Hoe/WateringCan: Farming, Axe: Foraging, Pickaxe: Mining).</summary>
    private int ToolSkillLevel(PlayerItemKind kind)
    {
        int[] levels = onlineGame != null ? onlineGame.Snapshot?.skillLevels : null;
        int skill = kind switch
        {
            PlayerItemKind.Hoe or PlayerItemKind.WateringCan or PlayerItemKind.Shears or PlayerItemKind.MilkPail => 0,
            PlayerItemKind.Axe => 2,
            PlayerItemKind.Pickaxe => 3,
            _ => -1
        };
        if (skill < 0 || levels == null || skill >= levels.Length) return 0;
        int buff = skill switch
        {
            0 => characterStats?.FarmingBonus ?? 0,
            2 => characterStats?.ForagingBonus ?? 0,
            _ => characterStats?.MiningBonus ?? 0
        };
        return levels[skill] + buff;
    }

    internal void PresentReward(Sprite icon, string message, string cue = "getNewSpecialItem", string followUp = null)
    {
        StartCoroutine(ShowReward(icon, message, cue, followUp));
    }

    private IEnumerator ShowReward(Sprite icon, string message, string cue, string followUp)
    {
        while (isUsingItem || farmActionPending || isFishing || endingDay || sleepPrompt ||
            LocationManager.Instance?.IsWarping == true || cutscenes?.Service.IsBusy == true ||
            dailyEvents?.IsBusy == true || onlineGame?.IsChestInteractionOpen == true ||
            GameUi.IsMenuOrInventoryOpen || inventory.IsOpen)
            yield return null;

        isUsingItem = true;
        var appearance = GetComponent<PlayerAppearance>();
        appearance?.ShowRewardPose(icon);
        GameAudio.Play(cue);
        NpcConversation.ShowNotice(message);
        while (NpcConversation.IsOpen) yield return null;

        appearance?.ClearDisplayedItem();
        playerAnimation?.StopAction();
        if (!string.IsNullOrEmpty(followUp))
        {
            NpcConversation.ShowNotice(followUp);
            while (NpcConversation.IsOpen) yield return null;
        }
        isUsingItem = false;
    }

    private IEnumerator ShowHarvest(ItemData harvested)
    {
        GameAudio.Play("harvest");
        isUsingItem = true;
        inventory.IsLocked = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        var appearance = GetComponent<PlayerAppearance>();
        float harvestDuration = playerAnimation == null ? 0.42f : playerAnimation.Play(FarmerAnimationAction.Harvest);
        yield return new WaitForSeconds(Mathf.Max(0.42f, harvestDuration));
        playerAnimation?.Play(FarmerAnimationAction.ShowHoldingEdible);
        appearance?.ShowHarvestItem(harvested, 0.85f);
        yield return new WaitForSeconds(0.85f);
        playerAnimation?.StopAction();
        motor.IsMovementEnabled = true;
        inventory.IsLocked = false;
        isUsingItem = false;
    }

    private IEnumerator ConsumeFood(ItemData food, int slot)
    {
        isUsingItem = true;
        inventory.IsLocked = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        bool consumed = false;
        string error = null;
        if (onlineGame != null)
        {
            bool finished = false;
            onlineGame.ConsumeFood(slot, food.itemId, result =>
            {
                error = result;
                consumed = string.IsNullOrEmpty(result);
                inventory.IsLocked = true;
                finished = true;
            });
            while (!finished) yield return null;
        }
        else error = "Đăng nhập để sử dụng thực phẩm.";

        if (consumed)
        {
            GameAudio.Play(food.isDrink ? "gulp" : "eat");
            // Eating faces down, holds the item up, then plays Eat/Drink.
            motor.FaceTowards(Vector2.down);
            playerAnimation?.Play(FarmerAnimationAction.ShowHoldingEdible);
            GetComponent<PlayerAppearance>()?.ShowHarvestItem(food, 0.42f);
            yield return new WaitForSeconds(0.42f);
            float duration = playerAnimation == null ? 0.8f :
                playerAnimation.Play(food.isDrink ? FarmerAnimationAction.Drink : FarmerAnimationAction.Eat);
            yield return new WaitForSeconds(Mathf.Max(0.65f, duration));
            characterStats?.ApplyFood(food);
            playerAnimation?.StopAction();
        }
        else if (!string.IsNullOrEmpty(error)) NpcConversation.ShowNotice(error);

        motor.IsMovementEnabled = true;
        inventory.IsLocked = false;
        isUsingItem = false;
    }

    private IEnumerator UseItem(IItemUseBehavior behavior, InventorySlot item)
    {
        isUsingItem = true;
        inventory.IsLocked = true;
        motor.IsMovementEnabled = false;
        motor.SetMovement(Vector2.zero);
        float duration = Mathf.Max(toolActionDuration,
            playerAnimation == null ? 0f : playerAnimation.Play(behavior.UseAnimation));
        if (item.kind == PlayerItemKind.Sword) StartCoroutine(SwordLunge(item, duration));

        // Hoe, axe and pickaxe land on frame 3 of the swing: 230 ms facing down, 180 ms otherwise.
        float impact = item.kind is PlayerItemKind.Hoe or PlayerItemKind.Axe or PlayerItemKind.Pickaxe
            ? (motor.Facing.y < -0.5f ? 230f : 180f) / GenericToolVisualStrategy.ClipMilliseconds
            : behavior.ImpactFraction;
        yield return new WaitForSeconds(duration * impact);
        if (item.kind == PlayerItemKind.Sword)
        {
            GameAudio.Play("swordswipe");
            MineMonsterSpawner.DamageMonstersInFront(motor, item.itemData, characterStats);
        }
        behavior.Apply(new ItemUseContext(transform, motor.Facing, item, inventory, farmingInteractor));
        yield return new WaitForSeconds(duration * (1f - impact));

        motor.IsMovementEnabled = true;
        farmingInteractor?.SetPointerTarget(null);
        inventory.IsLocked = false;
        if (playerAnimation != null) playerAnimation.ToolPower = 0;
        isUsingItem = false;
    }

    /// <summary>
    /// Farmer.showSwordSwipe: the swing steps the farmer forward as frame 1 starts (55 ms of
    /// 275) and back a little on frame 5 (175 ms). Daggers lean in first, then pull back.
    /// </summary>
    private IEnumerator SwordLunge(InventorySlot item, float duration)
    {
        Vector2 forward = motor.Facing;
        bool dagger = item.itemData is MeleeWeaponData { weaponType: MeleeWeaponType.Dagger };
        float scale = duration / 0.275f;
        if (dagger) motor.Nudge(forward * 0.6f);
        yield return new WaitForSeconds(0.055f * scale);
        motor.Nudge(forward * (dagger ? -0.5f : 0.5f));
        yield return new WaitForSeconds(0.12f * scale);
        motor.Nudge(forward * -0.3f);
    }
    private IEnumerator PlayDelayedAudio(string cue, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameAudio.Play(cue);
    }

    private static bool IsAnimalTool(InventorySlot slot) =>
        slot?.itemData?.itemId is "tool_MilkPail" or "tool_Shears";
}
