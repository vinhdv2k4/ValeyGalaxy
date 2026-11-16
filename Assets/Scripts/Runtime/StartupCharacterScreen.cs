using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UIText = UnityEngine.UI.Text;

/// <summary>
/// Builds the new farmer screen at runtime using standard
/// dialogue box and texture box frame layouts.
/// No CharacterCreation prefab or pre-cut frame image is needed.
/// </summary>
public static class StartupCharacterScreen
{
    private static readonly Color32 Ink = new Color32(34, 17, 34, 255);
    private static readonly Dictionary<string, Sprite> CursorSprites = new Dictionary<string, Sprite>();

    public static GameObject Build(Transform canvas)
    {
        var root = Node("CharacterCreation", canvas, Vector2.zero, Vector2.zero);
        Stretch((RectTransform)root.transform);
        var shade = Picture("Shade", root.transform, null, Vector2.zero, Vector2.zero);
        Stretch((RectTransform)shade.transform);
        shade.color = Color.clear;
        shade.raycastTarget = true;

        // Original 632+border-width menu is about 672 x 690 pixels in the
        // supplied 1920 x 1080 screenshot. CanvasScaler is 960 x 540, so a
        // half-scale local canvas keeps one source pixel equal to one screen pixel.
        var card = Node("Card", root.transform, new Vector2(0, -12), new Vector2(672, 690));
        card.transform.localScale = Vector3.one * 0.5f;
        DialogueBox(card.transform, 672, 690);

        // Customization UI box (0,256,60,60)
        // for this farm strip, split into 20 x 20 source cells.
        var farm = Node("FarmPanel", root.transform, new Vector2(210, 8), new Vector2(120, 652));
        farm.transform.localScale = Vector3.one * 0.5f;
        TextureBox(farm.transform, 120, 652);
        Bitmap("FarmPanelTitle", farm.transform, "FARM", new Vector2(0, 291), new Vector2(95, 26), 0.5f);
        string[] farmNames = { "Standard", "Riverland", "Forest", "Hills", "Wilderness", "FourCorners", "Beach" };
        for (int i = 0; i < farmNames.Length; i++)
        {
            // CharacterCustomization.LoadFarmTypeData uses five cells in row 324,
            // followed by two cells in row 345 of mouseCursors.
            int sourceX = i < 5 ? i * 22 : (i - 5) * 22;
            int sourceY = i < 5 ? 324 : 345;
            var button = Click("FarmBtn_" + i, farm.transform, CursorRect(sourceX, sourceY, 22, 20),
                new Vector2(0, 235 - i * 79), new Vector2(88, 80));
            button.interactable = i == 1; // Riverland is the server's fixed farm.
            button.transition = Selectable.Transition.None;
            button.GetComponent<Image>().color = i == 1 ? Color.white : new Color(1, 1, 1, 0.65f);
        }
        var highlight = Node("FarmHighlight", farm.transform,
            new Vector2(0, 235 - 79), new Vector2(92, 84));
        TextureBox(highlight.transform, 92, 84, StardewUiAtlas.Cursors, 375, 357, 3, 4, false);

        // Portrait: the preview layers remain independent so hair, clothes and
        // colours can change without re-cutting or rebuilding the atlas.
        var portrait = Node("PortraitFrame", card.transform, new Vector2(-190, 207), new Vector2(128, 192));
        Picture("DayBg", portrait.transform, Resource("daybg_0"), Vector2.zero, new Vector2(128, 192));
        Picture("PreviewBody", portrait.transform, Resource("FarmerBody"), Vector2.zero, new Vector2(64, 128));
        Picture("PreviewPants", portrait.transform, null, Vector2.zero, new Vector2(64, 128));
        Picture("PreviewShirt", portrait.transform, Resource("FarmerShirt"), new Vector2(0, -8), new Vector2(32, 32));
        Picture("PreviewShirtDye", portrait.transform, null, new Vector2(0, -8), new Vector2(32, 32));
        Picture("PreviewHair", portrait.transform, Resource("HairStyle0"), Vector2.zero, new Vector2(64, 128));
        Picture("PreviewAccessory", portrait.transform, null, new Vector2(0, 16), new Vector2(64, 64));
        Picture("PreviewArms", portrait.transform, null, Vector2.zero, new Vector2(64, 128));

        // Tile 44 (left), 33 (right),
        // 46 (OK). Tile position * 64 wraps by Cursors.width (704).
        Click("DirectionPrev", card.transform, CursorTile(44), new Vector2(-254, 133), new Vector2(64, 64));
        Click("DirectionNext", card.transform, CursorTile(33), new Vector2(-126, 133), new Vector2(64, 64));
        Click("Randomize", card.transform, CursorRect(381, 361, 10, 10),
            new Vector2(-288, 290), new Vector2(40, 40));

        Bitmap("NameLabel", card.transform, "Name", new Vector2(-59, 283), new Vector2(110, 32), 0.5f, TextAnchor.MiddleLeft);
        Input("NameInput", card.transform, "Farmer name", new Vector2(118, 283));
        Bitmap("FarmLabel", card.transform, "Farm", new Vector2(-59, 233), new Vector2(110, 27), 0.5f, TextAnchor.MiddleLeft);
        Bitmap("FarmLabelSecond", card.transform, "Name", new Vector2(-59, 203), new Vector2(110, 27), 0.5f, TextAnchor.MiddleLeft);
        Input("FarmInput", card.transform, "Farm name", new Vector2(118, 218));
        Bitmap("FarmSuffix", card.transform, "Farm", new Vector2(258, 218), new Vector2(70, 32), 0.5f, TextAnchor.MiddleLeft);
        Bitmap("FavoriteLabel", card.transform, "Favorite", new Vector2(-34, 168), new Vector2(112, 27), 0.5f, TextAnchor.MiddleLeft);
        Bitmap("FavoriteLabelSecond", card.transform, "Thing", new Vector2(-34, 138), new Vector2(112, 27), 0.5f, TextAnchor.MiddleLeft);
        Input("FavoriteInput", card.transform, "Favorite thing", new Vector2(118, 153));

        Click("GenderMale", card.transform, CursorRect(128, 192, 16, 16), new Vector2(-228, 67), new Vector2(64, 64));
        Picture("GenderMaleCheck", card.transform, CursorTile(34), new Vector2(-228, 67), new Vector2(64, 64));
        Click("GenderFemale", card.transform, CursorRect(144, 192, 16, 16), new Vector2(-148, 67), new Vector2(64, 64));
        Picture("GenderFemaleCheck", card.transform, CursorTile(34), new Vector2(-148, 67), new Vector2(64, 64));
        Bitmap("AnimalLabel", card.transform, "Animal", new Vector2(-41, 81), new Vector2(155, 27), 0.5f, TextAnchor.MiddleLeft);
        Bitmap("AnimalLabelSecond", card.transform, "Preference", new Vector2(-41, 52), new Vector2(155, 27), 0.5f, TextAnchor.MiddleLeft);
        Click("PetPrev", card.transform, CursorTile(44), new Vector2(103, 67), new Vector2(64, 64));
        Picture("PreviewPet", card.transform, CursorRect(160, 208, 16, 16), new Vector2(168, 67), new Vector2(48, 48));
        Click("PetNext", card.transform, CursorTile(33), new Vector2(233, 67), new Vector2(64, 64));

        string[] keys = { "Skin", "Hair", "Shirt", "Pants", "Accessory" };
        string[] labels = { "Skin", "Hair", "Shirt", "Pants", "Acc." };
        for (int i = 0; i < keys.Length; i++)
            Selector(card.transform, keys[i], labels[i], 4 - i * 68);

        ColorGroup(card.transform, "Eye", "Eye Color:", 4);
        ColorGroup(card.transform, "Hair", "Hair Color:", -65);
        ColorGroup(card.transform, "Pants", "Pants Color:", -134);

        Click("SkipIntroCheckbox", card.transform, CursorRect(227, 425, 9, 9),
            new Vector2(-7, -199), new Vector2(36, 36));
        Bitmap("SkipIntroLabel", card.transform, "Skip Intro", new Vector2(89, -199),
            new Vector2(145, 34), 0.5f, TextAnchor.MiddleLeft);
        Click("Create", card.transform, CursorTile(46), new Vector2(267, -276), new Vector2(64, 64));

        // Keep Back reachable on keyboard/mouse without covering the original box.
        var back = BoxButton("Back", card.transform, new Vector2(803, -455), new Vector2(180, 80));
        Bitmap("Label", back.transform, "BACK", Vector2.zero, new Vector2(156, 48), 0.85f);
        Picture("Arrow", back.transform, CursorTile(33), new Vector2(98, 0), new Vector2(32, 32));
        Text("Message", card.transform, string.Empty, new Vector2(148, 106), new Vector2(300, 25), 13);

        root.SetActive(false);
        return root;
    }

