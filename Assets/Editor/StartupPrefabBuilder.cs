using System.IO;
using UIText = UnityEngine.UI.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Builds editable uGUI prefabs from the prepared source sprites.</summary>
[InitializeOnLoad]
public static class StartupPrefabBuilder
{
    private const string Folder = "Assets/Resources/Startup";
    private const string MenuPath = Folder + "/StartupMenu.prefab";
    private const string AuthPath = Folder + "/AuthPanel.prefab";
    private const string ChestPanelPath = Folder + "/PlacedChestPanel.prefab";
    private const string ChestPath = Folder + "/PlayerChest.prefab";
    private static readonly Color Paper = new Color(0.96f, 0.84f, 0.61f);
    private static readonly Color Ink = new Color(0.35f, 0.20f, 0.12f);
    private static readonly Color Wood = new Color(0.48f, 0.25f, 0.12f);

    static StartupPrefabBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfMissing;
            return;
        }
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        bool builtByEditor = false;
        if (menu != null)
            foreach (string label in AssetDatabase.GetLabels(menu))
                if (label == "startup-ui-editor-v9") builtByEditor = true;
        if (!File.Exists(MenuPath) || !File.Exists(AuthPath) ||
            !File.Exists(ChestPanelPath) || !File.Exists(ChestPath) || !builtByEditor)
            Rebuild();
    }

    [MenuItem("Tools/Startup UI/Rebuild Prefabs")]
    public static void Rebuild()
    {
        Directory.CreateDirectory(Folder);
        foreach (string path in Directory.GetFiles(Folder, "*.png"))
        {
            string assetPath = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            Vector4 border = Path.GetFileName(path) switch
            {
                "MenuBox.png" => new Vector4(12, 12, 16, 12),
                "FarmBorder.png" => new Vector4(1, 1, 1, 1),
                _ => Vector4.zero
            };
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spritePixelsPerUnit != 16 || importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        GameObject auth = BuildAuth();
        PrefabUtility.SaveAsPrefabAsset(auth, AuthPath);
        Object.DestroyImmediate(auth);

        GameObject chestPanel = BuildChestPanel();
        PrefabUtility.SaveAsPrefabAsset(chestPanel, ChestPanelPath);
        Object.DestroyImmediate(chestPanel);

        GameObject chest = BuildChest();
        PrefabUtility.SaveAsPrefabAsset(chest, ChestPath);
        Object.DestroyImmediate(chest);

        GameObject menu = BuildMenu();
        PrefabUtility.SaveAsPrefabAsset(menu, MenuPath);
        Object.DestroyImmediate(menu);
        var builtMenu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        if (builtMenu != null) AssetDatabase.SetLabels(builtMenu, new[] { "startup-ui-editor-v9" });
        AssetDatabase.SaveAssets();
        Debug.Log("Startup UI prefabs rebuilt in " + Folder);
    }

    private static Sprite Sprite(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");

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

    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position,
                                 Vector2 size, Color color)
    {
        var go = Node(name, parent, position, size);
        go.AddComponent<CanvasRenderer>();
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = sprite != null;
        return image;
    }

    private static UIText Text(string name, Transform parent, string value, Vector2 position,
                                 Vector2 size, int fontSize = 22, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = Node(name, parent, position, size);
        var text = go.AddComponent<UIText>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = Ink;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Vector2 position,
                                 Vector2 size, Sprite sprite = null)
    {
        var image = Picture(name, parent, sprite, position, size, sprite == null ? Paper : Color.white);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        if (sprite == null)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Wood;
            outline.effectDistance = new Vector2(3, -3);
        }
        if (!string.IsNullOrEmpty(label))
            Text("Label", button.transform, label, Vector2.zero, size - new Vector2(6, 4), 18);
        return button;
    }

    private static InputField Input(string name, Transform parent, string hint,
                                        Vector2 position, Vector2 size, bool password = false)
    {
        var image = Picture(name, parent, null, position, size, new Color(1f, 0.97f, 0.83f));
        image.raycastTarget = true;
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Wood;
        outline.effectDistance = new Vector2(2, -2);
        var input = image.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        input.characterLimit = password ? 128 : 32;
        var viewport = Node("Viewport", image.transform, Vector2.zero, size - new Vector2(14, 4));
        Stretch((RectTransform)viewport.transform);
        viewport.AddComponent<RectMask2D>();
        var value = Text("Value", viewport.transform, string.Empty, Vector2.zero,
                         size - new Vector2(18, 6), 18, TextAnchor.MiddleLeft);
        value.fontStyle = FontStyle.Normal;
        var placeholder = Text("Placeholder", viewport.transform, hint, Vector2.zero,
                               size - new Vector2(18, 6), 18, TextAnchor.MiddleLeft);
        placeholder.fontStyle = FontStyle.Normal;
        placeholder.color = new Color(0.55f, 0.45f, 0.35f);
        input.textComponent = value;
        input.placeholder = placeholder;
        return input;
    }

    private static GameObject Overlay(string name, out Transform card, Vector2 cardSize)
    {
        var root = Node(name, null, Vector2.zero, Vector2.zero);
        Stretch((RectTransform)root.transform);
        var shade = Picture("Shade", root.transform, null, Vector2.zero,
                            new Vector2(960, 540), new Color(0.05f, 0.08f, 0.07f, 0.78f));
        shade.raycastTarget = true;
        var frame = Picture("Card", root.transform, null, Vector2.zero, cardSize, Paper);
        frame.raycastTarget = true;
        var edge = frame.gameObject.AddComponent<Outline>();
        edge.effectColor = Wood;
        edge.effectDistance = new Vector2(7, -7);
        card = frame.transform;
        return root;
    }

    private static GameObject BuildAuth()
    {
        GameObject root = Overlay("AuthPanel", out Transform card, new Vector2(520, 520));
        Text("Title", card, "LOG IN", new Vector2(0, 211), new Vector2(450, 42), 30);
        Button("Back", card, "X", new Vector2(221, 218), new Vector2(37, 37));
        Input("UsernameInput", card, "Username or email", new Vector2(0, 138), new Vector2(390, 42));
        Input("EmailInput", card, "Email address", new Vector2(0, 80), new Vector2(390, 42));
        Input("PasswordInput", card, "Password", new Vector2(0, 22), new Vector2(390, 42), true);
        Input("ConfirmInput", card, "Confirm password", new Vector2(0, -36), new Vector2(390, 42), true);
        Text("Message", card, "Sign in with username or email.", new Vector2(0, -98),
             new Vector2(440, 44), 15);
        Button("Submit", card, "LOG IN", new Vector2(0, -157), new Vector2(224, 45));
        Button("Switch", card, "LOG IN / REGISTER", new Vector2(0, -220), new Vector2(270, 34));
        return root;
    }

    private static GameObject BuildChest()
    {
        var root = new GameObject("PlayerChest");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite("ChestClosed");
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.9f, 0.85f);
        root.AddComponent<PlacedPlayerChest>();
        return root;
    }

    private static GameObject BuildChestPanel()
    {
        var root = new GameObject("PlacedChestPanel");
        var canvasGo = Node("Canvas", root.transform, Vector2.zero, Vector2.zero);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Stretch((RectTransform)canvasGo.transform);
        var shade = Picture("Shade", canvasGo.transform, null, Vector2.zero,
                            new Vector2(960, 540), new Color(0.03f, 0.07f, 0.08f, 0.83f));
        Stretch((RectTransform)shade.transform);
        shade.raycastTarget = true;
        var card = Picture("Card", canvasGo.transform, null, Vector2.zero,
                           new Vector2(930, 516), Paper);
        card.raycastTarget = true;
        var edge = card.gameObject.AddComponent<Outline>();
        edge.effectColor = Wood;
        edge.effectDistance = new Vector2(7, -7);
        Text("Title", card.transform, "WOODEN CHEST", new Vector2(0, 227),
             new Vector2(400, 35), 26);
        var money = Text("Money", card.transform, "MONEY: 0g", new Vector2(-349, 228),
                         new Vector2(190, 30), 17);
        var close = Button("Close", card.transform, "X", new Vector2(427, 226), new Vector2(37, 37));
        Text("BagTitle", card.transform, "BACKPACK", new Vector2(-225, 169),
             new Vector2(200, 30), 22);
        var chestName = Text("ChestName", card.transform, "CHEST", new Vector2(225, 169),
                             new Vector2(240, 30), 21);
        var bags = new Button[36];
        var chests = new Button[36];
        for (int i = 0; i < 36; i++)
        {
            int row = i / 6;
            int column = i % 6;
            float y = 122 - row * 48;
            bags[i] = Button("Bag" + i, card.transform, null,
                             new Vector2(-378 + column * 61, y), new Vector2(56, 42));
            chests[i] = Button("Chest" + i, card.transform, null,
                               new Vector2(72 + column * 61, y), new Vector2(56, 42));
            Text("Value", bags[i].transform, "", Vector2.zero, new Vector2(54, 40), 10);
            Text("Value", chests[i].transform, "", Vector2.zero, new Vector2(54, 40), 10);
        }
        var message = Text("Message", card.transform, "Select a stack, then a destination slot.",
                           new Vector2(0, -235), new Vector2(770, 26), 16);
        var ui = root.AddComponent<PlacedChestUI>();
        ui.bagButtons = bags;
        ui.chestButtons = chests;
        ui.closeButton = close;
        ui.moneyLabel = money;
        ui.chestLabel = chestName;
        ui.messageLabel = message;
        return root;
    }

    private static GameObject BuildMenu()
    {
        var root = new GameObject("StartupMenu");
        var canvasGo = Node("Canvas", root.transform, Vector2.zero, Vector2.zero);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Stretch((RectTransform)canvasGo.transform);
        Transform ui = canvasGo.transform;

        var sky = Picture("Sky", ui, null, Vector2.zero, new Vector2(960, 540),
                          new Color(100f / 255f, 208f / 255f, 240f / 255f));
        Stretch((RectTransform)sky.transform);
        // Mountain panorama background matching Image 1
        Sprite bgSprite = Sprite("TitleBackground") ?? Sprite("Valley");
        Picture("Valley", ui, bgSprite, Vector2.zero,
                new Vector2(960, 540), Color.white);
        Picture("CloudLarge", ui, Sprite("CloudLarge"), new Vector2(250, 165),
                new Vector2(196, 112), Color.white);
        Picture("CloudSmall", ui, Sprite("CloudSmall"), new Vector2(-270, 105),
                new Vector2(142, 85), Color.white);
        Picture("TitleLogo", ui, Sprite("TitleLogo"), new Vector2(0, 650),
                new Vector2(560, 262), Color.white);
        // Draw the birds across the mountain ridges
        Picture("BirdA", ui, Sprite("Bird0"), new Vector2(200, -15),
                new Vector2(65, 53), Color.white);
        Picture("BirdB", ui, Sprite("Bird1"), new Vector2(255, -35),
                new Vector2(56, 46), Color.white);

        string[] names = { "New", "Load", "Coop", "Exit" };
        for (int i = 0; i < names.Length; i++)
            Button("Button" + names[i], ui, string.Empty, new Vector2(-267 + i * 178, -160),
                   new Vector2(158, 124), Sprite("Button" + names[i]));
        Button("ButtonLogin", ui, "LOG IN", new Vector2(344, 235), new Vector2(100, 32));
        Button("ButtonRegister", ui, "REGISTER", new Vector2(232, 235), new Vector2(110, 32));
        Text("AccountStatus", ui, "Account: guest", new Vector2(-349, 235),
             new Vector2(242, 32), 16);

        var auth = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AuthPath));
        auth.transform.SetParent(ui, false);
        auth.name = "AuthPanel";
        var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventGo.transform.SetParent(root.transform, false);
        root.AddComponent<StartupMenuFlow>();
        return root;
    }
}

