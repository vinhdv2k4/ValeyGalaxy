using System.Collections.Generic;
using UnityEngine;

public sealed class NpcConversation : MonoBehaviour
{
    private static NpcConversation instance;
    private NpcActor actor;
    private string speaker;
    private List<NpcDialoguePage> pages;
    private int pageIndex;
    private Texture2D frameTexture;
    private Texture2D fontTexture;
    private ToolbarUI toolbar;
    private bool toolbarWasActive;
    private float pageStartedAt;
    private bool revealAll;
    private IReadOnlyList<(string Label, System.Action Choose)> choices;
    private string choiceTitle;
    private Vector2 choiceScroll;
    private GUIStyle choiceHeading;
    private GUIStyle unicodeDialogue;
    private System.Action<string> submitText;
    private string entryText;
    private Sprite portraitOverride;
    private UnityEngine.InputSystem.Keyboard entryKeyboard;
    private int choiceSelection;
    private bool serviceChoices;
    private FarmLetterView letter;

    public static bool IsOpen => instance != null && instance.enabled;

    public static bool TryOpen(Vector2? pointer, Vector3 playerPosition, Vector2 facing, CharacterMotor player)
    {
        NpcActor closest = FindClosest(pointer, playerPosition, facing);
        if (closest == null) return false;
        player.FaceTowards((Vector2)closest.transform.position - (Vector2)playerPosition);
        closest.FaceTowards((Vector2)playerPosition - (Vector2)closest.transform.position);
        Show(closest, closest.DisplayName, closest.Talk(FarmDayClock.Instance?.Day ?? 1));
        return true;
    }

