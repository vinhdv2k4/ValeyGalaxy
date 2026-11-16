using System;
using System.Collections.Generic;
using System.Linq;
using DailyEvents;
using UnityEngine;

/// <summary>Connects calendar rules to the local farmer, map actions and account progress.</summary>
internal sealed class DailyEventPlayer
{
    private const string OpeningJournalEntry = "farm_intro";
    private readonly IDailyEventSession session;
    private readonly PlayerInventory inventory;
    private readonly CharacterMotor farmer;
    private readonly FishingController fishing;
    private readonly DailyEventCalendar calendar = DailyEventAssets.Calendar;
    private bool pending;
    private float nextAutoAttempt;
    private int observedDay;
    private DailyEventDefinition festival;
    private Vector2Int festivalReturn;
    private readonly HashSet<int> displaySlots = new();
    private readonly Dictionary<Vector2Int, GameObject> eggs = new();
    private Sprite eggSprite;
    private float huntEndsAt;
    private int eggsCollected;
    private int iceFishCaught;
    private readonly Queue<int> shellNotes = new();
    private readonly List<GameObject> markers = new();
    private GameLocation markerMap;
    private int markerVersion = -1, markerTime = -1, markerDay = -1;
    private DailyEventDefinition nextMail;
    private OnlineGameSnapshot mailboxSnapshot;
    private int mailboxDay, mailboxVersion = -1;
    private bool HasRentBill => session.Snapshot?.rentDebt > 0 && farmer.GetComponent<OnlineGameState>() != null;
    internal bool IsBusy => pending;

    private OnlineDailyEventStatus Progress => session.Snapshot?.dailyEvents;
    private FarmDayClock Clock => FarmDayClock.Instance;
    private GameLocation Location => LocationManager.Instance?.CurrentLocation;
    private FarmDate Date => Clock.Date;
    private bool Ready => Clock != null && Progress != null && session.Snapshot.gameDay == Clock.Day;
    private int Completed(string key) => Progress?.CompletedOn(key) ?? 0;
    private bool IsAvailable(DailyEventDefinition entry) => calendar.IsAvailable(entry, Date, Completed);

    internal DailyEventPlayer(OnlineGameState online, PlayerInventory inventory, CharacterMotor farmer,
        FishingController fishing) : this(new OnlineDailyEventSession(online), inventory, farmer, fishing)
    {
    }

    internal DailyEventPlayer(IDailyEventSession session, PlayerInventory inventory, CharacterMotor farmer,
        FishingController fishing)
    {
        this.session = session;
        this.inventory = inventory;
        this.farmer = farmer;
        this.fishing = fishing;
    }

    internal bool Tick(bool autoStartStory = true)
    {
        RefreshMailbox();
        if (!Ready || pending || Location == null) return false;
        if (festival != null && (!festival.IsDue(Date) || Location.LocationName != festival.map &&
            Location.LocationName is not ("Submarine" or "MermaidHouse")))
        {
            festival = null;
            displaySlots.Clear();
            ClearHunt();
            iceFishCaught = 0;
            shellNotes.Clear();
        }
        if (festival != null && Location.LocationName == festival.map)
            FestivalCast.Populate(Location, festival, calendar.Villagers, Progress, farmer.StandingPosition, Date.Year);
        RefreshMarkers();
        RefreshBushes();
        if (huntEndsAt > 0f && Time.unscaledTime >= huntEndsAt)
        {
            FinishHunt();
            return true;
        }
        if (observedDay != Clock.Day)
        {
            observedDay = Clock.Day;
            if (Clock.Day == calendar.Find("earthquake").day)
            {
                if (autoStartStory)
                {
                    ShowEarthquake(calendar.Find("earthquake"));
                    return true;
                }
            }
        }

        if (!autoStartStory || Time.unscaledTime < nextAutoAttempt) return false;
        foreach (DailyEventDefinition entry in calendar.Events)
        {
            if (entry.kind != "story" || entry.cutscene || entry.HasTile || !CanMeet(entry)) continue;
            // farm_intro journal entry, which StartupFarmArrival already
            // plays with Robin and Lewis on the map. Auto-meeting it here only opened a Lewis
            // dialogue box with no Lewis in the world when stepping outside on day 1.
            if (entry.id == OpeningJournalEntry) continue;
            nextAutoAttempt = Time.unscaledTime + 15f;
            Meet(entry);
            return true;
        }
        return false;
    }

