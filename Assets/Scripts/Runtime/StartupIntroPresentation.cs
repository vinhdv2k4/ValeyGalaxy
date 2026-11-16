using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FarmCutscenes;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal sealed class StartupIntroScript
{
    public float fade, blackHold, timePassHold;
    public float jojaOverheadHold, jojaPanTime, letterOpenTime, busCloudsTime, busRoadPanTime, busDriveTime, busSignTime;
    public float roadSpeed, grandpaFade, grandpaSpeechEnd, grandpaEnvelopeAt, grandpaEnvelopeFlight;
    public string yearsLater, deskHint, letter;
    public SpeechLine[] grandpaSpeech;
    public EventScript arrival, farm;

    internal sealed class SpeechLine
    {
        public float at;
        public string text, femaleText;
    }

    internal static StartupIntroScript Load() =>
        JsonConvert.DeserializeObject<StartupIntroScript>(Resources.Load<TextAsset>("Cutscenes/StartupIntro").text);

    internal (EventDefinition Arrival, EventDefinition Farm) CompileWorldScenes()
    {
        var commands = new EventCommandFactory();
        commands.Register("guide", UnityCutsceneStage.CreateGuideCommand);
        var repository = new EventRepository(new EventScriptFile { events = new[] { arrival, farm } },
            new EventConditionFactory(), commands);
        return (repository.Events[0], repository.Events[1]);
    }

    // Only Escape skips. Space must stay free: NpcConversation uses it to advance dialogue.
    internal static bool SkipPressed =>
        Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
        Gamepad.current?.startButton.wasPressedThisFrame == true;
}

/// <summary>Title-canvas presentation. The in-world arrival uses the regular event commands.</summary>
internal sealed class StartupIntroPresentation
{
    private static readonly Vector2 FrameSize = new(960, 540);
    private static readonly Color Sky = new Color32(64, 136, 248, 255);
    private static readonly Color Ink = new Color32(62, 32, 26, 255);

    private readonly RectTransform canvas;
    private readonly Image curtain;
    private readonly StartupIntroScript script;
    private readonly CancellationToken token;
    private readonly List<Sprite> sprites = new();
    private RectTransform backdrop;
    private RectTransform frame;

    internal StartupIntroPresentation(RectTransform canvas, Image curtain, StartupIntroScript script,
        CancellationToken token)
    {
        this.canvas = canvas;
        this.curtain = curtain;
        this.script = script;
        this.token = token;
    }

    internal Task FadeToBlack(Action<float> fadeClouds) => Fade(curtain.color.a, 1, progress: fadeClouds);

    internal async Task PlayGrandpaAsync(CreatedFarmer farmer)
    {
        Texture2D atlas = BeginScene("jojacorps", Color.black);
        Texture2D letterAtlas = null;
        try
        {
            GameAudio.PlayMusic(null);
            GameAudio.PlayAmbience(null);
            await Animate(script.blackHold);

            await PlayGrandpaSpeech(atlas, farmer.gender == 1);
            GameAudio.PlayAmbience(null);
            GameAudio.PlayMusic(null);
            ClearFrame();

            var years = Label(frame, Vector2.zero, FrameSize, 37, Color.white);
            years.alignment = TextAnchor.MiddleCenter;
            years.text = script.yearsLater;
            await Fade(1, 0);
            await Animate(script.timePassHold);
            await Fade(0, 1);
            ClearFrame();

            Picture(Crop(atlas, 0, 0, 427, 240), Vector2.zero, FrameSize.x / 427f);
            GameAudio.PlayAmbience("jojaOfficeSoundscape");
            await Fade(1, 0);
            await Animate(script.jojaOverheadHold);
            await Fade(0, 1);
            ClearFrame();

            var office = Picture(Crop(atlas, 0, 600, 1200, 180), Vector2.zero, FrameSize.y / 180);
            await Fade(1, 0);
            await Animate(script.jojaPanTime, t => Place(office.rectTransform,
                new Vector2((FrameSize.x - office.rectTransform.sizeDelta.x) * t, 0)));
            GameAudio.Play("breathout");
            await Fade(0, 1);
            ClearFrame();

            Picture(Crop(atlas, 0, 240, 320, 180), Vector2.zero, FrameSize.x / 320);
            var drawer = Clickable(Picture(Crop(atlas, 264, 434, 162, 141), new Vector2(488, 105), 2.4f));
            var hint = Label(frame, new Vector2(480, 455), new Vector2(470, 56), 24, Color.white);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.text = script.deskHint;
            await Fade(1, 0);
            await WaitForClick(drawer);
            hint.gameObject.SetActive(false);
            drawer.interactable = false;
            var openingFrames = new Sprite[3];
            for (int i = 0; i < openingFrames.Length; i++)
                openingFrames[i] = Crop(atlas, 729 + i * 131, 508, 131, 79);
            var sealedLetter = Picture(openingFrames[0], new Vector2(588, 259), 1.8f);
            GameAudio.Play("openBox");
            await Animate(script.letterOpenTime, t =>
                sealedLetter.sprite = openingFrames[Mathf.Min((int)(t * openingFrames.Length), openingFrames.Length - 1)]);
            GameAudio.Play("newRecipe");
            GameAudio.PlayAmbience(null);
            GameAudio.PlayMusic("musicboxsong");

            Box(frame, Vector2.zero, FrameSize, new Color(0, 0, 0, 0.4f));
            letterAtlas = Resources.Load<Texture2D>("Cutscenes/letterBG");
            var paper = Picture(Crop(letterAtlas, 0, 0, 320, 180), new Vector2(80, 45), 2.5f);
            var body = Label(paper.rectTransform, new Vector2(58, 42), new Vector2(684, 366), 24, Ink);
            body.text = string.Format(script.letter, farmer.name, farmer.farmName);
            var close = Picture(Crop(StardewUiAtlas.Cursors, 337, 494, 12, 12),
                new Vector2(742, 10), 3f, paper.rectTransform);
            await WaitForClick(Clickable(close));
            GameAudio.Play("newRecipe");
            await Fade(0, 1);
        }
        finally
        {
            EndScene(atlas);
            if (letterAtlas != null) Resources.UnloadAsset(letterAtlas);
        }
    }