    public static bool TryGiveGift(Vector2? pointer, Vector3 playerPosition, Vector2 facing,
                                   CharacterMotor player, PlayerInventory inventory,
                                   OnlineGameState online)
    {
        if (!pointer.HasValue || inventory == null || online == null) return false;
        InventorySlot selected = inventory.SelectedItem;
        if (selected == null || selected.amount < 1 || selected.itemData == null ||
            PlayerAppearance.IsTool(selected) || selected.kind == PlayerItemKind.Seeds) return false;
        NpcActor closest = FindClosest(pointer, playerPosition, facing);
        if (closest == null) return false;
        player.FaceTowards((Vector2)closest.transform.position - (Vector2)playerPosition);
        closest.FaceTowards((Vector2)playerPosition - (Vector2)closest.transform.position);
        online.GiveNpcGift(closest.DisplayName, inventory.SelectedIndex,
            selected.itemData.itemId, error =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    GameAudio.Play("give_gift");
                    ShowNotice(closest.DisplayName + " đã nhận quà.");
                }
                else ShowNotice(error);
            });
        return true;
    }

    internal static NpcActor FindClosest(Vector2? pointer, Vector3 playerPosition, Vector2 facing)
    {
        NpcActor closest = null;
        float bestDistance = 2.4f * 2.4f;
        foreach (NpcActor npc in FindObjectsByType<NpcActor>(FindObjectsSortMode.None))
        {
            if (!npc.IsVisible) continue;
            Vector2 offset = (Vector2)npc.transform.position - (Vector2)playerPosition;
            float distance = offset.sqrMagnitude;
            if (distance > bestDistance) continue;
            if (pointer.HasValue)
            {
                if (!npc.ContainsPointer(pointer.Value)) continue;
            }
            else if (Vector2.Dot(offset.normalized, facing) < 0.35f) continue;

            closest = npc;
            bestDistance = distance;
        }

        return closest;
    }

    public static event System.Action Closed;

    // OnGUI runs several times a frame: look a speaker's portrait sheet up once per speaker.
    private static NpcCatalog portraitCatalog;
    private string portraitSpeaker;
    private Sprite portraitSheet;

    private Sprite SpeakerPortraitSheet(string name)
    {
        if (name == portraitSpeaker) return portraitSheet;
        portraitSpeaker = name;
        if (portraitCatalog == null) portraitCatalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (portraitCatalog == null) return portraitSheet = null;
        if (name == "Gil") return portraitSheet = portraitCatalog.gilPortrait;
        NpcDefinition definition = name == "Marlon" ? portraitCatalog.marlon : null;
        if (definition == null && portraitCatalog.characters != null)
            foreach (var character in portraitCatalog.characters)
                if (character != null && string.Equals(character.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    definition = character;
                    break;
                }
        return portraitSheet = definition?.portrait;
    }

    internal static bool TryGuildBoard(Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        if (LocationManager.Instance?.CurrentLocation?.LocationName != "AdventureGuild") return false;
        if (NpcShop.HitActionTile(pointer, standing, facing, 8, 10))
        {
            ShowPages(string.Empty, new List<NpcDialoguePage>
            {
                new("Monster Eradication Goals\nSlimes 0/1000     Void Spirits 0/150\nBats 0/200     Skeletons 0/50\nCave Insects 0/125     Duggies 0/30", 0),
                new("Dust Sprites 0/500     Rock Crabs 0/60\nMummies 0/100     Pepper Rex 0/50\nSerpents 0/250     Magma Sprites 0/150", 0),
                new("Monster defeats are not recorded in this version. Gil can only award completed goals.", 0)
            });
            return true;
        }
        if (!NpcShop.HitActionTile(pointer, standing, facing, 11, 12) &&
            !NpcShop.HitActionTile(pointer, standing, facing, 12, 12)) return false;
        ShowPages("Gil", new List<NpcDialoguePage> { new("Come back when you've got something to show me, kid.", 0) });
        return true;
    }

    public static void ShowNotice(string message)
    {
        Show(null, string.Empty, new List<NpcDialoguePage> { new NpcDialoguePage(message, 0) });
    }

    public static void ShowPages(string speakerName, List<NpcDialoguePage> dialoguePages)
    {
        Show(null, speakerName, dialoguePages);
    }

    internal static void ShowLetter(string body, bool wizardPaper, System.Action acceptQuest,
        string actionLabel = "Accept Quest", bool acceptOnClose = true)
    {
        Show(null, string.Empty, null, "shwip");
        instance.letter = new FarmLetterView(body, wizardPaper, acceptQuest, actionLabel, acceptOnClose);
        InputController.SuppressWorldUseAfterModal();
    }

    internal static void ShowCutscenePage(string speaker, string text, int portrait, Sprite sprite)
    {
        Show(null, speaker, new List<NpcDialoguePage> { new(text, portrait) });
        instance.portraitOverride = sprite;
    }

    internal static void ShowTextEntry(string title, string initial, System.Action<string> submitted)
    {
        ShowChoices(title, System.Array.Empty<(string, System.Action)>());
        instance.entryText = initial;
        instance.submitText = submitted;
        instance.entryKeyboard = UnityEngine.InputSystem.Keyboard.current;
        if (instance.entryKeyboard != null) instance.entryKeyboard.onTextInput += instance.OnNameCharacter;
    }

    private void OnNameCharacter(char character)
    {
        if (entryText.Length < 20 && !char.IsControl(character)) entryText += character;
    }

    private void StopNameInput()
    {
        if (entryKeyboard != null) entryKeyboard.onTextInput -= OnNameCharacter;
        entryKeyboard = null;
    }

    internal static void ShowChoices(string title, IReadOnlyList<(string Label, System.Action Choose)> options)
    {
        Show(null, string.Empty, null);
        instance.choiceTitle = title;
        instance.choiceScroll = Vector2.zero;
        instance.choiceSelection = 0;
        instance.choices = options;
    }

    internal static void ShowServiceChoices(IReadOnlyList<(string Label, System.Action Choose)> options)
    {
        ShowChoices(string.Empty, options);
        instance.serviceChoices = true;
    }

    internal static void ShowServiceNotice(NpcActor keeper, string message) =>
        Show(keeper, keeper?.DisplayName ?? "Clint", new List<NpcDialoguePage> { new(message, 0) });

    private static void Show(NpcActor speakerActor, string name, List<NpcDialoguePage> dialoguePages,
        string openingCue = "dialogueCharacter")
    {
        GameAudio.Play(openingCue);
        if (instance == null)
            instance = new GameObject("NPC Conversation").AddComponent<NpcConversation>();
        instance.actor?.SetTalking(false);
        instance.actor = speakerActor;
        speakerActor?.SetTalking(true);
        instance.speaker = name;
        instance.pages = dialoguePages;
        instance.letter = null;
        instance.choices = null;
        instance.serviceChoices = false;
        instance.StopNameInput();
        instance.submitText = null;
        instance.portraitOverride = null;
        instance.pageIndex = 0;
        instance.pageStartedAt = Time.unscaledTime;
        instance.revealAll = false;
        NpcCatalog catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        instance.frameTexture = catalog?.dialogueFrame;
        instance.fontTexture = catalog?.dialogueFont;
        if (instance.toolbar == null)
        {
            instance.toolbar = FindAnyObjectByType<ToolbarUI>();
            instance.toolbarWasActive = instance.toolbar != null && instance.toolbar.gameObject.activeSelf;
            if (instance.toolbarWasActive) instance.toolbar.gameObject.SetActive(false);
        }
        instance.enabled = true;
    }

    public static void AdvanceOrClose()
    {
        if (instance == null || instance.choices != null) return;
        if (instance.letter != null) { Close(); return; }
        if (!instance.revealAll && instance.VisibleCharacterCount < instance.pages[instance.pageIndex].Text.Length)
        {
            instance.revealAll = true;
            return;
        }
        if (instance.pages != null && instance.pageIndex + 1 < instance.pages.Count)
        {
            GameAudio.Play("smallSelect");
            instance.pageIndex++;
            instance.pageStartedAt = Time.unscaledTime;
            instance.revealAll = false;
        }
        else
            Close();
    }

    public static void Close()
    {
        if (instance == null) return;
        var letter = instance.letter;
        instance.letter = null;
        GameAudio.Play(letter != null ? "shwip" : "dialogueCharacterClose");
        InputController.SuppressWorldUseAfterModal();
        instance.actor?.SetTalking(false);
        if (instance.toolbarWasActive && instance.toolbar != null)
            instance.toolbar.gameObject.SetActive(true);
        instance.toolbar = null;
        instance.toolbarWasActive = false;
        instance.StopNameInput();
        instance.enabled = false;
        Closed?.Invoke();
        letter?.Close();
    }

    private void OnDestroy()
    {
        StopNameInput();
        if (toolbarWasActive && toolbar != null) toolbar.gameObject.SetActive(true);
        if (instance == this) instance = null;
    }

    private int VisibleCharacterCount => revealAll ? pages[pageIndex].Text.Length :
        Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - pageStartedAt) * 35f),
            0, pages[pageIndex].Text.Length);

    private void Update()
    {
        if (letter != null)
        {
            letter.HandleInput();
            return;
        }
        if (serviceChoices)
        {
            UpdateServiceChoices();
            return;
        }
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (choices != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (submitText != null)
                {
                    if (keyboard.backspaceKey.wasPressedThisFrame && entryText.Length > 0)
                        entryText = entryText[..^1];
                    if (keyboard.enterKey.wasPressedThisFrame && !string.IsNullOrWhiteSpace(entryText))
                    {
                        var submit = submitText;
                        string name = entryText.Trim();
                        Close();
                        submit(name);
                    }
                }
                else if (choices.Count > 0)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame)
                        choiceSelection = (choiceSelection + choices.Count - 1) % choices.Count;
                    if (keyboard.downArrowKey.wasPressedThisFrame)
                        choiceSelection = (choiceSelection + 1) % choices.Count;
                    if (keyboard.enterKey.wasPressedThisFrame)
                    {
                        var choose = choices[choiceSelection].Choose;
                        Close();
                        choose();
                    }
                }
                return;
            }
            if (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.escapeKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.eKey.wasPressedThisFrame)
            {
                AdvanceOrClose();
            }
        }
    }

    private void OnGUI()
    {
        if (letter != null)
        {
            letter.Draw(fontTexture);
            return;
        }
        if (choices != null)
        {
            if (serviceChoices) DrawServiceChoices();
            else DrawChoices();
            return;
        }
        if (pages == null || pages.Count == 0) return;

        if (actor == null && string.IsNullOrEmpty(speaker))
        {
            DrawObjectDialogue();
            return;
        }


        // Dialogue box size & position:
        float width = Mathf.Min(Screen.width - 48f, 1160f);
        float height = Mathf.Clamp(Screen.height * 0.38f, 340f, 384f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - height - 20f;
        var box = new Rect(x, y, width, height);

        // Click to advance or skip typing
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
        {
            AdvanceOrClose();
            e.Use();
        }

        // 1. Outer wooden dialogue box with parchment interior & ornate corners
        StardewUiAtlas.DrawStardewDialogueBox(box);

        bool hasPortrait = PlayerPrefs.GetInt("GameMenu.ShowPortraits", 1) != 0 &&
            (actor != null || !string.IsNullOrEmpty(speaker));
        NpcDialoguePage page = pages[pageIndex];

        float textRightEdge;
        if (hasPortrait)
        {
            // Layout offsets for portrait plate and divider
            float num1 = box.x + box.width - 448f + 4f;
            float num2 = box.x + box.width - num1;
            float plateX = num1 - 8f;
            float plateY = box.y;

            // Vertical wood divider between text and portrait
            float divX = num1 - 40f;
            StardewUiAtlas.DrawCursorsSprite(new Rect(divX, box.y, 36f, box.height), StardewUiAtlas.DividerStripeRect);
            StardewUiAtlas.DrawCursorsSprite(new Rect(divX, box.y - 20f, 40f, 28f), StardewUiAtlas.DividerTopCapRect);
            StardewUiAtlas.DrawCursorsSprite(new Rect(divX, box.yMax - 8f, 40f, 32f), StardewUiAtlas.DividerBotCapRect);

            // Portrait backing board (583, 411, 115, 97) at 4x scale = 460 x 388 px
            StardewUiAtlas.DrawCursorsSprite(new Rect(plateX, plateY, 460f, 388f), StardewUiAtlas.PortraitPlateRect);

            // NPC Portrait inside aperture cutout: (num3 + 16, num4 + 24)
            float num3 = num1 + 76f;
            float num4 = box.y + box.height * 0.5f - 148f - 36f;
            Sprite portrait = actor != null ? actor.PortraitFor(page.PortraitIndex) :
                NpcSprites.PortraitFrame(portraitOverride, page.PortraitIndex);
            if (portrait == null && !string.IsNullOrEmpty(speaker))
                portrait = NpcSprites.PortraitFrame(SpeakerPortraitSheet(speaker), page.PortraitIndex);
            if (portrait != null)
            {
                DrawSprite(new Rect(num3 + 16f, num4 + 24f, 256f, 256f), portrait);
            }

            // Friendship Jewel in the slot on bottom right of the portrait aperture
            StardewUiAtlas.DrawCursorsSprite(new Rect(plateX + 372f, plateY + 248f, 44f, 44f), StardewUiAtlas.FriendshipJewelRect);

            // Nameplate text centered in the scroll banner at bottom of plate (num4 + 296 + 16)
            if (!string.IsNullOrEmpty(speaker))
            {
                Color oldC = GUI.color;
                GUI.color = Color.white;
                float nameY = num4 + 312f;
                NpcBitmapText.Draw(fontTexture, new Rect(plateX + 40f, nameY, 380f, 32f), speaker, 3.0f, true);
                GUI.color = oldC;
            }

            textRightEdge = divX - 16f;
        }
        else
        {
            textRightEdge = box.xMax - 48f;
        }

        // Left dialogue text area
        float textX = box.x + 48f;
        float textY = box.y + 44f;
        float textWidth = textRightEdge - textX;
        float textHeight = box.height - 88f;

        Color prevColor = GUI.color;
        GUI.color = Color.white;
        string currentText = page.Text.Substring(0, VisibleCharacterCount);
        var textArea = new Rect(textX, textY, textWidth, textHeight);
        // Draws the spoken text.
        bool drawn = DrawSpriteText(textArea, page.Text, VisibleCharacterCount);
        if (drawn) { /* font sheet */ }
        else if (GameText.CanDraw(page.Text))
            GameText.Draw(textArea, currentText, GameText.Face.Dialogue);
        else
        {
            unicodeDialogue ??= new GUIStyle(GUI.skin.label)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                fontSize = 32,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.24f, 0.13f, 0.08f) }
            };
            GUI.Label(textArea, currentText.Replace('^', '\n'), unicodeDialogue);
        }
        GUI.color = prevColor;

        // Prompt / advance icon at bottom-right of left dialogue panel
        if (VisibleCharacterCount >= page.Text.Length)
        {
            if (pageIndex < pages.Count - 1)
            {
                // More pages: pulsing down arrow
                float bounce = Mathf.Sin(Time.unscaledTime * 8f) * 4f;
                StardewUiAtlas.DrawCursorsSprite(new Rect(textRightEdge - 52f, box.yMax - 60f + bounce, 36f, 36f), StardewUiAtlas.DialogueNextArrowRect);
            }
            else
            {
                // Final page: scroll close icon
                StardewUiAtlas.DrawCursorsSprite(new Rect(textRightEdge - 56f, box.yMax - 64f, 44f, 48f), StardewUiAtlas.DialogueCloseScrollRect);
            }
        }
    }

    private void UpdateServiceChoices()
    {
        var key = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (key?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
        {
            Close();
            return;
        }
        int direction = key?.upArrowKey.wasPressedThisFrame == true || pad?.dpad.up.wasPressedThisFrame == true ? -1 :
            key?.downArrowKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true ? 1 : 0;
        if (direction != 0)
        {
            choiceSelection = (choiceSelection + direction + choices.Count) % choices.Count;
            GameAudio.Play("shiny4");
        }
        if (key?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
            SelectService(choiceSelection);
    }

    private void DrawServiceChoices()
    {
        GUI.depth = -60;
        const float width = 580f, rowHeight = 54f;
        float height = rowHeight * choices.Count + 20f;
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / (width + 100f), Screen.height / (height + 100f)));
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * .5f,
            Screen.height - (height + 58f) * scale, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            StardewUiAtlas.DrawStardewDialogueBox(new Rect(0f, 0f, width, height));
            for (int i = 0; i < choices.Count; i++)
            {
                Rect row = new(12f, 10f + i * rowHeight, width - 24f, rowHeight);
                bool hover = row.Contains(Event.current.mousePosition);
                if (hover && Event.current.type == EventType.MouseMove) choiceSelection = i;
                if (hover || i == choiceSelection)
                    StardewUiAtlas.DrawTextureBox(row, StardewUiAtlas.Cursors,
                        new Rect(375, 357, 3, 3), Color.white, 4f, false);
                var labelArea = new Rect(row.x + 16f, row.y + 10f, row.width - 32f, 44f);
                if (!DrawSpriteText(labelArea, choices[i].Label))
                    GameText.Draw(labelArea, choices[i].Label, GameText.Face.Dialogue);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    SelectService(i);
                    return;
                }
            }
        }
        finally { GUI.matrix = previous; }
    }

    private void SelectService(int index)
    {
        var action = choices[index].Choose;
        Close();
        action();
    }

    private void DrawChoices()
    {
        float width = Mathf.Min(Screen.width - 32f, 740f);
        float height = Mathf.Min(Screen.height - 32f, 520f);
        Rect frame = new((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        StardewUiAtlas.DrawStardewDialogueBox(frame);
        choiceHeading ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            wordWrap = true,
            normal = { textColor = new Color(0.24f, 0.13f, 0.08f) }
        };
        System.Action selected = null;
        using (new GUILayout.AreaScope(new Rect(frame.x + 32f, frame.y + 32f, width - 64f, height - 64f)))
        {
            GUILayout.Label(choiceTitle, choiceHeading);
            using (var scroll = new GUILayout.ScrollViewScope(choiceScroll))
            {
                choiceScroll = scroll.scrollPosition;
                if (submitText != null)
                {
                    GUILayout.Label(entryText + "|", GUI.skin.textField, GUILayout.MinHeight(30f));
                    if (!string.IsNullOrWhiteSpace(entryText) && GUILayout.Button("Xác nhận"))
                    {
                        var submit = submitText;
                        string name = entryText.Trim();
                        selected = () => submit(name);
                    }
                }
                for (int i = 0; i < choices.Count; i++)
                {
                    var option = choices[i];
                    string label = i == choiceSelection ? "› " + option.Label : option.Label;
                    if (GUILayout.Button(label, GUILayout.MinHeight(30f))) selected = option.Choose;
                }
            }
            if (GUILayout.Button("Đóng")) selected = () => { };
        }
        if (selected == null) return;
        Close();
        selected();
    }

    private void DrawObjectDialogue()
    {
        // Computes width and height, then
        // anchors the result 64 px above the viewport bottom. TV broadcasts
        // and locked-door notices use this form, with no portrait plate.
        NpcDialoguePage page = pages[pageIndex];
        string[] lines = page.Text.Replace('^', '\n').Split('\n');
        float measured = 0f;
        foreach (string line in lines)
            measured = Mathf.Max(measured, NpcBitmapText.GetStringWidth(line));
        float width = Mathf.Clamp(measured + 64f, 220f,
            Mathf.Max(220f, Mathf.Min(1240f, Screen.width - 112f)));
        int renderedLines = 0;
        foreach (string line in lines)
            renderedLines += Mathf.Max(1,
                Mathf.CeilToInt(NpcBitmapText.GetStringWidth(line) /
                                Mathf.Max(1f, width - 48f)));
        float height = renderedLines * 54f + 4f;
        Rect box = new((Screen.width - width) * 0.5f,
            Screen.height - height - 64f, width, height);

        Event current = Event.current;
        if (current != null && current.type == EventType.MouseDown &&
            (current.button == 0 || current.button == 1))
        {
            AdvanceOrClose();
            current.Use();
        }
        StardewUiAtlas.DrawStardewDialogueBox(box);
        var noticeArea = new Rect(box.x + 28f, box.y + 24f, box.width - 64f, box.height - 40f);
        if (!DrawSpriteText(noticeArea, page.Text, VisibleCharacterCount))
            GameText.Draw(noticeArea, page.Text.Substring(0, VisibleCharacterCount), GameText.Face.Dialogue);
        if (VisibleCharacterCount >= page.Text.Length)
            StardewUiAtlas.DrawCursorsSprite(
                new Rect(box.xMax - 42f, box.yMax - 44f, 32f, 36f),
                StardewUiAtlas.DialogueCloseScrollRect);
    }

    /// <summary>
    /// Draws with the font sheet (plus its Vietnamese supplement).
    /// Returns false when the sheet is missing or lacks a character, so callers can fall back.
    /// </summary>
    private bool DrawSpriteText(Rect area, string text, int visibleCount = int.MaxValue)
    {
        if (fontTexture == null) fontTexture = Resources.Load<NpcCatalog>("NpcCatalog")?.dialogueFont;
        if (string.IsNullOrEmpty(text) || !NpcBitmapText.CanDraw(fontTexture, text)) return false;
        NpcBitmapText.Draw(fontTexture, area, text, NpcBitmapText.DefaultScale, visibleCount: visibleCount);
        return true;
    }

    private static void DrawSprite(Rect destination, Sprite sprite)
    {
        if (sprite == null) return;
        Rect source = sprite.textureRect;
        Texture2D texture = sprite.texture;
        var uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(destination, texture, uv);
    }
}
