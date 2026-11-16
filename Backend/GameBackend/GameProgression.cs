using System.Text.Json;

// Progress that the current server can verify from its own transactions.
public sealed class PlayerProgress
{
    public bool LegacyHydrated { get; set; }
    public HashSet<int> Achievements { get; set; } = new();
    public Dictionary<string, int> FishCaught { get; set; } = new();
    public HashSet<string> CraftedRecipes { get; set; } = new();
    public HashSet<string> LearnedShopRecipes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> BasicShipped { get; set; } = new();
    public Dictionary<string, int> FriendshipPoints { get; set; } = new();
    public HashSet<int> ClaimedMineChests { get; set; } = new();
    public int HouseUpgradeDay { get; set; }
    public int TrashCanLevel { get; set; }
    public Dictionary<int, List<int>> BundleDonations { get; set; } = new();
    public HashSet<int> BundlesComplete { get; set; } = new();
    public List<int> BundleRewardsPending { get; set; } = new();
    public ToolUpgradeOrder? ToolUpgrade { get; set; }
    public Dictionary<string, NaturalResourceState> NaturalResources { get; set; } = new();
    public Dictionary<int, FarmSoilTile> FarmSoil { get; set; } = new();
    public Dictionary<string, RanchConstruction> RanchBuildings { get; set; } = new();
    public List<RanchBuilding> RanchHomes { get; set; } = new();
    public List<RanchProduct> RanchProducts { get; set; } = new();
    public int PiecesOfHay { get; set; }
    public List<PlacedObject> PlacedObjects { get; set; } = new();
    public List<RanchAnimal> RanchAnimals { get; set; } = new();
    public Dictionary<string, int> DailyEvents { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> FestivalScores { get; set; } = new(StringComparer.Ordinal);
    public MilestoneProgress Milestones { get; set; } = new();
    // Weather set by an admin account for testing, by game day (FarmWeather values).
    public Dictionary<int, int> WeatherOverrides { get; set; } = new();
    // Farm rent taken automatically overnight, shown to the farmer when they wake up on that day.
    public RentCharge? LastRentCharge { get; set; }
    public MineKnockout? LastMineKnockout { get; set; }
    // LevelUpMenu professions, 0-29 (skill * 6 + choice).
    public HashSet<int> Professions { get; set; } = new();
    // Daily forage picked today: "day/location/x/y".
    public HashSet<string> ForagePicked { get; set; } = new(StringComparer.Ordinal);
    public PassOutCharge? LastPassOutCharge { get; set; }

    public static PlayerProgress FromJson(string? json)
    {
        PlayerProgress progress;
        try { progress = JsonSerializer.Deserialize<PlayerProgress>(json ?? "{}") ?? new(); }
        catch (JsonException) { progress = new(); }
        progress.Achievements ??= new();
        progress.FishCaught ??= new();
        progress.CraftedRecipes ??= new();
        progress.LearnedShopRecipes ??= new(StringComparer.Ordinal);
        progress.BasicShipped ??= new();
        progress.FriendshipPoints ??= new();
        progress.ClaimedMineChests ??= new();
        progress.NaturalResources ??= new();
        progress.FarmSoil ??= new();
        progress.RanchBuildings ??= new();
        progress.RanchAnimals ??= new();
        progress.RanchHomes ??= new();
        progress.RanchProducts ??= new();
        progress.PlacedObjects ??= new();
        progress.BundleDonations ??= new();
        progress.BundlesComplete ??= new();
        progress.BundleRewardsPending ??= new();
        MigrateRanch(progress);
        progress.DailyEvents ??= new(StringComparer.Ordinal);
        progress.FestivalScores ??= new(StringComparer.Ordinal);
        progress.Milestones ??= new();
        progress.WeatherOverrides ??= new();
        progress.Professions ??= new();
        progress.ForagePicked ??= new(StringComparer.Ordinal);
        progress.Milestones.Seen ??= new(StringComparer.Ordinal);
        progress.Milestones.Rooms ??= new(StringComparer.Ordinal);
        return progress;
    }

    private static readonly Guid LegacyCoop = new("00000000-0000-0000-0000-00000000c0b1");
    private static readonly Guid LegacyBarn = new("00000000-0000-0000-0000-00000000ba51");

    // Older saves kept one building per kind on a fixed plot.
    private static void MigrateRanch(PlayerProgress progress)
    {
        foreach (var (kind, old) in progress.RanchBuildings)
        {
            if (progress.RanchHomes.Any(home => home.Kind == kind)) continue;
            (int x, int y) = kind == "Barn" ? (69, 19) : (51, 18);
            progress.RanchHomes.Add(new(kind == "Barn" ? LegacyBarn : LegacyCoop, kind, x, y, old.Level, old.TargetLevel, old.ReadyDay));
        }
        progress.RanchBuildings.Clear();
        for (int i = 0; i < progress.RanchAnimals.Count; i++)
        {
            var animal = progress.RanchAnimals[i];
            if (animal.Home != Guid.Empty && progress.RanchHomes.Any(home => home.Id == animal.Home)) continue;
            var home = progress.RanchHomes.FirstOrDefault(home => home.Kind == animal.Kind);
            if (home != null) progress.RanchAnimals[i] = animal with { Home = home.Id };
        }
    }
}

public sealed record ToolUpgradeOrder(string ItemId, int ReadyDay);

public static class GameProgression
{
    // Stats.checkForShippingAchievements: each of these crops must be shipped 15 times.
    private static readonly string[] PolycultureCrops =
    {
        "24", "188", "190", "192", "248", "250", "252", "254", "256", "258",
        "260", "262", "264", "266", "268", "270", "272", "274", "276", "278",
        "280", "282", "284", "300", "304", "398", "400", "433"
    };
    private static readonly HashSet<string> MonocultureCrops = new(PolycultureCrops)
    { "454", "591", "593", "595", "597" };

