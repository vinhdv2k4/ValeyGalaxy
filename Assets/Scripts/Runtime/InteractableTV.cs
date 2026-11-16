using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>FarmHouse TV interaction behavior.</summary>
public sealed class InteractableTV : MonoBehaviour
{
    private enum Channel { Weather, Fortune, Tips, Cooking, Leaderboard, LandRental, LandAuction, Leave }
    private enum MenuPage { Channels, Leaderboard, LandRental, LandAuction }

    private readonly List<(Channel channel, string label)> options = new();
    private static InteractableTV activeMenu;
    private Texture2D font;
    private ToolbarUI toolbar;
    private bool toolbarWasActive;
    private SpriteRenderer screen;
    private SpriteRenderer screenOverlay;
    private Sprite firstFrame;
    private Sprite secondFrame;
    private Sprite overlayFrame;
    private float screenAnimationStarted;
    private Channel playingChannel;
    private int broadcastStage;
    private int selectedOption = -1;
    private MenuPage menuPage;
    private StartupApiClient.TvLeaderboard leaderboard;
    private StartupApiClient.TvLandStatus land;
    private Vector2 landScroll;
    private StartupApiClient.TvAuctionStatus auction;
    private int leaderboardPage;
    private string bidInput = "";
    private string economyMessage;
    private bool economyBusy;

    public static bool IsMenuOpen => activeMenu != null;
    internal static void CloseMenu() => activeMenu?.CloseChannelMenu();

    private void Awake() => font = StardewUiAtlas.FontBold;

    public static bool TryInteract(Vector2? pointer, Vector3 standingPosition, Vector2 facing)
    {
        if (NpcConversation.IsOpen || IsMenuOpen ||
            LocationManager.Instance?.CurrentLocation?.LocationName != "FarmHouse") return false;

        InteractableTV nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (InteractableTV tv in FindObjectsByType<InteractableTV>(FindObjectsSortMode.None))
        {
            if (!tv.isActiveAndEnabled) continue;
            SpriteRenderer renderer = tv.GetComponent<SpriteRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            Vector2 target = bounds.ClosestPoint(standingPosition);
            float distance = Vector2.Distance(standingPosition, target);
            if (distance > 1.3f || distance >= nearestDistance) continue;
            if (pointer.HasValue)
            {
                if (!bounds.Contains(new Vector3(pointer.Value.x, pointer.Value.y, bounds.center.z)))
                    continue;
            }
            else if (Vector2.Dot(((Vector2)bounds.center - (Vector2)standingPosition).normalized,
                         facing.normalized) < 0.35f) continue;
            nearest = tv;
            nearestDistance = distance;
        }
        if (nearest == null) return false;
        nearest.OpenChannelMenu();
        return true;
    }

    private void OpenChannelMenu()
    {
        GameAudio.Play("select");
        if (font == null) font = StardewUiAtlas.FontBold;
        // These two channels are always available. The weekly shows follow the day of the week,
        // with Wednesday reruns appearing only after the first week.
        options.Clear();
        options.Add((Channel.Weather, "Weather Report"));
        options.Add((Channel.Fortune, "Fortune Teller"));
        int day = Mathf.Max(1, FarmDayClock.Instance?.Day ?? 1);
        int weekday = (day - 1) % 7; // Monday = 0
        if (weekday == 0 || weekday == 3)
            options.Add((Channel.Tips, "Livin' Off The Land"));
        if (weekday == 6 || weekday == 2 && day > 7)
            options.Add((Channel.Cooking, "The Queen of Sauce"));
        // These services must remain reachable regardless of the broadcast day.
        options.Add((Channel.Leaderboard, "Farm Leaderboard"));
        options.Add((Channel.LandRental, "Rent / Return Land"));
        options.Add((Channel.LandAuction, "Land Auction"));
        options.Add((Channel.Leave, "(Leave)"));
        selectedOption = -1;
        menuPage = MenuPage.Channels;

        activeMenu = this;
        toolbar = FindAnyObjectByType<ToolbarUI>();
        toolbarWasActive = toolbar != null && toolbar.gameObject.activeSelf;
        if (toolbarWasActive) toolbar.gameObject.SetActive(false);
    }

