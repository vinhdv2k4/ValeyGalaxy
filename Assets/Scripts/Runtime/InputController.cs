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

    public bool ConsumeAttack() => Consume(ref attackPressed);
    public bool ConsumeInteract() => Consume(ref interactPressed);
    public bool ConsumeInventory() => Consume(ref inventoryPressed);

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            move = Gamepad.current == null ? Vector2.zero : Gamepad.current.leftStick.ReadValue();
            return;
        }

        move = ReadMove(keyboard);
        attackPressed |= Pressed(Mouse.current?.leftButton) || Pressed(keyboard.spaceKey);
        interactPressed |= Pressed(Mouse.current?.rightButton) || Pressed(keyboard.eKey);
        inventoryPressed |= Pressed(keyboard.tabKey) || Pressed(keyboard.iKey);
        plantPressed |= Pressed(keyboard.fKey);

        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.01f)
                move = Gamepad.current.leftStick.ReadValue();

            attackPressed |= Pressed(Gamepad.current.buttonWest);
            interactPressed |= Pressed(Gamepad.current.buttonSouth);
            inventoryPressed |= Pressed(Gamepad.current.startButton);
        }
    }

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
