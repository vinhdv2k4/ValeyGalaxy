using System.Globalization;
using Microsoft.EntityFrameworkCore;

public sealed class MilestoneProgress
{
    public HashSet<string> Seen { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Rooms { get; set; } = new(StringComparer.Ordinal);
    public int FirstCopperDay { get; set; }
    public bool CopperHistoryChecked { get; set; }
    public string Pet { get; set; } = "";
    public string PetName { get; set; } = "";
    public string Cave { get; set; } = "";
    public int CaveHarvestDay { get; set; }
    public int Candles { get; set; }
}

public static partial class GameStateEndpoints
{
    private static void MapMilestones(WebApplication app) =>
        app.MapPost("/api/game/milestones", async (HttpRequest request, MilestoneRequest input,
            GameDb db, IItemCatalog items) =>
            await Change(request, db, input.RequestId, doc => ActOnMilestone(doc, input, db, items),
                includeFarmTileLimit: input.Action == "claim" && input.Id == "starter_gift"));

    private static string? ActOnMilestone(StateDocument doc, MilestoneRequest input, GameDb db, IItemCatalog items)
    {
        if (input.Day != doc.GameDay) return "Ngày trong game đã thay đổi.";
        var progress = doc.Progress.Milestones;
        if (input.Action == "donate") return DonateBundle(doc, input);
        if (input.Action == "harvest") return HarvestCave(doc, input, items);
        if (input.Action == "smelt") return SmeltMetal(doc, input, items);
        if (input.Action == "claim" && input.Id == "starter_gift")
            return ClaimStarterGift(doc, input, items);
        if (input.Action == "unlock_door") return UnlockRoomDoor(doc, input, db);
        if (input.Action == "fix_beach_bridge") return FixBeachBridge(doc);
        if (input.Action != "complete" || string.IsNullOrEmpty(input.Id)) return "Hành động không hợp lệ.";
        if (progress.Seen.Contains(input.Id)) return null;
        // The overnight cutaway has no reward and finishes during the private farmhouse warp.
        if (input.Id == "mine_landslide_clear")
        {
            if (input.Location != "FarmHouse" || input.Time != 600 ||
                doc.GameDay != EventCalendar.Value.Find("mine_open")!.day)
                return "Chưa đủ điều kiện hoàn thành cắt cảnh.";
            progress.Seen.Add(input.Id);
            return null;
        }
        // The private opening plays before world presence starts.
        if (input.Id == "60367" && doc.GameDay == 1 && input.Time == 600 && input.Location == "FarmHouse" &&
            db.Farmers.Any(farmer => farmer.UserId == doc.UserId && farmer.IntroMode != 0))
        {
            progress.Seen.Add(input.Id);
            return null;
        }
        if (string.IsNullOrEmpty(input.Location) || !WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, input.Location))
            return "Vị trí người chơi chưa đồng bộ.";
        if (input.Time is < 600 or >= 2600 || input.Time % 100 >= 60)
            return "Giờ trong game không hợp lệ.";

