using System;

internal static class SkillRecipeRequirement
{
    internal static bool TryRead(string rule, out int skill, out int level)
    {
        skill = -1;
        level = 0;
        if (rule == null || !rule.StartsWith("s ", StringComparison.Ordinal)) return false;
        string[] parts = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !int.TryParse(parts[2], out level) || level is < 1 or > 10) return false;
        skill = parts[1] switch
        {
            "Farming" => 0, "Fishing" => 1, "Foraging" => 2, "Mining" => 3, "Combat" => 4, _ => -1
        };
        return skill >= 0;
    }
}