    internal bool TryInteract(Vector2? pointer, bool interaction = true)
    {
        if (pending || Location == null) return false;
        Vector3 standing = farmer.StandingPosition;
        Vector2 facing = farmer.Facing;
        if (interaction && Location.Mailbox?.CanInteract(pointer, standing, facing) == true)
        {
            farmer.SetMovement(Vector2.zero);
            if (!Ready) NpcConversation.ShowNotice("Đang tải hộp thư.");
            else
            {
                RefreshMailbox();
                if (HasRentBill && nextMail != null)
                {
                    var unread = nextMail;
                    NpcConversation.ShowChoices("Hộp thư", new (string, Action)[]
                    {
                        ("Hóa đơn thuê trại", OpenRentBill),
                        (unread.title, () => OpenUnreadLetter(unread)),
                        ("Đóng", () => { })
                    });
                }
                else if (HasRentBill) OpenRentBill();
                else if (nextMail == null) NpcConversation.ShowNotice("You don't have any mail.");
                else OpenUnreadLetter(nextMail);
            }
            return true;
        }
        if (!Ready) return false;

        if (!interaction) return false;

        if (festival != null && Location.LocationName == festival.map)
        {
            if (TryCollectEgg(pointer, standing, facing)) return true;
            if (festival.activity == "maze" && Completed(festival.Key(Date)) == 0 &&
                NpcShop.HitActionTile(pointer, standing, facing, festival.spotX, festival.spotY))
            {
                RunActivity(festival, x: festival.spotX, y: festival.spotY);
                return true;
            }
            if (FestivalCast.TryFind(Location, pointer, standing, facing, out string name))
            {
                TalkAtFestival(festival, name);
                return true;
            }
        }
        if (TryForage(pointer, standing, facing)) return true;

        NpcActor npc = null;
        if (Location.LocationName == "Saloon" && inventory.SelectedItem?.itemData?.itemId == "object_304" &&
            Completed("summer_14_1") > 0 && Completed("summer_14_1/delivered") == 0)
        {
            npc = NpcConversation.FindClosest(pointer, standing, facing);
            if (npc?.DisplayName == "Gus")
            {
                var request = calendar.Find("summer_14_1");
                Submit(request, "brew", () => ShowText("Gus", "Đây là Pale Ale cho Pam."), slot: inventory.SelectedIndex);
                return true;
            }
        }
        foreach (DailyEventDefinition entry in calendar.Events)
        {
            string key = entry.Key(Date);
            if (entry.kind == "story" && !entry.cutscene && entry.HasTile && CanMeet(entry) &&
                NpcShop.HitActionTile(pointer, standing, facing, entry.x, entry.y))
            {
                Meet(entry);
                return true;
            }
            if (entry.kind != "mail" || entry.item.Length == 0 || Completed(key) == 0 ||
                Completed(key + "/delivered") > 0) continue;

            if (entry.HasLostItem && Completed(key + "/found") == 0)
            {
                if (entry.location != Location.LocationName ||
                    !NpcShop.HitActionTile(pointer, standing, facing, entry.x, entry.y)) continue;
                Submit(entry, "find", () => NpcConversation.ShowNotice("Đã tìm thấy vật phẩm. " + entry.objective),
                    x: entry.x, y: entry.y);
                return true;
            }
            if (inventory.SelectedItem?.itemData?.itemId != entry.item) continue;
            npc ??= NpcConversation.FindClosest(pointer, standing, facing);
            if (npc?.DisplayName != entry.target) continue;
            Submit(entry, "deliver", () => ShowText(entry.target, entry.success), npc.DisplayName,
                inventory.SelectedIndex);
            return true;
        }
        return false;
    }

    private bool CanMeet(DailyEventDefinition entry) =>
        Location.LocationName == entry.location && entry.WithinHours(Clock.TimeOfDay) &&
        (!entry.clearWeather || !Clock.IsRainingToday) && IsAvailable(entry);

    private void Meet(DailyEventDefinition entry)
    {
        Submit(entry, "meet", () => ShowText(entry.actor, entry.body), x: entry.x, y: entry.y);
    }

