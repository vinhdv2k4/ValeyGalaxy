using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UIText = UnityEngine.UI.Text;

/// <summary>
/// Chest menu laid out like the original ItemGrabMenu: the chest's 36 slots on top, the backpack
/// below. Left-click sends a whole stack to the other side (stacking onto matching items first),
/// right-click sends one. Side buttons organize the chest, fill its stacks and organize the bag.
/// </summary>
public sealed class PlacedChestUI : MonoBehaviour
{
    // Kept so the existing panel prefab keeps its references; the menu itself is drawn below.
    public Button[] bagButtons;
    public Button[] chestButtons;
    public Button closeButton;
    public UIText moneyLabel;
    public UIText chestLabel;
    public UIText messageLabel;

    private const float Width = 940f, Height = 700f;
    private const int Columns = 12, ChestSlots = 36;
    private static readonly Rect OrganizeSource = new(162, 440, 16, 16);
    private static readonly Rect FillStacksSource = new(103, 469, 16, 16);
    private static readonly Rect OkSource = new(128, 256, 64, 64);
    private static readonly Color TextColor = new(86f / 255f, 22f / 255f, 12f / 255f);

    private OnlineGameState game;
    private string chestId;
    private bool busy;
    private string message;
    private float messageUntil;
    private int openedFrame;

    public bool IsOpen => gameObject.activeSelf;

    public void Initialize(OnlineGameState owner)
    {
        game = owner;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        foreach (Graphic graphic in GetComponents<Graphic>()) graphic.enabled = false;
        gameObject.SetActive(false);
    }

    public void Open(string id)
    {
        GameAudio.Play("openChest");
        chestId = id;
        message = null;
        busy = false;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
        gameObject.SetActive(false);
        chestId = null;
    }

    public void Refresh()
    {
        if (IsOpen && CurrentChest() == null) Close();
    }

    private OnlineChest CurrentChest()
    {
        var chests = game?.Snapshot?.chests;
        if (chests == null) return null;
        foreach (var chest in chests)
            if (chest != null && chest.id == chestId) return chest;
        return null;
    }

    private static OnlineStack Find(OnlineStack[] entries, int index)
    {
        if (entries == null) return null;
        foreach (var entry in entries) if (entry != null && entry.slot == index && entry.amount > 0) return entry;
        return null;
    }

