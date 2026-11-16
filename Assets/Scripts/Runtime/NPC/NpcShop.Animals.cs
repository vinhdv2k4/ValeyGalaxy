using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Marnie's livestock menu: pick an animal, choose a coop or barn with room on the farm, then name it.
/// </summary>
public sealed partial class NpcShop
{
    private const float AnimalMenuWidth = 3 * 128f + 2 * 40f;
    private const float AnimalMenuHeight = 320f + 64f;
    private static readonly Rect RandomNameSource = new(381, 361, 10, 10);
    private static readonly string[] NameStarts = { "Bông", "Mít", "Na", "Bơ", "Su", "Tí", "Mận", "Đậu", "Gạo", "Kem", "Bi", "Xoài", "Cốm", "Mochi", "Lu", "Bánh" };
    private static readonly string[] NameEnds = { "", " Nhỏ", " Béo", " Con", " Xinh", " Mập", " Tròn", "" };

    private RanchOfferView animalBeingPurchased;
    private OnlineRanchHome animalHome;
    private bool namingAnimal;
    private string animalName = "";
    private bool animalNameFocus;

    private void DrawAnimalShop()
    {
        if (farmTask != FarmTask.None) { DrawFarmTask(); return; }
        var online = inventory?.GetComponent<OnlineGameState>();
        var catalog = FarmRanchAssets.Data;
        const float canvasWidth = 760f, canvasHeight = 640f;
        float scale = Mathf.Min(1f, Screen.width / (canvasWidth + 32f), Screen.height / (canvasHeight + 32f));
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - canvasWidth * scale) * 0.5f,
            (Screen.height - canvasHeight * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try { DrawAnimalStock(online, catalog, canvasWidth); }
        finally { GUI.matrix = oldMatrix; }
        StardewUiAtlas.DrawHudMoneyBox(GetCurrentFunds());
    }

    private string AnimalLock(RanchOfferView offer, OnlineGameSnapshot snapshot)
    {
        var homes = (snapshot.ranchHomes ?? Array.Empty<OnlineRanchHome>())
            .Where(home => home.kind == offer.kind && home.level >= offer.level).ToArray();
        if (homes.Length == 0) return "Cần " + FarmRanchAssets.Plan(offer.kind, offer.level)?.name;
        if (homes.All(home => AnimalsIn(home, snapshot) >= home.capacity)) return "Chuồng đã đầy";
        return null;
    }

    private static int AnimalsIn(OnlineRanchHome home, OnlineGameSnapshot snapshot) =>
        snapshot.ranchAnimals?.Count(animal => animal.home == home.id) ?? 0;

    private void DrawAnimalStock(OnlineGameState online, RanchShopData catalog, float canvasWidth)
    {
        Texture2D font = StardewUiAtlas.FontBold;
        float x = (canvasWidth - AnimalMenuWidth) * 0.5f, y = 72f;
        var box = new Rect(x, y, AnimalMenuWidth, AnimalMenuHeight);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        string title = "Mua vật nuôi";
        float titleWidth = NpcBitmapText.GetStringWidth(title, 2.4f) + 80f;
        var titleScroll = new Rect(x + 96f - 40f, y - 56f, titleWidth, 72f);
        DrawScroll(titleScroll);
        NpcBitmapText.Draw(font, new Rect(titleScroll.x, titleScroll.y + 18f, titleScroll.width, 40f), title, 2.4f, centered: true);

        var cancel = new Rect(box.xMax + 4f, box.yMax - 64f - 40f, 64f, 64f);
        if (SpriteButton(cancel, CancelSource, Color.white) && !purchasePending) { Close(); return; }
        if (online?.Snapshot == null || catalog?.animals == null)
        {
            DrawText(font, new Rect(x + 40f, y + 120f, 400f, 40f), "Chưa tải được dữ liệu.", 2f);
            return;
        }

        RanchOfferView hovered = null;
        string hoveredLock = null;
        for (int i = 0; i < catalog.animals.Length; i++)
        {
            RanchOfferView offer = catalog.animals[i];
            var slot = new Rect(x + 40f + i % 3 * 128f, y + 96f + 20f + i / 3 * 85f, 128f, 64f);
            string locked = AnimalLock(offer, online.Snapshot);
            bool hover = slot.Contains(Event.current.mousePosition);
            Rect art = hover && locked == null ? new Rect(slot.center - slot.size * 0.55f, slot.size * 1.1f) : slot;
            StardewUiAtlas.DrawCursorsSprite(art, new Rect(i % 3 * 32, 448 + i / 3 * 16, 32, 16),
                locked == null ? Color.white : new Color(0f, 0f, 0f, 0.4f));
            if (hover) { hovered = offer; hoveredLock = locked; }
            if (!GUI.Button(slot, GUIContent.none, GUIStyle.none) || purchasePending) continue;
            if (locked != null) { GameAudio.Play("cancel"); continue; }
            if (GetCurrentFunds() < offer.price)
            {
                GameAudio.Play("cancel");
                feedback = "Bạn không đủ tiền.";
                continue;
            }
            animalBeingPurchased = offer;
            feedback = "";
            BeginFarmTask(FarmTask.PlaceAnimal);
        }

        if (hovered != null)
        {
            if (hoveredLock != null) DrawHoverText(hoveredLock);
            else
            {
                var nameScroll = new Rect(x + 16f + 64f, box.yMax - 32f + 48f, NpcBitmapText.GetStringWidth("Truffle Pig", 2.4f) + 80f, 72f);
                DrawScroll(nameScroll);
                NpcBitmapText.Draw(font, new Rect(nameScroll.x, nameScroll.y + 18f, nameScroll.width, 40f), hovered.name, 2.4f, centered: true);
                var priceScroll = new Rect(x + 16f + 128f, nameScroll.yMax + 8f, NpcBitmapText.GetStringWidth("$99999999g", 2.4f) + 80f, 72f);
                DrawScroll(priceScroll);
                DrawColored(new Rect(priceScroll.x + 40f, priceScroll.y + 18f, priceScroll.width - 80f, 40f),
                    $"$ {hovered.price:N0}g", 2.2f, GetCurrentFunds() >= hovered.price ? TextColor : new Color(TextColor.r, TextColor.g, TextColor.b, 0.5f));
                DrawAnimalDescription(hovered);
            }
        }
        if (!string.IsNullOrEmpty(feedback))
            DrawColored(new Rect(x + 40f, box.yMax - 70f, AnimalMenuWidth - 80f, 36f), feedback, 1.7f, Color.red);
    }

