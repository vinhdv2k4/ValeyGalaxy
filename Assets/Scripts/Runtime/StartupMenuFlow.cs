using UIText = UnityEngine.UI.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the authored startup, account and character prefabs.</summary>
public sealed class StartupMenuFlow : MonoBehaviour
{
    [Header("Game API")]
    public string apiBaseUrl = "http://127.0.0.1:5055";

    private enum Page { TitleScreen, Menu, Auth, CharacterCreation, OpeningCutscene, EnteringGame }
    private static readonly string[] TitleButtonNames = { "ButtonNew", "ButtonLoad", "ButtonCoop", "ButtonExit" };
    // The developer splash plays once per launch; later visits skip to the title rise.
    private static bool splashShown;
    private Page page;
    private bool titleMouseReady;
    private bool registering;
    private StartupApiClient accounts;
    private bool requestBusy;
    private Coroutine characterEntry;

    private StartupTitleScene title;
    private RectTransform canvas;
    private Image curtain;
    private StartupIntroScript introScript;
    // The part (grandpa story or bus ride) that Escape currently skips.
    private CancellationTokenSource openingPart;
    private Button newButton;
    private Button loadButton;
    private Button loginButton;
    private Button registerButton;
    private UIText accountStatus;
    private UIText loginButtonText;
    private GameObject authPanel;
    private GameObject characterPanel;
    private RectTransform authCard;
    private InputField userInput;
    private InputField emailInput;
    private InputField passwordInput;
    private InputField confirmInput;
    private Button authSubmitButton;
    private UIText authTitle;
    private UIText authMessage;
    private UIText authSubmitText;

    // Character Customization fields
    private InputField nameInput;
    private InputField farmInput;
    private InputField favoriteInput;
    private StardewBitmapText nameLabel;
    private StardewBitmapText farmLabel;
    private StardewBitmapText farmLabelSecond;
    private StardewBitmapText favoriteLabel;
    private StardewBitmapText favoriteLabelSecond;
    private UIText createMessage;
    private Button createButton;
    private Image createButtonImage;

    // Portrait layers
    private Image previewBody;
    private Image previewHair;
    private Image previewShirt;
    private Image previewShirtDye;
    private Image previewPants;
    private Image previewAccessory;
    private Image previewArms;
    private Image previewPet;

    // Gender
    private Button genderMaleBtn;
    private Image genderMaleCheck;
    private Button genderFemaleBtn;
    private Image genderFemaleCheck;

    // Skip Intro
    private Button skipIntroCheckbox;
    private Image skipIntroImage;
    private bool skipIntro;

    // Farm Panel
    private RectTransform farmHighlight;
    private readonly Button[] farmButtons = new Button[7];
    private int selectedFarmType;

    // Embedded in-place Sliders
    private Slider eyeHue;
    private Slider eyeSaturation;
    private Slider eyeValue;
    private Slider hairHue;
    private Slider hairSaturation;
    private Slider hairValue;
    private Slider pantsHue;
    private Slider pantsSaturation;
    private Slider pantsValue;

    // Selectors text map
    private readonly Dictionary<string, StardewBitmapText> selectorValues = new();

    private int previewDirection = 2; // 2 = Down (Front, camera-facing default)
    private FarmerSpriteCatalog spriteCatalog;
    private Texture2D previewBodyTexture;
    private readonly List<Sprite> previewSprites = new List<Sprite>();

    // 0: Gender (0 male, 1 female)
    // 1: Hair (0..73)
    // 2: Skin (0..23)
    // 3: Shirt (0..111)
    // 4: Pants (0..9)
    // 5: Accessory (-1..18)
    // 6: Pet (0 cat, 1 dog)
    // 7: PetBreed (0..2)
    private readonly int[] selected = new int[8];
    private readonly int[] rgbColors = new int[3]; // 0: Hair, 1: Eye, 2: Pants
    private readonly int[] choiceCounts = { 2, 74, 24, 112, 10, 20, 2, 3 };

    private static readonly Color[] SkinColors =
    {
        new Color(1f, 1f, 1f), new Color(0.94f, 0.79f, 0.68f),
        new Color(0.83f, 0.63f, 0.49f), new Color(0.70f, 0.48f, 0.37f),
        new Color(0.56f, 0.36f, 0.27f), new Color(0.42f, 0.26f, 0.20f)
    };
    private static readonly Color[] HairColors =
    {
        new Color(0.47f, 0.28f, 0.17f), new Color(0.21f, 0.20f, 0.24f),
        new Color(0.88f, 0.70f, 0.29f), new Color(0.72f, 0.28f, 0.20f),
        new Color(0.27f, 0.53f, 0.79f), new Color(0.32f, 0.67f, 0.35f)
    };
    private static readonly Color[] EyeColors =
    {
        new Color(0.36f, 0.22f, 0.13f), new Color(0.24f, 0.45f, 0.78f),
        new Color(0.26f, 0.57f, 0.28f), new Color(0.48f, 0.51f, 0.55f),
        new Color(0.68f, 0.40f, 0.14f), new Color(0.49f, 0.34f, 0.62f)
    };

