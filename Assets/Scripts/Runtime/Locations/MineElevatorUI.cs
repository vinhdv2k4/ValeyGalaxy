using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Draws the mine elevator panel and its floor buttons from the UI atlases.
/// </summary>
public sealed class MineElevatorUI : MonoBehaviour
{
    private static MineElevatorUI instance;
    public static bool IsOpen => instance != null && instance.enabled && instance.isOpen;
    private static readonly Color FloorNumberGold = new Color32(255, 215, 0, 255);
    private static readonly Color CurrentFloorGray = new(0.5f, 0.5f, 0.5f, 0.75f);

    private bool isOpen;
    private Action<int> onSelectFloor;
    private int currentFloor;
    private int openedFrame;
    private int stopCount, selection;
    private bool keyboardSelection;
    private int Columns => Mathf.Min(5, stopCount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance == null)
        {
            var go = new GameObject("MineElevatorUI");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MineElevatorUI>();
            instance.enabled = true;
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    internal static void Open(int currentFloorLevel, int lastStop, Action<int> onFloorChosen)
    {
        if (lastStop < 5) return;
        GameAudio.Play("crystal");
        EnsureInstance();
        instance.currentFloor = currentFloorLevel;
        instance.stopCount = Mathf.Clamp(lastStop, 5, 120) / 5 + 1;
        instance.selection = 0;
        instance.keyboardSelection = false;
        instance.openedFrame = Time.frameCount;
        instance.onSelectFloor = onFloorChosen;
        instance.isOpen = true;
        instance.enabled = true;
    }

    public static void Close()
    {
        if (instance == null) return;
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
        instance.isOpen = false;
    }

    private void Update()
    {
        if (!isOpen || Time.frameCount == openedFrame) return;

        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true || keyboard?.eKey.wasPressedThisFrame == true ||
            pad?.buttonEast.wasPressedThisFrame == true)
        {
            Close();
            return;
        }
        if (keyboard?.leftArrowKey.wasPressedThisFrame == true || pad?.dpad.left.wasPressedThisFrame == true) MoveSelection(-1);
        if (keyboard?.rightArrowKey.wasPressedThisFrame == true || pad?.dpad.right.wasPressedThisFrame == true) MoveSelection(1);
        if (keyboard?.upArrowKey.wasPressedThisFrame == true || pad?.dpad.up.wasPressedThisFrame == true) MoveSelection(-Columns);
        if (keyboard?.downArrowKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true) MoveSelection(Columns);
        if (keyboard?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
            SelectFloor(selection * 5);
    }

    private void MoveSelection(int step)
    {
        keyboardSelection = true;
        selection = Mathf.Clamp(selection + step, 0, stopCount - 1);
    }

    private void SelectFloor(int floor)
    {
        if (floor == currentFloor) return;
        GameAudio.Play("smallSelect");
        Close();
        onSelectFloor?.Invoke(floor);
    }

    private void OnGUI()
    {
        if (!isOpen) return;

        // 1. Dim background
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        // 2. Menu Dimensions & Position
        float menuWidth = 100f + Columns * 44f;
        float menuHeight = 100f + Mathf.CeilToInt((float)stopCount / Columns) * 44f;
        float menuX = Mathf.Floor((Screen.width - menuWidth) * 0.5f);
        float menuY = Mathf.Floor((Screen.height - menuHeight) * 0.5f);
        Rect menuRect = new(menuX, menuY, menuWidth, menuHeight);

        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;
        Rect closeBtnRect = new(menuRect.xMax - 56f, menuRect.y - 16f, 48f, 48f);
        bool isCloseHovered = closeBtnRect.Contains(mousePos);
        bool clicked = e.type == EventType.MouseDown && e.button == 0;
        if (clicked && (isCloseHovered || !menuRect.Contains(mousePos)))
        {
            Close();
            e.Use();
            return;
        }

        // 3. Draw Dialogue Box Frame (9-slice)
        StardewUiAtlas.DrawGameMenuFrame(menuRect);

        // 4. Upper Right Close Button ('X')
        StardewUiAtlas.DrawCursorsSprite(closeBtnRect, StardewUiAtlas.UpperRightCloseButtonRect,
            isCloseHovered ? new Color(1f, 0.9f, 0.9f, 1f) : Color.white);

        // 5. Draw 5x5 Grid of Elevator Buttons
        int cols = Columns;
        const float btnSize = 40f;
        const float pitch = 44f;
        float gridStartX = menuX + 52f;
        float gridStartY = menuY + 45f;

        for (int i = 0; i < stopCount; i++)
        {
            int floor = i * 5;
            int col = i % cols;
            int row = i / cols;

            float bx = gridStartX + col * pitch;
            float by = gridStartY + row * pitch;
            Rect btnRect = new(bx, by, btnSize, btnSize);

            bool isHovered = btnRect.Contains(mousePos);
            bool isCurrent = floor == currentFloor;

            Rect sourceRect = isHovered || keyboardSelection && i == selection
                ? StardewUiAtlas.ElevatorButtonHover : StardewUiAtlas.ElevatorButtonNormal;
            Rect shadowRect = new(bx - 4f, by + 4f, btnSize, btnSize);
            StardewUiAtlas.DrawCursorsSprite(shadowRect, sourceRect, new Color(0f, 0f, 0f, 0.5f));
            StardewUiAtlas.DrawCursorsSprite(btnRect, sourceRect, Color.white);
            DrawFloorNumber(btnRect, floor, isCurrent ? CurrentFloorGray : FloorNumberGold);

            if (clicked && isHovered)
            {
                e.Use();
                SelectFloor(floor);
                return;
            }
        }
    }

    private static void DrawFloorNumber(Rect button, int floor, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;

        // MineElevatorMenu uses NumberSprite at 2x with a 12px advance.
        int digitCount = floor >= 100 ? 3 : floor >= 10 ? 2 : 1;
        float x = button.x + 8f + digitCount * 6f;
        float y = button.y + 14f;
        do
        {
            int digit = floor % 10;
            Rect source = new(512 + digit % 6 * 8, 128 + digit / 6 * 8, 8, 8);
            StardewUiAtlas.DrawCursorsSprite(new Rect(x, y, 16f, 16f), source, color);
            x -= 12f;
            floor /= 10;
        }
        while (floor > 0);
    }
}