    private void RefreshMarkers()
    {
        if (markerMap == Location && markerVersion == session.Snapshot.version &&
            markerTime == Clock.TimeOfDay && markerDay == Clock.Day) return;
        foreach (var marker in markers) UnityEngine.Object.Destroy(marker);
        markers.Clear();
        markerMap = Location;
        markerVersion = session.Snapshot.version;
        markerTime = Clock.TimeOfDay;
        markerDay = Clock.Day;

        foreach (var entry in calendar.Events)
        {
            if (!entry.HasTile || entry.location != markerMap.LocationName) continue;
            string key = entry.Key(Date);
            if (entry.HasLostItem && Completed(key) > 0 && Completed(key + "/found") == 0)
                PlaceMarker(entry.title, entry.x, entry.y, session.ResolveItem(entry.item)?.GetIcon());
            else if (entry.id == "junimo_scroll" && CanMeet(entry))
                PlaceMarker(entry.title, entry.x, entry.y, session.ResolveItem("object_770")?.GetIcon());
        }
        if (festival?.activity == "maze" && markerMap.LocationName == festival.map &&
            Completed(festival.Key(Date)) == 0)
            PlaceMarker("Rương cuối mê cung", festival.spotX, festival.spotY,
                session.ResolveItem("object_130")?.GetIcon());
    }

    private void PlaceMarker(string name, int x, int y, Sprite sprite)
    {
        if (sprite == null) return;
        var marker = new GameObject(name);
        marker.transform.SetParent(markerMap.transform, false);
        Vector3 tileCenter = markerMap.TileCenter(x, y);
        marker.transform.position = tileCenter;
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = WorldSortOrder.GetOrder(tileCenter.y, 0f, 10);
        markers.Add(marker);
    }

    private static readonly string[] BerrySeasons = { "salmonberry_season", "blackberry_season" };

    /// <summary>The berry season running today (spring 15-18, fall 8-11), or null.</summary>
    private DailyEventDefinition BerrySeason()
    {
        foreach (string id in BerrySeasons)
        {
            var entry = calendar.Find(id);
            if (entry != null && entry.IsDue(Date)) return entry;
        }
        return null;
    }

    private static bool IsBerryLocation(string location) =>
        location is "Farm" or "Forest" or "Backwoods" or "Mountain" or "BusStop" or "Railroad" or "Woods";

    private int bushDay = -1, bushVersion = -1;
    private GameLocation bushMap;
    private float nextBushRefresh;

    /// <summary>
    /// During a berry season each medium bush has a 50% chance a day to fill with
    /// berries, and keeps them until shaken. Out of season bushes just follow the season.
    /// </summary>
    private void RefreshBushes()
    {
        if (Location == null || Clock == null) return;
        int version = session.Snapshot?.version ?? -1;
        if (bushMap == Location && bushDay == Clock.Day && bushVersion == version && Time.unscaledTime < nextBushRefresh)
            return;
        bushMap = Location;
        bushDay = Clock.Day;
        bushVersion = version;
        nextBushRefresh = Time.unscaledTime + 2f;
        var season = IsBerryLocation(Location.LocationName) ? BerrySeason() : null;
        foreach (var bush in Location.GetComponentsInChildren<FarmBush>(true))
        {
            if (!bush.CanBearBerries) continue;
            bool berries = false;
            if (season != null)
            {
                Vector2Int tile = NpcPathfinder.WorldToTile(bush.transform.position);
                string key = $"{season.Key(Date)}/{Date.Day}/{Location.LocationName}/{tile.x}/{tile.y}";
                berries = Completed(key) == 0 && Bloomed(Location.LocationName, tile, season.day);
            }
            bush.Show(Date.SeasonIndex, berries);
        }
    }

    /// <summary>True once any day of the season so far rolled the 50% bloom.</summary>
    private bool Bloomed(string location, Vector2Int tile, int seasonStartDayOfYear)
    {
        int year = (Date.Day - 1) / 112;
        int first = year * 112 + seasonStartDayOfYear;
        for (int day = first; day <= Date.Day; day++)
        {
            uint hash = 2166136261u;
            foreach (char c in location) hash = (hash ^ c) * 16777619u;
            hash = (hash ^ (uint)tile.x) * 16777619u;
            hash = (hash ^ (uint)tile.y) * 16777619u;
            hash = (hash ^ (uint)day) * 16777619u;
            if ((hash >> 8) % 2 == 0) return true;
        }
        return false;
    }