    private void Awake()
    {
        bool developmentOverrides = Application.isEditor || Debug.isDebugBuild;
        apiBaseUrl = StartupApiUrl.Resolve(apiBaseUrl, PlayerPrefs.GetString("ServerApiUrl", ""),
            System.Environment.GetCommandLineArgs(), developmentOverrides);
        if (developmentOverrides) PlayerPrefs.SetString("ServerApiUrl", apiBaseUrl);
        else PlayerPrefs.DeleteKey("ServerApiUrl");
        PlayerPrefs.Save();
        accounts = new StartupApiClient(apiBaseUrl);
        introScript = StartupIntroScript.Load();
        canvas = Find<RectTransform>("Canvas");
        // The old static backdrop is replaced by the layered title scene.
        foreach (string legacy in new[] { "Sky", "Valley", "CloudLarge", "CloudSmall", "BirdA", "BirdB", "TitleLogo" })
            transform.Find("Canvas/" + legacy)?.gameObject.SetActive(false);
        title = new StartupTitleScene(canvas, !splashShown);
        splashShown = true;

        var curtainRect = (RectTransform)new GameObject("Opening Curtain", typeof(RectTransform)).transform;
        curtainRect.SetParent(canvas, false);
        curtainRect.anchorMin = Vector2.zero;
        curtainRect.anchorMax = Vector2.one;
        curtainRect.sizeDelta = Vector2.zero;
        curtain = curtainRect.gameObject.AddComponent<Image>();
        curtain.color = Color.clear;
        curtain.raycastTarget = false;

        newButton = Find<Button>("Canvas/ButtonNew");
        loadButton = Find<Button>("Canvas/ButtonLoad");
        loginButton = Find<Button>("Canvas/ButtonLogin");
        registerButton = Find<Button>("Canvas/ButtonRegister");
        loginButtonText = Find<UIText>("Canvas/ButtonLogin/Label");
        accountStatus = Find<UIText>("Canvas/AccountStatus");
        authPanel = transform.Find("Canvas/AuthPanel").gameObject;
        authCard = Find<RectTransform>("Canvas/AuthPanel/Card");
        userInput = Find<InputField>("Canvas/AuthPanel/Card/UsernameInput");
        emailInput = Find<InputField>("Canvas/AuthPanel/Card/EmailInput");
        passwordInput = Find<InputField>("Canvas/AuthPanel/Card/PasswordInput");
        confirmInput = Find<InputField>("Canvas/AuthPanel/Card/ConfirmInput");
        authSubmitButton = Find<Button>("Canvas/AuthPanel/Card/Submit");
        authTitle = Find<UIText>("Canvas/AuthPanel/Card/Title");
        authMessage = Find<UIText>("Canvas/AuthPanel/Card/Message");
        authSubmitText = Find<UIText>("Canvas/AuthPanel/Card/Submit/Label");



        newButton.onClick.AddListener(OpenNewGame);
        loadButton.onClick.AddListener(LoadGame);
        Find<Button>("Canvas/ButtonCoop").interactable = false;
        Find<Button>("Canvas/ButtonExit").onClick.AddListener(ExitGame);
        loginButton.onClick.AddListener(() => { if (accounts.IsSignedIn) Logout(); else ShowAuth(false); });
        registerButton.onClick.AddListener(() => ShowAuth(true));
        authSubmitButton.onClick.AddListener(SubmitAuth);
        Find<Button>("Canvas/AuthPanel/Card/Back").onClick.AddListener(ShowMenu);
        Find<Button>("Canvas/AuthPanel/Card/Switch").onClick.AddListener(() => ShowAuth(!registering));

        authPanel.SetActive(false);
        characterPanel?.SetActive(false);
        BeginTitleScreen();
        StartCoroutine(MonitorServer());
    }

