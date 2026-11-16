using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Opening sequence before the title menu: developer splash, fade from white, pause on the
/// valley, camera rise into the sky, logo swipe and buttons appearing one by one. Laid out in
/// 1920x1080 pixels (twice the 960x540 canvas) and updated on a fixed 60 Hz tick.
/// </summary>
internal sealed class StartupTitleScene
{
    private const float TickMs = 1000f / 60f;
    public const int ButtonCount = 4;

    private readonly RectTransform canvas;
    private readonly Canvas canvasComponent;
    private readonly Texture2D cursors;
    private readonly Texture2D titleButtons;
    private readonly Texture2D clouds;
    private readonly List<Sprite> sprites = new();

    private readonly RectTransform stage;
    private readonly RectTransform overlay;

    private readonly Image gradient;
    private readonly Image stars;
    private readonly Image[] bigCloudImages;
    private readonly Image[] farMountains;
    private readonly Image[] smallCloudImages;
    private readonly Image[] nearMountains;
    private readonly Bird[] birds;
    private readonly Sprite[] birdFrames;
    private readonly Image bushLeft;
    private readonly Image bushRight;
    private readonly Image logo;
    private readonly Image logoSwipe;
    private readonly Image gust;

    // Developer splash.
    private readonly Image white;
    private readonly Image devText;
    private readonly Image ape;
    private readonly Image apeSurprised;
    private readonly Sprite[] devTextFrames;
    private readonly Sprite[] apeFrames;
    private readonly Sprite[] apeSurprisedFrames;

    private readonly Sprite[] leftGustFrames;
    private readonly Sprite[] rightGustFrames;

    private readonly List<float> bigClouds = new();
    private readonly List<float> smallClouds = new();

    private float logoFadeTimer = 5000;
    private float logoSurprisedTimer;
    private float fadeFromWhiteTimer = 4000;
    private float pauseBeforeViewportRiseTimer;
    private float viewportY;
    private float viewportDY;
    private float logoSwipeTimer;
    private float showButtonsTimer;
    private int buttonsToShow;
    private bool titleInPosition;
    private int bCount;
    private readonly bool shades;

    private Sprite[] gustFrames;
    private Vector2 gustPosition;
    private float gustTimer;
    private bool gustActive;

    private float accumulator;
    private float width;
    private float height;

    /// <summary>Cloud opacity, faded out when leaving the menu.</summary>
    public float CloudAlpha = 1f;

    public int ButtonsToShow => buttonsToShow;
    public bool SplashVisible => logoFadeTimer > 0 || fadeFromWhiteTimer > 0;
    public bool IntroFinished => titleInPosition && logoFadeTimer <= 0 && buttonsToShow >= ButtonCount && showButtonsTimer <= 0;

    private sealed class Bird
    {
        public Image image;
        public Vector2 position;
        public float delay;
        public float animTimer;
        public bool alive = true;
    }

