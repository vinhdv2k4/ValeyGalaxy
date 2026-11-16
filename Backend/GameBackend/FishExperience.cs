using System.Text.Json;

public static class FishExperience
{
    private static readonly Lazy<Dictionary<string, string>> FishData = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "Fish.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? new Dictionary<string, string>();
    });

    // FishingRod.cs awards max(1, (quality + 1) * 3 + difficulty / 3).
    // The current catch endpoint stores a normal-quality fish, so quality is zero.
    public static int ForNormalCatch(string itemId)
    {
        string id = itemId.StartsWith("fish_", StringComparison.Ordinal) ? itemId[5..] : itemId;
        if (!FishData.Value.TryGetValue(id, out string? data)) return 3;
        string[] fields = data.Split('/');
        return fields.Length > 1 && int.TryParse(fields[1], out int difficulty)
            ? Math.Max(1, 3 + difficulty / 3) : 3;
    }
}
