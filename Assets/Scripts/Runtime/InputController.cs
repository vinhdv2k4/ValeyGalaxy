using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class InputController : MonoBehaviour
{
    public Vector2 move;
    public bool plantPressed;
    public bool attackPressed;
    public bool interactPressed;

    private bool inventoryPressed;
    private Vector2? actionPointer;
    private bool secondaryUse;
    private bool suppressUseUntilRelease;

    public void SuppressUseUntilReleased()
    {
        suppressUseUntilRelease = true;
        attackPressed = false;
        interactPressed = false;
        inventoryPressed = false;
        plantPressed = false;
        actionPointer = null;
        secondaryUse = false;
    }

    public static void SuppressWorldUseAfterModal()
    {
        foreach (var input in FindObjectsByType<InputController>(FindObjectsSortMode.None))
            input.SuppressUseUntilReleased();
    }

    public bool ConsumeAttack() => Consume(ref attackPressed);
    public bool ConsumeInteract() => Consume(ref interactPressed);
    public bool ConsumeInventory() => Consume(ref inventoryPressed);
    public bool ConsumeUse(out Vector2? pointer)
        => ConsumeUse(out pointer, out _);

    public bool ConsumeUse(out Vector2? pointer, out bool secondary) => ConsumeUse(out pointer, out secondary, out _);

    internal bool ConsumeUse(out Vector2? pointer, out bool secondary, out bool interaction)
    {
        if (suppressUseUntilRelease)
        {
            SuppressUseUntilReleased();
            pointer = null;
            secondary = false;
            interaction = false;
            return false;
        }
        bool interacted = Consume(ref interactPressed);
        bool pressed = Consume(ref attackPressed) || interacted;
        pointer = pressed ? actionPointer : null;
        secondary = pressed && secondaryUse;
        interaction = interacted || secondary;
        actionPointer = null;
        secondaryUse = false;
        return pressed;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        move = keyboard == null ? Vector2.zero : ReadMove(keyboard);

        if (keyboard != null)
        {
            inventoryPressed |= Pressed(keyboard.tabKey) || Pressed(keyboard.iKey);
            plantPressed |= Pressed(keyboard.fKey);
        }
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.01f)
                move = Gamepad.current.leftStick.ReadValue();
            inventoryPressed |= Pressed(Gamepad.current.startButton);
        }

        if (suppressUseUntilRelease)
        {
            attackPressed = false;
            interactPressed = false;
            actionPointer = null;
            secondaryUse = false;
            if (!IsAnyUseButtonHeld()) suppressUseUntilRelease = false;
            return;
        }

        // Decorative HUD images should not block the world, but interactive slots and
        // modal menus should block both mouse buttons.
        bool isPointerOverUI = Mouse.current != null &&
            StardewCursorManager.BlocksWorldInput(Mouse.current.position.ReadValue());
        if (!isPointerOverUI)
        {
            bool clickedRight = Pressed(Mouse.current?.rightButton);
            bool clickedLeft = Pressed(Mouse.current?.leftButton);
            bool clicked = clickedLeft || clickedRight;
            if (clicked)
            {
                attackPressed = true;
                secondaryUse = clickedRight;
                if (Camera.main != null)
                    actionPointer = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            }
        }

        if (keyboard != null)
        {
            attackPressed |= Pressed(keyboard.spaceKey);
            interactPressed |= Pressed(keyboard.eKey);
        }

        if (Gamepad.current != null)
        {
            attackPressed |= Pressed(Gamepad.current.buttonWest);
            interactPressed |= Pressed(Gamepad.current.buttonSouth);
        }
    }

    /// <summary>True while a use button is still down, for charging the Hoe and Watering Can.</summary>
    internal bool IsUseHeld => IsAnyUseButtonHeld();

    private static bool IsAnyUseButtonHeld() =>
        Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed) ||
        Keyboard.current != null && (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed) ||
        Gamepad.current != null && (Gamepad.current.buttonWest.isPressed || Gamepad.current.buttonSouth.isPressed);

    private static Vector2 ReadMove(Keyboard keyboard)
    {
        var value = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
        return value.sqrMagnitude > 1f ? value.normalized : value;
    }

    private static bool Pressed(ButtonControl control) => control != null && control.wasPressedThisFrame;
    private static bool Consume(ref bool value)
    {
        if (!value) return false;
        value = false;
        return true;
    }
}