    internal StartupTitleScene(RectTransform canvas, bool playSplash)
    {
        this.canvas = canvas;
        canvasComponent = canvas.GetComponent<Canvas>();
        cursors = StardewUiAtlas.Cursors;
        titleButtons = Resources.Load<Texture2D>("Startup/TitleButtons");
        clouds = Resources.Load<Texture2D>("Startup/TitleClouds");
        if (cursors == null || titleButtons == null || clouds == null)
            throw new MissingReferenceException("Title scene needs Cursors, Startup/TitleButtons and Startup/TitleClouds.");

        // The canvas may not be laid out yet during Awake; fall back to 1920x1080.
        width = canvas.rect.width >= 1 ? canvas.rect.width * 2f : 1920f;
        height = canvas.rect.height >= 1 ? canvas.rect.height * 2f : 1080f;
        shades = Random.value < 0.5f;

        stage = Layer("Title Scene");
        stage.SetAsFirstSibling();
        Box(stage, Vector2.zero, Vector2.zero, new Color32(64, 136, 248, 255), stretch: true);
        gradient = Add(stage, Crop(cursors, 703, 1912, 1, 264), 1f);
        stars = Add(stage, Crop(cursors, 0, 1453, 638, 195), 4f);

        Sprite bigCloud = Crop(clouds, 0, 0, 512, 337);
        bigClouds.Add(width * 3 / 4);
        bigCloudImages = new[] { Add(stage, bigCloud, 3f) };

        farMountains = new[]
        {
            Add(stage, Crop(cursors, 0, 886, 639, 148), 3f),
            Add(stage, Crop(cursors, 0, 886, 640, 148), 3f)
        };

        float[] smallStarts =
        {
            width - 1, width - 1 + 690, width * 2 / 3, width / 8, width - 1 + 1290, width * 3 / 4, 1,
            width / 2 + 450, width - 1 + 1890, width - 1 + 390, width / 3 + 570, 301, width / 2 + 2490,
            width * 2 / 3 + 360, width * 3 / 4 + 510, width / 4 + 660
        };
        Sprite[] smallSprites =
        {
            Crop(clouds, 152, 447, 123, 55), Crop(clouds, 0, 471, 149, 66), Crop(clouds, 410, 467, 63, 37)
        };
        smallCloudImages = new Image[smallStarts.Length];
        for (int i = 0; i < smallStarts.Length; i++)
        {
            smallClouds.Add(smallStarts[i] + Random.Range(0, 400));
            smallCloudImages[i] = Add(stage, smallSprites[i % 3], 3f);
        }

        nearMountains = new[]
        {
            Add(stage, Crop(cursors, 0, 737, 639, 148), 3f),
            Add(stage, Crop(cursors, 0, 737, 640, 148), 3f)
        };

        birdFrames = Frames(titleButtons, 296, 227, 26, 21, 4);
        birds = new[]
        {
            new Bird { image = Add(stage, birdFrames[0], 3f), position = new Vector2(width - 210, height - 390) },
            new Bird { image = Add(stage, birdFrames[0], 3f), position = new Vector2(width - 120, height - 360), delay = 100 }
        };

        bushLeft = Add(stage, Crop(clouds, 0, 554, 165, 142), 3f);
        bushRight = Add(stage, Crop(clouds, 390, 543, 122, 153), 3f);

        Sprite logoSprite = Crop(titleButtons, 0, 0, 400, 187);
        logo = Add(stage, logoSprite, 1f);
        logoSwipe = Add(stage, logoSprite, 1f);
        logoSwipe.rectTransform.pivot = new Vector2(0.5f, 1f - 93f / 187f);
        leftGustFrames = Frames(titleButtons, 296, 208, 22, 18, 3);
        rightGustFrames = Frames(titleButtons, 296, 187, 27, 21, 3);
        gust = Add(stage, leftGustFrames[0], 1f);
        gust.enabled = false;

        overlay = Layer("Title Splash");
        white = Box(overlay, Vector2.zero, Vector2.zero, Color.white, stretch: true);
        devTextFrames = new[] { Crop(titleButtons, 171, 311, 111, 60), Crop(titleButtons, 282, 311, 111, 60) };
        int apeRow = 306 + (shades ? 69 : 0);
        apeFrames = new[] { Crop(titleButtons, 0, apeRow, 85, 69), Crop(titleButtons, 85, apeRow, 85, 69) };
        apeSurprisedFrames = new[] { Crop(titleButtons, 176, 375, 85, 69), Crop(titleButtons, 260, 375, 85, 69) };
        devText = Add(overlay, devTextFrames[0], 3f);
        ape = Add(overlay, apeFrames[0], 3f);
        apeSurprised = Add(overlay, apeSurprisedFrames[0], 3f);

        if (!playSplash) SkipToTitleButtons(false);
        Render();
    }

    public void Tick(float deltaSeconds)
    {
        accumulator += Mathf.Min(deltaSeconds, 0.1f) * 1000f;
        while (accumulator >= TickMs)
        {
            accumulator -= TickMs;
            Step(TickMs);
        }
        Render();
    }