    private void InitializeCharacter()
    {
        if (characterPanel != null) return;
        characterPanel = StartupCharacterScreen.Build(transform.Find("Canvas"));
        // Character Creation UI elements
        nameInput = Find<InputField>("Canvas/CharacterCreation/Card/NameInput");
        farmInput = Find<InputField>("Canvas/CharacterCreation/Card/FarmInput");
        favoriteInput = Find<InputField>("Canvas/CharacterCreation/Card/FavoriteInput");
        nameLabel = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/NameLabel");
        farmLabel = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/FarmLabel");
        farmLabelSecond = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/FarmLabelSecond");
        favoriteLabel = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/FavoriteLabel");
        favoriteLabelSecond = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/FavoriteLabelSecond");
        createButton = Find<Button>("Canvas/CharacterCreation/Card/Create");
        createButtonImage = createButton.GetComponent<Image>();
        createMessage = Find<UIText>("Canvas/CharacterCreation/Card/Message");

        previewBody = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewBody");
        previewPants = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewPants");
        previewShirt = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewShirt");
        previewShirtDye = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewShirtDye");
        previewHair = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewHair");
        previewAccessory = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewAccessory");
        previewArms = Find<Image>("Canvas/CharacterCreation/Card/PortraitFrame/PreviewArms");
        previewPet = Find<Image>("Canvas/CharacterCreation/Card/PreviewPet");

        genderMaleBtn = Find<Button>("Canvas/CharacterCreation/Card/GenderMale");
        genderMaleCheck = Find<Image>("Canvas/CharacterCreation/Card/GenderMaleCheck");
        genderFemaleBtn = Find<Button>("Canvas/CharacterCreation/Card/GenderFemale");
        genderFemaleCheck = Find<Image>("Canvas/CharacterCreation/Card/GenderFemaleCheck");

        genderMaleBtn.onClick.AddListener(() => SetGender(0));
        genderFemaleBtn.onClick.AddListener(() => SetGender(1));

        Find<Button>("Canvas/CharacterCreation/Card/PetPrev").onClick.AddListener(() => ChangePet(-1));
        Find<Button>("Canvas/CharacterCreation/Card/PetNext").onClick.AddListener(() => ChangePet(1));

        Find<Button>("Canvas/CharacterCreation/Card/DirectionPrev").onClick.AddListener(() => ChangeDirection(-1));
        Find<Button>("Canvas/CharacterCreation/Card/DirectionNext").onClick.AddListener(() => ChangeDirection(1));
        Find<Button>("Canvas/CharacterCreation/Card/Randomize").onClick.AddListener(RandomizeFarmer);

        // 5 Selectors: Skin, Hair, Shirt, Pants, Accessory
        string[] selectors = { "Skin", "Hair", "Shirt", "Pants", "Accessory" };
        int[] selectorIndices = { 2, 1, 3, 4, 5 };
        for (int i = 0; i < selectors.Length; i++)
        {
            string sName = selectors[i];
            int sIdx = selectorIndices[i];
            selectorValues[sName] = Find<StardewBitmapText>("Canvas/CharacterCreation/Card/" + sName + "Value");
            Find<Button>("Canvas/CharacterCreation/Card/" + sName + "Prev").onClick.AddListener(() => ChangeChoice(sIdx, -1));
            Find<Button>("Canvas/CharacterCreation/Card/" + sName + "Next").onClick.AddListener(() => ChangeChoice(sIdx, 1));
        }

        // Color Sliders
        eyeHue = Find<Slider>("Canvas/CharacterCreation/Card/EyeHue");
        eyeSaturation = Find<Slider>("Canvas/CharacterCreation/Card/EyeSaturation");
        eyeValue = Find<Slider>("Canvas/CharacterCreation/Card/EyeValue");

        hairHue = Find<Slider>("Canvas/CharacterCreation/Card/HairHue");
        hairSaturation = Find<Slider>("Canvas/CharacterCreation/Card/HairSaturation");
        hairValue = Find<Slider>("Canvas/CharacterCreation/Card/HairValue");

        pantsHue = Find<Slider>("Canvas/CharacterCreation/Card/PantsHue");
        pantsSaturation = Find<Slider>("Canvas/CharacterCreation/Card/PantsSaturation");
        pantsValue = Find<Slider>("Canvas/CharacterCreation/Card/PantsValue");

        eyeHue.onValueChanged.AddListener(_ => OnEyeColorChanged());
        eyeSaturation.onValueChanged.AddListener(_ => OnEyeColorChanged());
        eyeValue.onValueChanged.AddListener(_ => OnEyeColorChanged());

        hairHue.onValueChanged.AddListener(_ => OnHairColorChanged());
        hairSaturation.onValueChanged.AddListener(_ => OnHairColorChanged());
        hairValue.onValueChanged.AddListener(_ => OnHairColorChanged());

        pantsHue.onValueChanged.AddListener(_ => OnPantsColorChanged());
        pantsSaturation.onValueChanged.AddListener(_ => OnPantsColorChanged());
        pantsValue.onValueChanged.AddListener(_ => OnPantsColorChanged());

        // Farm Panel
        farmHighlight = Find<RectTransform>("Canvas/CharacterCreation/FarmPanel/FarmHighlight");
        for (int f = 0; f < 7; f++)
        {
            int farmIdx = f;
            farmButtons[f] = Find<Button>("Canvas/CharacterCreation/FarmPanel/FarmBtn_" + f);
            farmButtons[f].onClick.AddListener(() => SetFarmType(farmIdx));
        }

        skipIntroCheckbox = Find<Button>("Canvas/CharacterCreation/Card/SkipIntroCheckbox");
        skipIntroImage = skipIntroCheckbox.GetComponent<Image>();
        skipIntroCheckbox.onClick.AddListener(ToggleSkipIntro);

        createButton.onClick.AddListener(CreateFarmer);
        Find<Button>("Canvas/CharacterCreation/Card/Back").onClick.AddListener(() =>
        {
            if (!requestBusy) ShowMenu();
        });

        nameInput.onValueChanged.AddListener(_ => ValidateInputs());
        farmInput.onValueChanged.AddListener(_ => ValidateInputs());
        favoriteInput.onValueChanged.AddListener(_ => ValidateInputs());

        spriteCatalog = FarmerSpriteCatalog.Load();
    }

    private T Find<T>(string path) where T : Component
    {
        Transform found = transform.Find(path);
        if (found == null) throw new MissingReferenceException("Startup prefab is missing " + path);
        T component = found.GetComponent<T>();
        if (component == null)
        {
            // The customization prefab currently contains both a selector
            // label and a color slider named HairValue / PantsValue.
            int separator = path.LastIndexOf('/');
            Transform parent = transform.Find(path.Substring(0, separator));
            string childName = path.Substring(separator + 1);
            if (parent != null)
                foreach (Transform child in parent)
                    if (child.name == childName && child.TryGetComponent(out component))
                        return component;
            throw new MissingReferenceException("Startup prefab component missing: " + path);
        }
        return component;
    }

    private void Start()
    {
        // GameAudio starts MainTheme when the menu scene loads; keep the splash silent
        // until the logo fades out.
        if (title.SplashVisible) GameAudio.PlayMusic(null);
    }

    private bool showingWait;

    private void Update()
    {
        // Hourglass while the server answers (login, farm load) and while the game is entered.
        bool waiting = requestBusy || page == Page.EnteringGame;
        if (waiting != showingWait) StardewCursorManager.SetWait(showingWait = waiting);
        if (page != Page.OpeningCutscene)
        {
            if (page == Page.TitleScreen) HandleTitleInput();
            title.Tick(Time.unscaledDeltaTime);
        }
        if (page == Page.TitleScreen)
        {
            RevealTitleButtons(title.ButtonsToShow);
            if (title.IntroFinished) ShowMenu();
        }
        else if (page == Page.OpeningCutscene && StartupIntroScript.SkipPressed && openingPart != null &&
                 !openingPart.IsCancellationRequested)
        {
            GameAudio.Play("bigDeSelect");
            openingPart.Cancel();
        }
    }

    private void HandleTitleInput()
    {
        // Ignore the click that started Play mode in the editor.
        var mouse = Mouse.current;
        if (mouse != null && !mouse.leftButton.isPressed) titleMouseReady = true;
        if (titleMouseReady && mouse?.leftButton.wasPressedThisFrame == true)
            title.Click(mouse.position.ReadValue());

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if (keyboard?.escapeKey.wasPressedThisFrame == true || gamepad?.startButton.wasPressedThisFrame == true)
            title.Cancel(true);
        else if (keyboard?.bKey.wasPressedThisFrame == true || keyboard?.spaceKey.wasPressedThisFrame == true ||
                 gamepad?.buttonEast.wasPressedThisFrame == true)
            title.Cancel(false);
    }

    private void RevealTitleButtons(int count)
    {
        for (int i = 0; i < TitleButtonNames.Length; i++)
        {
            var button = Find<Button>("Canvas/" + TitleButtonNames[i]);
            if (button.gameObject.activeSelf != i < count) button.gameObject.SetActive(i < count);
            button.enabled = false;
        }
    }

