using System.Text.Json;

public sealed record BundleRequest(Guid RequestId, int Bundle, int Slot);
public sealed record BundleIngredientStatus(string ItemId, int Amount, int Quality, bool Done);
public sealed record BundleStatus(int Index, string Name, int Color, int Required, string? RewardId, int RewardAmount,
    int Gold, BundleIngredientStatus[] Ingredients, bool Complete);
public sealed record BundleAreaStatus(int Area, string RoomId, string Name, BundleStatus[] Bundles, bool Complete);
public sealed record CommunityBundleStatus(BundleAreaStatus[] Areas, int[] PendingRewards);

/// <summary>
/// The Community Center bundles: each bundle lists ingredients (item, amount,
/// minimum quality), how many are required, a colour and a reward. Finishing every bundle of an
/// area restores that room.
/// </summary>
public static partial class GameStateEndpoints
{
    private sealed record BundleIngredient(string ItemId, int Amount, int Quality);
    private sealed record BundleDefinition(int Index, int Area, string Name, int Color, int Required,
        string? RewardId, int RewardAmount, int Gold, BundleIngredient[] Ingredients);

    private static readonly (string Key, string RoomId, string Name)[] BundleAreas =
    {
        ("Pantry", "pantry", "Nhà Kho"), ("Crafts Room", "crafts", "Phòng Thủ Công"), ("Fish Tank", "fish", "Bể Cá"),
        ("Boiler Room", "boiler", "Phòng Lò Hơi"), ("Vault", "vault", "Két Sắt"), ("Bulletin Board", "bulletin", "Bảng Thông Báo")
    };

    private static readonly Dictionary<string, string> BundleNames = new(StringComparer.Ordinal)
    {
        ["Spring Crops"] = "Nông sản mùa xuân", ["Summer Crops"] = "Nông sản mùa hè", ["Fall Crops"] = "Nông sản mùa thu",
        ["Quality Crops"] = "Nông sản chất lượng", ["Animal"] = "Vật nuôi", ["Artisan"] = "Thủ công",
        ["Spring Foraging"] = "Hái lượm mùa xuân", ["Summer Foraging"] = "Hái lượm mùa hè", ["Fall Foraging"] = "Hái lượm mùa thu",
        ["Winter Foraging"] = "Hái lượm mùa đông", ["Construction"] = "Xây dựng", ["Exotic Foraging"] = "Hái lượm hiếm",
        ["River Fish"] = "Cá sông", ["Lake Fish"] = "Cá hồ", ["Ocean Fish"] = "Cá biển", ["Night Fishing"] = "Câu đêm",
        ["Specialty Fish"] = "Cá đặc biệt", ["Crab Pot"] = "Lồng bẫy cua", ["Blacksmith's"] = "Thợ rèn",
        ["Geologist's"] = "Nhà địa chất", ["Adventurer's"] = "Nhà thám hiểm", ["Chef's"] = "Đầu bếp",
        ["Field Research"] = "Khảo sát thực địa", ["Enchanter's"] = "Phù thủy", ["Dye"] = "Thuốc nhuộm", ["Fodder"] = "Thức ăn gia súc"
    };

    private static readonly Lazy<BundleDefinition[]> BundleData = new(LoadBundles);