    public void Click(Vector2 screenPosition)
    {
        if (logoFadeTimer > 0)
        {
            Vector2 p = ToStage(screenPosition);
            var skipButton = new Rect(width / 2 - 261, height / 2 - 102, 249, 201);
            if (!skipButton.Contains(p)) return;
            if (logoSurprisedTimer <= 0)
            {
                logoSurprisedTimer = 1500;
                GameAudio.PlayMusic(null);
                GameAudio.Play(Random.value < 0.5f ? "Duck" : "fishSlap");
            }
            else if (logoSurprisedTimer > 1)
                logoSurprisedTimer = Mathf.Max(1, logoSurprisedTimer - 500);
            return;
        }
        if (fadeFromWhiteTimer > 0) return;
        if (!titleInPosition && logoSwipeTimer <= 0) RiseImmediately();
    }

    /// <summary>Escape skips the splash; B needs three presses.</summary>
    public void Cancel(bool escape)
    {
        if (logoFadeTimer > 0)
        {
            bCount += escape ? 4 : 1;
            if (bCount < 3) return;
            GameAudio.Play("bigDeSelect");
            SkipToTitleButtons(true);
            return;
        }
        // Keyboard equivalent of clicking while the camera is still rising.
        if (fadeFromWhiteTimer <= 0 && !titleInPosition && logoSwipeTimer <= 0) RiseImmediately();
    }

    /// <summary>Jumps to the finished title (used when returning to the menu).</summary>
    public void SnapToMenu()
    {
        logoFadeTimer = logoSurprisedTimer = fadeFromWhiteTimer = pauseBeforeViewportRiseTimer = 0;
        viewportDY = 0;
        if (viewportY > -1000) viewportY = -1000;
        logoSwipeTimer = 0;
        showButtonsTimer = 0;
        buttonsToShow = ButtonCount;
        titleInPosition = true;
        ClearBirds();
        Render();
    }

    private void SkipToTitleButtons(bool playMusic)
    {
        logoFadeTimer = 0;
        logoSurprisedTimer = 0;
        titleInPosition = false;
        RiseImmediately();
        if (playMusic) GameAudio.PlayMusic("MainTheme");
    }

    private void RiseImmediately()
    {
        pauseBeforeViewportRiseTimer = 0;
        fadeFromWhiteTimer = 0;
        viewportY = -999f;
        viewportDY = -0.01f;
        ClearBirds();
        logoSwipeTimer = 1f;
    }

    private void ClearBirds()
    {
        foreach (Bird bird in birds)
        {
            bird.alive = false;
            bird.image.enabled = false;
        }
    }

    private void Step(float ms)
    {
        if (logoFadeTimer > 0)
        {
            if (logoSurprisedTimer > 0)
            {
                logoSurprisedTimer -= ms;
                if (logoSurprisedTimer <= 0) logoFadeTimer = 1;
            }
            else
            {
                float previous = logoFadeTimer;
                logoFadeTimer -= ms;
                if (logoFadeTimer < 4000 && previous >= 4000) GameAudio.Play("mouseClick");
                if (logoFadeTimer < 2500 && previous >= 2500) GameAudio.Play("mouseClick");
                if (logoFadeTimer < 2000 && previous >= 2000) GameAudio.Play("mouseClick");
                if (logoFadeTimer <= 0) GameAudio.PlayMusic("MainTheme");
            }
        }
        else if (fadeFromWhiteTimer > 0)
        {
            fadeFromWhiteTimer -= ms;
            if (fadeFromWhiteTimer <= 0) pauseBeforeViewportRiseTimer = 3500;
        }
        else if (pauseBeforeViewportRiseTimer > 0)
        {
            pauseBeforeViewportRiseTimer -= ms;
            if (pauseBeforeViewportRiseTimer <= 0) viewportDY = -0.05f;
        }

        viewportY += viewportDY;
        if (viewportDY < 0) viewportDY -= 3f / 500f;
        if (viewportY <= -1000)
        {
            if (viewportDY != 0)
            {
                logoSwipeTimer = 1000;
                showButtonsTimer = 200;
            }
            viewportDY = 0;
        }

        if (logoSwipeTimer > 0)
        {
            logoSwipeTimer -= ms;
            if (logoSwipeTimer <= 0)
            {
                AddLeafGust(left: true);
                AddLeafGust(left: false);
                titleInPosition = true;
            }
        }

        if (showButtonsTimer > 0)
        {
            showButtonsTimer -= ms;
            if (showButtonsTimer <= 0 && buttonsToShow < ButtonCount)
            {
                buttonsToShow++;
                GameAudio.Play("Cowboy_gunshot");
                showButtonsTimer = 200;
            }
        }

        if (titleInPosition && Random.value < 0.005f) AddLeafGust(Random.value < 0.5f);

        for (int i = 0; i < bigClouds.Count; i++)
        {
            bigClouds[i] -= 0.1f;
            if (bigClouds[i] < -1536) bigClouds[i] = width;
        }
        for (int i = 0; i < smallClouds.Count; i++)
        {
            smallClouds[i] -= 0.3f;
            if (smallClouds[i] < -447) smallClouds[i] = width;
        }

        if (gustActive)
        {
            gustTimer += ms;
            // Three frames back and forth, three loops: 0 1 2 1 0 1 2 1 0 1 2 1 0
            if (gustTimer >= 13 * 100f) gustActive = false;
        }

        foreach (Bird bird in birds)
        {
            if (!bird.alive) continue;
            bird.position.y -= viewportDY * 2f;
            if (bird.delay > 0) { bird.delay -= ms; continue; }
            bird.animTimer += ms;
            bird.position.x -= 1f;
            if (bird.position.x < -2000f) bird.alive = false;
        }
    }