    private void BeginTitleScreen()
    {
        page = Page.TitleScreen;
        titleMouseReady = false;
        curtain.color = Color.clear;
        curtain.transform.SetAsLastSibling();
        authPanel.SetActive(false);
        characterPanel?.SetActive(false);
        SetMainButtons(false);
    }

    private void ShowMenu()
    {
        if (characterEntry != null) { StopCoroutine(characterEntry); characterEntry = null; }
        if (characterPanel != null)
            ((RectTransform)characterPanel.transform).anchoredPosition = Vector2.zero;
        page = Page.Menu;
        title.SnapToMenu();
        title.CloudAlpha = 1f;
        curtain.color = Color.clear;
        authPanel.SetActive(false);
        characterPanel?.SetActive(false);
        SetMainButtons(true);
        RefreshAccountState();
    }

    private void SetMainButtons(bool visible)
    {
        string[] names = { "ButtonNew", "ButtonLoad", "ButtonCoop", "ButtonExit",
                           "ButtonLogin", "ButtonRegister", "AccountStatus" };
        foreach (string name in names)
        {
            Transform item = transform.Find("Canvas/" + name);
            item.gameObject.SetActive(visible);
            if (item.TryGetComponent(out Button button)) button.enabled = true;
        }
    }

    private void RefreshAccountState()
    {
        string connection = accounts.IsServerConnected
            ? (accounts.IsSignedIn ? "Connected | " + accounts.OnlinePlayers + " online" : "Server online")
            : (accounts.ConnectionError == StartupApiClient.SessionExpiredMessage
                ? "Session expired" : "Server unavailable");
        accountStatus.text = (accounts.IsSignedIn ? "Account: " + accounts.UserName : "Account: guest")
            + "\n" + connection;
        loginButtonText.text = accounts.IsSignedIn ? "LOG OUT" : "LOG IN";
        registerButton.gameObject.SetActive(!accounts.IsSignedIn);
        newButton.interactable = !accounts.IsSignedIn || accounts.Farmer == null;
        loadButton.interactable = accounts.Farmer != null && accounts.IsServerConnected;
    }

    private IEnumerator MonitorServer()
    {
        while (true)
        {
            yield return accounts.Probe(_ => { if (page == Page.Menu) RefreshAccountState(); });
            yield return new WaitForSecondsRealtime(15f);
        }
    }

    private void OpenNewGame()
    {
        if (!accounts.IsSignedIn) { ShowAuth(false); return; }
        if (requestBusy) return;
        requestBusy = true;
        StartCoroutine(RefreshBeforeNewGame());
    }

    private IEnumerator RefreshBeforeNewGame()
    {
        string error = null;
        yield return accounts.RefreshAccount(result => error = result);
        requestBusy = false;
        if (!string.IsNullOrEmpty(error))
        {
            RefreshAccountState();
            accountStatus.text = error;
            yield break;
        }
        if (accounts.Farmer == null) ShowCharacter();
        else ShowMenu();
    }

    private void LoadGame()
    {
        if (accounts.Farmer == null) return;
        BeginLeaving();
    }

    private void ShowAuth(bool register)
    {
        page = Page.Auth;
        registering = register;
        authPanel.SetActive(true);
        characterPanel?.SetActive(false);
        SetMainButtons(false);
        authTitle.text = register ? "CREATE ACCOUNT" : "LOG IN";
        authSubmitText.text = register ? "REGISTER" : "LOG IN";
        authMessage.text = register ? "Use a valid email and a strong password." : "Sign in with username or email.";
        emailInput.gameObject.SetActive(register);
        confirmInput.gameObject.SetActive(register);
        authCard.sizeDelta = new Vector2(520f, register ? 520f : 390f);
        AuthPosition("Title", register ? 211f : 147f);
        AuthPosition("Back", register ? 218f : 157f);
        AuthPosition("UsernameInput", register ? 138f : 76f);
        AuthPosition("PasswordInput", register ? 22f : 19f);
        AuthPosition("Message", register ? -98f : -43f);
        AuthPosition("Submit", register ? -157f : -103f);
        AuthPosition("Switch", register ? -220f : -157f);
        userInput.placeholder.GetComponent<UIText>().text = register ? "Username" : "Username or email";
        passwordInput.text = string.Empty;
        confirmInput.text = string.Empty;
        userInput.Select();
    }

