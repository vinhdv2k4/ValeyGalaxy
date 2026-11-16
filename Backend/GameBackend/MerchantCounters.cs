using System.Text.Json;

public static partial class GameStateEndpoints
{
    private static readonly Lazy<Dictionary<string, string>> GuildBoots = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "ContentData", "Boots.json"))) ?? new());

    private static bool AtMerchantCounter(Guid player, string shop) => shop switch
    {
        "Marlon" => WorldSessionEndpoints.IsPlayerNear(player, "AdventureGuild", 5, 12, 2.5f),
        "Robin" => WorldSessionEndpoints.IsPlayerNear(player, "ScienceHouse", 8, 19, 2.5f),
        "Marnie" => WorldSessionEndpoints.IsPlayerNear(player, "AnimalShop", 12, 15, 3f),
        _ => false
    };

    private static bool IsGuildRing(string id) => id is "object_810" or "object_811" ||
        id.StartsWith("object_", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out int number) &&
        number is >= 516 and <= 534;

    private static bool IsGuildGear(string id) => IsGuildRing(id) ||
        id.StartsWith("weapon_", StringComparison.Ordinal) || id.StartsWith("boot_", StringComparison.Ordinal);

    private static int GuildResalePrice(ItemDefinition item)
    {
        if (IsGuildRing(item.Id)) return item.SellPrice;
        if (item.Id.StartsWith("weapon_", StringComparison.Ordinal))
        {
            var stock = GuildStock.Value;
            if (stock.Resale?.TryGetValue(item.Id, out int price) == true) return price;
            return stock.Offers.FirstOrDefault(offer => offer.ItemId == item.Id)?.SellPrice ?? 0;
        }
        if (!item.Id.StartsWith("boot_", StringComparison.Ordinal) ||
            !GuildBoots.Value.TryGetValue(item.Id[5..], out string? raw)) return 0;
        string[] fields = raw.Split('/');
        return fields.Length >= 5 && int.TryParse(fields[3], out int defense) && int.TryParse(fields[4], out int immunity)
            ? (defense + immunity) * 50 : 0;
    }
}
