using System.Globalization;

/// <summary>
/// Skill professions (LevelUpMenu: skill * 6 + 0/1 at level 5, + 2/3 or 4/5 at level 10) and the sale
/// bonuses they give (Object.getPriceAfterMultipliers).
/// </summary>
public static partial class GameStateEndpoints
{
    private static void MapProfessions(WebApplication app)
    {
        app.MapPost("/api/game/profession", async (HttpRequest request, ProfessionRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => ChooseProfession(doc, input)));
    }

    private static string? ChooseProfession(StateDocument doc, ProfessionRequest input)
    {
        if (input.Skill is < 0 or > 4 || input.Level is not (5 or 10)) return "Nghề không hợp lệ.";
        if (GameProgression.LevelFromExperience(doc.SkillExperience[input.Skill]) < input.Level)
            return "Bạn chưa đạt cấp này.";
        var chosen = doc.Progress.Professions;
        int first = input.Skill * 6;
        int[] options;
        if (input.Level == 5) options = [first, first + 1];
        else if (chosen.Contains(first)) options = [first + 2, first + 3];
        else if (chosen.Contains(first + 1)) options = [first + 4, first + 5];
        else return "Hãy chọn nghề cấp 5 trước.";
        // A retried or repeated choice for the same level is a no-op; a different one is refused.
        if (options.Any(chosen.Contains)) return chosen.Contains(input.Profession) ? null : "Bạn đã chọn nghề cho cấp này.";
        if (!options.Contains(input.Profession)) return "Nghề không hợp lệ.";
        chosen.Add(input.Profession);
        return null;
    }

    // Data/ObjectInformation categories the multipliers look at, by object index.
    private static readonly HashSet<int> Eggs = [107, 174, 176, 180, 182, 289, 305, 442, 928];
    private static readonly HashSet<int> Milk = [184, 186, 436, 438];
    private static readonly HashSet<int> AnimalGoods = [440, 444, 446];
    // Mayonnaise, cheese and cloth also count for the Rancher (matched by name in the original).
    private static readonly HashSet<int> RancherArtisan = [306, 307, 308, 807, 424, 426, 428];
    private static readonly HashSet<int> Vegetables = [20, 24, 78, 188, 190, 192, 248, 250, 256, 259, 262, 264, 266,
        270, 271, 272, 274, 276, 278, 280, 284, 300, 304, 830];
    private static readonly HashSet<int> Fruit = [252, 254, 258, 260, 268, 282, 398, 400, 454, 613, 634, 635, 636,
        637, 638, 832, 834];
    // Forage fruit is isSpawnedObject in the original and gets no Tiller bonus.
    private static readonly HashSet<int> Flowers = [376, 402, 418, 421, 591, 593, 595, 597];
    private static readonly HashSet<int> ArtisanGoods = [303, 306, 307, 308, 340, 342, 344, 346, 348, 350, 395, 424, 426,
        428, 432, 447, 459, 614, 807];
    private static readonly HashSet<int> Syrups = [724, 725, 726];
    private static readonly HashSet<int> Bars = [334, 335, 336, 337, 910];
    private static readonly HashSet<int> Gems = [60, 62, 64, 66, 68, 70, 72, 74, 80, 82, 84, 86];

    private static bool IsFishIndex(int index) =>
        index is >= 128 and <= 165 or 267 or 269 or 372 or >= 698 and <= 708 or >= 715 and <= 723 or 734 or 775 or
            >= 795 and <= 800 or >= 836 and <= 838;

    private static bool IsMineral(int index) => index is >= 538 and <= 578;

    /// <summary>The Data/ObjectInformation index behind an item id, or -1.</summary>
    internal static int ObjectIndex(string itemId, IItemCatalog catalog)
    {
        string id = itemId;
        int cut = id.IndexOfAny(['@', '#']);
        if (cut > 0) id = id[..cut];
        foreach (string prefix in new[] { "object_", "fish_" })
            if (id.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                return parsed;
        return catalog.TryGet(id, out var item) ? item.ObjectIndex : -1;
    }

    /// <summary>Object.getPriceAfterMultipliers for this farmer's professions.</summary>
    private static float ProfessionPriceMultiplier(StateDocument doc, string itemId, IItemCatalog catalog)
    {
        var professions = doc.Progress.Professions;
        if (professions.Count == 0) return 1f;
        int index = ObjectIndex(itemId, catalog);
        bool fish = IsFishIndex(index) || catalog.TryGet(itemId, out var item) && item.Category == "Fish";
        float multiplier = 1f;
        if (professions.Contains(0) && (Eggs.Contains(index) || Milk.Contains(index) || AnimalGoods.Contains(index) ||
                                        RancherArtisan.Contains(index)))
            multiplier *= 1.2f;
        if (professions.Contains(1) && (Vegetables.Contains(index) || Flowers.Contains(index) || Fruit.Contains(index)))
            multiplier *= 1.1f;
        if (professions.Contains(4) && ArtisanGoods.Contains(index)) multiplier *= 1.4f;
        if (professions.Contains(6) && fish) multiplier *= professions.Contains(8) ? 1.5f : 1.25f;
        if (professions.Contains(15) && Syrups.Contains(index)) multiplier *= 1.25f;
        if (professions.Contains(20) && Bars.Contains(index)) multiplier *= 1.5f;
        if (professions.Contains(23) && (Gems.Contains(index) || IsMineral(index))) multiplier *= 1.3f;
        return multiplier;
    }

    /// <summary>Sale price of one item after profession bonuses, truncated like the original.</summary>
    private static int SalePrice(StateDocument doc, ItemDefinition item, IItemCatalog catalog) =>
        (int)(item.SellPrice * ProfessionPriceMultiplier(doc, item.Id, catalog));
}

public sealed record ProfessionRequest(Guid RequestId, int Skill, int Level, int Profession);