    private static void DrawAnimalDescription(RanchOfferView offer)
    {
        GameText.DrawTooltip(offer.name, offer.description ?? "", 760f, 640f);
    }

    // ── Farm part ────────────────────────────────────────────────────────────────────────────

    private bool CanHouse(OnlineRanchHome home)
    {
        var snapshot = inventory?.GetComponent<OnlineGameState>()?.Snapshot;
        return home != null && snapshot != null && animalBeingPurchased != null &&
               home.kind == animalBeingPurchased.kind && home.level >= animalBeingPurchased.level &&
               AnimalsIn(home, snapshot) < home.capacity;
    }

    private void ChooseAnimalHome(OnlineRanchHome home)
    {
        if (home == null) return;
        if (!CanHouse(home))
        {
            GameAudio.Play("cancel");
            ShowHud(home.kind != animalBeingPurchased.kind || home.level < animalBeingPurchased.level
                ? $"{animalBeingPurchased.name} không sống được ở đây." : "Chuồng này đã đầy.");
            return;
        }
        ClearBuildingTint();
        animalHome = home;
        namingAnimal = true;
        animalNameFocus = true;
        animalName = RandomAnimalName();
        GameAudio.Play("smallSelect");
    }

    private static string RandomAnimalName() =>
        NameStarts[UnityEngine.Random.Range(0, NameStarts.Length)] + NameEnds[UnityEngine.Random.Range(0, NameEnds.Length)];

    private void DrawNamingBox(float width, float height)
    {
        Texture2D font = StardewUiAtlas.FontBold;
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = previous;
        var box = new Rect(width / 2f - 256f, height / 2f - 192f - 32f, 512f, 192f);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        NpcBitmapText.Draw(font, new Rect(box.x + 40f, box.y + 64f + 8f, 440f, 36f), "Đặt tên cho vật nuôi:", 2f);
        var field = new Rect(width / 2f - 192f, height / 2f, 256f, 48f);
        StardewUiAtlas.DrawTextureBox(new Rect(field.x - 8f, field.y - 6f, field.width + 16f, field.height + 12f),
            StardewUiAtlas.MenuTiles, StardewUiAtlas.MenuBoxSourceRect, Color.white, 1f);
        GUI.SetNextControlName("AnimalName");
        var style = new GUIStyle(GUI.skin.textField) { fontSize = 22, normal = { textColor = TextColor }, focused = { textColor = TextColor } };
        style.normal.background = style.focused.background = style.hover.background = null;
        animalName = GUI.TextField(field, animalName ?? "", 18, style);
        if (animalNameFocus) { GUI.FocusControl("AnimalName"); animalNameFocus = false; }

        var done = new Rect(field.xMax + 32f + 4f, height / 2f - 8f, 64f, 64f);
        var dice = new Rect(field.xMax + 64f + 48f - 8f + 64f, height / 2f + 4f, 40f, 40f);
        bool enter = Event.current.type == EventType.KeyDown &&
                     (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (SpriteButton(dice, RandomNameSource, Color.white) && !purchasePending)
        {
            animalName = RandomAnimalName();
            GameAudio.Play("drumkit6");
        }
        if ((SpriteButton(done, ConfirmSource, Color.white) || enter) && !purchasePending) SubmitAnimalPurchase();
    }

    private void SubmitAnimalPurchase()
    {
        var online = inventory?.GetComponent<OnlineGameState>();
        string name = animalName?.Trim() ?? "";
        if (online == null || animalHome == null || animalBeingPurchased == null) return;
        if (name.Length == 0) { GameAudio.Play("cancel"); return; }
        RanchOfferView offer = animalBeingPurchased;
        purchasePending = true;
        online.BuyRanchAnimal(offer.id, name, animalHome.id, error =>
        {
            purchasePending = false;
            if (error != null)
            {
                GameAudio.Play("cancel");
                ShowHud(error);
                return;
            }
            GameAudio.Play("purchase");
            namingAnimal = false;
            animalHome = null;
            EndFarmTask();
            feedback = "";
            Close();
            NpcConversation.ShowNotice($"Marnie: Tuyệt! Cô sẽ đưa {name} về trang trại của cháu ngay. Nhớ cho ăn và vuốt ve mỗi ngày nhé.");
        });
    }

    private void CancelNaming()
    {
        namingAnimal = false;
        animalHome = null;
        GameAudio.Play("bigDeSelect");
    }
}