    private static BundleDefinition[] LoadBundles()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "Bundles.json");
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
        var result = new List<BundleDefinition>();
        foreach (var (key, value) in raw)
        {
            string[] keyParts = key.Split('/');
            int area = Array.FindIndex(BundleAreas, entry => entry.Key == keyParts[0]);
            if (area < 0 || keyParts.Length < 2 || !int.TryParse(keyParts[1], out int index)) continue;
            string[] fields = value.Split('/');
            if (fields.Length < 4) continue;
            string[] reward = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? rewardId = reward.Length >= 2 ? reward[0] switch
            {
                "BO" => "big_craftable_" + reward[1],
                _ => BundleItemId(reward[1])
            } : null;
            int rewardAmount = reward.Length >= 3 && int.TryParse(reward[2], out int amount) ? amount : 1;
            string[] tokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ingredients = new List<BundleIngredient>();
            int gold = 0;
            for (int i = 0; i + 2 < tokens.Length; i += 3)
            {
                if (tokens[i] == "-1") { gold = int.Parse(tokens[i + 1]); continue; }
                ingredients.Add(new(BundleItemId(tokens[i]), int.Parse(tokens[i + 1]), int.Parse(tokens[i + 2])));
            }
            int color = int.TryParse(fields[3], out int c) ? c : 0;
            int required = fields.Length > 4 && int.TryParse(fields[4], out int r) ? r : ingredients.Count;
            string name = BundleNames.TryGetValue(fields[0], out string? vietnamese) ? vietnamese : fields[0];
            result.Add(new(index, area, name, color, required, rewardId, rewardAmount, gold, ingredients.ToArray()));
        }
        return result.OrderBy(bundle => bundle.Area).ThenBy(bundle => bundle.Index).ToArray();
    }

    private static string BundleItemId(string token) => token switch
    {
        "24" => "parsnip", "188" => "green_bean", "190" => "cauliflower", "192" => "potato",
        "388" => "wood", "390" => "stone", "382" => "coal", "378" => "copper_ore", "380" => "iron_ore",
        "384" => "gold_ore", "771" => "fiber",
        _ => "object_" + token
    };

    /// <summary>Fish are kept as "fish_N" in the bag; bundles name them by object number.</summary>
    private static bool BundleItemMatches(string ingredientId, string stackId)
    {
        string baseId = RecipeIngredients.BaseId(stackId);
        if (baseId == ingredientId) return true;
        return ingredientId.StartsWith("object_", StringComparison.Ordinal) && baseId == "fish_" + ingredientId[7..];
    }

    private static void MapBundles(WebApplication app)
    {
        app.MapPost("/api/game/bundles/donate", async (HttpRequest request, BundleRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => DonateToBundle(doc, input)));
        app.MapPost("/api/game/bundles/purchase", async (HttpRequest request, BundleRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => PurchaseBundle(doc, input)));
        app.MapPost("/api/game/bundles/claim", async (HttpRequest request, BundleRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => ClaimBundleRewards(doc, catalog)));
    }

    private static string? BundleAccessError(StateDocument doc) =>
        IsJojaMember(doc) ? "Bạn đã là thành viên Joja; các Junimo không còn nhận đồ nữa." :
        !doc.Progress.DailyEvents.ContainsKey("community_center") ||
        !WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, "CommunityCenter")
            ? "Hãy đến bảng Junimo trong Nhà văn hóa." : null;

    private static bool AreaRestored(StateDocument doc, int area) =>
        doc.Progress.Milestones.Rooms.Contains(BundleAreas[area].RoomId);

    private static bool BundleComplete(StateDocument doc, BundleDefinition bundle) =>
        AreaRestored(doc, bundle.Area) || doc.Progress.BundlesComplete.Contains(bundle.Index);

    private static void FinishBundle(StateDocument doc, BundleDefinition bundle)
    {
        doc.Progress.BundlesComplete.Add(bundle.Index);
        if (bundle.RewardId != null) doc.Progress.BundleRewardsPending.Add(bundle.Index);
        if (BundleData.Value.Where(other => other.Area == bundle.Area).All(other => BundleComplete(doc, other)))
            doc.Progress.Milestones.Rooms.Add(BundleAreas[bundle.Area].RoomId);
    }

    private static string? DonateToBundle(StateDocument doc, BundleRequest input)
    {
        string? access = BundleAccessError(doc);
        if (access != null) return access;
        var bundle = BundleData.Value.FirstOrDefault(item => item.Index == input.Bundle);
        if (bundle == null || bundle.Gold > 0) return "Gói này không nhận vật phẩm.";
        if (BundleComplete(doc, bundle)) return "Gói này đã hoàn thành.";
        var stack = doc.Inventory.Find(s => s.Slot == input.Slot);
        if (stack == null || stack.Amount <= 0) return "Ô này đang trống.";
        var donated = doc.Progress.BundleDonations.TryGetValue(bundle.Index, out var list) ? list : new List<int>();
        int quality = RecipeIngredients.Quality(stack.ItemId);
        int match = -1;
        bool needsMore = false;
        for (int i = 0; i < bundle.Ingredients.Length; i++)
        {
            var ingredient = bundle.Ingredients[i];
            if (donated.Contains(i) || !BundleItemMatches(ingredient.ItemId, stack.ItemId)) continue;
            if (quality < ingredient.Quality) continue;
            if (stack.Amount < ingredient.Amount) { needsMore = true; continue; }
            match = i;
            break;
        }
        if (match < 0) return needsMore ? "Chưa đủ số lượng cho gói này." : "Gói này không cần vật phẩm đó.";
        stack.Amount -= bundle.Ingredients[match].Amount;
        if (stack.Amount == 0) doc.Inventory.Remove(stack);
        donated.Add(match);
        doc.Progress.BundleDonations[bundle.Index] = donated;
        if (donated.Count >= bundle.Required) FinishBundle(doc, bundle);
        return null;
    }

    private static string? PurchaseBundle(StateDocument doc, BundleRequest input)
    {
        string? access = BundleAccessError(doc);
        if (access != null) return access;
        var bundle = BundleData.Value.FirstOrDefault(item => item.Index == input.Bundle);
        if (bundle == null || bundle.Gold <= 0) return "Gói này không mua bằng tiền.";
        if (BundleComplete(doc, bundle)) return "Gói này đã hoàn thành.";
        if (doc.Money < bundle.Gold) return $"Bạn cần {bundle.Gold:N0}g.";
        doc.Money -= bundle.Gold;
        FinishBundle(doc, bundle);
        return null;
    }

    private static string? ClaimBundleRewards(StateDocument doc, IItemCatalog catalog)
    {
        var pending = doc.Progress.BundleRewardsPending;
        if (pending.Count == 0) return "Không có phần thưởng nào.";
        foreach (int index in pending.ToList())
        {
            var bundle = BundleData.Value.FirstOrDefault(item => item.Index == index);
            if (bundle?.RewardId == null || !catalog.TryGet(bundle.RewardId, out var item)) { pending.Remove(index); continue; }
            if (!CanAdd(doc.Inventory, item.Id, bundle.RewardAmount, item.MaxStack, doc.BagSize))
                return pending.Count == 0 ? null : "Túi đồ đã đầy.";
            Add(doc.Inventory, item.Id, bundle.RewardAmount, item.MaxStack, doc.BagSize);
            pending.Remove(index);
        }
        return null;
    }

    private static CommunityBundleStatus BundleStatusFor(StateDocument doc)
    {
        var areas = new List<BundleAreaStatus>();
        for (int area = 0; area < BundleAreas.Length; area++)
        {
            var bundles = BundleData.Value.Where(bundle => bundle.Area == area).Select(bundle =>
            {
                bool complete = BundleComplete(doc, bundle);
                var donated = doc.Progress.BundleDonations.GetValueOrDefault(bundle.Index) ?? new List<int>();
                return new BundleStatus(bundle.Index, bundle.Name, bundle.Color, bundle.Required, bundle.RewardId,
                    bundle.RewardAmount, bundle.Gold,
                    bundle.Ingredients.Select((ingredient, i) => new BundleIngredientStatus(ingredient.ItemId,
                        ingredient.Amount, ingredient.Quality, complete || donated.Contains(i))).ToArray(), complete);
            }).ToArray();
            areas.Add(new(area, BundleAreas[area].RoomId, BundleAreas[area].Name, bundles, AreaRestored(doc, area)));
        }
        return new(areas.ToArray(), doc.Progress.BundleRewardsPending.ToArray());
    }
}
