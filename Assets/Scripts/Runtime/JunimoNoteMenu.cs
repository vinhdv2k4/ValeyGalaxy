using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Junimo note at the Community Center: one parchment page per room listing its bundles, and a
/// page per bundle with the requested items, the donation slots and the backpack. Clicking an item
/// in the backpack donates it to the open bundle; the vault bundles are bought with gold instead.
/// </summary>
public sealed class JunimoNoteMenu : MonoBehaviour
{
    private const float Width = 1280f, Height = 720f;

    private static readonly Vector2[] BundleSpots =
    {
        new(592, 136), new(392, 384), new(784, 388), new(304, 252), new(892, 252),
        new(588, 276), new(588, 380), new(440, 164), new(776, 164)
    };

    private static readonly string[] AreaRewards =
    {
        "Phần thưởng: Nhà kính", "Phần thưởng: Sửa cây cầu", "Phần thưởng: Tảng đá lấp lánh bị dời đi",
        "Phần thưởng: Sửa xe goòng", "Phần thưởng: Sửa xe buýt", "Phần thưởng: Tình bạn"
    };

    private static readonly Rect NextArrow = new(365, 495, 12, 11);
    private static readonly Rect PreviousArrow = new(352, 495, 12, 11);
    private static readonly Rect BackArrow = new(0, 256, 64, 64);
    private static readonly Rect CloseSource = new(337, 494, 12, 12);

    private static JunimoNoteMenu instance;

    private OnlineGameState game;
    private Texture2D note;
    private int area;
    private int bundleIndex = -1;
    private bool busy;
    private string message;
    private float messageUntil;
    private int openedFrame;
    private int hoveredBundle = -1;
    private float hoverStarted;

    public static bool IsOpen => instance != null && instance.enabled;

    /// <summary>Opens the note of one room, as CommunityCenter.checkBundle(area) does.</summary>
    public static void Open(OnlineGameState owner, int roomArea)
    {
        Open(owner);
        if (!IsOpen) return;
        var areas = owner.Snapshot.bundles.areas;
        for (int i = 0; i < areas.Length; i++)
            if (areas[i] != null && areas[i].area == roomArea) { instance.area = i; return; }
    }

    public static void Open(OnlineGameState owner)
    {
        if (owner?.Snapshot?.bundles?.areas == null || owner.Snapshot.bundles.areas.Length == 0) return;
        if (instance == null)
        {
            var host = new GameObject("JunimoNoteMenu");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<JunimoNoteMenu>();
        }
        instance.game = owner;
        instance.note = instance.note != null ? instance.note : Resources.Load<Texture2D>("UI/JunimoNote");
        instance.area = FirstOpenArea(owner.Snapshot.bundles);
        instance.bundleIndex = -1;
        instance.busy = false;
        instance.message = null;
        instance.openedFrame = Time.frameCount;
        instance.enabled = true;
        GameAudio.Play("shwip");
    }

    private static int FirstOpenArea(OnlineBundleStatus status)
    {
        for (int i = 0; i < status.areas.Length; i++)
            if (!status.areas[i].complete) return i;
        return 0;
    }

    private void Close()
    {
        enabled = false;
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
    }

    private OnlineBundleArea CurrentArea
    {
        get
        {
            var areas = game?.Snapshot?.bundles?.areas;
            return areas == null || areas.Length == 0 ? null : areas[Mathf.Clamp(area, 0, areas.Length - 1)];
        }
    }

    private OnlineBundle CurrentBundle
    {
        get
        {
            var bundles = CurrentArea?.bundles;
            if (bundles == null || bundleIndex < 0) return null;
            foreach (var bundle in bundles) if (bundle.index == bundleIndex) return bundle;
            return null;
        }
    }

