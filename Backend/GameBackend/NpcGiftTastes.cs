using System.Text.Json;

public static class NpcGiftTastes
{
    private static readonly Lazy<Dictionary<string, string>> Data = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ContentData", "NPCGiftTastes.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("NPC gift tastes are missing.");
    });

    public static bool IsKnownNpc(string name) => Data.Value.ContainsKey(name) &&
        !name.StartsWith("Universal_", StringComparison.Ordinal);

    public static int FriendshipPoints(string npcName, ItemDefinition item)
    {
        string baseId = item.Id.Split('@')[0];
        string token = baseId switch
        {
            "parsnip" => "24",
            "potato" => "192",
            "green_bean" => "188",
            "cauliflower" => "190",
            _ when baseId.StartsWith("object_", StringComparison.Ordinal) => baseId[7..],
            _ when baseId.StartsWith("fish_", StringComparison.Ordinal) => baseId[5..],
            _ => baseId
        };
        int taste = 8;
        if (Has("Universal_Love", token)) taste = 0;
        else if (Has("Universal_Hate", token)) taste = 6;
        else if (Has("Universal_Like", token)) taste = 2;
        else if (Has("Universal_Dislike", token)) taste = 4;
        else if (Has("Universal_Neutral", token)) taste = 8;

        string[] fields = Data.Value[npcName].Split('/');
        if (fields.Length >= 10)
        {
            if (Contains(fields[1], token)) taste = 0;
            else if (Contains(fields[7], token)) taste = 6;
            else if (Contains(fields[3], token)) taste = 2;
            else if (Contains(fields[5], token)) taste = 4;
            else if (Contains(fields[9], token)) taste = 8;
        }
        return taste switch { 0 => 80, 2 => 45, 4 => -20, 6 => -40, _ => 20 };
    }

    private static bool Has(string key, string token) =>
        Data.Value.TryGetValue(key, out string? values) && Contains(values, token);

    private static bool Contains(string values, string token) =>
        values.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(token, StringComparer.Ordinal);
}
