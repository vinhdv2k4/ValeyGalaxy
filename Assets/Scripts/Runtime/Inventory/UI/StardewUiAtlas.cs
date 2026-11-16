using UnityEngine;

/// <summary>
/// Central shared provider for UI textures and 9-slice box drawing.
/// Provides access to UI textures and atlas sprites.
/// </summary>
public static class StardewUiAtlas
{
    private static Texture2D cursorsTexture;
    private static Texture2D menuTilesTexture;
    private static Texture2D fontTexture;

    public const float CursorsWidth = 704f;
    public const float CursorsHeight = 2256f;
    public const float MenuTilesWidth = 256f;
    public const float MenuTilesHeight = 1152f;

    // Standard menu dialog box rect
    public static readonly Rect MenuBoxSourceRect = new(0, 256, 60, 60);

    // Standard HUD Rects in Cursors.png
    public static readonly Rect PortraitPlateRect = new(583, 411, 115, 97);
    public static readonly Rect DividerTopCapRect = new(278, 313, 10, 7);
    public static readonly Rect DividerBotCapRect = new(278, 328, 10, 8);
    public static readonly Rect DividerStripeRect = new(278, 324, 9, 1);
    public static readonly Rect FriendshipJewelRect = new(140, 532, 11, 11);
    public static readonly Rect DialogueCloseScrollRect = new(289, 342, 11, 12);
    public static readonly Rect DialogueNextArrowRect = new(232, 346, 9, 9);
    public static readonly Rect UpperRightCloseButtonRect = new(337, 494, 12, 12);

    // Energy / Stamina meter in Cursors.png
    public static readonly Rect StaminaTopRect = new(256, 408, 12, 16);
    public static readonly Rect StaminaMidRect = new(256, 424, 12, 16);
    public static readonly Rect StaminaBotRect = new(256, 448, 12, 16);

    // Health meter in Cursors.png
    public static readonly Rect HealthTopRect = new(268, 408, 12, 16);
    public static readonly Rect HealthMidRect = new(268, 424, 12, 16);
    public static readonly Rect HealthBotRect = new(268, 448, 12, 16);

    // Mine level plaque in Cursors.png
    public static readonly Rect MineLevelPlaqueLeft = new(327, 281, 3, 17);
    public static readonly Rect MineLevelPlaqueMid = new(330, 281, 1, 17);
    public static readonly Rect MineLevelPlaqueRight = new(333, 281, 3, 17);

    // Elevator buttons
    public static readonly Rect ElevatorButtonNormal = new(256, 256, 10, 10);
    public static readonly Rect ElevatorButtonHover = new(267, 256, 10, 10);

    // Dialogue box source rectangles
    public static readonly Rect DialogueParchmentRect = new(306, 320, 16, 16);
    public static readonly Rect DialogueBorderTopRect = new(275, 313, 1, 6);
    public static readonly Rect DialogueBorderBotRect = new(275, 328, 1, 8);
    public static readonly Rect DialogueBorderLeftRect = new(264, 325, 8, 1);
    public static readonly Rect DialogueBorderRightRect = new(293, 324, 7, 1);
    public static readonly Rect DialogueCornerTLRect = new(261, 311, 14, 13);
    public static readonly Rect DialogueCornerTRRect = new(291, 311, 12, 11);
    public static readonly Rect DialogueCornerBRRect = new(291, 326, 12, 12);
    public static readonly Rect DialogueCornerBLRect = new(261, 327, 14, 11);

    // Inventory Menu slot tiles
    public static readonly Rect SlotNormalRect = new(128, 128, 64, 64);
    public static readonly Rect SlotSelectedRect = new(0, 896, 64, 64);
    public static readonly Rect SlotLockedRect = new(64, 896, 64, 64);

    // Trash can in Cursors.png (CraftingPage.cs:71, InventoryPage.cs:77)
    public static readonly Rect TrashCanBodyRect = new(564, 102, 18, 26);
    public static readonly Rect TrashCanLidRect = new(564, 129, 18, 10);