    private void Update()
    {
        if (Time.frameCount == openedFrame) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame && !keyboard.eKey.wasPressedThisFrame) return;
        if (bundleIndex >= 0) ClosePage();
        else Close();
    }

    private void ClosePage()
    {
        bundleIndex = -1;
        GameAudio.Play("shwip");
    }

    private void OnGUI()
    {
        if (game?.Snapshot?.bundles?.areas == null || note == null) { enabled = false; return; }
        GUI.depth = -70;
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        float scale = Mathf.Min(Screen.width / (Width + 40f), Screen.height / (Height + 120f));
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) * 0.5f, (Screen.height - Height * scale) * 0.5f - 30f * scale, 0f),
            Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            string hover = null, hoverBody = null;
            if (bundleIndex >= 0 && CurrentBundle != null) DrawBundlePage(CurrentBundle, ref hover, ref hoverBody);
            else DrawOverview(ref hover);
            DrawScroll(AreaRewards[Mathf.Clamp(area, 0, AreaRewards.Length - 1)], Width * 0.5f, Height + 20f);
            var close = new Rect(Width - 36f, -8f, 48f, 48f);
            if (Button(close, CloseSource, Cursor)) { Close(); return; }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
                DrawScroll(message, Width * 0.5f, -84f);
            if (hover != null || hoverBody != null) GameText.DrawTooltip(hover, hoverBody, Width, Height + 100f);
        }
        finally { GUI.matrix = old; }
    }

    private static Texture2D Cursor => StardewUiAtlas.Cursors;

    private void DrawOverview(ref string hover)
    {
        var current = CurrentArea;
        DrawNote(new Rect(0f, 0f, Width, Height), new Rect(0, 0, 320, 180));
        GameText.Draw(new Rect(16f, 12f, Width, 50f), current.name, GameText.Face.Dialogue,
            new Color(GameText.TextColor.r, GameText.TextColor.g, GameText.TextColor.b, 0.88f), 1f, true);

        int hoveredNow = -1;
        var bundles = current.bundles ?? Array.Empty<OnlineBundle>();
        for (int i = 0; i < bundles.Length && i < BundleSpots.Length; i++)
        {
            var bundle = bundles[i];
            var rect = new Rect(BundleSpots[i].x, BundleSpots[i].y, 64f, 64f);
            bool over = rect.Contains(Event.current.mousePosition);
            if (over) hoveredNow = bundle.index;
            int frame = 1;
            if (bundle.complete) frame = 15;
            else if (over && hoveredBundle == bundle.index)
            {
                int step = (int)((Time.unscaledTime - hoverStarted) / 0.07f) % 4;
                frame = step == 3 ? 1 : step;
            }
            var source = new Rect(bundle.color * 256 % 512 + frame * 16, 244 + bundle.color * 256 / 512 * 16, 16, 16);
            DrawNote(rect, source);
            if (over) hover = bundle.complete ? bundle.name + " (đã xong)" : "Gói " + bundle.name;
            if (Clicked(rect) && !bundle.complete)
            {
                bundleIndex = bundle.index;
                GameAudio.Play("shwip");
            }
        }
        if (hoveredNow != hoveredBundle)
        {
            hoveredBundle = hoveredNow;
            hoverStarted = Time.unscaledTime;
            if (hoveredNow >= 0) GameAudio.Play("dwop");
        }

        if (HasPendingReward(current))
        {
            var present = new Rect(592f, 512f, 72f, 80f);
            int frame = (int)(Time.unscaledTime / 0.07f) % 4;
            DrawNote(present, new Rect(548 + frame * 18, 262, 18, 20));
            if (present.Contains(Event.current.mousePosition)) hover = "Nhận phần thưởng";
            if (Clicked(present)) Send("claim", 0, 0, "coin");
        }

        var areas = game.Snapshot.bundles.areas;
        if (areas.Length > 1)
        {
            if (Button(new Rect(Width - 128f, 0f, 48f, 44f), NextArrow, Cursor))
            { area = (area + 1) % areas.Length; GameAudio.Play("shwip"); }
            if (Button(new Rect(64f, 0f, 48f, 44f), PreviousArrow, Cursor))
            { area = (area + areas.Length - 1) % areas.Length; GameAudio.Play("shwip"); }
        }
    }

    private bool HasPendingReward(OnlineBundleArea current)
    {
        var pending = game.Snapshot.bundles.pendingRewards;
        if (pending == null || current.bundles == null) return false;
        foreach (var bundle in current.bundles)
            if (Array.IndexOf(pending, bundle.index) >= 0) return true;
        return false;
    }

    private void DrawBundlePage(OnlineBundle bundle, ref string hover, ref string hoverBody)
    {
        DrawNote(new Rect(0f, 0f, Width, Height), new Rect(320, 0, 320, 180));
        int icon = bundle.index;
        DrawNote(new Rect(872f, 88f, 128f, 128f), new Rect(icon * 32 % 640, 180 + 32 * (icon * 32 / 640), 32, 32));

        string label = "Gói " + bundle.name;
        float textWidth = GameText.Measure(label, GameText.Face.Dialogue);
        DrawNote(new Rect(936f - textWidth / 2f - 16f, 228f, 16f, 68f), new Rect(517, 266, 4, 17));
        DrawNote(new Rect(936f - textWidth / 2f, 228f, textWidth, 68f), new Rect(520, 266, 1, 17));
        DrawNote(new Rect(936f + textWidth / 2f, 228f, 16f, 68f), new Rect(524, 266, 4, 17));
        GameText.Draw(new Rect(936f - textWidth / 2f, 236f, textWidth + 8f, 50f), label, GameText.Face.Dialogue);

        if (Button(new Rect(88f, 84f, 64f, 64f), BackArrow, Cursor)) { ClosePage(); return; }

        if (bundle.gold > 0) DrawVault(bundle, ref hover);
        else DrawIngredients(bundle, ref hover, ref hoverBody);
        DrawInventory(bundle, ref hover, ref hoverBody);
    }

    private void DrawVault(OnlineBundle bundle, ref string hover)
    {
        var buy = new Rect(800f, 504f, 260f, 80f);
        bool over = buy.Contains(Event.current.mousePosition);
        DrawNote(over ? new Rect(buy.x - 6f, buy.y - 4f, 272f, 88f) : buy, new Rect(517, 286, 65, 20));
        if (over) hover = $"Mua với giá {bundle.gold:N0}g";
        if (Clicked(buy)) Send("purchase", bundle.index, 0, "purchase");
        StardewUiAtlas.DrawMoneyBox(new Vector2(16f, Height - 76f), game.Snapshot.money, 4f);
    }

    private void DrawIngredients(OnlineBundle bundle, ref string hover, ref string hoverBody)
    {
        var ingredients = bundle.ingredients ?? Array.Empty<OnlineBundleIngredient>();
        var listRects = RowsCentered(ingredients.Length, 932f, 364f);
        for (int i = 0; i < listRects.Count; i++)
        {
            var entry = ingredients[i];
            Rect rect = listRects[i];
            string id = WithQuality(entry.itemId, entry.quality);
            ItemData item = game.ResolveItem(entry.itemId);
            if (!entry.done) DrawShadow(rect);
            DrawIcon(item?.GetIcon(), rect, entry.done ? 0.25f : 1f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, entry.done ? 0.25f : 1f);
            StardewUiAtlas.DrawStackCount(rect, entry.amount, id);
            GUI.color = previous;
            if (rect.Contains(Event.current.mousePosition))
            {
                hover = item?.displayName ?? entry.itemId;
                hoverBody = entry.quality > 0 ? "Chất lượng tối thiểu: " + QualityName(entry.quality) : null;
            }
        }

        int donated = 0;
        foreach (var entry in ingredients) if (entry.done) donated++;
        var filled = new List<OnlineBundleIngredient>();
        foreach (var entry in ingredients) if (entry.done) filled.Add(entry);
        var slotRects = RowsCentered(Mathf.Max(1, bundle.required), 932f, 540f);
        for (int i = 0; i < slotRects.Count; i++)
        {
            Rect rect = slotRects[i];
            DrawNote(rect, new Rect(512, 244, 18, 18));
            if (i >= filled.Count) continue;
            var entry = filled[i];
            DrawIcon(game.ResolveItem(entry.itemId)?.GetIcon(), new Rect(rect.x + 4f, rect.y + 4f, 64f, 64f), 1f);
            StardewUiAtlas.DrawStackCount(new Rect(rect.x + 4f, rect.y + 4f, 64f, 64f), entry.amount,
                WithQuality(entry.itemId, entry.quality));
        }
    }

    private void DrawInventory(OnlineBundle bundle, ref string hover, ref string hoverBody)
    {
        var snapshot = game.Snapshot;
        int capacity = Mathf.Clamp(snapshot.bagSize, 12, 36);
        for (int slot = 0; slot < capacity; slot++)
        {
            var rect = new Rect(128f + slot % 6 * 72f, 140f + slot / 6 * 72f, 64f, 64f);
            OnlineStack stack = null;
            if (snapshot.inventory != null)
                foreach (var entry in snapshot.inventory)
                    if (entry != null && entry.slot == slot && entry.amount > 0) { stack = entry; break; }
            if (stack == null) continue;
            ItemData item = game.ResolveItem(stack.itemId);
            bool useful = bundle.gold <= 0 && Accepts(bundle, stack);
            bool over = rect.Contains(Event.current.mousePosition);
            Rect iconRect = over && useful ? new Rect(rect.x - 3f, rect.y - 3f, 70f, 70f) : rect;
            DrawIcon(item?.GetIcon(), iconRect, useful ? 1f : 0.3f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, useful ? 1f : 0.3f);
            StardewUiAtlas.DrawStackCount(rect, stack.amount, stack.itemId);
            GUI.color = previous;
            if (over)
            {
                hover = item?.displayName ?? stack.itemId;
                hoverBody = item?.description;
            }
            if (Clicked(rect))
            {
                if (useful) Send("donate", bundle.index, slot, "newArtifact");
                else GameAudio.Play("cancel");
            }
        }
    }

    private static bool Accepts(OnlineBundle bundle, OnlineStack stack)
    {
        if (bundle.complete || bundle.ingredients == null) return false;
        string baseId = ItemQuality.BaseId(stack.itemId);
        int quality = ItemQuality.Of(stack.itemId);
        foreach (var entry in bundle.ingredients)
        {
            if (entry.done || quality < entry.quality || stack.amount < entry.amount) continue;
            if (baseId == entry.itemId) return true;
            if (entry.itemId.StartsWith("object_", StringComparison.Ordinal) && baseId == "fish_" + entry.itemId.Substring(7))
                return true;
        }
        return false;
    }

    private void Send(string action, int bundle, int slot, string sound)
    {
        if (busy) return;
        busy = true;
        var before = CurrentBundle;
        bool wasComplete = before != null && before.complete;
        game.BundleAction(action, bundle, slot, error =>
        {
            busy = false;
            if (error != null)
            {
                GameAudio.Play("cancel");
                message = error;
                messageUntil = Time.unscaledTime + 3f;
                return;
            }
            GameAudio.Play(sound);
            var after = CurrentBundle;
            if (after != null && after.complete && !wasComplete)
            {
                GameAudio.Play("leafrustle");
                bundleIndex = -1;
                if (CurrentArea != null && CurrentArea.complete)
                {
                    // CommunityCenter.restoreAreaCutscene: the note closes and the Junimos come
                    // to rebuild the room.
                    if (CommunityCenterRooms.Celebrate(CurrentArea.area, CurrentArea.name))
                    {
                        Close();
                        return;
                    }
                    message = CurrentArea.name + " đã được khôi phục!";
                    messageUntil = Time.unscaledTime + 4f;
                }
            }
        });
    }

    private static List<Rect> RowsCentered(int count, float centerX, float centerY)
    {
        var result = new List<Rect>();
        if (count <= 0) return result;
        if (count <= 4) { AddRow(result, count, centerX, centerY); return result; }
        int top = (count + 1) / 2;
        if (count == 7 || count == 9 || count == 11) top = count / 2 + 1;
        if (count == 5) top = 3;
        AddRow(result, top, centerX, centerY - 36f);
        AddRow(result, count - top, centerX, centerY + 40f);
        return result;
    }

    private static void AddRow(List<Rect> list, int boxes, float centerX, float centerY)
    {
        float x = centerX - boxes * (72f + 12f) / 2f;
        for (int i = 0; i < boxes; i++) list.Add(new Rect(x + i * 84f, centerY - 36f, 72f, 72f));
    }

    private static string WithQuality(string itemId, int quality) => quality > 0 ? itemId + "@" + quality : itemId;

    private static string QualityName(int quality) => quality switch
    {
        1 => "Bạc", 2 => "Vàng", _ => "Iridium"
    };

    private void DrawNote(Rect destination, Rect source)
    {
        if (Event.current.type != EventType.Repaint) return;
        GUI.DrawTextureWithTexCoords(destination, note, StardewUiAtlas.ToUv(source, note.width, note.height));
    }

    private static void DrawShadow(Rect slot)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(slot.center.x - 24f, slot.center.y + 18f, 48f, 14f), Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previous;
    }

    private static void DrawIcon(Sprite sprite, Rect rect, float alpha)
    {
        if (sprite == null || Event.current.type != EventType.Repaint) return;
        Rect source = sprite.textureRect;
        Texture2D texture = sprite.texture;
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height));
        GUI.color = previous;
    }

    /// <summary>Text on the wooden scroll, centred on x.</summary>
    private static void DrawScroll(string text, float centerX, float y)
    {
        if (string.IsNullOrEmpty(text)) return;
        float width = GameText.Measure(text, GameText.Face.Dialogue);
        float left = centerX - width / 2f;
        StardewUiAtlas.DrawCursorsSprite(new Rect(left - 48f, y, 48f, 72f), new Rect(325, 318, 12, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(left, y, width, 72f), new Rect(337, 318, 1, 18));
        StardewUiAtlas.DrawCursorsSprite(new Rect(left + width, y, 48f, 72f), new Rect(338, 318, 12, 18));
        GameText.Draw(new Rect(left, y + 14f, width + 8f, 50f), text, GameText.Face.Dialogue);
    }

    private static bool Button(Rect rect, Rect source, Texture2D sheet)
    {
        bool over = rect.Contains(Event.current.mousePosition);
        Rect drawn = over ? new Rect(rect.center - rect.size * 0.55f, rect.size * 1.1f) : rect;
        if (Event.current.type == EventType.Repaint && sheet != null)
            GUI.DrawTextureWithTexCoords(drawn, sheet, StardewUiAtlas.ToUv(source, sheet.width, sheet.height));
        return Clicked(rect);
    }

    private static bool Clicked(Rect rect)
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition)) return false;
        e.Use();
        return true;
    }
}
