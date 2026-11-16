using System.Collections.Generic;

/// <summary>Recipe ingredient categories ("-5" = any egg, ...), matching the server's rules.</summary>
internal static class RecipeCategories
{
    private static readonly HashSet<string> Eggs = new()
        { "object_176", "object_174", "object_180", "object_182", "object_442", "object_305", "object_107", "object_928" };
    private static readonly HashSet<string> Milk = new() { "object_184", "object_186", "object_436", "object_438" };
    private static readonly HashSet<string> WildSeeds = new() { "object_495", "object_496", "object_497", "object_498" };

    internal static bool IsCategory(string token) => token is "-4" or "-5" or "-6" or "-777";

    internal static bool Matches(string token, string baseItemId) => token switch
    {
        "-4" => baseItemId != null && baseItemId.StartsWith("fish_", System.StringComparison.Ordinal),
        "-5" => Eggs.Contains(baseItemId),
        "-6" => Milk.Contains(baseItemId),
        "-777" => WildSeeds.Contains(baseItemId),
        _ => false
    };

    internal static string Label(string token) => token switch
    {
        "-4" => "Cá bất kỳ",
        "-5" => "Trứng bất kỳ",
        "-6" => "Sữa bất kỳ",
        "-777" => "Hạt giống hoang dã",
        _ => token
    };
}