    /// <summary>
    /// The wooden money counter from the day/time box: eight maroon digit wheels starting 10 pixels
    /// in and 6 down from the box corner, one every 6 pixels; zeros before the first digit stay blank.
    /// </summary>
    internal static void DrawMoneyBox(Vector2 position, long money, float scale = 4f)
    {
        DrawCursorsSprite(new Rect(position.x, position.y, 65f * scale, 17f * scale), new Rect(340, 472, 65, 17));
        long shown = money < 0 ? 0 : money > 99999999 ? 99999999 : money;
        string digits = shown.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        Color maroon = new(128f / 255f, 0f, 0f, GUI.color.a);
        bool started = false;
        for (int i = 0; i < 8; i++)
        {
            int digit = digits[i] - '0';
            started |= digit != 0 || i == 7;
            if (!started) continue;
            DrawCursorsSprite(new Rect(position.x + (10f + 6f * i) * scale, position.y + 6f * scale, 5f * scale, 8f * scale),
                new Rect(286, 502 - digit * 8, 5, 8), maroon);
        }
    }

    /// <summary>Redraws the HUD money box over a dimmed menu, where the clock box keeps it.</summary>
    internal static void DrawHudMoneyBox(long money)
    {
        Rect? frame = DayTimeHud.MoneyFrameScreenRect;
        if (frame == null || frame.Value.width <= 0f) return;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        DrawMoneyBox(frame.Value.position, money, frame.Value.width / 65f);
        GUI.matrix = old;
    }

    /// <summary>Each trash can upgrade uses the next 18-pixel column of the sheet.</summary>
    internal static Rect TrashCanBody(int level) => new(564 + Mathf.Clamp(level, 0, 4) * 18, 102, 18, 26);
    internal static Rect TrashCanLid(int level) => new(564 + Mathf.Clamp(level, 0, 4) * 18, 129, 18, 10);

    /// <summary>Draws the trash can with its lid swung open while an item hovers over it (IMGUI).</summary>
    internal static void DrawTrashCan(Rect body, int level, bool open)
    {
        DrawCursorsSprite(body, TrashCanBody(level));
        Rect lid = new(body.x, body.y, body.width, body.width * 10f / 18f);
        if (!open) { DrawCursorsSprite(lid, TrashCanLid(level)); return; }
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(-35f, new Vector2(lid.x, lid.yMax));
        DrawCursorsSprite(new Rect(lid.x, lid.y - 4f, lid.width, lid.height), TrashCanLid(level));
        GUI.matrix = matrix;
    }

    public static Texture2D Cursors
    {
        get
        {
            if (cursorsTexture == null) LoadAtlases();
            return cursorsTexture;
        }
    }

    public static Texture2D MenuTiles
    {
        get
        {
            if (menuTilesTexture == null) LoadAtlases();
            return menuTilesTexture;
        }
    }

    public static Texture2D FontBold
    {
        get
        {
            if (fontTexture == null) LoadAtlases();
            return fontTexture;
        }
    }

    private static void LoadAtlases()
    {
        NpcCatalog catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (catalog != null)
        {
            if (catalog.cursors != null) cursorsTexture = catalog.cursors;
            if (catalog.dialogueFrame != null) menuTilesTexture = catalog.dialogueFrame;
            if (catalog.dialogueFont != null) fontTexture = catalog.dialogueFont;
        }

#if UNITY_EDITOR
        if (cursorsTexture == null)
            cursorsTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/Cursors.png");
        if (menuTilesTexture == null)
            menuTilesTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Maps/MenuTiles.png");
        if (fontTexture == null)
            fontTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/font_bold.png");
#endif
    }

    public static Rect ToUv(Rect pixelRect, float atlasW, float atlasH)
    {
        return new Rect(
            pixelRect.x / atlasW,
            (atlasH - pixelRect.y - pixelRect.height) / atlasH,
            pixelRect.width / atlasW,
            pixelRect.height / atlasH
        );
    }

    public static Rect ToUvCursors(Rect pixelRect) => ToUv(pixelRect, CursorsWidth, CursorsHeight);
    public static Rect ToUvMenuTiles(Rect pixelRect) => ToUv(pixelRect, MenuTilesWidth, MenuTilesHeight);

    public static Rect MenuTileRect(int index) => new(index % 4 * 64, index / 4 * 64, 64, 64);

    internal static void DrawGameMenuFrame(Rect frame)
    {
        float x = frame.x;
        float y = frame.y;
        float w = frame.width;
        float h = frame.height;
        DrawMenuTile(9, new Rect(x + 28, y + 28, w - 64, h - 64));
        DrawMenuTile(0, new Rect(x, y, 64, 64));
        DrawMenuTile(2, new Rect(x + 64, y, w - 128, 64));
        DrawMenuTile(3, new Rect(x + w - 64, y, 64, 64));
        DrawMenuTile(8, new Rect(x, y + 64, 64, h - 128));
        DrawMenuTile(11, new Rect(x + w - 64, y + 64, 64, h - 128));
        DrawMenuTile(12, new Rect(x, y + h - 64, 64, 64));
        DrawMenuTile(14, new Rect(x + 64, y + h - 64, w - 128, 64));
        DrawMenuTile(15, new Rect(x + w - 64, y + h - 64, 64, 64));
    }

