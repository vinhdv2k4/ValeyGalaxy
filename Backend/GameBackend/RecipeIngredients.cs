/// <summary>
/// Matches recipe ingredient tokens against inventory stacks. A positive token is one item in
/// any quality ("object_176", "object_176@2", ...); a negative token is a category such as
/// "any egg" or "any fish". The lowest quality is used up first.
/// </summary>
public static class RecipeIngredients
{
    private static readonly HashSet<string> Eggs = new(StringComparer.Ordinal)
        { "object_176", "object_174", "object_180", "object_182", "object_442", "object_305", "object_107", "object_928" };
    private static readonly HashSet<string> Milk = new(StringComparer.Ordinal)
        { "object_184", "object_186", "object_436", "object_438" };
    private static readonly HashSet<string> WildSeeds = new(StringComparer.Ordinal)
        { "object_495", "object_496", "object_497", "object_498" };

    public static string BaseId(string itemId)
    {
        int split = itemId.LastIndexOf('@');
        return split > 0 ? itemId[..split] : itemId;
    }

    public static int Quality(string itemId)
    {
        int split = itemId.LastIndexOf('@');
        return split > 0 && int.TryParse(itemId.AsSpan(split + 1), out int quality) ? quality : 0;
    }

    public static bool IsCategory(string token) => token is "-4" or "-5" or "-6" or "-777";

    public static bool Matches(string token, string itemId, Func<string, string> recipeItemId)
    {
        string id = BaseId(itemId);
        return token switch
        {
            "-4" => id.StartsWith("fish_", StringComparison.Ordinal),
            "-5" => Eggs.Contains(id),
            "-6" => Milk.Contains(id),
            "-777" => WildSeeds.Contains(id),
            _ => id == recipeItemId(token)
        };
    }

    /// <summary>Removes the ingredients from <paramref name="stacks"/>; false when something is missing.</summary>
    public static bool TryConsume(List<StackEntry> stacks, IReadOnlyDictionary<string, int> ingredients,
        Func<string, string> recipeItemId)
    {
        // Specific items first so a category never eats an item another ingredient needs.
        foreach (var (token, needed) in ingredients.OrderBy(pair => IsCategory(pair.Key) ? 1 : 0))
        {
            int remaining = needed;
            var matching = stacks.Where(stack => stack.Amount > 0 && Matches(token, stack.ItemId, recipeItemId))
                .OrderBy(stack => Quality(stack.ItemId)).ThenBy(stack => stack.Slot).ToList();
            foreach (var stack in matching)
            {
                int taken = Math.Min(remaining, stack.Amount);
                stack.Amount -= taken;
                remaining -= taken;
                if (stack.Amount == 0) stacks.Remove(stack);
                if (remaining == 0) break;
            }
            if (remaining > 0) return false;
        }
        return true;
    }
}