    private async Task PlayGrandpaSpeech(Texture2D atlas, bool female)
    {
        // The room is drawn at 3x in a centered 1294x730 stage, halved for the 960x540 canvas.
        const float scale = 1.5f;
        Vector2 roomOrigin = (FrameSize - new Vector2(647, 365)) * 0.5f;
        Sprite roomA = Crop(atlas, 427, 0, 427, 240);
        Sprite roomB = Crop(atlas, 427, 240, 427, 240);
        var room = Picture(roomA, roomOrigin, scale);
        var fireFrames = new Sprite[4];
        for (int i = 0; i < fireFrames.Length; i++)
            fireFrames[i] = Crop(atlas, 427 + i * 74, 480, 74, 42);
        var fire = Picture(fireFrames[0], roomOrigin + new Vector2(317, 74) * scale, scale);
        Picture(Crop(atlas, 427, 522, 70, 32), roomOrigin + new Vector2(320, 75) * scale, scale);
        void Flicker()
        {
            float clock = Time.unscaledTime;
            room.sprite = Mathf.Repeat(clock, 0.3f) < 0.15f ? roomA : roomB;
            fire.sprite = fireFrames[Mathf.FloorToInt(clock * 10) % fireFrames.Length];
        }

        Vector2 letterStart = roomOrigin + new Vector2(477, 345) * 0.5f;
        var envelope = Picture(Crop(atlas, 729, 525, 131, 63), letterStart, 1.2f);
        envelope.gameObject.SetActive(false);
        var caption = Label(frame, new Vector2(64, roomOrigin.y + 669 * 0.5f), new Vector2(832, 92), 23, Color.white);
        caption.alignment = TextAnchor.UpperCenter;
        var shadow = caption.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1, 1, 1, 0.25f);
        shadow.effectDistance = new Vector2(-1.5f, 0);
        await Fade(1, 0, Flicker, duration: script.grandpaFade);
        GameAudio.PlayMusic("grandpas_theme");
        GameAudio.PlayAmbience("cracklingFire");