    internal static void DrawMenuTile(int index, Rect destination) =>
        DrawMenuTilesSprite(destination, MenuTileRect(index));

    internal static string StackDigits(int amount) => amount is > 1 and < int.MaxValue
        ? amount.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    internal static Rect TinyDigitSource(char digit) => new(368 + (digit - '0') * 5, 56, 5, 7);

    internal static Rect StackDigitBounds(Rect slot, int digitCount, int digitIndex)
    {
        // Object.drawInMenu uses 3x scale and Utility.drawTinyDigits advances 14px per digit.
        float scale = slot.width / 64f;
        return new Rect(slot.xMax + (9f - digitCount * 15f + digitIndex * 14f) * scale,
            slot.yMax - 17f * scale, 15f * scale, 21f * scale);
    }

    internal static void DrawStackCount(Rect slot, int amount, string itemId)
    {
        ItemQuality.DrawStar(slot, itemId);
        DrawStackCount(slot, amount);
    }

    internal static void DrawStackCount(Rect slot, int amount)
    {
        if (Event.current.type != EventType.Repaint) return;
        string digits = StackDigits(amount);
        for (int i = 0; i < digits.Length; i++)
            DrawCursorsSprite(StackDigitBounds(slot, digits.Length, i), TinyDigitSource(digits[i]), Color.white);
    }

    public static void DrawCursorsSprite(Rect destination, Rect sourceRect, Color? color = null)
    {
        Texture2D tex = Cursors;
        if (tex == null) return;
        Color oldColor = GUI.color;
        if (color.HasValue) GUI.color = color.Value;
        GUI.DrawTextureWithTexCoords(destination, tex, ToUvCursors(sourceRect));
        GUI.color = oldColor;
    }

    public static void DrawMenuTilesSprite(Rect destination, Rect sourceRect, Color? color = null)
    {
        Texture2D tex = MenuTiles;
        if (tex == null) return;
        Color oldColor = GUI.color;
        if (color.HasValue) GUI.color = color.Value;
        GUI.DrawTextureWithTexCoords(destination, tex, ToUvMenuTiles(sourceRect));
        GUI.color = oldColor;
    }

    /// <summary>
    /// Renders the standard dialogue box using 9-slice layout.
    /// Uses parchment center, wooden carved borders, and 4 corner rivets.
    /// </summary>
    public static void DrawStardewDialogueBox(Rect box)
    {
        float x = box.x;
        float y = box.y;
        float w = box.width;
        float h = box.height;

        // 1. Parchment center fill (306, 320, 16, 16)
        DrawCursorsSprite(new Rect(x, y, w, h), DialogueParchmentRect);

        // 2. Wooden carved borders
        DrawCursorsSprite(new Rect(x, y - 20f, w, 24f), DialogueBorderTopRect);
        DrawCursorsSprite(new Rect(x + 12f, y + h, w - 20f, 32f), DialogueBorderBotRect);
        DrawCursorsSprite(new Rect(x - 32f, y + 24f, 32f, h - 28f), DialogueBorderLeftRect);
        DrawCursorsSprite(new Rect(x + w, y, 28f, h), DialogueBorderRightRect);

        // 3. Ornate Carved Corners at 4x scale
        DrawCursorsSprite(new Rect(x - 44f, y - 28f, 56f, 52f), DialogueCornerTLRect);
        DrawCursorsSprite(new Rect(x + w - 8f, y - 28f, 48f, 44f), DialogueCornerTRRect);
        DrawCursorsSprite(new Rect(x + w - 8f, y + h - 8f, 48f, 48f), DialogueCornerBRRect);
        DrawCursorsSprite(new Rect(x - 44f, y + h - 4f, 56f, 44f), DialogueCornerBLRect);
    }

    /// <summary>
    /// Renders the standard dialogue frame.
    /// </summary>
    public static void DrawDialogueBox(Rect box, Color? tint = null, bool drawShadow = true)
    {
        DrawStardewDialogueBox(box);
    }