    private void AddLeafGust(bool left)
    {
        // Only one leaf gust plays at a time.
        if (gustActive) return;
        int s = LogoScale;
        gustFrames = left ? leftGustFrames : rightGustFrames;
        gustPosition = left ? new Vector2(16 * s, 16 * s) : new Vector2(327 * s, 107 * s);
        gustTimer = 0;
        gustActive = true;
    }

    private int LogoScale => height <= 800 ? 2 : 3;

    private void Render()
    {
        if (canvas.rect.width >= 1 && canvas.rect.height >= 1)
        {
            width = canvas.rect.width * 2f;
            height = canvas.rect.height * 2f;
        }
        Fit(stage);
        Fit(overlay);
        float w = width, h = height, vy = viewportY;

        Put(gradient, 0, -900 - vy * 0.66f);
        gradient.rectTransform.sizeDelta = new Vector2(w, 900 + h - 360);
        Put(stars, -30, -1080 - vy * 0.66f);

        for (int i = 0; i < bigCloudImages.Length; i++)
        {
            Put(bigCloudImages[i], bigClouds[i], h - 750 - vy * 0.5f);
            SetAlpha(bigCloudImages[i], CloudAlpha);
        }
        Put(farMountains[0], -90, h - 474 - vy * 0.66f);
        Put(farMountains[1], 1827, h - 474 - vy * 0.66f);
        for (int i = 0; i < smallCloudImages.Length; i++)
        {
            Put(smallCloudImages[i], smallClouds[i], h - 900 - i * 12 * 3 - vy * 0.5f);
            SetAlpha(smallCloudImages[i], CloudAlpha);
        }
        Put(nearMountains[0], 0, h - 444 - vy);
        Put(nearMountains[1], 1917, h - 444 - vy);

        foreach (Bird bird in birds)
        {
            bird.image.enabled = bird.alive && bird.delay <= 0;
            if (!bird.image.enabled) continue;
            bird.image.sprite = birdFrames[PingPong((int)(bird.animTimer / 100f), birdFrames.Length)];
            Put(bird.image, bird.position.x, bird.position.y);
        }

        Put(bushLeft, 0, h - 426 - vy * 2f);
        Put(bushRight, w - 366, h - 459 - vy * 2f);

        int s = LogoScale;
        float logoX = w / 2 - 200 * s;
        float logoY = -300 * s - vy / 3f * s;
        Put(logo, logoX, logoY);
        logo.rectTransform.sizeDelta = new Vector2(400, 187) * s;
        logoSwipe.enabled = logoSwipeTimer > 0;
        if (logoSwipe.enabled)
        {
            float bump = (0.5f - Mathf.Abs(logoSwipeTimer / 1000f - 0.5f)) * 0.1f;
            logoSwipe.rectTransform.sizeDelta = new Vector2(400, 187) * (s + bump);
            Put(logoSwipe, w / 2, logoY + 93 * s);
        }

        gust.enabled = gustActive;
        if (gustActive)
        {
            gust.sprite = gustFrames[PingPong((int)(gustTimer / 100f), gustFrames.Length)];
            gust.rectTransform.sizeDelta = gust.sprite.rect.size * s;
            Put(gust, logoX + gustPosition.x, logoY + gustPosition.y);
        }

        RenderSplash(w, h);
    }