    private static void Selector(Transform card, string key, string label, float y)
    {
        Click(key + "Prev", card, CursorTile(44), new Vector2(-254, y), new Vector2(64, 64));
        Bitmap(key + "Label", card, label, new Vector2(-189, y + 11), new Vector2(74, 26), 0.5f);
        Bitmap(key + "Value", card, "1", new Vector2(-189, y - 22), new Vector2(74, 25), 0.5f);
        Click(key + "Next", card, CursorTile(33), new Vector2(-124, y), new Vector2(64, 64));
    }

    private static void ColorGroup(Transform card, string key, string caption, float y)
    {
        Bitmap(key + "ColorLabel", card, caption, new Vector2(-14, y), new Vector2(155, 29), 0.5f, TextAnchor.MiddleLeft);
        Slider(key + "Hue", card, new Vector2(138, y + 20), true);
        Slider(key + "Saturation", card, new Vector2(138, y));
        Slider(key + "Value", card, new Vector2(138, y - 20));
    }

    private static void DialogueBox(Transform parent, float width, float height)
    {
        Texture2D texture = StardewUiAtlas.MenuTiles;
        if (texture == null) { Debug.LogError("MenuTiles atlas is missing for character creation."); return; }
        // Fill at x+28,y+28, width-64,height-64;
        // four 64px corners and four stretched 64px edge cells.
        Tile("Fill", parent, texture, 64, 128, 64, 64, 28, 28, width - 64, height - 64);
        Tile("TL", parent, texture, 0, 0, 64, 64, 0, 0, 64, 64);
        Tile("TR", parent, texture, 192, 0, 64, 64, width - 64, 0, 64, 64);
        Tile("BR", parent, texture, 192, 192, 64, 64, width - 64, height - 64, 64, 64);
        Tile("BL", parent, texture, 0, 192, 64, 64, 0, height - 64, 64, 64);
        Tile("Top", parent, texture, 128, 0, 64, 64, 64, 0, width - 128, 64);
        Tile("Bottom", parent, texture, 128, 192, 64, 64, 64, height - 64, width - 128, 64);
        Tile("Left", parent, texture, 0, 128, 64, 64, 0, 64, 64, height - 128);
        Tile("Right", parent, texture, 192, 128, 64, 64, width - 64, 64, 64, height - 128);
    }