    public static void DrawTextureBox(Rect box, Texture2D texture, Rect sourceRect, Color color, float scale = 4f, bool drawShadow = true)
    {
        if (texture == null)
        {
            GUI.Box(box, GUIContent.none);
            return;
        }

        float atlasW = texture.width;
        float atlasH = texture.height;

        int num = Mathf.RoundToInt(sourceRect.width / 3f); // 20px
        float corner = num * scale; // 80px

        float x = box.x;
        float y = box.y;
        float width = box.width;
        float height = box.height;

        Rect srcTL = new(sourceRect.x, sourceRect.y, num, num);
        Rect srcTM = new(sourceRect.x + num, sourceRect.y, num, num);
        Rect srcTR = new(sourceRect.x + num * 2, sourceRect.y, num, num);
        Rect srcML = new(sourceRect.x, sourceRect.y + num, num, num);
        Rect srcCenter = new(sourceRect.x + num, sourceRect.y + num, num, num);
        Rect srcMR = new(sourceRect.x + num * 2, sourceRect.y + num, num, num);
        Rect srcBL = new(sourceRect.x, sourceRect.y + num * 2, num, num);
        Rect srcBM = new(sourceRect.x + num, sourceRect.y + num * 2, num, num);
        Rect srcBR = new(sourceRect.x + num * 2, sourceRect.y + num * 2, num, num);

        // 1. Draw Drop Shadow (offset +8px right and down, 40% black)
        if (drawShadow)
        {
            Color oldC = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            float sx = x + 8f;
            float sy = y + 8f;

            // Shadow corners & edges & center
            GUI.DrawTextureWithTexCoords(new Rect(sx, sy, corner, corner), texture, ToUv(srcTL, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx + width - corner, sy, corner, corner), texture, ToUv(srcTR, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx, sy + height - corner, corner, corner), texture, ToUv(srcBL, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx + width - corner, sy + height - corner, corner, corner), texture, ToUv(srcBR, atlasW, atlasH));

            GUI.DrawTextureWithTexCoords(new Rect(sx + corner, sy, width - corner * 2f, corner), texture, ToUv(srcTM, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx + corner, sy + height - corner, width - corner * 2f, corner), texture, ToUv(srcBM, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx, sy + corner, corner, height - corner * 2f), texture, ToUv(srcML, atlasW, atlasH));
            GUI.DrawTextureWithTexCoords(new Rect(sx + width - corner, sy + corner, corner, height - corner * 2f), texture, ToUv(srcMR, atlasW, atlasH));

            GUI.DrawTextureWithTexCoords(new Rect(sx + corner, sy + corner, width - corner * 2f, height - corner * 2f), texture, ToUv(srcCenter, atlasW, atlasH));
            GUI.color = oldC;
        }

        // 2. Draw Main Frame
        Color previousColor = GUI.color;
        GUI.color = color;

        // Center parchment
        GUI.DrawTextureWithTexCoords(new Rect(x + corner, y + corner, width - corner * 2f, height - corner * 2f), texture, ToUv(srcCenter, atlasW, atlasH));

        // Corners
        GUI.DrawTextureWithTexCoords(new Rect(x, y, corner, corner), texture, ToUv(srcTL, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x + width - corner, y, corner, corner), texture, ToUv(srcTR, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x, y + height - corner, corner, corner), texture, ToUv(srcBL, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x + width - corner, y + height - corner, corner, corner), texture, ToUv(srcBR, atlasW, atlasH));

        // Edges
        GUI.DrawTextureWithTexCoords(new Rect(x + corner, y, width - corner * 2f, corner), texture, ToUv(srcTM, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x + corner, y + height - corner, width - corner * 2f, corner), texture, ToUv(srcBM, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x, y + corner, corner, height - corner * 2f), texture, ToUv(srcML, atlasW, atlasH));
        GUI.DrawTextureWithTexCoords(new Rect(x + width - corner, y + corner, corner, height - corner * 2f), texture, ToUv(srcMR, atlasW, atlasH));

        GUI.color = previousColor;
    }

    /// <summary>Red-to-yellow-to-green stamina color gradient.</summary>
    public static Color GetRedToGreenLerpColor(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) * 2f;
            return Color.Lerp(new Color(1f, 0.82f, 0.15f), new Color(0.18f, 0.88f, 0.12f), t);
        }
        else
        {
            float t = ratio * 2f;
            return Color.Lerp(new Color(0.92f, 0.18f, 0.12f), new Color(1f, 0.82f, 0.15f), t);
        }
    }
}