    private bool TryForage(Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        var entry = BerrySeason();
        if (entry == null || !IsBerryLocation(Location.LocationName)) return false;
        foreach (var hit in Physics2D.OverlapCircleAll(standing, 1.7f))
        {
            if (!hit.TryGetComponent(out FarmBush bush) || !bush.HasBerries) continue;
            Vector2Int tile = NpcPathfinder.WorldToTile(bush.transform.position);
            if (!NpcShop.HitActionTile(pointer, standing, facing, tile.x, tile.y)) continue;
            string key = $"{entry.Key(Date)}/{Date.Day}/{Location.LocationName}/{tile.x}/{tile.y}";
            if (Completed(key) > 0) return true;
            // The bush sways at once; the berries pop out as debris once
            // the server has handed them over, and fly to the farmer.
            bush.Shake(standing.x > bush.transform.position.x);
            Submit(entry, "forage", () =>
            {
                bush.Show(Date.SeasonIndex, false);
                DropBerries(bush, entry.id == "blackberry_season" ? "object_410" : "object_296");
            }, x: tile.x, y: tile.y);
            return true;
        }
        return false;
    }

    /// <summary>1 + Foraging level / 4 berries, popping out around the bush.</summary>
    private void DropBerries(FarmBush bush, string berryId)
    {
        var online = farmer.GetComponent<OnlineGameState>();
        var snapshot = online?.Snapshot;
        if (online == null || snapshot == null) return;
        int foraging = snapshot.skillLevels != null && snapshot.skillLevels.Length > 2 ? snapshot.skillLevels[2] : 0;
        string id = snapshot.HasProfession(16) ? berryId + "@4" : berryId;
        ItemData item = online.ResolveItem(id) ?? online.ResolveItem(berryId);
        if (item == null) return;
        Vector3 center = bush.transform.position + new Vector3(0.5f, 0.6f, 0f);
        for (int i = 0, count = 1 + foraging / 4; i < count; i++)
            WorldItemDrop.Spawn(item, 1, center, UnityEngine.Random.insideUnitCircle.normalized + Vector2.up * 0.5f,
                alreadyGranted: true);
    }

    private void RefreshMailbox()
    {
        var mailbox = Location?.Mailbox;
        if (mailbox == null) return;
        if (!Ready)
        {
            mailbox.SetUnread(false);
            return;
        }
        var snapshot = session.Snapshot;
        if (mailboxSnapshot != snapshot || mailboxVersion != snapshot.version || mailboxDay != Clock.Day)
        {
            mailboxSnapshot = snapshot;
            mailboxVersion = snapshot.version;
            mailboxDay = Clock.Day;
            nextMail = calendar.FirstUnreadMail(Date, Completed);
        }
        mailbox.SetUnread(nextMail != null || HasRentBill);
    }

    private void OpenRentBill()
    {
        var online = farmer.GetComponent<OnlineGameState>();
        var snapshot = online?.Snapshot;
        if (snapshot == null || snapshot.rentDebt <= 0) return;
        long amount = Math.Min(snapshot.money, snapshot.rentDebt);
        string body = $"HÓA ĐƠN THUÊ TRẠI^^Số tiền cần thanh toán  {snapshot.rentDebt:N0}g" +
            $"^Vàng hiện có  {snapshot.money:N0}g^^Số nợ sẽ được tự động trừ vào sáng mai. Trả ngay " +
            "để tiếp tục cuốc ô mới, gieo hạt hoặc thuê thêm đất trên TV. Bạn vẫn có thể tưới và thu " +
            "hoạch cây đã trồng.";
        if (amount <= 0) body += "^^Bạn có thể mở lại thư này khi đã có vàng.";
        NpcConversation.ShowLetter(body, false, amount > 0 ? Pay : null,
            $"Trả {amount:N0}g", acceptOnClose: false);

        void Pay()
        {
            NpcConversation.Close();
            pending = true;
            online.PayBaseFarmRentDebt(error =>
            {
                pending = false;
                RefreshMailbox();
                if (error != null) NpcConversation.ShowNotice(error);
                else
                {
                    GameAudio.Play("purchase");
                    long remaining = online.Snapshot.rentDebt;
                    NpcConversation.ShowNotice(remaining > 0 ? $"Đã thanh toán. Hóa đơn còn {remaining:N0}g."
                        : "Đã thanh toán tiền thuê trại. Bạn có thể tiếp tục cuốc đất và gieo hạt.");
                }
            });
        }
    }