    private static void TextureBox(Transform parent, float width, float height)
    {
        TextureBox(parent, width, height, StardewUiAtlas.MenuTiles, 0, 256, 60, 1, true);
    }

    private static void TextureBox(Transform parent, float width, float height,
        Texture2D texture, int sourceX, int sourceY, int sourceWidth, int scale, bool shadow)
    {
        if (texture == null) return;
        int cell = sourceWidth / 3;
        int edge = cell * scale;
        if (shadow)
        {
            // TextureBox offsets its dark shadow copy by (-8,+8).
            // The first copy uses a half-cell inset for its centre fill.
            Color dark = new Color(0, 0, 0, 0.4f);
            BoxCell("ShadowTR", parent, texture, sourceX, sourceY, cell, 0, 2,
                width - edge - 8, 8, edge, edge, dark);
            BoxCell("ShadowBL", parent, texture, sourceX, sourceY, cell, 2, 0,
                -8, height - edge + 8, edge, edge, dark);
            BoxCell("ShadowBR", parent, texture, sourceX, sourceY, cell, 2, 2,
                width - edge - 8, height - edge + 8, edge, edge, dark);
            BoxCell("ShadowTop", parent, texture, sourceX, sourceY, cell, 0, 1,
                edge - 8, 8, width - 2 * edge, edge, dark);
            BoxCell("ShadowBottom", parent, texture, sourceX, sourceY, cell, 2, 1,
                edge - 8, height - edge + 8, width - 2 * edge, edge, dark);
            BoxCell("ShadowLeft", parent, texture, sourceX, sourceY, cell, 1, 0,
                -8, edge + 8, edge, height - 2 * edge, dark);
            BoxCell("ShadowRight", parent, texture, sourceX, sourceY, cell, 1, 2,
                width - edge - 8, edge + 8, edge, height - 2 * edge, dark);
            BoxCell("ShadowFill", parent, texture, sourceX, sourceY, cell, 1, 1,
                edge / 2f - 8, edge / 2f + 8, width - edge, height - edge, dark);
        }
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
        {
            float x = column == 0 ? 0 : column == 1 ? edge : width - edge;
            float y = row == 0 ? 0 : row == 1 ? edge : height - edge;
            float w = column == 1 ? width - 2 * edge : edge;
            float h = row == 1 ? height - 2 * edge : edge;
            BoxCell("Frame" + row + column, parent, texture, sourceX, sourceY,
                cell, row, column, x, y, w, h, Color.white);
        }
    }

    private static void BoxCell(string name, Transform parent, Texture2D texture,
        int sourceX, int sourceY, int cell, int row, int column,
        float x, float y, float width, float height, Color tint)
    {
        Tile(name, parent, texture, sourceX + column * cell, sourceY + row * cell,
            cell, cell, x, y, width, height, tint);
    }