        bool morning = input.Location == "Farm" && input.Time < 1100;
        int weather = WeatherForDay(doc.UserId, doc.GameDay);
        bool wetWeather = input.Rain || DailyEvents.FarmWeather.IsRain(weather) || weather == DailyEvents.FarmWeather.Snow;
        switch (input.Id)
        {
            case "pet_adoption" when morning && !wetWeather && doc.TotalEarnings >= 1000:
                if (input.Choice is not ("dog" or "cat" or "decline")) return "Hãy chọn thú cưng.";
                if (input.Choice != "decline")
                {
                    string name = input.Name?.Trim() ?? "";
                    if (name.Length is < 1 or > 20 || name.Any(char.IsControl))
                        return "Tên thú cưng phải có từ 1 đến 20 ký tự.";
                    progress.Pet = input.Choice;
                    progress.PetName = name;
                    AdjustMilestoneFriendship(doc, db, "Marnie", 50);
                }
                break;
            case "farm_cave" when morning && doc.TotalEarnings >= 25_000:
                if (input.Choice is not ("bats" or "mushrooms")) return "Hãy chọn cách cải tạo hang.";
                progress.Cave = input.Choice;
                break;
            case "furnace_blueprint" when morning && progress.FirstCopperDay > 0 &&
                                            doc.GameDay > progress.FirstCopperDay:
                break;
            // The opening ceremony is held inside the restored center, so the weather does not matter.
            case "community_opening" when input.Location == "CommunityCenter" && !IsJojaMember(doc) &&
                                          input.Time is >= 800 and < 2200 && progress.Rooms.Count == 6:
                break;
            // Event 502261: Morris opens the Joja warehouse in front of the old center.
            case JojaOpeningId when input.Location == "Town" && IsJojaMember(doc) && AllJojaProjectsDone(doc) &&
                                    input.Time is >= 800 and < 2000:
                break;
            case "grandpa_evaluation" when input.Location == "Farm" && input.Time >= 2000 && doc.GameDay >= 225:
                progress.Candles = EvaluateGrandpa(doc);
                break;
            default:
                return "Chưa đủ điều kiện hoàn thành cắt cảnh.";
        }
        progress.Seen.Add(input.Id);
        return null;
    }

    private static string? ClaimStarterGift(StateDocument doc, MilestoneRequest input, IItemCatalog items)
    {
        const string giftId = "starter_gift";
        var seen = doc.Progress.Milestones.Seen;
        if (seen.Contains(giftId)) return null;
        // Riverland's FarmHouseStarterSeedsPosition is (4, 7).
        if (!seen.Contains("60367") || input.Location != "FarmHouse" ||
            !WorldSessionEndpoints.IsPlayerNearPosition(doc.UserId, "FarmHouse", 4.5f, -6.5f, 2.5f))
            return "Hãy đứng cạnh hộp quà trong nhà để mở.";
        var error = GrantEventItem(doc, items, "seeds_parsnip", 15);
        if (error == null) seen.Add(giftId);
        return error;
    }

    internal const string BeachBridgeFixed = "beachBridgeFixed";
    private const int BeachBridgeWood = 300;

    // The broken bridge east of Elliott's cabin is rebuilt with 300 wood.
    private static string? FixBeachBridge(StateDocument doc)
    {
        var seen = doc.Progress.Milestones.Seen;
        if (seen.Contains(BeachBridgeFixed)) return null;
        if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Beach", 58, 13, 3f))
            return "Hãy đến gần cây cầu gãy.";
        if (!TakeItems(doc.Inventory, "wood", BeachBridgeWood))
            return $"Cần {BeachBridgeWood} gỗ để sửa cầu.";
        seen.Add(BeachBridgeFixed);
        return null;
    }

