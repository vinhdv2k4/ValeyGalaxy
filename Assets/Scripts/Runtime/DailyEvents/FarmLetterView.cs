using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

internal sealed class FarmLetterView
{
    private const float Width = 1280f;
    private const float Height = 720f;
    private const float TextWidth = Width - 64f;
    private const int LinesPerPage = 10;
    private readonly Texture2D paper = Resources.Load<Texture2D>("Cutscenes/letterBG");
    private readonly Texture2D coloredFont;
    private readonly List<string> pages = new();
    private readonly bool wizardPaper;
    private readonly bool acceptOnClose;
    private readonly string actionLabel;
    private readonly int openedFrame = Time.frameCount;
    private Action acceptQuest;
    private int page;

    internal FarmLetterView(string body, bool wizardPaper, Action acceptQuest, string actionLabel, bool acceptOnClose)
    {
        this.wizardPaper = wizardPaper;
        this.acceptQuest = acceptQuest;
        this.actionLabel = actionLabel;
        this.acceptOnClose = acceptOnClose;
        if (wizardPaper) coloredFont = Resources.Load<Texture2D>("Fonts/font_colored");
        var lines = new List<string>();
        foreach (string paragraph in body.Split('^'))
        {
            string line = string.Empty;
            bool first = true;
            foreach (string word in paragraph.Split(' '))
            {
                string next = first ? word : line + " " + word;
                if (!first && NpcBitmapText.GetStringWidth(next) > TextWidth - 24f)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = next;
                first = false;
            }
            lines.Add(line);
        }
        for (int i = 0; i < lines.Count; i += LinesPerPage)
            pages.Add(string.Join("\n", lines.GetRange(i, Math.Min(LinesPerPage, lines.Count - i))));
    }

    internal void HandleInput()
    {
        if (openedFrame == Time.frameCount) return;
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true || gamepad?.buttonEast.wasPressedThisFrame == true)
            NpcConversation.Close();
        else if (keyboard?.leftArrowKey.wasPressedThisFrame == true || gamepad?.dpad.left.wasPressedThisFrame == true)
            TurnPage(-1);
        else if (keyboard?.rightArrowKey.wasPressedThisFrame == true || gamepad?.dpad.right.wasPressedThisFrame == true)
            TurnPage(1);
        else if (keyboard?.enterKey.wasPressedThisFrame == true || gamepad?.buttonSouth.wasPressedThisFrame == true)
        {
            if (page + 1 < pages.Count) TurnPage(1);
            else if (acceptQuest != null) AcceptQuest();
            else NpcConversation.Close();
        }
    }

    internal void AcceptQuest()
    {
        Action accept = acceptQuest;
        if (accept == null) return;
        acceptQuest = null;
        accept();
    }

    internal void Close()
    {
        if (acceptOnClose) AcceptQuest();
    }

    internal void Draw(Texture2D font)
    {
        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        float scale = Mathf.Min(1f, (Screen.width - 32f) / Width, (Screen.height - 32f) / Height);
        if (scale <= 0f) return;
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2f,
            (Screen.height - Height * scale) / 2f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        try
        {
            var bounds = new Rect(0, 0, Width, Height);
            var source = new Rect(wizardPaper ? 640 : 0, 0, 320, 180);
            if (paper != null)
                GUI.DrawTextureWithTexCoords(bounds, paper, StardewUiAtlas.ToUv(source, paper.width, paper.height));
            else StardewUiAtlas.DrawStardewDialogueBox(bounds);

            GUI.color = wizardPaper ? new Color(0f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.75f);
            NpcBitmapText.Draw(coloredFont != null ? coloredFont : font,
                new Rect(32, 32, TextWidth, Height - 128f), pages[page]);
            GUI.color = previousColor;

            if (DrawButton(new Rect(Width - 48f, 0, 48, 48), StardewUiAtlas.UpperRightCloseButtonRect))
            {
                NpcConversation.Close();
                return;
            }
            if (page > 0 && DrawButton(new Rect(32, Height - 96f, 48, 44), new Rect(352, 495, 12, 11)))
                TurnPage(-1);
            if (page + 1 < pages.Count &&
                DrawButton(new Rect(Width - 80f, Height - 96f, 48, 44), new Rect(365, 495, 12, 11)))
                TurnPage(1);
            if (acceptQuest != null && page == pages.Count - 1)
            {
                float buttonWidth = Mathf.Max(240f, NpcBitmapText.GetStringWidth(actionLabel, 2f) + 40f);
                var button = new Rect((Width - buttonWidth) * .5f, Height - 108f, buttonWidth, 56);
                StardewUiAtlas.DrawTextureBox(button, StardewUiAtlas.Cursors, new Rect(403, 373, 9, 9), Color.white);
                NpcBitmapText.Draw(font, new Rect(button.x, button.y + 11f, button.width, button.height),
                    actionLabel, 2f, centered: true);
                if (GUI.Button(button, GUIContent.none, GUIStyle.none)) AcceptQuest();
            }
            var input = Event.current;
            if (openedFrame != Time.frameCount && input.type == EventType.MouseUp && input.button == 0)
            {
                if (bounds.Contains(input.mousePosition)) TurnPage(1);
                else NpcConversation.Close();
                input.Use();
            }
        }
        finally
        {
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
    }

    private void TurnPage(int step)
    {
        int next = Mathf.Clamp(page + step, 0, pages.Count - 1);
        if (next == page) return;
        page = next;
        GameAudio.Play("shwip");
    }

    private static bool DrawButton(Rect bounds, Rect source)
    {
        StardewUiAtlas.DrawCursorsSprite(bounds, source);
        return GUI.Button(bounds, GUIContent.none, GUIStyle.none);
    }
}