    private void RenderSplash(float w, float h)
    {
        bool visible = SplashVisible;
        if (overlay.gameObject.activeSelf != visible) overlay.gameObject.SetActive(visible);
        if (!visible) return;

        SetAlpha(white, Mathf.Clamp01(fadeFromWhiteTimer / 2000f));
        bool logoVisible = logoFadeTimer > 0;
        devText.enabled = logoVisible;
        ape.enabled = logoVisible && logoSurprisedTimer <= 0;
        apeSurprised.enabled = logoVisible && logoSurprisedTimer > 0;
        if (!logoVisible) return;

        float alpha = logoFadeTimer < 500 ? logoFadeTimer / 500f
            : logoFadeTimer > 4500 ? 1f - (logoFadeTimer - 4500) / 500f : 1f;
        bool blink = (int)logoFadeTimer / 100 % 2 == 0;
        devText.sprite = devTextFrames[!blink || logoSurprisedTimer > 0 ? 0 : 1];
        SetAlpha(devText, alpha);
        Put(devText, w / 2, h / 2 - 90);

        ape.sprite = apeFrames[blink ? 1 : 0];
        SetAlpha(ape, alpha);
        Put(ape, w / 2 - 261, h / 2 - 102);

        apeSurprised.sprite = apeSurprisedFrames[logoSurprisedTimer > 800 || logoSurprisedTimer < 400 ? 0 : 1];
        SetAlpha(apeSurprised, logoSurprisedTimer < 200 ? logoSurprisedTimer / 200f : 1f);
        Put(apeSurprised, w / 2 - 261, h / 2 - 102);
    }

    /// <summary>Back-and-forth frame order: 0 1 .. n-1 .. 1 0 1 ..</summary>
    private static int PingPong(int step, int frames)
    {
        int period = 2 * (frames - 1);
        int index = step % period;
        return index < frames ? index : period - index;
    }

    private Vector2 ToStage(Vector2 screenPosition)
    {
        Camera camera = canvasComponent != null && canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvasComponent.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, screenPosition, camera, out Vector2 local);
        return new Vector2(local.x + width / 2, height / 2 - local.y);
    }

    public void Dispose()
    {
        if (stage != null) Object.Destroy(stage.gameObject);
        if (overlay != null) Object.Destroy(overlay.gameObject);
        foreach (Sprite sprite in sprites) Object.Destroy(sprite);
        sprites.Clear();
    }

    private RectTransform Layer(string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(canvas, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * 0.5f;
        return rect;
    }

    private void Fit(RectTransform layer) => layer.sizeDelta = new Vector2(width, height);

    private Sprite Crop(Texture2D texture, int x, int y, int w, int h)
    {
        var sprite = Sprite.Create(texture, new Rect(x, texture.height - y - h, w, h),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
        sprites.Add(sprite);
        return sprite;
    }

    private Sprite[] Frames(Texture2D texture, int x, int y, int w, int h, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++) frames[i] = Crop(texture, x + i * w, y, w, h);
        return frames;
    }

    private static Image Add(RectTransform parent, Sprite sprite, float scale)
    {
        var image = Box(parent, Vector2.zero, sprite.rect.size * scale, Color.white);
        image.sprite = sprite;
        return image;
    }

    private static Image Box(RectTransform parent, Vector2 position, Vector2 size, Color color, bool stretch = false)
    {
        var rect = (RectTransform)new GameObject("Title Image", typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(position.x, -position.y);
        }
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Put(Image image, float x, float y) =>
        image.rectTransform.anchoredPosition = new Vector2(x, -y);

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