    internal void OpenJournal()
    {
        if (!Ready)
        {
            NpcConversation.ShowNotice("Đang tải lịch sự kiện.");
            return;
        }
        var options = new List<(string, Action)>();
        foreach (var entry in calendar.Events)
        {
            string key = entry.Key(Date);
            bool read = Completed(key) > 0;
            if (entry.kind == "mail" && read)
            {
                string label = entry.title;
                if (entry.item.Length > 0)
                    label += Completed(key + "/delivered") > 0 ? " ✓" : " · Đang làm";
                options.Add((label, () => OpenJournalLetter(entry)));
            }
            else if (entry.IsDue(Date) && entry.kind != "mail")
            {
                options.Add((entry.title, () => OpenEvent(entry)));
            }
            else if (entry.kind == "story" && entry.HasTile && IsAvailable(entry))
            {
                options.Add((entry.title, () => ShowText("", $"{entry.location} ({entry.x}, {entry.y})")));
            }
        }
        if (festival != null)
            options.Insert(0, ("Hoạt động · " + festival.title, () => OpenFestival(festival)));
        if (Completed("winter_shadow") > 0 && Completed("magnifying_glass") == 0)
            options.Add(("Nhiệm vụ 31 · Bí ẩn mùa đông", () => ShowText("", calendar.Find("winter_shadow").body)));
        if (options.Count == 0)
        {
            NpcConversation.ShowNotice("Hôm nay không có mục nào trong lịch.");
            return;
        }
        NpcConversation.ShowChoices($"{Date.Season} {Date.DayOfSeason} · Năm {Date.Year}", options);
    }

    private void OpenUnreadLetter(DailyEventDefinition entry)
    {
        bool wizardPaper = entry.id == "winter_12_1" || entry.id.Contains("wizard", StringComparison.OrdinalIgnoreCase);
        void ShowLetter(Action acceptQuest = null)
        {
            string body = entry.body.Replace("%secretsanta", Progress.secretRecipient)
                .Replace("@", StartupSession.Farmer?.name ?? "farmer");
            NpcConversation.ShowLetter(body, wizardPaper, acceptQuest);
        }

        if (Completed(entry.Key(Date)) > 0)
            ShowLetter();
        else if (entry.quest.Length > 0)
        {
            ShowLetter(() => Submit(entry, "read", () =>
            {
                RefreshMailbox();
                GameAudio.Play("newArtifact");
            }));
        }
        else Submit(entry, "read", () =>
        {
            RefreshMailbox();
            ShowLetter();
        });
    }

    private void OpenJournalLetter(DailyEventDefinition entry)
    {
        string key = entry.Key(Date);
        string body = entry.body.Replace("%secretsanta", Progress.secretRecipient);
        if (entry.objective.Length > 0 && Completed(key + "/delivered") == 0)
            body += "^^" + entry.objective;
        if (entry.HasLostItem && Completed(key + "/found") == 0)
            body += $"^{entry.location} ({entry.x}, {entry.y})";
        ShowText(entry.actor, body);
    }

    private void OpenEvent(DailyEventDefinition entry)
    {
        if (entry.kind != "festival")
        {
            ShowText(entry.actor, entry.body);
            return;
        }
        if (festival == entry)
        {
            OpenFestival(entry);
            return;
        }
        if (!entry.WithinHours(Clock.TimeOfDay) || Location?.LocationName != entry.location)
        {
            ShowText("", $"{entry.body}^{entry.location} · {entry.start / 100:00}:{entry.start % 100:00} – " +
                $"{entry.end / 100:00}:{entry.end % 100:00}");
            return;
        }
        Submit(entry, "visit", () =>
        {
            festivalReturn = NpcPathfinder.WorldToTile(farmer.StandingPosition);
            festival = entry;
            LocationManager.Instance.WarpFarmer(entry.map, entry.x, entry.y, Vector2.down);
        });
    }