    private void Update()
    {
        if (Time.frameCount == openedFrame) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)) Close();
    }

    private void OnGUI()
    {
        var snapshot = game?.Snapshot;
        var chest = CurrentChest();
        if (snapshot == null || chest == null) { if (IsOpen && snapshot != null) Close(); return; }
        GUI.depth = -60;
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / (Width + 40f), Screen.height / (Height + 40f)), 0.5f, 1.4f);
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) * 0.5f, (Screen.height - Height * scale) * 0.5f, 0f),
            Quaternion.identity, new Vector3(scale, scale, 1f));
        try { DrawMenu(snapshot, chest); }
        finally { GUI.matrix = old; }
    }

    private void DrawMenu(OnlineGameSnapshot snapshot, OnlineChest chest)
    {
        Texture2D font = StardewUiAtlas.FontBold;
        var chestBox = new Rect(20f, 20f, 832f, 300f);
        var bagBox = new Rect(20f, 350f, 832f, 300f);
        StardewUiAtlas.DrawStardewDialogueBox(chestBox);
        StardewUiAtlas.DrawStardewDialogueBox(bagBox);
        GameText.Draw(new Rect(chestBox.x + 40f, chestBox.y + 22f, 500f, 44f), chest.name, GameText.Face.Dialogue);

        string tooltip = null;
        DrawGrid(new Vector2(chestBox.x + 32f, chestBox.y + 72f), chest.slots, ChestSlots, true, ref tooltip);
        int bagSize = Mathf.Clamp(snapshot.bagSize, Columns, ChestSlots);
        DrawGrid(new Vector2(bagBox.x + 32f, bagBox.y + 72f), snapshot.inventory, bagSize, false, ref tooltip);

        // The chest's two buttons sit on its right edge, a third of the way down; OK closes the menu.
        float third = chestBox.y + 72f + 3 * 68f / 3f;
        var fillStacks = new Rect(chestBox.xMax - 32f, third - 144f + 72f, 64f, 64f);
        var organizeChest = new Rect(chestBox.xMax - 32f, third - 64f + 72f, 64f, 64f);
        if (CursorButton(organizeChest, OrganizeSource)) Send("organize", false, 0, 0, "Ship");
        if (organizeChest.Contains(Event.current.mousePosition)) tooltip = "Sắp xếp";
        if (CursorButton(fillStacks, FillStacksSource)) Send("fill-stacks", false, 0, 0, "Ship");
        if (fillStacks.Contains(Event.current.mousePosition)) tooltip = "Gom vào các chồng đã có";

        var ok = new Rect(bagBox.xMax + 4f, bagBox.yMax - 160f, 64f, 64f);
        if (CursorButton(ok, OkSource)) { Close(); return; }

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
        {
            var box = new Rect(20f, Height - 50f, GameText.Measure(message, GameText.Face.Dialogue) + 56f, 60f);
            StardewUiAtlas.DrawTextureBox(box, StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
            GameText.Draw(new Rect(box.x + 26f, box.y + 10f, box.width - 30f, 44f), message, GameText.Face.Dialogue, Color.red);
        }
        if (tooltip != null) DrawTooltip(tooltip);
    }

    private void DrawGrid(Vector2 origin, OnlineStack[] stacks, int unlocked, bool chestSide, ref string tooltip)
    {
        for (int slot = 0; slot < ChestSlots; slot++)
        {
            var rect = new Rect(origin.x + slot % Columns * 64f, origin.y + slot / Columns * 68f, 64f, 64f);
            bool locked = slot >= unlocked;
            StardewUiAtlas.DrawMenuTilesSprite(rect, locked ? StardewUiAtlas.SlotLockedRect : StardewUiAtlas.SlotNormalRect,
                locked ? new Color(1f, 1f, 1f, 0.5f) : null);
            if (locked) continue;
            OnlineStack stack = Find(stacks, slot);
            if (stack == null) continue;
            ItemData item = game.ResolveItem(stack.itemId);
            bool hover = rect.Contains(Event.current.mousePosition);
            Rect iconRect = hover ? new Rect(rect.x + 6f, rect.y + 6f, 52f, 52f) : new Rect(rect.x + 8f, rect.y + 8f, 48f, 48f);
            DrawSprite(item?.GetIcon(), iconRect);
            StardewUiAtlas.DrawStackCount(rect, stack.amount, stack.itemId);
            if (hover)
            {
                tooltip = item?.displayName ?? stack.itemId;
                if (!string.IsNullOrWhiteSpace(item?.description)) tooltip += "\n" + item.description;
            }
            Event e = Event.current;
            if (e.type != EventType.MouseDown || !hover || busy) continue;
            e.Use();
            Send("quick-move", chestSide, slot, e.button == 1 ? 1 : 0, "dwop");
        }
    }

    private void Send(string action, bool fromChest, int slot, int amount, string sound)
    {
        if (busy || game == null) return;
        busy = true;
        game.ChestAction(action, chestId, fromChest, slot, amount, error =>
        {
            busy = false;
            Report(error, sound);
        });
    }

    private void Report(string error, string sound)
    {
        if (error == null) { GameAudio.Play(sound); return; }
        GameAudio.Play("cancel");
        message = error;
        messageUntil = Time.unscaledTime + 3f;
    }

    private static bool CursorButton(Rect rect, Rect source)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        StardewUiAtlas.DrawCursorsSprite(hover ? new Rect(rect.center - rect.size * 0.55f, rect.size * 1.1f) : rect, source);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private static void DrawSprite(Sprite sprite, Rect rect)
    {
        if (sprite == null || Event.current.type != EventType.Repaint) return;
        Rect source = sprite.textureRect;
        Texture2D texture = sprite.texture;
        GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height));
    }

    private static void DrawTooltip(string text)
    {
        int split = text.IndexOf('\n');
        GameText.DrawTooltip(split < 0 ? text : text.Substring(0, split), split < 0 ? null : text.Substring(split + 1),
            Width, Height);
    }
}