    private void AuthPosition(string name, float y)
    {
        var rect = Find<RectTransform>("Canvas/AuthPanel/Card/" + name);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private void SubmitAuth()
    {
        if (requestBusy) return;
        requestBusy = true;
        authSubmitButton.interactable = false;
        authMessage.text = "Connecting...";
        string user = userInput.text.Trim();
        string email = emailInput.text.Trim();
        string password = passwordInput.text;
        string confirm = confirmInput.text;
        StartCoroutine(registering
            ? accounts.Register(user, email, password, confirm, AuthComplete)
            : accounts.Login(user, password, AuthComplete));
    }

    private void AuthComplete(string error)
    {
        requestBusy = false;
        authSubmitButton.interactable = true;
        passwordInput.text = string.Empty;
        confirmInput.text = string.Empty;
        if (!string.IsNullOrEmpty(error)) { authMessage.text = error; return; }
        requestBusy = true;
        StartCoroutine(CompleteAuthConnection());
    }

    private IEnumerator CompleteAuthConnection()
    {
        string accountError = null;
        yield return accounts.RefreshAccount(error => accountError = error);
        if (!string.IsNullOrEmpty(accountError))
        {
            requestBusy = false;
            authMessage.text = accountError;
            yield break;
        }
        yield return accounts.Probe(error =>
        {
            requestBusy = false;
            if (!string.IsNullOrEmpty(error)) { ShowMenu(); return; }
            if (accounts.Farmer == null) ShowCharacter();
            else ShowMenu();
        });
    }

    private void Logout()
    {
        StartCoroutine(accounts.Logout(RefreshAccountState));
    }

    private void ShowCharacter()
    {
        if (accounts.Farmer != null) { ShowMenu(); return; }
        InitializeCharacter();
        page = Page.CharacterCreation;
        authPanel.SetActive(false);
        characterPanel.SetActive(true);
        SetMainButtons(false);
        nameInput.text = string.Empty;
        farmInput.text = string.Empty;
        favoriteInput.text = string.Empty;
        System.Array.Clear(selected, 0, selected.Length);
        System.Array.Clear(rgbColors, 0, rgbColors.Length);
        selected[5] = -1;
        previewDirection = 2; // Default Down / Front (facing camera)
        selectedFarmType = 1;
        SetSkipIntro(false);

        rgbColors[0] = FarmerSpriteCatalog.EncodeColor(HairColors[0]);
        rgbColors[1] = FarmerSpriteCatalog.EncodeColor(EyeColors[0]);
        rgbColors[2] = FarmerSpriteCatalog.EncodeColor(new Color(0.18f, 0.28f, 0.65f));

        createMessage.text = string.Empty;
        SetFarmType(selectedFarmType);
        SyncSlidersFromColors();
        RefreshChoices();
        if (characterEntry != null) StopCoroutine(characterEntry);
        characterEntry = StartCoroutine(SlideCharacterIn());
    }

    private IEnumerator SlideCharacterIn()
    {
        var rect = (RectTransform)characterPanel.transform;
        var group = characterPanel.GetComponent<CanvasGroup>();
        if (group == null) group = characterPanel.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        rect.anchoredPosition = new Vector2(960f, 0f);
        float elapsed = 0f;
        const float duration = 0.72f;
        while (elapsed < duration && page == Page.CharacterCreation)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = new Vector2(Mathf.Lerp(960f, 0f, t), 0f);
            yield return null;
        }
        rect.anchoredPosition = Vector2.zero;
        group.blocksRaycasts = true;
        characterEntry = null;
    }

    private void SetGender(int gender)
    {
        if (selected[0] == gender) return;
        selected[0] = gender;
        // Switching between the two base bodies resets the hairstyle.
        selected[1] = gender == 0 ? 0 : 16;
        GameAudio.Play("coin");
        RefreshChoices();
    }

    private void ChangePet(int delta)
    {
        selected[7] += delta;
        if (selected[7] >= 3)
        {
            selected[7] = 0;
            selected[6] = 1 - selected[6];
        }
        else if (selected[7] < 0)
        {
            selected[7] = 2;
            selected[6] = 1 - selected[6];
        }
        GameAudio.Play("coin");
        RefreshChoices();
    }

    private void SetFarmType(int f)
    {
        selectedFarmType = 1; // Server and new game both use Riverland Farm.
        if (farmHighlight != null)
            farmHighlight.anchoredPosition = new Vector2(0, 235 - selectedFarmType * 79);
    }

    private void ToggleSkipIntro()
    {
        if (!requestBusy) SetSkipIntro(!skipIntro);
    }

    private void SetSkipIntro(bool skip)
    {
        skipIntro = skip;
        if (skipIntroImage != null)
        {
            Sprite s = StartupCharacterScreen.CropCursors(skipIntro ? 236 : 227, 425, 9, 9);
            if (s != null) skipIntroImage.sprite = s;
        }
        GameAudio.Play("drumkit6");
    }

    private void OnEyeColorChanged()
    {
        Color c = Color.HSVToRGB(eyeHue.value, eyeSaturation.value, eyeValue.value);
        rgbColors[1] = FarmerSpriteCatalog.EncodeColor(c);
        RefreshChoices();
    }

    private void OnHairColorChanged()
    {
        Color c = Color.HSVToRGB(hairHue.value, hairSaturation.value, hairValue.value);
        rgbColors[0] = FarmerSpriteCatalog.EncodeColor(c);
        RefreshChoices();
    }

    private void OnPantsColorChanged()
    {
        Color c = Color.HSVToRGB(pantsHue.value, pantsSaturation.value, pantsValue.value);
        rgbColors[2] = FarmerSpriteCatalog.EncodeColor(c);
        RefreshChoices();
    }

    private void SyncSlidersFromColors()
    {
        Color hairColor = FarmerSpriteCatalog.DecodeColor(rgbColors[0], HairColors[0]);
        Color.RGBToHSV(hairColor, out float hh, out float hs, out float hv);
        hairHue.SetValueWithoutNotify(hh);
        hairSaturation.SetValueWithoutNotify(hs);
        hairValue.SetValueWithoutNotify(hv);

        Color eyeColor = FarmerSpriteCatalog.DecodeColor(rgbColors[1], EyeColors[0]);
        Color.RGBToHSV(eyeColor, out float eh, out float es, out float ev);
        eyeHue.SetValueWithoutNotify(eh);
        eyeSaturation.SetValueWithoutNotify(es);
        eyeValue.SetValueWithoutNotify(ev);

        Color pantsColor = FarmerSpriteCatalog.DecodeColor(rgbColors[2], Color.white);
        Color.RGBToHSV(pantsColor, out float ph, out float ps, out float pv);
        pantsHue.SetValueWithoutNotify(ph);
        pantsSaturation.SetValueWithoutNotify(ps);
        pantsValue.SetValueWithoutNotify(pv);
    }

    private void ChangeChoice(int index, int delta)
    {
        if (index == 5)
        {
            // Accessory: -1..18 (20 total values)
            selected[index] = (selected[index] + delta + 21) % 20 - 1;
            GameAudio.Play("purchase");
        }
        else if (index == 2)
        {
            selected[index] = (selected[index] + delta + choiceCounts[index]) % choiceCounts[index];
            GameAudio.Play("skeletonStep");
        }
        else if (index == 1)
        {
            selected[index] = (selected[index] + delta + choiceCounts[index]) % choiceCounts[index];
            GameAudio.Play("grassyStep");
        }
        else
        {
            selected[index] = (selected[index] + delta + choiceCounts[index]) % choiceCounts[index];
            GameAudio.Play("coin");
        }
        RefreshChoices();
    }