    private void OpenFestival(DailyEventDefinition entry)
    {
        var options = new List<(string, Action)>();
        string key = entry.Key(Date);
        if (entry.activity == "market") key += "/night/" + Date.Day;
        if (Location?.LocationName == entry.map && Completed(key) == 0)
        {
            switch (entry.activity)
            {
                case "dance":
                    foreach (string candidate in entry.people.Skip(2))
                        options.Add(("Mời " + candidate, () => RunActivity(entry, candidate)));
                    break;
                case "exchange":
                    options.Add(("Trao món đang cầm cho " + Progress.secretRecipient,
                        () => RunActivity(entry, Progress.secretRecipient, inventory.SelectedIndex)));
                    break;
                case "soup":
                    options.Add(("Góp món đang cầm", () => RunActivity(entry, slot: inventory.SelectedIndex)));
                    break;
                case "grange":
                    options.Add(("Chọn 9 sản vật", () => OpenGrange(entry)));
                    break;
                case "maze":
                    options.Add(("Manh mối cuối mê cung", () => ShowText("Wizard", entry.body)));
                    break;
                case "market":
                    options.Add(("Ghé tàu ngầm", () => VisitMarketVenue("Submarine", 4, 7)));
                    options.Add(("Xem nàng tiên cá", () => VisitMarketVenue("MermaidHouse", 10, 7)));
                    if (Completed(entry.Key(Date) + "/coffee/" + Date.Day) == 0)
                        options.Add(("Coffee Guy · Nhận cà phê", () =>
                            Submit(entry, "coffee", () => ShowText("Coffee Guy", "Mời bạn một ly cà phê nóng."))));
                    if (Completed(entry.Key(Date) + "/painting/" + Date.Day) == 0)
                        options.Add(("Painter Lupini · Mua tranh đêm nay (1.200g)", () =>
                            Submit(entry, "painting", () => ShowText("Painter Lupini", "Cảm ơn bạn đã mua tranh."))));
                    break;
                case "egg":
                    if (huntEndsAt == 0f) options.Add(("Bắt đầu săn trứng", StartHunt));
                    break;
                case "ice":
                    options.Add(($"Câu cá xuyên băng · {iceFishCaught}/3", () => StartIceCatch(entry)));
                    break;
                case "jellies":
                    options.Add(("Thả thuyền nến", () => RunActivity(entry)));
                    break;
                default:
                    options.Add(("Tham gia · " + entry.title, () => RunActivity(entry)));
                    break;
            }
        }
        if (entry.activity == "egg" && Location?.LocationName == entry.map)
            options.Add(("Pierre · Mua hạt dâu tây (100g)", () =>
                Submit(entry, "strawberry", () => ShowText("Pierre", "Cảm ơn bạn đã mua hạt dâu tây."))));
        if (Location?.LocationName == "MermaidHouse" && Completed("night_market/pearl") == 0)
        {
            for (int note = 1; note <= 5; note++)
            {
                int shell = note;
                options.Add(($"Gõ vỏ sò {shell}", () => PlayShell(entry, shell)));
            }
        }
        if (Location?.LocationName is "Submarine" or "MermaidHouse")
            options.Add(("Về bến Chợ Đêm", () =>
                LocationManager.Instance.WarpFarmer(entry.map, entry.x, entry.y, Vector2.down)));
        else
            options.Add(("Rời lễ hội", () =>
                LocationManager.Instance.WarpFarmer(entry.location, festivalReturn.x, festivalReturn.y, Vector2.down)));
        NpcConversation.ShowChoices(entry.title, options);
    }

    private void OpenGrange(DailyEventDefinition entry)
    {
        var options = new List<(string, Action)>();
#if UNITY_EDITOR
        if (session is QaDailyEventSession)
        {
            string[] samples = { "Củ cải", "Khoai tây", "Súp lơ", "Dưa lưới", "Bí ngô", "Ớt cay", "Nấm", "Cá", "Mật ong" };
            for (int i = 0; i < samples.Length; i++) AddSlot(i, samples[i]);
        }
        else
#endif
        for (int i = 0; i < inventory.UnlockedSlots; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Data is ToolData || slot.Data.kind == PlayerItemKind.Seeds) continue;
            AddSlot(i, slot.Data.displayName);
        }
        if (displaySlots.Count == 9)
            options.Insert(0, ("Mời Lewis chấm", () => RunActivity(entry, slots: displaySlots.ToArray())));
        options.Add(("Quay lại", () => OpenFestival(entry)));
        NpcConversation.ShowChoices($"Gian hàng · {displaySlots.Count}/9", options);