        int nextLine = 0;
        bool letterReceived = false;
        await Animate(script.grandpaSpeechEnd, progress =>
        {
            Flicker();
            float elapsed = progress * script.grandpaSpeechEnd;
            while (nextLine < script.grandpaSpeech.Length && elapsed >= script.grandpaSpeech[nextLine].at)
            {
                var line = script.grandpaSpeech[nextLine++];
                caption.text = female ? line.femaleText ?? line.text : line.text;
            }
            if (elapsed < script.grandpaEnvelopeAt) return;
            if (!letterReceived)
            {
                letterReceived = true;
                envelope.gameObject.SetActive(true);
                GameAudio.Play("newRecipe");
            }
            float flight = (elapsed - script.grandpaEnvelopeAt) / script.grandpaEnvelopeFlight;
            if (flight <= 1f)
                Place(envelope.rectTransform, letterStart + new Vector2(150 * flight, 310 * flight * flight - 80 * flight));
            else if (envelope.gameObject.activeSelf) envelope.gameObject.SetActive(false);
        });
        await Fade(0, 1, Flicker, duration: script.grandpaFade);
    }

    internal async Task PlayBusAsync()
    {
        Texture2D atlas = BeginScene("Intro", Sky);
        try
        {
            var scenery = Box(frame, Vector2.zero, FrameSize, Sky).rectTransform;
            var clouds = RepeatStrip(Crop(atlas, 128, 0, 112, 96), scenery, 0, 3);
            var cursors = StardewUiAtlas.Cursors;
            var distant = RepeatStrip(Crop(cursors, 0, 886, 639, 148), scenery, 154, 2);
            var valley = RepeatStrip(Crop(cursors, 0, 737, 639, 120), scenery, 185, 2);
            Box(scenery, new Vector2(0, 408), new Vector2(FrameSize.x, 132), new Color32(117, 118, 116, 255));
            var shoulder = RepeatStrip(Crop(atlas, 0, 176, 48, 48), scenery, 350, 2);
            var road = RepeatStrip(Crop(atlas, 0, 240, 16, 16), scenery, 455, 3);
            var fence = new Image[13];
            for (int i = 0; i < fence.Length; i++)
            {
                fence[i] = Box(scenery, new Vector2(i * 80, 367), new Vector2(80, 5), new Color32(150, 102, 58, 255));
                Box(fence[i].rectTransform, new Vector2(0, 10), new Vector2(80, 4), new Color32(116, 73, 44, 255));
                Box(fence[i].rectTransform, new Vector2(2, -5), new Vector2(7, 24), new Color32(180, 126, 70, 255));
            }
            var sign = Picture(Crop(atlas, 48, 176, 64, 64), new Vector2(970, 260), 2.5f, scenery);
            var car = Picture(Crop(atlas, 160, 112, 80, 64), new Vector2(970, 319), 2.5f, scenery);
            var bus = Picture(Crop(atlas, 0, 0, 128, 64), new Vector2(209, 322), 2.75f, scenery);
            Sprite wheel = Crop(atlas, 21, 54, 5, 5);
            var front = Picture(wheel, new Vector2(85, 54) * 2.75f, 2.75f, bus.rectTransform);
            var back = Picture(wheel, new Vector2(21, 54) * 2.75f, 2.75f, bus.rectTransform);
            CenterWheel(front.rectTransform);
            CenterWheel(back.rectTransform);

            GameAudio.PlayMusic("spring_day_ambient");
            Place(scenery, new Vector2(0, 265));
            await Fade(1, 0);
            await Animate(script.busCloudsTime);
            await Animate(script.busRoadPanTime, t => Place(scenery, new Vector2(0, Mathf.Lerp(265, 0, t))));
            GameAudio.PlayAmbience("roadnoise");
            float distance = 0;
            void Scroll()
            {
                distance += script.roadSpeed * Time.unscaledDeltaTime;
                ScrollStrip(clouds, distance * 0.2f);
                ScrollStrip(distant, distance * 0.25f);
                ScrollStrip(valley, distance * 0.5f);
                ScrollStrip(shoulder, distance);
                ScrollStrip(road, distance);
                ScrollStrip(fence, distance);
                Place(bus.rectTransform, new Vector2(209, 322 + Mathf.Sin(distance / 8) * 1.5f));
                front.rectTransform.localRotation = Quaternion.Euler(0, 0, -distance / 3 / 16 * 360);
                back.rectTransform.localRotation = Quaternion.Euler(0, 0, -(distance + 4) / 3 / 16 * 360);
            }
            await Animate(script.busDriveTime, t =>
            {
                Scroll();
                Place(car.rectTransform, new Vector2(Mathf.Lerp(1050, -1400, t), 319));
            });
            await Animate(script.busSignTime, t =>
            {
                Scroll();
                Place(sign.rectTransform, new Vector2(Mathf.Lerp(970, -170, t), 260));
            });
            await Fade(0, 1, Scroll);
        }
        finally { EndScene(atlas); }
    }

    private Texture2D BeginScene(string name, Color background)
    {
        var atlas = Resources.Load<Texture2D>("Cutscenes/" + name);
        if (atlas == null) throw new InvalidOperationException("Missing intro atlas " + name);
        backdrop = Box(canvas, Vector2.zero, Vector2.zero, Color.black).rectTransform;
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.SetSiblingIndex(curtain.transform.GetSiblingIndex());
        frame = Box(backdrop, Vector2.zero, FrameSize, background).rectTransform;
        frame.name = name;
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        ResizeFrame();
        frame.gameObject.AddComponent<RectMask2D>();
        return atlas;
    }

    private void ResizeFrame()
    {
        Vector2 size = canvas.rect.size;
        frame.localScale = Vector3.one * Mathf.Min(size.x / FrameSize.x, size.y / FrameSize.y);
    }

    private void EndScene(Texture2D atlas)
    {
        if (curtain != null) curtain.color = Color.black;
        GameAudio.PlayMusic(null);
        GameAudio.PlayAmbience(null);
        if (backdrop != null)
        {
            backdrop.gameObject.SetActive(false);
            Object.Destroy(backdrop.gameObject);
        }
        frame = null;
        foreach (Sprite sprite in sprites) Object.Destroy(sprite);
        sprites.Clear();
        Resources.UnloadAsset(atlas);
    }

    private void ClearFrame()
    {
        foreach (Transform child in frame)
        {
            child.gameObject.SetActive(false);
            Object.Destroy(child.gameObject);
        }
    }

    private async Task WaitForClick(Button button)
    {
        bool pressed = false;
        button.onClick.AddListener(() => pressed = true);
        while (!pressed)
        {
            await Awaitable.NextFrameAsync(token);
            ResizeFrame();
            if (Keyboard.current?.enterKey.wasPressedThisFrame == true) break;
        }
        await Awaitable.NextFrameAsync(token);
    }

    private Task Fade(float start, float end, Action update = null, Action<float> progress = null,
        float? duration = null) => Animate(duration ?? script.fade, t =>
    {
        curtain.color = new Color(0, 0, 0, Mathf.Lerp(start, end, t));
        progress?.Invoke(t);
        update?.Invoke();
    });

    private async Task Animate(float seconds, Action<float> update = null)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        {
            if (frame != null) ResizeFrame();
            update?.Invoke(Mathf.Clamp01(elapsed / seconds));
            await Awaitable.NextFrameAsync(token);
            elapsed += Time.unscaledDeltaTime;
        }
        update?.Invoke(1);
    }

    private Sprite Crop(Texture2D texture, int x, int y, int w, int h)
    {
        var sprite = Sprite.Create(texture, new Rect(x, texture.height - y - h, w, h),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
        sprites.Add(sprite);
        return sprite;
    }

    private Image Picture(Sprite sprite, Vector2 position, float scale, RectTransform parent = null)
    {
        var image = Box(parent ?? frame, position, sprite.rect.size * scale, Color.white);
        image.sprite = sprite;
        return image;
    }

    private Image[] RepeatStrip(Sprite sprite, RectTransform parent, float y, float scale)
    {
        float width = sprite.rect.width * scale;
        var tiles = new Image[Mathf.CeilToInt(FrameSize.x / width) + 1];
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = Picture(sprite, new Vector2(i * width, y), scale, parent);
        return tiles;
    }

    private static void ScrollStrip(Image[] tiles, float distance)
    {
        float width = tiles[0].rectTransform.sizeDelta.x;
        float shift = Mathf.Repeat(distance, width);
        for (int i = 0; i < tiles.Length; i++)
        {
            var rect = tiles[i].rectTransform;
            rect.anchoredPosition = new Vector2(i * width - shift, rect.anchoredPosition.y);
        }
    }

    private static void CenterWheel(RectTransform wheel)
    {
        wheel.pivot = new Vector2(0.5f, 0.5f);
        wheel.anchoredPosition += new Vector2(wheel.sizeDelta.x, -wheel.sizeDelta.y) * 0.5f;
    }

    private static RectTransform Node(RectTransform parent, Vector2 position, Vector2 size, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.sizeDelta = size;
        Place(rect, position);
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 position) =>
        rect.anchoredPosition = new Vector2(position.x, -position.y);

    private static Image Box(RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Node(parent, position, size, "Intro Image").gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button Clickable(Image image)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Text Label(RectTransform parent, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var label = Node(parent, position, size, "Intro Text").gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }
}
