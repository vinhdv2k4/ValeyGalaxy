/// <summary>
/// The Joja route (Locations/JojaMart.signUpForJoja and Menus/JojaCDMenu): a 5,000g membership at
/// Morris's desk replaces the Junimo bundles with the Community Development form, whose five projects
/// each finish one room for gold. With all five bought, Morris's ceremony turns the center into a
/// Joja warehouse.
/// </summary>
public static partial class GameStateEndpoints
{
    internal const string JojaMemberFlag = "JojaMember";
    internal const string JojaOpeningId = "joja_opening";
    private const long JojaMembershipPrice = 5000;
    private const int JojaDeskX = 21, JojaDeskY = 25;

    // JojaCDMenu buttons 0-4: Bus (vault), Minecarts (boiler), Bridge (crafts), Greenhouse (pantry), Panning (fish).
    private static readonly (string Room, long Price)[] JojaProjects =
        [("vault", 40000), ("boiler", 15000), ("crafts", 25000), ("pantry", 35000), ("fish", 20000)];

    private static bool IsJojaMember(StateDocument doc) => doc.Progress.Milestones.Seen.Contains(JojaMemberFlag);

    private static bool AllJojaProjectsDone(StateDocument doc) =>
        JojaProjects.All(project => doc.Progress.Milestones.Rooms.Contains(project.Room));

    private static void MapJojaRoute(WebApplication app)
    {
        app.MapPost("/api/game/joja/join", async (HttpRequest request, JojaRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (IsJojaMember(doc)) return null;
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "JojaMart", JojaDeskX, JojaDeskY, 3f))
                    return "Hãy đến quầy của Morris trong JojaMart.";
                if (doc.Progress.Milestones.Seen.Contains("community_opening"))
                    return "Trung tâm Cộng đồng đã được phục hồi.";
                if (doc.Money < JojaMembershipPrice) return "Bạn không đủ 5.000g.";
                doc.Money -= JojaMembershipPrice;
                doc.Progress.Milestones.Seen.Add(JojaMemberFlag);
                return null;
            }));

        app.MapPost("/api/game/joja/project", async (HttpRequest request, JojaRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc =>
            {
                if (!IsJojaMember(doc)) return "Chỉ thành viên Joja mới dùng được phiếu này.";
                if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "JojaMart", JojaDeskX, JojaDeskY, 3f))
                    return "Hãy đến quầy của Morris trong JojaMart.";
                if (input.Project is < 0 or >= 5) return "Dự án không hợp lệ.";
                var (room, price) = JojaProjects[input.Project];
                if (doc.Progress.Milestones.Rooms.Contains(room)) return null;
                if (doc.Money < price) return $"Bạn không đủ {price:N0}g.";
                doc.Money -= price;
                doc.Progress.Milestones.Rooms.Add(room);
                // Marks the room as done by Joja, so the Junimos never come to restore it.
                doc.Progress.Milestones.Seen.Add("joja_" + room);
                return null;
            }));
    }
}

public sealed record JojaRequest(Guid RequestId, int Project = -1);