        void AddSlot(int index, string name)
        {
            options.Add(($"{(displaySlots.Contains(index) ? "✓" : "+")} {name} · Ô {index + 1}", () =>
            {
                if (!displaySlots.Remove(index) && displaySlots.Count < 9) displaySlots.Add(index);
                OpenGrange(entry);
            }));
        }
    }

    private void VisitMarketVenue(string map, int x, int y) =>
        LocationManager.Instance.WarpFarmer(map, x, y, Vector2.down);

    private void PlayShell(DailyEventDefinition entry, int shell)
    {
        GameAudio.Play("clam_tone");
        shellNotes.Enqueue(shell);
        if (shellNotes.Count > 5) shellNotes.Dequeue();
        if (shellNotes.SequenceEqual(new[] { 1, 5, 4, 2, 3 }))
            Submit(entry, "pearl", () => ShowText("Mermaid", "Bạn đã giải được giai điệu và nhận một viên ngọc trai."),
                slots: shellNotes.ToArray());
        else
            OpenFestival(entry);
    }

    private void StartHunt()
    {
        var atlas = Resources.Load<Texture2D>("Maps/springobjects");
        eggSprite = ItemData.CreateAtlasSprite(atlas, 176);
        if (eggSprite == null) return;
        Vector2Int origin = NpcPathfinder.WorldToTile(farmer.StandingPosition);
        foreach (var tile in FestivalCast.OpenTiles(Location, origin).Where((_, index) => index % 2 == 0).Take(16))
        {
            var marker = new GameObject("Festival Egg");
            marker.transform.SetParent(Location.transform);
            marker.transform.position = Location.TileCenter(tile.x, tile.y);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = eggSprite;
            renderer.sortingOrder = WorldSortOrder.GetOrder(marker.transform.position.y);
            eggs.Add(tile, marker);
        }
        eggsCollected = 0;
        huntEndsAt = Time.unscaledTime + 50f;
    }

    private bool TryCollectEgg(Vector2? pointer, Vector2 standing, Vector2 facing)
    {
        Vector2Int? match = null;
        foreach (var tile in eggs.Keys)
        {
            if (!NpcShop.HitActionTile(pointer, standing, facing, tile.x, tile.y)) continue;
            match = tile;
            break;
        }
        if (!match.HasValue) return false;
        UnityEngine.Object.Destroy(eggs[match.Value]);
        eggs.Remove(match.Value);
        eggsCollected++;
        GameAudio.Play("coin");
        if (eggs.Count == 0) FinishHunt();
        return true;
    }

    private void ClearHunt()
    {
        foreach (GameObject egg in eggs.Values) UnityEngine.Object.Destroy(egg);
        eggSprite = null;
        eggs.Clear();
        huntEndsAt = 0f;
    }

    private void FinishHunt()
    {
        ClearHunt();
        string winner = eggsCollected >= 9 ? "Bạn thắng Abigail!" : "Abigail thắng cuộc.";
        Submit(festival, "activity", () => ShowText("Lewis", $"Bạn nhặt được {eggsCollected} trứng. {winner}"));
    }

    private void StartIceCatch(DailyEventDefinition entry)
    {
        fishing.StartFestivalCatch(caught =>
        {
            if (caught) iceFishCaught++;
            if (iceFishCaught >= 3)
                Submit(entry, "activity", () => ShowText("Lewis", "Bạn đã hoàn thành phần thi câu cá xuyên băng!"));
            else
                ShowText("Lewis", caught ? $"Bạn đã câu được {iceFishCaught}/3 cá." : "Cá đã thoát. Hãy thử lại.");
        });
    }

    private void TalkAtFestival(DailyEventDefinition entry, string name)
    {
        if (entry.activity == "dance" && entry.people.Skip(2).Contains(name))
        {
            NpcConversation.ShowChoices(name, new[] { ("Mời khiêu vũ", (Action)(() => RunActivity(entry, name))) });
            return;
        }
        if (entry.activity == "exchange" && name == Progress.secretRecipient)
        {
            NpcConversation.ShowChoices(name, new[]
            {
                ("Trao món đang cầm", (Action)(() => RunActivity(entry, name, inventory.SelectedIndex)))
            });
            return;
        }
        if (name == entry.people[0]) OpenFestival(entry);
        else if (name == "Pierre" && entry.activity == "egg")
            NpcConversation.ShowChoices("Pierre", new[]
            {
                ("Mua hạt dâu tây (100g)", (Action)(() =>
                    Submit(entry, "strawberry", () => ShowText("Pierre", "Cảm ơn bạn đã mua hạt dâu tây."))))
            });
        else ShowText(name, "Chúc bạn có một ngày lễ vui vẻ!");
    }

    private void RunActivity(DailyEventDefinition entry, string npc = null, int slot = -1,
        int x = 0, int y = 0, int[] slots = null)
    {
        Submit(entry, "activity", () =>
        {
            if (entry.activity == "jellies" && Location != null)
                FestivalJellyShow.StartAt(Location);
            string outcome = entry.activity switch
            {
                "dance" => npc + " đồng ý khiêu vũ cùng bạn.",
                "soup" => "Thống đốc nếm món súp chung và cảm ơn mọi người đã góp nguyên liệu.",
                "grange" => GrangeResult(Progress.fairScore),
                "maze" => "Bạn đã tìm thấy bí mật của mê cung!",
                "exchange" => $"{npc} nhận quà của bạn. Người tặng quà cho bạn là {Progress.secretGiver}.",
                _ => entry.body
            };
            ShowText("", outcome);
        }, npc, slot, x, y, slots);
    }

    private static string GrangeResult(int score) => score == -666
        ? "Lewis nhận ra chiếc quần đùi tím và loại gian hàng khỏi cuộc thi."
        : $"Gian hàng đạt {score} điểm, hạng {(score >= 90 ? 1 : score >= 75 ? 2 : score >= 60 ? 3 : 4)}.";

    private void Submit(DailyEventDefinition entry, string action, Action success, string npc = null,
        int slot = -1, int x = 0, int y = 0, int[] slots = null)
    {
        if (!Ready || pending) return;
        pending = true;
        farmer.SetMovement(Vector2.zero);
        session.Act(new StartupApiClient.DailyEventAction
        {
            day = Clock.Day, time = Clock.TimeOfDay, rain = Clock.IsRainingToday,
            eventId = entry.id, action = action, location = Location?.LocationName,
            npc = npc, slot = slot, x = x, y = y, slots = slots
        }, error =>
        {
            pending = false;
            if (string.IsNullOrEmpty(error)) success();
            else NpcConversation.ShowNotice(error);
        });
    }

    private static void ShowEarthquake(DailyEventDefinition entry)
    {
        GameAudio.Play("thunder");
        NpcConversation.ShowNotice(entry.body);
    }

