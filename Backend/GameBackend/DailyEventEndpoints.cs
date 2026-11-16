using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using DailyEvents;

public static partial class GameStateEndpoints
{
    internal static readonly Lazy<DailyEventCalendar> EventCalendar = new(() =>
        new DailyEventCalendar(JsonSerializer.Deserialize<DailyEventFile>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "DailyEvents.json")),
            new JsonSerializerOptions { IncludeFields = true }) ??
            throw new InvalidDataException("Daily event catalog is missing.")));

    private static void MapDailyEventEndpoints(WebApplication app)
    {
        _ = EventCalendar.Value;
        app.MapPost("/api/game/events/action", async (HttpRequest request, DailyEventRequest input,
            GameDb db, IItemCatalog items) =>
            await Change(request, db, input.RequestId, doc => ApplyDailyEvent(doc, input, items)));
    }

    private static string? ApplyDailyEvent(StateDocument doc, DailyEventRequest input, IItemCatalog items)
    {
        var calendar = EventCalendar.Value;
        var entry = calendar.Find(input.EventId);
        if (entry == null || doc.GameDay != input.Day) return "Sự kiện hoặc ngày hiện tại không hợp lệ.";

        var date = new FarmDate(doc.GameDay);
        var progress = doc.Progress.DailyEvents;
        string key = entry.Key(date);
        bool available = calendar.IsAvailable(entry, date, completed => progress.GetValueOrDefault(completed));
        string? error;

        switch (input.Action)
        {
            case "read" when entry.kind == "mail":
                if (progress.ContainsKey(key)) return null;
                if (!available) return "Thư chưa đến.";
                error = GrantEventItem(doc, items, entry.rewardItem);
                break;
            case "meet" or "cutscene" when entry.kind == "story" && (input.Action == "cutscene") == entry.cutscene:
                if (progress.ContainsKey(key)) return null;
                if (entry.cutscene && !WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, entry.location))
                    return "Vị trí người chơi chưa đồng bộ.";
                if (!available || input.Location != entry.location || !entry.WithinHours(input.Time) ||
                    entry.clearWeather && (input.Rain || FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay))) ||
                    entry.HasTile && (input.X != entry.x || input.Y != entry.y ||
                        !IsNearEventTile(doc.UserId, entry.location, entry.x, entry.y)))
                    return "Chưa đủ điều kiện gặp nhân vật.";
                error = entry.cutscene && doc.Inventory.Any(slot => slot.ItemId == entry.rewardItem)
                    ? null : GrantEventItem(doc, items, entry.rewardItem);
                break;
            case "find" when entry.HasLostItem:
                string found = key + "/found";
                if (progress.ContainsKey(found)) return null;
                if (!progress.ContainsKey(key) || input.Location != entry.location ||
                    input.X != entry.x || input.Y != entry.y || !IsNearEventTile(doc.UserId, entry.location, entry.x, entry.y))
                    return "Hãy đọc thư trước và tìm ở đúng địa điểm.";
                key = found;
                error = GrantEventItem(doc, items, entry.item);
                break;
            case "forage" when entry.kind == "forage":
                if (!entry.IsDue(date) ||
                    input.Location is not ("Farm" or "Forest" or "Backwoods" or "Mountain" or "BusStop" or "Railroad" or "Woods") ||
                    input.X is < 0 or > 200 || input.Y is < 0 or > 200 ||
                    !IsNearEventTile(doc.UserId, input.Location, input.X, input.Y))
                    return "Không có quả để hái ở đây.";
                string today = key + "/" + date.Day + "/";
                key = today + input.Location + "/" + input.X + "/" + input.Y;
                if (progress.ContainsKey(key)) return null;
                if (progress.Keys.Count(value => value.StartsWith(today, StringComparison.Ordinal)) >= 30)
                    return "Bạn đã hái hết phần quả dại hôm nay.";
                // Salmonberry in spring, Blackberry in fall; 1 + Foraging level / 4 berries,
                // iridium quality for the Botanist.
                string berry = entry.id == "blackberry_season" ? "object_410" : "object_296";
                if (doc.Progress.Professions.Contains(16)) berry += "@4";
                int foraging = GameProgression.LevelFromExperience(doc.SkillExperience[2]);
                error = GrantEventItem(doc, items, berry, 1 + foraging / 4);
                break;
            case "deliver" when entry.kind == "mail" && entry.item.Length > 0:
                string read = key;
                key += "/delivered";
                if (progress.ContainsKey(key)) return null;
                if (!progress.ContainsKey(read) || entry.HasLostItem && !progress.ContainsKey(read + "/found") ||
                    input.Npc != entry.target)
                    return "Nhiệm vụ chưa mở hoặc người nhận không đúng.";
                error = DeliverEventItem(doc, entry, input.Slot);
                break;
            case "brew" when entry.id == "summer_14_1":
                if (!progress.ContainsKey(key) || progress.ContainsKey(key + "/delivered") ||
                    input.Location != "Saloon")
                    return "Hãy đọc thư của Pam và mang hoa bia tới quán của Gus.";
                var hops = doc.Inventory.Find(stack => stack.Slot == input.Slot && stack.ItemId == "object_304" &&
                    stack.Amount > 0);
                if (hops == null) return "Hãy cầm hoa bia trên tay.";
                if (--hops.Amount == 0) doc.Inventory.Remove(hops);
                return GrantEventItem(doc, items, entry.item);
            case "visit" when entry.kind == "festival":
                if (!entry.IsDue(date) || !entry.WithinHours(input.Time) || input.Location != entry.location)
                    return "Lễ hội chưa mở cửa.";
                key += "/visit/" + date.Day;
                if (progress.ContainsKey(key)) return null;
                error = null;
                break;
            case "activity" when entry.kind == "festival":
                if (progress.ContainsKey(key)) return null;
                if (!entry.IsDue(date) || !progress.ContainsKey(key + "/visit/" + date.Day) ||
                    input.Location != entry.map)
                    return "Hãy vào lễ hội để tham gia.";
                if (entry.activity == "market") key += "/night/" + date.Day;
                if (progress.ContainsKey(key)) return null;
                error = CompleteFestivalActivity(doc, entry, input, items);
                break;
            case "coffee" or "painting" or "pearl" when entry.activity == "market":
                if (!entry.IsDue(date) || !progress.ContainsKey(key + "/visit/" + date.Day))
                    return "Chợ Đêm chưa mở cửa.";
                if (input.Action == "pearl")
                {
                    key = "night_market/pearl";
                    if (progress.ContainsKey(key)) return null;
                    if (input.Location != "MermaidHouse" || input.Slots == null ||
                        !input.Slots.SequenceEqual(new[] { 1, 5, 4, 2, 3 }))
                        return "Chuỗi nốt nhạc chưa đúng.";
                    error = GrantEventItem(doc, items, "object_797");
                    break;
                }
                if (input.Location != entry.map) return "Hãy ghé quầy ở bến Chợ Đêm.";
                key += "/" + input.Action + "/" + date.Day;
                if (progress.ContainsKey(key)) return null;
                bool painting = input.Action == "painting";
                if (painting && doc.Money < 1200) return "Cần 1.200g để mua tranh.";
                int paintingId = 1838 + (date.DayOfYear - entry.day) * 2 + (date.Year - 1) % 3 * 6;
                error = GrantEventItem(doc, items, painting ? "furniture_" + paintingId : "coffee");
                if (painting && error == null) doc.Money -= 1200;
                break;
            case "strawberry" when entry.activity == "egg":
                if (!entry.IsDue(date) || !progress.ContainsKey(key + "/visit/" + date.Day) ||
                    input.Location != entry.map)
                    return "Hãy đến quầy của Pierre tại Lễ hội Trứng.";
                if (doc.Money < 100) return "Hạt dâu tây có giá 100g.";
                error = GrantEventItem(doc, items, "object_745");
                if (error == null) doc.Money -= 100;
                return error;
            default:
                return "Hành động sự kiện không hợp lệ.";
        }
        if (error != null) return error;
        progress[key] = date.Day;
        return null;
    }

    private static string? GrantEventItem(StateDocument doc, IItemCatalog items, string id, int quantity = 1)
    {
        if (id.Length == 0) return null;
        if (!items.TryGet(id, out var item)) return "Vật phẩm sự kiện chưa được cấu hình.";
        if (!CanAdd(doc.Inventory, id, quantity, item.MaxStack, doc.BagSize)) return "Túi đồ đã đầy.";
        Add(doc.Inventory, id, quantity, item.MaxStack, doc.BagSize);
        return null;
    }

    private static string? DeliverEventItem(StateDocument doc, DailyEventDefinition entry, int slot)
    {
        if (!doc.Inventory.Any(stack => stack.Slot == slot && stack.ItemId == entry.item && stack.Amount > 0) ||
            doc.Inventory.Where(stack => stack.ItemId == entry.item).Sum(stack => (long)stack.Amount) < entry.quantity)
            return $"Cần chọn đúng vật phẩm và mang đủ {entry.quantity} món.";
        if (doc.Money > MoneyLimit - entry.gold) return "Đã đạt giới hạn tiền.";
        int remaining = entry.quantity;
        foreach (var stack in doc.Inventory.Where(stack => stack.ItemId == entry.item)
                     .OrderByDescending(stack => stack.Slot == slot).ToArray())
        {
            int consumed = Math.Min(remaining, stack.Amount);
            stack.Amount -= consumed;
            remaining -= consumed;
            if (stack.Amount == 0) doc.Inventory.Remove(stack);
            if (remaining == 0) break;
        }
        doc.Money += entry.gold;
        // Event rewards are earned gold too, so they must count toward the leaderboard.
        doc.TotalEarnings = Math.Min(long.MaxValue - entry.gold, doc.TotalEarnings) + entry.gold;
        return null;
    }

    private static string? CompleteFestivalActivity(StateDocument doc, DailyEventDefinition entry,
        DailyEventRequest input, IItemCatalog items)
    {
        switch (entry.activity)
        {
            case "dance":
                if (input.Npc == null || !entry.people.Skip(2).Contains(input.Npc))
                    return "Hãy chọn một ứng viên khiêu vũ.";
                return doc.Progress.FriendshipPoints.GetValueOrDefault(input.Npc) >= 1000
                    ? null : "Bạn cần đạt 4 tim với người này.";
            case "soup":
            case "exchange":
                return ContributeFestivalItem(doc, entry, input, items);
            case "grange":
                return JudgeGrange(doc, entry, input.Slots, items);
            case "maze":
                if (input.X != entry.spotX || input.Y != entry.spotY ||
                    !IsNearEventTile(doc.UserId, entry.map, entry.spotX, entry.spotY))
                    return "Hãy tìm rương cuối mê cung.";
                return GrantEventItem(doc, items, entry.rewardItem);
            case "egg":
            case "ice":
            case "jellies":
            case "market":
                return null;
            default:
                return "Hoạt động lễ hội chưa được cấu hình.";
        }
    }

    private static string? JudgeGrange(StateDocument doc, DailyEventDefinition entry, int[]? slots, IItemCatalog items)
    {
        const string invalid = "Hãy chọn 9 ô chứa sản vật để trưng bày.";
        if (slots == null || slots.Length != 9 || slots.Distinct().Count() != 9) return invalid;

        // Awards a base score, points by sell price and a diversity bonus.
        // Inventory stacks in this project store base-quality items.
        int score = 14 + slots.Length;
        var categories = new HashSet<string>(StringComparer.Ordinal);
        bool shorts = false;
        foreach (int slot in slots)
        {
            var stack = doc.Inventory.Find(stack => stack.Slot == slot && stack.Amount > 0);
            if (stack == null || !items.TryGet(stack.ItemId, out var item) ||
                item.IsTool || item.Category is "Weapon" or "Seeds" or "Furniture" or "BigCraftable")
                return invalid;
            shorts |= item.Id == "object_789";
            score += 1;
            foreach (int threshold in new[] { 20, 90, 200, 300, 400 })
                if (item.SellPrice >= threshold) score++;
            if (item.Category != "General") categories.Add(item.Category);
        }
        string key = entry.Key(new FarmDate(doc.GameDay));
        doc.Progress.FestivalScores[key] = shorts ? -666 : score + Math.Min(30, categories.Count * 5);
        return null;
    }

    private static bool IsNearEventTile(Guid userId, string location, int x, int y) =>
        WorldSessionEndpoints.IsPlayerNearPosition(userId, location, x + 0.5f, 0.5f - y, 2.8f);

    private static string? ContributeFestivalItem(StateDocument doc, DailyEventDefinition entry,
        DailyEventRequest input, IItemCatalog items)
    {
        var stack = doc.Inventory.Find(slot => slot.Slot == input.Slot && slot.Amount > 0);
        if (stack == null || !items.TryGet(stack.ItemId, out var item) || item.IsTool ||
            item.Category == "Weapon" || stack.ItemId is "object_788" or "object_789" or "object_790")
            return "Hãy chọn một vật phẩm phù hợp trong túi.";
        if (entry.activity == "soup" && item.Edibility <= 0) return "Chọn một nguyên liệu ăn được.";
        if (entry.activity == "exchange" && input.Npc != SecretVillagers(doc.UserId, new FarmDate(doc.GameDay)).Recipient)
            return "Đây không phải người bạn bí mật của bạn.";
        // Remove the gift first so its slot can hold the return gift in a full backpack.
        // Change discards this document if granting the return gift fails.
        if (--stack.Amount == 0) doc.Inventory.Remove(stack);
        return GrantEventItem(doc, items, entry.rewardItem);
    }

    private static (string Recipient, string Giver) SecretVillagers(Guid userId, FarmDate date)
    {
        Span<byte> seed = stackalloc byte[20];
        userId.TryWriteBytes(seed);
        BinaryPrimitives.WriteInt32LittleEndian(seed[16..], date.Year);
        byte[] hash = SHA256.HashData(seed);
        var villagers = EventCalendar.Value.Villagers;
        int recipient = (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)villagers.Count);
        int offset = 1 + (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(4)) % (uint)(villagers.Count - 1));
        return (villagers[recipient], villagers[(recipient + offset) % villagers.Count]);
    }

    private static DailyEventStatus DailyEventSnapshot(StateDocument doc)
    {
        var date = new FarmDate(doc.GameDay);
        var progress = doc.Progress.DailyEvents;
        var (recipient, giver) = SecretVillagers(doc.UserId, date);
        bool revealed = date.DayOfYear >= 109 ||
            progress.ContainsKey(EventCalendar.Value.Find("winter_18")!.Key(date));
        bool exchanged = progress.ContainsKey(EventCalendar.Value.Find("winter_star")!.Key(date));
        int fairScore = doc.Progress.FestivalScores.GetValueOrDefault("fair/" + date.Year);
        return new(progress.Select(pair => new DailyEventStamp(pair.Key, pair.Value)).ToArray(),
            revealed ? recipient : "", exchanged ? giver : "", fairScore);
    }
}

public sealed record DailyEventRequest(Guid RequestId, string? EventId, string? Action, int Day,
    string? Location = null, int Time = 600, bool Rain = false, string? Npc = null, int Slot = -1,
    int X = 0, int Y = 0, int[]? Slots = null);
public sealed record DailyEventStamp(string Key, int Day);
public sealed record DailyEventStatus(DailyEventStamp[] Completed, string SecretRecipient, string SecretGiver,
    int FairScore);
