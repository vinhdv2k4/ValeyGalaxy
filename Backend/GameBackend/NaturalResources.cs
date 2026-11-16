public static partial class GameStateEndpoints
{
    private static void MapNaturalResources(WebApplication app)
    {
        app.MapPost("/api/game/resources/clear", async (HttpRequest request,
            NaturalResourceRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (string.IsNullOrWhiteSpace(input.Location) || input.Location.Length > 64 ||
                    !input.Location.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or ' ') ||
                    int.TryParse(input.Location, out _) ||
                    input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512 ||
                    input.Kind is not ("tree" or "grass" or "weed") ||
                    input.Stump && input.Kind != "tree")
                    return "Invalid vegetation tile.";
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, input.Location, input.TileX, input.TileY, 4f))
                    return "Move closer to the resource.";

                string key = $"{input.Location}:{input.TileX}:{input.TileY}";
                var changes = doc.Progress.NaturalResources;
                var previous = changes.GetValueOrDefault(key);
                if (previous != null && previous.Kind != input.Kind)
                    return "Vegetation has changed. Re-enter the map.";
                bool mature = previous == null || !previous.Stump && doc.GameDay >= previous.RegrowDay;
                bool canClear = input.Kind != "tree" ? mature :
                    previous != null && (previous.Stump || doc.GameDay >= previous.SaplingDay);
                if (input.Stump ? !mature : !canClear)
                    return "Vegetation has changed. Re-enter the map.";

                int saplingDay = 0, regrowDay = 0;
                if (!input.Stump)
                {
                    regrowDay = NextGrowingDay(doc.GameDay, input.Kind switch { "grass" => 3, "weed" => 7, _ => 14 });
                    if (input.Kind == "tree") saplingDay = NextGrowingDay(doc.GameDay, 7);
                }
                changes[key] = new(input.Location, input.TileX, input.TileY, input.Kind,
                    input.Stump, saplingDay, regrowDay);
                // Cutting grass on the farm may send hay to the silos.
                if (input.Kind == "grass" && input.Location == "Farm" &&
                    doc.Progress.PiecesOfHay < HayCapacity(doc) &&
                    new Random(HashCode.Combine(doc.UserId, doc.GameDay, input.TileX, input.TileY)).NextDouble() < 0.5)
                    doc.Progress.PiecesOfHay++;
                return null;
            }));
    }

    private static int NextGrowingDay(int day, int remaining)
    {
        while (remaining > 0 && day < int.MaxValue)
        {
            day++;
            if ((day - 1) % 112 < 84) remaining--;
        }
        return day;
    }
}

public sealed record NaturalResourceRequest(Guid RequestId, string? Location, int TileX, int TileY,
    string? Kind, bool Stump);
public sealed record NaturalResourceState(string Location, int TileX, int TileY, string Kind,
    bool Stump, int SaplingDay, int RegrowDay);