#if UNITY_EDITOR
    internal void OpenPreview(DailyEventDefinition entry, Vector2Int returnTile)
    {
        switch (entry.kind)
        {
            case "mail": OpenUnreadLetter(entry); break;
            case "story" when !entry.HasTile: Meet(entry); break;
            case "story":
                RefreshMarkers();
                ShowText(entry.actor, $"{entry.body}^Tương tác tại ô ({entry.x}, {entry.y}) để kiểm tra.");
                break;
            case "festival":
                Submit(entry, "visit", () =>
                {
                    festivalReturn = returnTile;
                    festival = entry;
                    FestivalCast.Populate(Location, entry, calendar.Villagers, Progress,
                        farmer.StandingPosition, Date.Year);
                    RefreshMarkers();
                    OpenFestival(entry);
                });
                break;
            case "unlock" when entry.id == "earthquake": ShowEarthquake(entry); break;
            default: OpenEvent(entry); break;
        }
    }

    internal void ClearPreview()
    {
        Location?.Mailbox?.SetUnread(false);
        ClearHunt();
        foreach (var marker in markers)
            if (marker != null) UnityEngine.Object.Destroy(marker);
        markers.Clear();
    }

    internal void PreviewDelivery(DailyEventDefinition entry)
    {
        if (entry.kind == "mail" && !string.IsNullOrEmpty(entry.target))
            Submit(entry, "deliver", () => ShowText(entry.target, entry.success), entry.target);
    }
#endif

    private static void ShowText(string speaker, string message)
    {
        var pages = message.Replace("@", StartupSession.Farmer?.name ?? "farmer")
            .Split(new[] { "^^" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new NpcDialoguePage(part.Replace('^', '\n'), 0)).ToList();
        if (pages.Count > 0) NpcConversation.ShowPages(speaker, pages);
    }
}

internal interface IDailyEventSession
{
    OnlineGameSnapshot Snapshot { get; }
    ItemData ResolveItem(string id);
    void Act(StartupApiClient.DailyEventAction action, Action<string> done);
}

internal sealed class OnlineDailyEventSession : IDailyEventSession
{
    private readonly OnlineGameState online;
    internal OnlineDailyEventSession(OnlineGameState online) => this.online = online;
    public OnlineGameSnapshot Snapshot => online.Snapshot;
    public ItemData ResolveItem(string id) => online.ResolveItem(id);
    public void Act(StartupApiClient.DailyEventAction action, Action<string> done) => online.ActOnDailyEvent(action, done);
}