    /// <summary>Farmer.checkForLevelGain: experience needed for levels 1-10.</summary>
    private static readonly int[] LevelThresholds = { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };

    public static int LevelFromExperience(int xp)
    {
        int level = 0;
        foreach (int threshold in LevelThresholds)
            if (xp >= threshold) level++;
        return level;
    }

    /// <summary>The least experience that reaches <paramref name="level"/> (0 for level 0).</summary>
    public static int ExperienceForLevel(int level) =>
        level <= 0 ? 0 : LevelThresholds[Math.Min(level, LevelThresholds.Length) - 1];

    public static void AddExperience(int[] experience, int skill, int amount)
    {
        // Farmer.gainExperience ignores luck (index 5) and nonpositive awards.
        if (skill is < 0 or >= 5 || amount <= 0 || experience.Length <= skill) return;
        experience[skill] = (int)Math.Min(int.MaxValue, (long)experience[skill] + amount);
    }

    public static void Evaluate(PlayerProgress progress, long totalEarnings)
    {
        void Award(int id) => progress.Achievements.Add(id);
        if (totalEarnings >= 15_000) Award(0);
        if (totalEarnings >= 50_000) Award(1);
        if (totalEarnings >= 250_000) Award(2);
        if (totalEarnings >= 1_000_000) Award(3);
        if (totalEarnings >= 10_000_000) Award(4);

        int friends5 = progress.FriendshipPoints.Values.Count(points => points >= 1250);
        int friends10 = progress.FriendshipPoints.Values.Count(points => points >= 2500);
        if (friends5 >= 1) Award(6);
        if (friends5 >= 4) Award(11);
        if (friends5 >= 10) Award(12);
        if (friends5 >= 20) Award(13);
        if (friends10 >= 1) Award(7);
        if (friends10 >= 8) Award(9);

        if (progress.CraftedRecipes.Count >= 15) Award(20);
        if (progress.CraftedRecipes.Count >= 30) Award(21);

        if (progress.FishCaught.Count >= 10) Award(24);
        if (progress.FishCaught.Count >= 24) Award(25);
        if (progress.FishCaught.Values.Sum() >= 100) Award(27);

        if (PolycultureCrops.All(id => progress.BasicShipped.GetValueOrDefault(id) >= 15)) Award(31);
        if (MonocultureCrops.Any(id => progress.BasicShipped.GetValueOrDefault(id) >= 300)) Award(32);
    }

    public static void RecordFish(PlayerProgress progress, string itemId)
    {
        string id = itemId.StartsWith("fish_", StringComparison.Ordinal) ? itemId[5..] : itemId;
        progress.FishCaught[id] = progress.FishCaught.GetValueOrDefault(id) + 1;
    }

    public static void RecordShipment(PlayerProgress progress, string itemId, int amount)
    {
        itemId = itemId.Split('@')[0];
        string id = itemId switch
        {
            "parsnip" => "24", "green_bean" => "188",
            "cauliflower" => "190", "potato" => "192",
            _ when itemId.StartsWith("object_", StringComparison.Ordinal) => itemId[7..],
            _ => itemId
        };
        progress.BasicShipped[id] = (int)Math.Min(int.MaxValue,
            (long)progress.BasicShipped.GetValueOrDefault(id) + amount);
    }
}
