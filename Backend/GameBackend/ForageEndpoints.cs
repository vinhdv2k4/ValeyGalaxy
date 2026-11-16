using DailyEvents;

/// <summary>
/// Daily forage pickups: the spots come from the
/// shared ForageSpawns generator, so the server knows what lies where without trusting the client.
/// </summary>
public static partial class GameStateEndpoints
{
    internal static readonly Lazy<Dictionary<string, (int X, int Y)[]>> ForageTiles = new(() =>
        ForageSpawns.ParseTiles(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "ForageTiles.txt"))));

    private static void MapForage(WebApplication app)
    {
        app.MapPost("/api/game/forage/pick", async (HttpRequest request, ForagePickRequest input, GameDb db,
            IItemCatalog items) =>
            await Change(request, db, input.RequestId, doc => PickForage(doc, input, items)));
    }

    private static string? PickForage(StateDocument doc, ForagePickRequest input, IItemCatalog items)
    {
        string location = input.Location ?? "";
        string? itemId = ForageSpawns.At(ForageTiles.Value, location, doc.GameDay, input.X, input.Y);
        if (itemId == null) return "Không có gì để hái ở đây.";
        // Tile centres are (x + 0.5, 0.5 - y) in world units, as on the client.
        if (!WorldSessionEndpoints.IsPlayerNearPosition(doc.UserId, location, input.X + 0.5f, 0.5f - input.Y, 2.5f))
            return "Hãy đến gần hơn.";
        string prefix = doc.GameDay + "/";
        // Only today's pickups are kept.
        doc.Progress.ForagePicked.RemoveWhere(key => !key.StartsWith(prefix, StringComparison.Ordinal));
        string key = $"{prefix}{location}/{input.X}/{input.Y}";
        if (doc.Progress.ForagePicked.Contains(key)) return null;

        // Object forage quality: Botanist always iridium, else gold at Foraging / 30, silver at Foraging / 15.
        var random = new Random(HashCode.Combine(doc.UserId, doc.GameDay, input.X, input.Y));
        int foraging = GameProgression.LevelFromExperience(doc.SkillExperience[2]);
        int quality = doc.Progress.Professions.Contains(16) ? 4
            : random.NextDouble() < foraging / 30.0 ? 2
            : random.NextDouble() < foraging / 15.0 ? 1 : 0;
        // Gatherer: a 20% chance of a second one.
        int amount = doc.Progress.Professions.Contains(13) && random.NextDouble() < 0.2 ? 2 : 1;
        string? error = GrantEventItem(doc, items, WithQuality(itemId, quality), amount);
        if (error != null) return error;
        doc.Progress.ForagePicked.Add(key);
        GainExperience(doc, 2, 7);
        return null;
    }
}

public sealed record ForagePickRequest(Guid RequestId, string? Location, int X, int Y);