    private static void Tile(string name, Transform parent, Texture2D texture,
        float sx, float sy, float sw, float sh, float x, float y, float width, float height,
        Color? tint = null)
    {
        RectTransform frame = (RectTransform)parent;
        var go = Node(name, parent,
            new Vector2(-frame.rect.width * 0.5f + x + width * 0.5f,
                        frame.rect.height * 0.5f - y - height * 0.5f),
            new Vector2(width, height));
        var raw = go.AddComponent<RawImage>();
        raw.texture = texture;
        raw.uvRect = StardewUiAtlas.ToUv(new Rect(sx, sy, sw, sh), texture.width, texture.height);
        raw.color = tint ?? Color.white;
        raw.raycastTarget = false;
    }

    private static Sprite CursorTile(int index)
    {
        Texture2D atlas = StardewUiAtlas.Cursors;
        if (atlas == null) return null;
        // Source rect calculation for 64x64 cursor index
        return CursorRect(index * 64 % atlas.width, index * 64 / atlas.width * 64, 64, 64);
    }

    public static Sprite CropCursors(int x, int y, int width, int height) => CursorRect(x, y, width, height);

    private static Sprite CursorRect(int x, int y, int width, int height, Vector4 border = default)
    {
        Texture2D atlas = StardewUiAtlas.Cursors;
        if (atlas == null) return null;
        string key = $"{x},{y},{width},{height},{border.x},{border.y},{border.z},{border.w}";
        if (CursorSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Sprite sprite = Sprite.Create(atlas,
            new Rect(x, atlas.height - y - height, width, height),
            new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect, border);
        CursorSprites[key] = sprite;
        return sprite;
    }

    private static Sprite Resource(string name) => Resources.Load<Sprite>("Startup/" + name);

    private static GameObject Node(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = Node(name, parent, pos, size);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Button Click(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        Image image = Picture(name, parent, sprite, pos, size);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static Button BoxButton(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        Image hit = Picture(name, parent, null, pos, size);
        hit.color = Color.clear;
        hit.raycastTarget = true;
        TextureBox(hit.transform, size.x, size.y);
        var button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static StardewBitmapText Bitmap(string name, Transform parent, string value,
        Vector2 pos, Vector2 size, float scale, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = Node(name, parent, pos, size);
        var text = go.AddComponent<StardewBitmapText>();
        text.Text = value;
        text.FontScale = scale * 2f;
        if (text.PreferredWidth > size.x)
            text.FontScale *= size.x / text.PreferredWidth;
        text.Alignment = align;
        text.color = Ink;
        text.raycastTarget = false;
        return text;
    }

    private static UIText Text(string name, Transform parent, string value,
        Vector2 pos, Vector2 size, int fontSize)
    {
        var go = Node(name, parent, pos, size);
        var text = go.AddComponent<UIText>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Ink;
        text.raycastTarget = false;
        return text;
    }

    private static InputField Input(string name, Transform parent, string hint, Vector2 pos)
    {
        var image = Picture(name, parent, Resource("TextBox"), pos, new Vector2(192, 48));
        image.raycastTarget = true;
        var input = image.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.characterLimit = 32;
        var viewport = Node("Viewport", image.transform, Vector2.zero, new Vector2(176, 40));
        var mask = viewport.AddComponent<RectMask2D>();
        var value = Text("Value", mask.transform, string.Empty, Vector2.zero, new Vector2(169, 38), 18);
        value.alignment = TextAnchor.MiddleLeft;
        var placeholder = Text("Placeholder", mask.transform, string.Empty, Vector2.zero, new Vector2(169, 38), 17);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.59f, 0.39f, 0.27f);
        input.textComponent = value;
        input.placeholder = placeholder;
        input.customCaretColor = true;
        input.caretColor = Ink;
        // InputField needs a uGUI Text for editing and caret placement. Draw
        // ASCII input using the same sliced atlas as the surrounding labels.
        var bitmapValue = Bitmap("BitmapValue", value.transform, string.Empty,
            Vector2.zero, new Vector2(169, 38), 0.5f, TextAnchor.MiddleLeft);
        input.onValueChanged.AddListener(current =>
        {
            bool supported = true;
            foreach (char letter in current)
                if (letter < 32 || letter > 126) { supported = false; break; }
            bitmapValue.enabled = supported;
            bitmapValue.Text = supported ? current : string.Empty;
            value.color = supported ? Color.clear : Ink;
        });
        value.color = Color.clear;
        return input;
    }

    private static Slider Slider(string name, Transform parent, Vector2 pos, bool hue = false)
    {
        var track = Picture(name, parent, Resource(hue ? "SliderTrackHue" : "SliderTrackBase"),
            pos, new Vector2(128, 12));
        track.raycastTarget = true;
        var slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 1;
        var thumb = Picture("Handle", track.transform, Resource("SliderThumb"), Vector2.zero, new Vector2(15, 18));
        thumb.raycastTarget = true;
        slider.handleRect = (RectTransform)thumb.transform;
        slider.targetGraphic = thumb;
        return slider;
    }
}
