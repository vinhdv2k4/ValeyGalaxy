using System.Globalization;

public static partial class GameStateEndpoints
{
    private sealed record RobinRecipe(string Id, string Name, int Price);
    private static readonly RobinRecipe[] Braziers =
    [
        new("recipe_big_craftable_143", "Wooden Brazier", 250), new("recipe_big_craftable_144", "Stone Brazier", 400),
        new("recipe_big_craftable_150", "Barrel Brazier", 800), new("recipe_big_craftable_147", "Stump Brazier", 800),
        new("recipe_big_craftable_145", "Gold Brazier", 1000), new("recipe_big_craftable_148", "Carved Brazier", 2000),
        new("recipe_big_craftable_149", "Skull Brazier", 3000), new("recipe_big_craftable_151", "Marble Brazier", 5000)
    ];
    private static readonly RobinRecipe[] OtherRobinRecipes =
    [
        new("recipe_big_craftable_152", "Wood Lamp-post", 500), new("recipe_big_craftable_153", "Iron Lamp-post", 1000),
        new("recipe_object_328", "Wood Floor", 100), new("recipe_object_840", "Rustic Plank Floor", 200),
        new("recipe_object_329", "Stone Floor", 100), new("recipe_object_293", "Brick Floor", 500),
        new("recipe_object_841", "Stone Walkway Floor", 200), new("recipe_object_415", "Stepping Stone Path", 100),
        new("recipe_object_401", "Straw Floor", 200), new("recipe_object_409", "Crystal Path", 200)
    ];
    private static readonly RobinRecipe[] RobinRecipes = [.. Braziers, .. OtherRobinRecipes];
    // The exclusions used by Utility.isFurnitureOffLimitsForSale within Robin's random ranges.
    private static readonly HashSet<int> UnlistedRobinFurniture =
    [
        131, 134, 984, 985, 986, 989, 1226, 1298, 1299, 1300, 1301, 1302, 1303, 1304, 1305, 1306,
        1307, 1308, 1309, 1371, 1373, 1375, 1402, 1466, 1468, 1471, 1541, 1545, 1554, 1669, 1671, 1680,
        1687, 1692, 1733, 1760, 1761, 1762, 1763, 1764, 1796, 1798, 1800, 1802
    ];

    private static bool KnowsRobinRecipe(PlayerProgress progress, string recipe) =>
        progress.LearnedShopRecipes.Contains(recipe) || progress.CraftedRecipes.Contains(recipe);

    private static Dictionary<string, int> CarpenterStock(StateDocument doc, IItemCatalog catalog)
    {
        int day = Math.Max(1, doc.GameDay);
        var stock = new Dictionary<string, int> { ["wood"] = day > 112 ? 50 : 10, ["stone"] = day > 112 ? 100 : 20 };
        foreach (int id in new[] { 1614, 1616, 1402, 1466, 1680, 1792, 1794, 1798 }) AddFurniture(id);
        stock["furniture_2048"] = 500;
        stock["big_craftable_208"] = 2000;
        stock["big_craftable_214"] = 2000;
        stock["big_craftable_200"] = 400;
        stock["big_craftable_35"] = 250;
        stock["big_craftable_46"] = 350;
        if (day > 84) stock["big_craftable_211"] = 1000;
        if (doc.Progress.HouseUpgradeDay > 0 && day >= doc.Progress.HouseUpgradeDay)
        {
            stock["big_craftable_216"] = 3000;
            AddFurniture(1468);
            AddFurniture(1226);
            stock["furniture_2052"] = 2000;
        }
        if (doc.Progress.Milestones.Seen.Contains("1053978")) AddFurniture(2186);

        int dayOfMonth = (day - 1) % 28 + 1;
        var random = new Random(unchecked(day + doc.UserId.GetHashCode() / 2));
        switch (dayOfMonth % 7)
        {
            case 0: RandomFurniture(1296, 1391); RandomFurniture(416, 537); break;
            case 1: foreach (int id in new[] { 0, 192, 704, 1120, 1216, 1391 }) AddFurniture(id); break;
            case 2: foreach (int id in new[] { 3, 197, 709, 1122, 1218, 1393 }) AddFurniture(id); break;
            case 3: foreach (int id in new[] { 6, 202, 714, 1124, 1220, 1395 }) AddFurniture(id); break;
            case 4: RandomFurniture(1296, 1391); RandomFurniture(1296, 1391); break;
            case 5: RandomFurniture(1443, 1450); RandomFurniture(288, 313); break;
            case 6: RandomFurniture(1565, 1607); RandomFurniture(12, 129); break;
        }
        RandomFurniture(0, 1462);
        RandomFurniture(0, 1462);
        while (random.NextDouble() < 0.25 && RandomFurniture(1673, 1815)) { }

        RobinRecipe? brazier = Braziers.FirstOrDefault(recipe => !KnowsRobinRecipe(doc.Progress, recipe.Name));
        if (brazier != null) stock[brazier.Id] = brazier.Price;
        foreach (var recipe in OtherRobinRecipes)
            if (!KnowsRobinRecipe(doc.Progress, recipe.Name)) stock[recipe.Id] = recipe.Price;
        return stock;

        void AddFurniture(int id)
        {
            string key = "furniture_" + id.ToString(CultureInfo.InvariantCulture);
            if (catalog.TryGet(key, out var item) && item.BuyPrice > 0) stock.TryAdd(key, item.BuyPrice);
        }

        bool RandomFurniture(int first, int end)
        {
            var candidates = Enumerable.Range(first, end - first).Where(id => !UnlistedRobinFurniture.Contains(id))
                .Select(id => "furniture_" + id.ToString(CultureInfo.InvariantCulture))
                .Where(key => !stock.ContainsKey(key) && catalog.TryGet(key, out var item) && item.BuyPrice > 0).ToArray();
            if (candidates.Length == 0) return false;
            string key = candidates[random.Next(candidates.Length)];
            if (catalog.TryGet(key, out var furniture)) stock.Add(key, furniture.BuyPrice);
            return true;
        }
    }

    private static string? BuyRobinRecipe(StateDocument doc, BuyRequest input, IItemCatalog catalog)
    {
        if (input.Amount != 1 || input.ItemId == null) return "Chỉ mua một bản vẽ mỗi lần.";
        var recipe = RobinRecipes.SingleOrDefault(recipe => recipe.Id == input.ItemId);
        if (recipe == null || !CarpenterStock(doc, catalog).TryGetValue(input.ItemId, out int price))
            return "Bản vẽ này chưa được bán hoặc đã học.";
        if (doc.Money < price) return "Không đủ vàng.";
        doc.Money -= price;
        doc.Progress.LearnedShopRecipes.Add(recipe.Name);
        return null;
    }
}