    private void CloseChannelMenu()
    {
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
        bool wasEconomy = activeMenu == this && menuPage != MenuPage.Channels;
        if (activeMenu == this) activeMenu = null;
        if (toolbarWasActive && toolbar != null) toolbar.gameObject.SetActive(true);
        toolbar = null;
        toolbarWasActive = false;
        if (wasEconomy) TurnOffTV();
        menuPage = MenuPage.Channels;
    }

    // A soft glow from the switched-on screen, so the picture still reads in a dark room.
    private UnityEngine.Rendering.Universal.Light2D screenGlow;
    private static readonly Color ScreenGlowColor = new(0.8f, 0.9f, 1f, 1f);

    private void Update()
    {
        if (screen != null && screen.enabled && secondFrame != null)
            screen.sprite = Mathf.FloorToInt((Time.unscaledTime - screenAnimationStarted) / 0.15f) % 2 == 0
                ? firstFrame : secondFrame;
        if (screenGlow != null)
        {
            bool on = screen != null && screen.enabled;
            if (screenGlow.enabled != on) screenGlow.enabled = on;
            if (on) screenGlow.intensity = WorldLightingController.AmbientDarkness * 0.8f;
        }
        if (activeMenu != this || NpcConversation.IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseChannelMenu(); return; }
        if (menuPage != MenuPage.Channels)
        {
            if (menuPage == MenuPage.Leaderboard)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame && (leaderboard?.entries?.Length ?? 0) > 5)
                    leaderboardPage = 1;
                if (keyboard.leftArrowKey.wasPressedThisFrame) leaderboardPage = 0;
            }
            if (menuPage == MenuPage.LandRental && keyboard.rKey.wasPressedThisFrame &&
                land?.availableParcels != null && land.availableParcels.Length > 0)
                RentLand(land.availableParcels[0].id);
            if (menuPage == MenuPage.LandAuction && keyboard.enterKey.wasPressedThisFrame) PlaceBid();
            return;
        }
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            selectedOption = Mathf.Min(options.Count - 1, selectedOption + 1);
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            selectedOption = selectedOption <= 0 ? 0 : selectedOption - 1;
        if (selectedOption >= 0 && (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            SelectChannel(options[selectedOption].channel);
    }

    private void OnGUI()
    {
        if (font == null) font = StardewUiAtlas.FontBold;
        if (activeMenu != this || NpcConversation.IsOpen || StardewUiAtlas.Cursors == null || font == null) return;
        GUI.depth = -45;
        if (menuPage != MenuPage.Channels) { DrawEconomy(); return; }
        // Uses a 1200 px dialogue box at the bottom of
        // the viewport. Only the selected response gets a Cursors highlight.
        float width = Mathf.Min(1200f, Screen.width - 96f);
        float rowStep = Mathf.Min(64f, (Screen.height - 96f - 104f) / options.Count);
        float height = 104f + options.Count * rowStep;
        Rect box = new((Screen.width - width) * 0.5f,
            Screen.height - height - 48f, width, height);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        NpcBitmapText.Draw(font,
            new Rect(box.x + 32f, box.y + 24f, box.width - 64f, 56f),
            "Select a channel:", 2.5f);

        Event current = Event.current;
        int hoveredOption = -1;
        for (int i = 0; i < options.Count; i++)
        {
            Rect row = new(box.x + 16f, box.y + 88f + i * rowStep,
                box.width - 32f, rowStep - 8f);
            if (current != null && row.Contains(current.mousePosition))
                hoveredOption = i;
        }

        for (int i = 0; i < options.Count; i++)
        {
            Rect row = new(box.x + 16f, box.y + 88f + i * rowStep,
                box.width - 32f, rowStep - 8f);
            bool hovered = i == hoveredOption;
            bool selected = hoveredOption >= 0 ? hovered : i == selectedOption;
            if (selected)
            {
                StardewUiAtlas.DrawTextureBox(row, StardewUiAtlas.Cursors,
                    new Rect(375, 357, 3, 3), Color.white, 4f, false);
            }

            Color previous = GUI.color;
            GUI.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            NpcBitmapText.Draw(font,
                new Rect(row.x + 20f, row.y + 5f, row.width - 40f, row.height - 8f),
                options[i].label, rowStep < 56f ? 2f : 2.5f);
            GUI.color = previous;

            if (hovered && current != null && current.type == EventType.MouseDown && current.button == 0)
            {
                GameAudio.Play("smallSelect");
                Channel choice = options[i].channel;
                current.Use();
                SelectChannel(choice);
                return;
            }
        }
    }

    private void SelectChannel(Channel choice)
    {
        CloseChannelMenu();
        if (choice == Channel.Leaderboard || choice == Channel.LandRental || choice == Channel.LandAuction)
            OpenEconomy(choice);
        else if (choice != Channel.Leave) StartBroadcast(choice);
    }

    private void OpenEconomy(Channel channel)
    {
        menuPage = channel switch
        {
            Channel.Leaderboard => MenuPage.Leaderboard,
            Channel.LandRental => MenuPage.LandRental,
            _ => MenuPage.LandAuction
        };
        leaderboardPage = 0;
        landScroll = Vector2.zero;
        economyMessage = "Loading broadcast...";
        economyBusy = false;
        activeMenu = this;
        toolbar = FindAnyObjectByType<ToolbarUI>();
        toolbarWasActive = toolbar != null && toolbar.gameObject.activeSelf;
        if (toolbarWasActive) toolbar.gameObject.SetActive(false);
        SetScreen(channel, false);
        RefreshEconomy();
    }

    private void RefreshEconomy()
    {
        if (economyBusy) return;
        StartupApiClient api = StartupSession.Api;
        if (api == null || !api.IsSignedIn)
        {
            economyMessage = "Sign in to watch this channel.";
            return;
        }
        economyBusy = true;
        economyMessage = "Loading...";
        if (menuPage == MenuPage.Leaderboard)
            StartCoroutine(api.GetTvLeaderboard((value, error) =>
            {
                leaderboard = value; FinishEconomyRequest(error);
            }));
        else if (menuPage == MenuPage.LandRental)
            StartCoroutine(api.GetTvLand((value, error) =>
            {
                land = value; FinishEconomyRequest(error);
                if (error == null) FindAnyObjectByType<OnlineGameState>()?.Reload();
            }));
        else
            StartCoroutine(api.GetTvAuction((value, error) =>
            {
                auction = value;
                bidInput = value == null ? "" : value.minimumBid.ToString();
                FinishEconomyRequest(error);
                if (error == null) FindAnyObjectByType<OnlineGameState>()?.Reload();
            }));
    }

    private void FinishEconomyRequest(string error)
    {
        economyBusy = false;
        economyMessage = error;
    }

    private void RentLand(string parcelId)
    {
        if (economyBusy || land == null || string.IsNullOrEmpty(parcelId)) return;
        if (land.rentDebt > 0) { economyMessage = "Thanh toán hóa đơn trong hộp thư trước."; return; }
        economyBusy = true;
        StartCoroutine(StartupSession.Api.RentTvLand(parcelId, land.rentPrice, (value, error) =>
        {
            FinishEconomyRequest(error ?? (value == null ? "Không đọc được hạn mức đất sau khi thuê." : null));
            if (error != null || value == null) return;
            land = value;
            FindAnyObjectByType<OnlineGameState>()?.Reload();
            NpcConversation.ShowNotice(
                $"Đã thuê thêm đất. Bạn hiện có thể cuốc và gieo trồng tối đa {land.farmTileLimit} ô đất.");
        }));
    }

    private void ReturnLand(string parcelId)
    {
        if (economyBusy || string.IsNullOrEmpty(parcelId)) return;
        economyBusy = true;
        StartCoroutine(StartupSession.Api.ReturnTvLand(parcelId, (value, error) =>
        {
            if (value != null) land = value;
            FinishEconomyRequest(error ?? $"Đã trả đất. Hạn mức còn {land.farmTileLimit} ô.");
            if (error == null) FindAnyObjectByType<OnlineGameState>()?.Reload();
        }));
    }

    private void PlaceBid()
    {
        if (economyBusy || auction == null || string.IsNullOrEmpty(auction.parcelId)) return;
        if (!long.TryParse(bidInput, out long amount) || amount < auction.minimumBid)
        {
            economyMessage = "Enter at least " + auction.minimumBid + "g.";
            return;
        }
        economyBusy = true;
        StartCoroutine(StartupSession.Api.BidTvLand(auction.parcelId, amount, (value, error) =>
        {
            if (value != null)
            {
                auction = value;
                bidInput = value.minimumBid.ToString();
            }
            FinishEconomyRequest(error ?? "Bid placed. Your money is held until outbid or settlement.");
            if (error == null) FindAnyObjectByType<OnlineGameState>()?.Reload();
        }));
    }

    private void DrawEconomy()
    {
        float width = Mathf.Min(1000f, Screen.width - 48f);
        float height = Mathf.Min(620f, Screen.height - 40f);
        Rect box = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        string title = menuPage switch
        {
            MenuPage.Leaderboard => "FARM LEADERBOARD",
            MenuPage.LandRental => "RENT / RETURN LAND",
            _ => "LAND AUCTION"
        };
        DrawEconomyText(new Rect(box.x + 32f, box.y + 24f, box.width - 64f, 46f), title, 2.5f);
        float y = box.y + 84f;
        if (menuPage == MenuPage.Leaderboard)
        {
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 42f),
                "SCORE = TOTAL EARNED - UNPAID BASE RENT", 1.6f);
            y += 44f;
            var entries = leaderboard?.entries;
            if (entries != null)
            {
                int start = leaderboardPage * 5;
                for (int i = start; i < Mathf.Min(entries.Length, start + 5); i++)
                {
                    var entry = entries[i];
                    DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 32f),
                        $"#{entry.rank} {entry.farmerName}  Score: {entry.score:N0}g", 1.8f);
                    DrawEconomyText(new Rect(box.x + 64f, y + 31f, box.width - 108f, 30f),
                        $"Earned: {entry.totalEarnings:N0}g  Debt: {entry.rentDebt:N0}g  Land: {entry.landCount}", 1.4f);
                    y += 66f;
                }
                if (entries.Length == 0) DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 60f), "No farmers ranked yet.", 2f);
            }
            if (leaderboardPage > 0) DrawEconomyAction(new Rect(box.x + 40f, box.yMax - 128f, 180f, 48f), "Previous", () => leaderboardPage = 0);
            if (leaderboardPage == 0 && entries != null && entries.Length > 5) DrawEconomyAction(new Rect(box.x + 240f, box.yMax - 128f, 180f, 48f), "Next", () => leaderboardPage = 1);
        }
        else if (menuPage == MenuPage.LandRental && land != null)
        {
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 34f),
                $"Đã cuốc {land.farmTilesUsed}/{land.farmTileLimit} ô. Mỗi thửa thêm {land.tilesPerParcel} ô.", 1.8f);
            y += 38f;
            var holdings = land.holdings ?? Array.Empty<StartupApiClient.TvHolding>();
            int rented = 0, owned = 0;
            foreach (var holding in holdings)
            {
                if (holding.isPermanent) owned++;
                else rented++;
            }
            string rentSummary = land.rentDebt > 0
                ? $"Hóa đơn {land.rentDebt:N0}g đang chờ ở hộp thư."
                : $"Số dư {land.money:N0}g.";
            if (land.rentDebt == 0 && rented < land.rentLimit)
                rentSummary += $" Thửa tiếp theo {land.rentPrice:N0}g / {land.rentDays} ngày thực.";
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 34f), rentSummary, 1.65f);
            y += 39f;
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 34f),
                $"Đang thuê {rented}/{land.rentLimit} thửa. Sở hữu {owned} thửa.", 1.65f);
            y += 39f;
            var available = rented < land.rentLimit && land.rentDebt == 0
                ? land.availableParcels ?? Array.Empty<StartupApiClient.TvParcel>()
                : Array.Empty<StartupApiClient.TvParcel>();
            // Rental plots are interchangeable (same price, same +tiles), so offer a
            // single "rent one more" row backed by the first free plot from the server.
            int rentRows = available.Length > 0 ? 1 : 0;
            float listWidth = box.width - 88f;
            float listHeight = Mathf.Max(20f, box.yMax - y - 92f);
            var bounds = new Rect(box.x + 44f, y, listWidth, listHeight);
            landScroll = GUI.BeginScrollView(bounds, landScroll,
                new Rect(0, 0, listWidth - 20f, Mathf.Max(listHeight - 1f, 48f * (rented + rentRows))));
            try
            {
                float rowY = 0f;
                foreach (var holding in holdings)
                {
                    if (holding.isPermanent) continue;
                    string parcelId = holding.parcelId;
                    DrawEconomyText(new Rect(0, rowY, listWidth - 156f, 44f),
                        $"{holding.name} · đến {FormatUtc(holding.expiresAtUtc)}", 1.6f);
                    DrawEconomyAction(new Rect(listWidth - 138f, rowY, 112f, 42f),
                        "Trả đất", () => ReturnLand(parcelId));
                    rowY += 48f;
                }
                if (rentRows > 0)
                {
                    string parcelId = available[0].id;
                    DrawEconomyText(new Rect(0, rowY, listWidth - 156f, 44f),
                        $"Thuê thêm 1 thửa  +{land.tilesPerParcel} ô · {land.rentPrice:N0}g / {land.rentDays} ngày", 1.6f);
                    DrawEconomyAction(new Rect(listWidth - 138f, rowY, 112f, 42f), "Thuê", () => RentLand(parcelId));
                    rowY += 48f;
                }
            }
            finally { GUI.EndScrollView(); }
        }
        else if (menuPage == MenuPage.LandAuction && auction != null)
        {
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 52f),
                $"Auction #{auction.round}: {auction.parcelName} [{auction.parcelId}]", 2f);
            y += 68f;
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 52f),
                $"Ends: {FormatUtc(auction.endsAtUtc)}   Bidder: {auction.highestBidder ?? "none"}", 1.8f);
            y += 68f;
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 52f),
                $"Top: {auction.highestBid:N0}g   Min: {auction.minimumBid:N0}g   Balance: {auction.yourMoney:N0}g", 1.8f);
            y += 68f;
            DrawEconomyText(new Rect(box.x + 44f, y, box.width - 88f, 52f),
                string.IsNullOrEmpty(auction.parcelId) ? "No auction land is currently available." :
                auction.youAreWinning ? "You are currently winning." : "Winning bid grants this land deed.", 2f);
            y += 76f;
            if (!string.IsNullOrEmpty(auction.parcelId))
            {
                bidInput = GUI.TextField(new Rect(box.x + 44f, y, 220f, 42f), bidInput, 12);
                DrawEconomyAction(new Rect(box.x + 284f, y, 220f, 48f), "Place bid", PlaceBid);
            }
            DrawEconomyText(new Rect(box.x + 44f, y + 62f, box.width - 88f, 42f),
                "Bids are held. Outbid players get their money back.", 1.6f);
        }
        if (!string.IsNullOrEmpty(economyMessage))
            DrawEconomyText(new Rect(box.x + 44f, box.yMax - 76f, box.width - 400f, 48f), economyMessage, 1.7f);
        DrawEconomyAction(new Rect(box.xMax - 348f, box.yMax - 76f, 140f, 48f), "Refresh", RefreshEconomy);
        DrawEconomyAction(new Rect(box.xMax - 188f, box.yMax - 76f, 140f, 48f), "Close", CloseChannelMenu);
    }

    private void DrawEconomyText(Rect rect, string value, float scale) =>
        NpcBitmapText.Draw(font, rect, value ?? "", scale);

    private void DrawEconomyAction(Rect rect, string label, Action action)
    {
        Event current = Event.current;
        bool hovered = current != null && rect.Contains(current.mousePosition);
        if (hovered) StardewUiAtlas.DrawTextureBox(rect, StardewUiAtlas.Cursors,
            new Rect(375, 357, 3, 3), Color.white, 4f, false);
        DrawEconomyText(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, rect.height - 8f), label, 1.8f);
        if (hovered && current.type == EventType.MouseDown && current.button == 0)
        {
            GameAudio.Play("smallSelect");
            current.Use();
            action();
        }
    }

    private static string FormatUtc(string value) =>
        DateTime.TryParse(value, out DateTime when) ? when.ToUniversalTime().ToString("MM/dd HH:mm 'UTC'") : "unknown";

    private void StartBroadcast(Channel channel)
    {
        playingChannel = channel;
        broadcastStage = 1;
        SetScreen(channel, false);
        NpcConversation.Closed -= OnDialogueClosed;
        NpcConversation.Closed += OnDialogueClosed;
        NpcConversation.ShowNotice(channel switch
        {
            Channel.Weather => "Welcome to the Weather Report!",
            Channel.Fortune => "Welcome to Welwick's Oracle...",
            Channel.Tips => "Livin' Off The Land. Back to nature tips for the modern farmer!",
            Channel.Cooking => "Greetings! It is I, the Queen of Sauce!",
            _ => string.Empty
        });
    }

    private void OnDialogueClosed()
    {
        if (broadcastStage == 1)
        {
            broadcastStage = 2;
            SetScreen(playingChannel, true);
            NpcConversation.ShowNotice(GetBroadcastContent());
        }
        else TurnOffTV();
    }

    private string GetBroadcastContent()
    {
        FarmDayClock clock = FarmDayClock.Instance;
        switch (playingChannel)
        {
            case Channel.Weather:
                return (clock?.WeatherForTomorrow ?? 0) switch
                {
                    1 => "It's going to rain tomorrow, all day.",
                    2 => "It's going to be windy all day tomorrow.",
                    3 => "A storm is approaching tomorrow. Thunder and lightning are expected.",
                    4 => "The weather will be clear tomorrow for the festival.",
                    5 => "Snow is expected tomorrow.",
                    _ => "It's going to be a beautiful, sunny day tomorrow."
                };
            case Channel.Fortune:
                float luck = clock?.DailyLuck ?? 0.001f;
                if (luck < -0.07f) return "The spirits are very displeased today.";
                if (luck < -0.02f) return "The spirits are somewhat annoyed today.";
                if (luck > 0.07f) return "The spirits are very happy today!";
                if (luck > 0.02f) return "The spirits are in good humor today.";
                return "The spirits feel neutral today. The day is in your hands.";
            case Channel.Tips:
                return "This channel is currently unavailable.";
            case Channel.Cooking:
                return "This channel is currently unavailable.";
            default:
                return string.Empty;
        }
    }

    private void SetScreen(Channel channel, bool secondScene)
    {
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        if (screen == null)
        {
            var screenObject = new GameObject("TV Broadcast Screen");
            screenObject.transform.SetParent(transform, false);
            // Screen offset for the Budget TV (1466): (24, 0), drawn at scale 2.
            screenObject.transform.localPosition = new Vector3(0.375f, 2f, 0f);
            screen = screenObject.AddComponent<SpriteRenderer>();
            screen.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 1;
        }
        ClearScreenFrames();
        Rect source = channel switch
        {
            Channel.Weather => secondScene ? new Rect(497, 305, 42, 28) : new Rect(413, 305, 42, 28),
            Channel.Fortune => secondScene ? new Rect(624, 305, 42, 28) : new Rect(540, 305, 42, 28),
            Channel.Tips => new Rect(517, 361, 42, 28),
            Channel.Cooking => new Rect(602, 361, 42, 28),
            Channel.Leaderboard => new Rect(517, 361, 42, 28),
            Channel.LandRental => new Rect(602, 361, 42, 28),
            Channel.LandAuction => new Rect(602, 361, 42, 28),
            _ => Rect.zero
        };
        firstFrame = CreateCursorSprite(cursors, source, 32f);
        if (!secondScene)
            secondFrame = CreateCursorSprite(cursors,
                new Rect(source.x + source.width, source.y, source.width, source.height), 32f);
        screen.sprite = firstFrame;
        screenAnimationStarted = Time.unscaledTime;
        screen.enabled = true;
        if (screenGlow == null)
        {
            screenGlow = WorldLightingController.CreateFurnitureLight(screen.transform, "TV Screen Glow",
                ScreenGlowColor, 1.75f);
            screenGlow.transform.localPosition = new Vector3(42f / 64f, -28f / 64f, 0f);
        }
        SetScreenOverlay(channel, secondScene, cursors);
    }

    private void SetScreenOverlay(Channel channel, bool secondScene, Texture2D cursors)
    {
        if (screenOverlay == null)
        {
            var overlayObject = new GameObject("TV Broadcast Overlay");
            overlayObject.transform.SetParent(screen.transform, false);
            screenOverlay = overlayObject.AddComponent<SpriteRenderer>();
            screenOverlay.sortingOrder = screen.sortingOrder + 1;
        }
        screenOverlay.enabled = false;
        overlayFrame = null;
        if (!secondScene || channel != Channel.Weather && channel != Channel.Fortune) return;

        Rect source;
        if (channel == Channel.Weather)
        {
            int weather = FarmDayClock.Instance?.WeatherForTomorrow ?? 0;
            source = weather switch
            {
                1 => new Rect(465, 333, 13, 13),
                2 => new Rect(465, 359, 13, 13),
                3 => new Rect(413, 346, 13, 13),
                4 => new Rect(413, 372, 13, 13),
                5 => new Rect(465, 346, 13, 13),
                _ => new Rect(413, 333, 13, 13)
            };
            screenOverlay.transform.localPosition = new Vector3(0.09375f, -0.09375f, 0f);
        }
        else
        {
            float luck = FarmDayClock.Instance?.DailyLuck ?? 0.001f;
            source = luck < -0.07f ? new Rect(592, 346, 13, 13) :
                luck < -0.02f ? new Rect(540, 346, 13, 13) :
                luck > 0.07f ? new Rect(644, 333, 13, 13) :
                luck > 0.02f ? new Rect(592, 333, 13, 13) :
                new Rect(540, 333, 13, 13);
            screenOverlay.transform.localPosition = new Vector3(0.46875f, -0.03125f, 0f);
        }
        overlayFrame = CreateCursorSprite(cursors, source, 32f);
        screenOverlay.sprite = overlayFrame;
        screenOverlay.enabled = true;
    }

    private static Sprite CreateCursorSprite(Texture2D atlas, Rect source, float pixelsPerUnit)
        => AtlasSprites.FromTop(atlas, (int)source.x, (int)source.y, (int)source.width, (int)source.height,
            new Vector2(0f, 1f), pixelsPerUnit, false);

    private void ClearScreenFrames() => firstFrame = secondFrame = null;

    private void TurnOffTV()
    {
        broadcastStage = 0;
        NpcConversation.Closed -= OnDialogueClosed;
        if (screen != null) screen.enabled = false;
        if (screenOverlay != null) screenOverlay.enabled = false;
    }

    private void OnDisable()
    {
        CloseChannelMenu();
        TurnOffTV();
    }

    private void OnDestroy()
    {
        TurnOffTV();
        ClearScreenFrames();
        overlayFrame = null;
    }
}