    private static string? UnlockRoomDoor(StateDocument doc, MilestoneRequest input, GameDb db)
    {
        if (string.IsNullOrEmpty(input.Id) || !NpcGiftTastes.IsKnownNpc(input.Id))
            return "Không tìm thấy chủ phòng.";
        string flag = "doorUnlock" + input.Id;
        var seen = doc.Progress.Milestones.Seen;
        if (seen.Contains(flag)) return null;
        if (string.IsNullOrEmpty(input.Location) || !WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, input.Location))
            return "Vị trí người chơi chưa đồng bộ.";
        if (!db.NpcFriendships.Any(friend => friend.UserId == doc.UserId && friend.NpcName == input.Id &&
            friend.Points >= 500))
            return $"Bạn cần ít nhất 2 tim với {input.Id} để mở cửa phòng.";
        seen.Add(flag);
        return null;
    }

    private static void AdjustMilestoneFriendship(StateDocument doc, GameDb db, string npc, int delta)
    {
        var record = db.NpcFriendships.SingleOrDefault(row => row.UserId == doc.UserId && row.NpcName == npc);
        if (record == null)
        {
            record = new NpcFriendship { UserId = doc.UserId, NpcName = npc };
            db.NpcFriendships.Add(record);
        }
        record.Points = Math.Clamp(record.Points + delta, 0, 2500);
        doc.Progress.FriendshipPoints[npc] = record.Points;
    }

    private static int EvaluateGrandpa(StateDocument doc)
    {
        // Utility.getGrandpaScore and getGrandpaCandlesFromScore, using progress tracked in this project.
        int score = new[] { 50_000, 100_000, 200_000, 300_000, 500_000 }.Count(value => doc.TotalEarnings >= value);
        if (doc.TotalEarnings >= 1_000_000) score += 2;
        if (doc.Progress.ClaimedMineChests.Contains(120)) score++;
        if (doc.Progress.Milestones.Rooms.Count == 6) score++;
        if (doc.Progress.Milestones.Seen.Contains("community_opening")) score += 2;
        int friends = doc.Progress.FriendshipPoints.Values.Count(points => points >= 1975);
        if (friends >= 5) score++;
        if (friends >= 10) score++;
        int levels = doc.SkillExperience.Sum(GameProgression.LevelFromExperience) / 2;
        if (levels >= 15) score++;
        if (levels >= 25) score++;
        score += doc.Progress.Achievements.Count(id => id is 5 or 26 or 34);
        return score >= 12 ? 4 : score >= 8 ? 3 : score >= 4 ? 2 : 1;
    }

    // One curated bundle per room, matched to objects the Unity project can currently obtain.
    private static readonly BundleRoom[] Rooms =
    [
        new("crafts", "Phòng Thủ Công", 0, [new("wood", 99), new("stone", 99), new("object_709", 10)]),
        new("pantry", "Nhà Kho", 0, [new("parsnip", 1), new("green_bean", 1), new("cauliflower", 1), new("potato", 1)]),
        new("fish", "Bể Cá", 0, [new("fish_145", 1), new("fish_143", 1), new("fish_706", 1), new("fish_699", 1)]),
        new("boiler", "Phòng Lò Hơi", 0, [new("object_334", 1), new("object_335", 1), new("object_336", 1)]),
        new("vault", "Két Sắt", 42_500, []),
        new("bulletin", "Bảng Thông Báo", 0, [new("object_422", 1), new("fish_702", 1), new("object_536", 1)])
    ];

    private static string? DonateBundle(StateDocument doc, MilestoneRequest input)
    {
        if (IsJojaMember(doc)) return "Bạn đã là thành viên Joja; các Junimo không còn nhận đồ nữa.";
        if (input.Location != "CommunityCenter" || !doc.Progress.DailyEvents.ContainsKey("community_center") ||
            !WorldSessionEndpoints.IsPlayerNearPosition(doc.UserId, "CommunityCenter", 30.5f, -17.5f, 7f))
            return "Hãy đến bảng Bundles trong Community Center.";
        var room = Array.Find(Rooms, candidate => candidate.Id == input.Id);
        if (room == null) return "Phòng Bundles không hợp lệ.";
        if (doc.Progress.Milestones.Rooms.Contains(room.Id)) return null;
        if (doc.Money < room.Gold || room.Items.Any(item => CountMilestoneItem(doc, item.ItemId) < item.Amount))
            return "Chưa đủ vật phẩm hoặc tiền để hoàn thành phòng này.";
        doc.Money -= room.Gold;
        foreach (var item in room.Items) RemoveMilestoneItem(doc, item.ItemId, item.Amount);
        doc.Progress.Milestones.Rooms.Add(room.Id);
        return null;
    }

    private static string? HarvestCave(StateDocument doc, MilestoneRequest input, IItemCatalog items)
    {
        var progress = doc.Progress.Milestones;
        if (input.Location != "FarmCave" || progress.Cave.Length == 0 ||
            !WorldSessionEndpoints.IsPlayerNearPosition(doc.UserId, "FarmCave", 5.5f, -5.5f, 6f))
            return "Hãy vào hang đã cải tạo để thu hoạch.";
        if (progress.CaveHarvestDay == doc.GameDay) return null;
        string[] harvest = progress.Cave == "bats"
            ? ["object_410", "object_296", "object_406", "object_613"]
            : ["object_404", "object_420", "object_422", "object_257"];
        string? error = GrantEventItem(doc, items, harvest[(doc.GameDay - 1) % harvest.Length]);
        if (error == null) progress.CaveHarvestDay = doc.GameDay;
        return error;
    }

    private static string? SmeltMetal(StateDocument doc, MilestoneRequest input, IItemCatalog items)
    {
        if (!doc.Progress.Milestones.Seen.Contains("furnace_blueprint") ||
            CountMilestoneItem(doc, "big_craftable_13") < 1)
            return "Bạn cần chế tạo Lò nung trước.";
        string output = input.Id switch
        {
            "copper_ore" => "object_334", "iron_ore" => "object_335", "gold_ore" => "object_336", _ => ""
        };
        if (output.Length == 0 || CountMilestoneItem(doc, input.Id!) < 5 || CountMilestoneItem(doc, "coal") < 1)
            return "Cần 5 quặng cùng loại và 1 than.";
        RemoveMilestoneItem(doc, input.Id!, 5);
        RemoveMilestoneItem(doc, "coal", 1);
        return GrantEventItem(doc, items, output);
    }

    private static long CountMilestoneItem(StateDocument doc, string id) =>
        doc.Inventory.Where(stack => stack.ItemId == id).Sum(stack => (long)stack.Amount);

    private static void RemoveMilestoneItem(StateDocument doc, string id, int quantity)
    {
        foreach (var stack in doc.Inventory.Where(stack => stack.ItemId == id).ToArray())
        {
            int consumed = Math.Min(quantity, stack.Amount);
            stack.Amount -= consumed;
            quantity -= consumed;
            if (stack.Amount == 0) doc.Inventory.Remove(stack);
            if (quantity == 0) break;
        }
    }

    private static MilestoneStatus BuildMilestoneStatus(StateDocument doc)
    {
        var progress = doc.Progress.Milestones;
        return new(progress.Seen.OrderBy(id => id, StringComparer.Ordinal).ToArray(), progress.FirstCopperDay,
            progress.Pet, progress.PetName, progress.Cave, progress.CaveHarvestDay, progress.Candles,
            Rooms.Select(room => new BundleRoomStatus(room.Id, room.Title, room.Gold, room.Items,
                progress.Rooms.Contains(room.Id))).ToArray(),
            doc.Progress.FriendshipPoints.Select(pair => new MilestoneFriend(pair.Key, pair.Value)).ToArray());
    }

    private static async Task HydrateCopperHistory(GameDb db, StateDocument doc)
    {
        var milestones = doc.Progress.Milestones;
        if (milestones.CopperHistoryChecked) return;
        if (milestones.FirstCopperDay == 0)
        {
            var previous = await db.ResourceClaims.AsNoTracking()
                .Where(row => row.UserId == doc.UserId && row.ItemId == "copper_ore")
                .Select(row => new { row.Location, row.Day }).Distinct().ToListAsync();
            milestones.FirstCopperDay = previous
                .Where(row => int.TryParse(row.Location, NumberStyles.None, CultureInfo.InvariantCulture, out int floor) && floor > 0)
                .Select(row => int.TryParse(row.Day, NumberStyles.None, CultureInfo.InvariantCulture, out int day) ? day : 0)
                .Where(day => day > 0 && day <= doc.GameDay).DefaultIfEmpty().Min();
        }
        milestones.CopperHistoryChecked = true;
    }
}

public sealed record MilestoneRequest(Guid RequestId, string? Action, string? Id, int Day, int Time,
    string? Location, bool Rain = false, string? Choice = null, string? Name = null);
public sealed record BundleItem(string ItemId, int Amount);
public sealed record BundleRoom(string Id, string Title, int Gold, BundleItem[] Items);
public sealed record BundleRoomStatus(string Id, string Title, int Gold, BundleItem[] Items, bool Done);
public sealed record MilestoneFriend(string Npc, int Points);
public sealed record MilestoneStatus(string[] Seen, int FirstCopperDay, string Pet, string PetName, string Cave,
    int CaveHarvestDay, int Candles, BundleRoomStatus[] Rooms, MilestoneFriend[] Friends);