    private void ChangeDirection(int delta)
    {
        // Sprite direction: 0 = Up, 1 = Right, 2 = Down, 3 = Left
        // Next (+1): Down(2) -> Right(1) -> Up(0) -> Left(3) -> Down(2)
        previewDirection = (previewDirection - delta + 4) % 4;
        GameAudio.Play("pickUpItem");
        RefreshChoices();
    }

    private void RandomizeFarmer()
    {
        string[] cues = { "drumkit6", "drumkit1", "dirtyHit", "axchop", "hoeHit", "fishSlap", "coin", "axe", "hammer", "drumkit2", "drumkit3", "drumkit4", "drumkit5" };
        GameAudio.Play(cues[Random.Range(0, cues.Length)]);

        // Skin: 75% standard 0..5, 25% exotic 0..23
        selected[2] = Random.value < 0.25f ? Random.Range(0, 24) : Random.Range(0, 6);

        // Hair: male 0..15, female 16..31
        selected[1] = selected[0] == 0 ? Random.Range(0, 16) : Random.Range(16, 32);

        // Shirt: 0..111
        selected[3] = Random.Range(0, 112);

        // Pants: 0..9
        selected[4] = Random.Range(0, 10);

        // Accessory: 33% chance; male 0..18, female 6..18 (no beards)
        if (Random.value < 0.33f)
            selected[5] = selected[0] == 0 ? Random.Range(0, 19) : Random.Range(6, 19);
        else
            selected[5] = -1;

        // Hair color
        Color rHair = new Color(Random.Range(0.1f, 1f), Random.Range(0.1f, 1f), Random.Range(0.1f, 1f));
        if (Random.value < 0.5f) rHair *= 0.5f;
        rgbColors[0] = FarmerSpriteCatalog.EncodeColor(rHair);

        // Eye color (darkened/subdued)
        Color rEye = new Color(Random.Range(0.1f, 1f), Random.Range(0.1f, 1f), Random.Range(0.1f, 1f));
        rEye *= 0.5f;
        rgbColors[1] = FarmerSpriteCatalog.EncodeColor(rEye);

        // Pants color
        Color rPants = new Color(Random.Range(0.1f, 1f), Random.Range(0.1f, 1f), Random.Range(0.1f, 1f));
        if (Random.value < 0.5f) rPants *= 0.5f;
        rgbColors[2] = FarmerSpriteCatalog.EncodeColor(rPants);

        SyncSlidersFromColors();
        RefreshChoices();
    }

    private void ValidateInputs()
    {
        bool nameValid = nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text);
        bool farmValid = farmInput != null && !string.IsNullOrWhiteSpace(farmInput.text);
        bool favoriteValid = favoriteInput != null && !string.IsNullOrWhiteSpace(favoriteInput.text);

        Color32 normalColor = new Color32(34, 17, 34, 255);
        Color32 errorColor = new Color32(200, 30, 30, 255);

        if (nameLabel != null) nameLabel.color = nameValid ? normalColor : errorColor;
        if (farmLabel != null) farmLabel.color = farmValid ? normalColor : errorColor;
        if (farmLabelSecond != null) farmLabelSecond.color = farmValid ? normalColor : errorColor;
        if (favoriteLabel != null) favoriteLabel.color = favoriteValid ? normalColor : errorColor;
        if (favoriteLabelSecond != null) favoriteLabelSecond.color = favoriteValid ? normalColor : errorColor;

        bool canLeave = nameValid && farmValid && favoriteValid;
        if (createButton != null)
        {
            createButton.interactable = canLeave;
            if (createButtonImage != null)
                createButtonImage.color = canLeave ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }
    }

    private void RefreshChoices()
    {
        // Update selector values
        if (selectorValues.TryGetValue("Skin", out var skinVal) && skinVal != null)
            skinVal.Text = (selected[2] + 1).ToString();
        if (selectorValues.TryGetValue("Hair", out var hairVal) && hairVal != null)
            hairVal.Text = (selected[1] + 1).ToString();
        if (selectorValues.TryGetValue("Shirt", out var shirtVal) && shirtVal != null)
            shirtVal.Text = (selected[3] + 1).ToString();
        if (selectorValues.TryGetValue("Pants", out var pantsVal) && pantsVal != null)
            pantsVal.Text = (selected[4] + 1).ToString();
        if (selectorValues.TryGetValue("Accessory", out var accVal) && accVal != null)
            accVal.Text = (selected[5] + 2).ToString();

        // Gender checkmarks
        if (genderMaleCheck != null) genderMaleCheck.enabled = selected[0] == 0;
        if (genderFemaleCheck != null) genderFemaleCheck.enabled = selected[0] == 1;

        // Pet portrait
        if (previewPet != null)
        {
            Sprite petSprite = spriteCatalog != null ? spriteCatalog.PetPortrait(selected[6], selected[7]) : null;
            if (petSprite == null)
            {
                petSprite = StartupCharacterScreen.CropCursors(
                    160 + (selected[6] == 0 ? 0 : 48) + selected[7] * 16, 208, 16, 16);
            }
            if (petSprite != null) previewPet.sprite = petSprite;
        }

        // Clean generated preview sprites
        foreach (Sprite sprite in previewSprites) if (sprite != null) Destroy(sprite);
        previewSprites.Clear();
        if (previewBodyTexture != null) Destroy(previewBodyTexture);
        if (spriteCatalog == null) return;

        // CharacterCustomization draws the 16x32 farmer at 4x inside its
        // portrait box. Match that scale so the complete sprite stays framed.
        previewBody.rectTransform.sizeDelta = new Vector2(64f, 128f);
        previewBody.rectTransform.anchoredPosition = Vector2.zero;
        previewPants.rectTransform.sizeDelta = new Vector2(64f, 128f);
        previewPants.rectTransform.anchoredPosition = Vector2.zero;
        previewHair.rectTransform.sizeDelta = new Vector2(64f, 128f);
        previewShirt.rectTransform.sizeDelta = new Vector2(32f, 32f);
        if (previewShirtDye != null) previewShirtDye.rectTransform.sizeDelta = new Vector2(32f, 32f);
        if (previewAccessory != null) previewAccessory.rectTransform.sizeDelta = new Vector2(64f, 64f);
        if (previewArms != null) previewArms.rectTransform.sizeDelta = new Vector2(64f, 128f);

        Color eyes = FarmerSpriteCatalog.DecodeColor(rgbColors[1], EyeColors[0]);
        previewBodyTexture = spriteCatalog.RecolorBody(selected[0], selected[2], eyes);

        // Body frame by direction: 0 = Up (frame 12), 1 = Right (frame 6), 2 = Down (frame 0), 3 = Left (frame 6)
        int bodyFrame = previewDirection == 2 ? 0 : previewDirection == 0 ? 12 : 6;
        Sprite bodySprite = previewBodyTexture == null ? null : spriteCatalog.Body(previewBodyTexture, bodyFrame);
        SetPreviewSprite(previewBody, bodySprite);

        // Hair
        int hairStyle = selected[1] >= 56 ? selected[1] + 44 : selected[1];
        SetPreviewSprite(previewHair, spriteCatalog.Hair(hairStyle, previewDirection));
        Color hairColor = FarmerSpriteCatalog.DecodeColor(rgbColors[0], HairColors[0]);
        previewHair.color = hairColor;

        // FarmerRenderer draws the 8x8 shirt at (4, 14) and the 16x16
        // accessory at (0, 4) front / (0, 1) side, relative to the
        // 16x32 body frame. Keep the UI images on that same pixel grid.
        float pixelScale = previewBody.rectTransform.rect.width / 16f;
        Vector2 bodyCenter = previewBody.rectTransform.anchoredPosition;
        float bodyTop = bodyCenter.y + previewBody.rectTransform.rect.height * 0.5f;
        // Female farmers sit one pixel lower in the 4x preview; pants and body keep their origin.
        float femaleOffset = selected[0] == 1 ? pixelScale : 0f;
        float shirtCenterY = bodyTop - 18f * pixelScale - femaleOffset;
        previewShirt.rectTransform.anchoredPosition = new Vector2(bodyCenter.x, shirtCenterY);
        if (previewShirtDye != null)
            previewShirtDye.rectTransform.anchoredPosition = new Vector2(bodyCenter.x, shirtCenterY);

        int hairOffset = (selected[0] == 0 && hairStyle >= 16 ? -1 :
                          selected[0] == 1 && hairStyle < 16 ? 1 : 0)
                         + (previewDirection == 0 ? 1 : 0);
        previewHair.rectTransform.anchoredPosition =
            new Vector2(bodyCenter.x, bodyCenter.y - hairOffset * pixelScale);
        if (previewAccessory != null)
        {
            int accessoryTop = previewDirection == 2 ? 4 : 1;
            previewAccessory.rectTransform.anchoredPosition =
                new Vector2(bodyCenter.x, bodyTop - (accessoryTop + 8) * pixelScale - femaleOffset);
        }

        // The arm layer is drawn over the shirt, hair and accessory for the idle frame.
        if (previewArms != null)
            SetPreviewSprite(previewArms, spriteCatalog.Body(previewBodyTexture, bodyFrame, true));

        // Shirt: Base + Dye Layer
        SetPreviewSprite(previewShirt, spriteCatalog.Shirt(selected[3], previewDirection, false));
        if (previewShirtDye != null)
        {
            SetPreviewSprite(previewShirtDye, spriteCatalog.Shirt(selected[3], previewDirection, true));
            previewShirtDye.color = Color.white;
        }

        // Pants (with female offset if selected[0] == 1)
        SetPreviewSprite(previewPants, spriteCatalog.Pants(selected[4], previewDirection, selected[0] == 1));
        Color pantsColor = FarmerSpriteCatalog.DecodeColor(rgbColors[2], Color.white);
        previewPants.color = pantsColor;

        // Accessory: 0..5 (beards) tinted with hairColor, >= 6 tinted white
        if (previewAccessory != null)
        {
            SetPreviewSprite(previewAccessory, spriteCatalog.Accessory(selected[5], previewDirection));
            previewAccessory.color = selected[5] >= 0 && selected[5] < 6 ? hairColor : Color.white;

            // Ordering: accessory < 8 drawn below hair, >= 8 drawn above hair
            if (selected[5] < 8)
                previewAccessory.transform.SetSiblingIndex(previewHair.transform.GetSiblingIndex() - 1);
            else
                previewAccessory.transform.SetSiblingIndex(previewHair.transform.GetSiblingIndex() + 1);
        }

        // Flipping for Left direction (3)
        bool uniqueLeft = spriteCatalog.HairUsesUniqueLeft(hairStyle);
        previewHair.rectTransform.localScale = new Vector3(previewDirection == 3 && !uniqueLeft ? -1 : 1, 1, 1);
        previewBody.rectTransform.localScale = new Vector3(previewDirection == 3 ? -1 : 1, 1, 1);
        previewPants.rectTransform.localScale = new Vector3(previewDirection == 3 ? -1 : 1, 1, 1);
        if (previewAccessory != null)
            previewAccessory.rectTransform.localScale = new Vector3(previewDirection == 3 ? -1 : 1, 1, 1);
        if (previewArms != null)
            previewArms.rectTransform.localScale = new Vector3(previewDirection == 3 ? -1 : 1, 1, 1);
        // Shirts use a dedicated left-facing tile at +16 in the atlas.
        previewShirt.rectTransform.localScale = Vector3.one;
        if (previewShirtDye != null)
            previewShirtDye.rectTransform.localScale = Vector3.one;

        ValidateInputs();
    }

    private void SetPreviewSprite(Image target, Sprite sprite)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.enabled = sprite != null;
        if (sprite != null) previewSprites.Add(sprite);
    }

    private void CreateFarmer()
    {
        if (accounts.Farmer != null) { BeginLeaving(); return; }
        string name = nameInput.text.Trim();
        string farm = farmInput.text.Trim();
        string favorite = favoriteInput.text.Trim();

        if (name.Length == 0 || farm.Length == 0 || favorite.Length == 0)
        {
            createMessage.text = "Name, farm and favorite thing are required.";
            GameAudio.Play("cancel");
            ValidateInputs();
            return;
        }
        if (name.Length > 24 || farm.Length > 24 || favorite.Length > 32)
        {
            createMessage.text = "One of the fields is too long.";
            GameAudio.Play("cancel");
            return;
        }

        var farmer = new CreatedFarmer
        {
            name = name,
            farmName = farm,
            favoriteThing = favorite,
            gender = selected[0],
            hair = selected[1] >= 56 ? selected[1] + 44 : selected[1],
            skin = selected[2],
            shirt = selected[3],
            pants = selected[4],
            farmType = selectedFarmType,
            accessory = selected[5],
            pet = selected[6],
            petBreed = selected[7],
            hairColor = 0,
            eyeColor = 0,
            hairColorRgb = rgbColors[0],
            eyeColorRgb = rgbColors[1],
            pantsColorRgb = rgbColors[2],
            introMode = skipIntro ? FarmerIntroMode.Skip : FarmerIntroMode.Watch
        };

        if (requestBusy) return;
        requestBusy = true;
        createButton.interactable = false;
        createMessage.text = "Saving farmer...";
        GameAudio.Play("smallSelect");
        StartCoroutine(accounts.SaveFarmer(farmer, error =>
        {
            requestBusy = false;
            createButton.interactable = true;
            if (!string.IsNullOrEmpty(error))
            {
                if (error == "Farmer already exists for this account.")
                {
                    requestBusy = true;
                    createButton.interactable = false;
                    createMessage.text = "Loading your existing farmer...";
                    StartCoroutine(LoadExistingFarmerAfterConflict());
                    return;
                }
                createMessage.text = error;
                GameAudio.Play("cancel");
            }
            else
            {
                BeginLeaving();
            }
        }));
    }

    private IEnumerator LoadExistingFarmerAfterConflict()
    {
        string error = null;
        yield return accounts.RefreshAccount(result => error = result);
        requestBusy = false;
        createButton.interactable = true;
        if (string.IsNullOrEmpty(error) && accounts.Farmer != null) BeginLeaving();
        else createMessage.text = error ?? "Could not load the existing farmer.";
    }

    private void BeginLeaving()
    {
        if (page is Page.EnteringGame or Page.OpeningCutscene) return;
        page = Page.EnteringGame;
        authPanel.SetActive(false);
        characterPanel?.SetActive(false);
        SetMainButtons(false);
        StartCoroutine(PrepareGame());
    }

    private IEnumerator PrepareGame()
    {
        CreatedFarmer farmer = accounts.Farmer;
        var opening = FarmerIntroMode.ExistingSave;
        // Loaded for every farmer so the game scene starts with the saved session (time,
        // place, energy) instead of showing 6:00 until the state arrives.
        OnlineGameSnapshot snapshot = null;
        string loadError = null;
        yield return accounts.LoadGameState((state, failure) => { snapshot = state; loadError = failure; });
        if (snapshot == null)
        {
            ShowMenu();
            accountStatus.text = loadError ?? "Could not load your farm.";
            yield break;
        }
        if (farmer.introMode != FarmerIntroMode.ExistingSave && snapshot.gameDay == 1 &&
            snapshot.milestones?.HasSeen(StartupSession.ArrivalEventId) != true)
            opening = farmer.introMode;

        curtain.transform.SetAsLastSibling();
        var transition = new StartupIntroPresentation(canvas, curtain, introScript, destroyCancellationToken);
        Task fade = transition.FadeToBlack(progress => title.CloudAlpha = 1f - progress);
        yield return new WaitUntil(() => fade.IsCompleted);
        if (opening == FarmerIntroMode.Watch)
        {
            page = Page.OpeningCutscene;
            Task story = PlayOpening(farmer, destroyCancellationToken);
            yield return new WaitUntil(() => story.IsCompleted);
            page = Page.EnteringGame;
            if (story.IsCanceled) yield break;
            if (story.IsFaulted)
            {
                Debug.LogException(story.Exception.GetBaseException());
                curtain.color = Color.clear;
                GameAudio.PlayMusic("MainTheme");
                ShowMenu();
                accountStatus.text = "Could not play the opening. Load your farmer to try again.";
                yield break;
            }
        }

        curtain.color = Color.black;
        // "Loading..." on the dark screen until the farm is ready.
        GameLoadingScreen.Show();
        StartupSession.EnterGame(farmer, accounts, opening, snapshot);
    }

    private async Task PlayOpening(CreatedFarmer farmer, CancellationToken token)
    {
        // Escape skips only the current part: the bus ride still plays after the grandpa story,
        // and Robin's welcome still plays after the bus.
        await PlayOpeningPart(token, part =>
            new StartupIntroPresentation(canvas, curtain, introScript, part).PlayGrandpaAsync(farmer));
        await PlayOpeningPart(token, part =>
            new StartupIntroPresentation(canvas, curtain, introScript, part).PlayBusAsync());
    }

    private async Task PlayOpeningPart(CancellationToken token, System.Func<CancellationToken, Task> play)
    {
        token.ThrowIfCancellationRequested();
        using var part = CancellationTokenSource.CreateLinkedTokenSource(token);
        openingPart = part;
        try { await play(part.Token); }
        catch (System.OperationCanceledException) when (!token.IsCancellationRequested) { }
        finally { openingPart = null; }
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (showingWait) StardewCursorManager.SetWait(false);
        title?.Dispose();
        foreach (Sprite sprite in previewSprites) if (sprite != null) Destroy(sprite);
        if (previewBodyTexture != null) Destroy(previewBodyTexture);
    }
}
