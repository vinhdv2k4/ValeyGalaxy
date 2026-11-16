using Microsoft.EntityFrameworkCore;

public static class PresenceEndpoints
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);

    public static async Task EnsurePresenceTable(GameDb db)
    {
        await db.Database.ExecuteSqlRawAsync(db.Database.IsNpgsql()
            ? """CREATE TABLE IF NOT EXISTS "SessionPresences" ("SessionId" uuid NOT NULL PRIMARY KEY REFERENCES "Sessions" ("Id") ON DELETE CASCADE, "LastSeenUtc" timestamp with time zone NOT NULL)"""
            : """CREATE TABLE IF NOT EXISTS "SessionPresences" ("SessionId" TEXT NOT NULL PRIMARY KEY REFERENCES "Sessions" ("Id") ON DELETE CASCADE, "LastSeenUtc" TEXT NOT NULL)""");
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_SessionPresences_LastSeenUtc" ON "SessionPresences" ("LastSeenUtc")""");
    }

    public static void MapPresence(this WebApplication app)
    {
        app.MapPost("/api/presence/heartbeat", async (HttpRequest request, GameDb db) =>
        {
            var hash = AuthTokens.GetTokenHash(request);
            if (hash == null) return Results.Unauthorized();
            var now = DateTime.UtcNow;
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresUtc > now);
            if (session == null) return Results.Unauthorized();

            var presence = await db.SessionPresences.FindAsync(session.Id);
            if (presence == null)
                db.SessionPresences.Add(new SessionPresence { SessionId = session.Id, LastSeenUtc = now });
            else
                presence.LastSeenUtc = now;
            await db.SaveChangesAsync();

            return Results.Ok(new PresenceResponse("connected", now, session.ExpiresUtc,
                await CountOnlinePlayers(db, now)));
        });

        app.MapGet("/api/presence/online", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            var now = DateTime.UtcNow;
            return Results.Ok(new OnlinePlayersResponse(now, await CountOnlinePlayers(db, now)));
        });
    }

    private static Task<int> CountOnlinePlayers(GameDb db, DateTime now)
    {
        var cutoff = now - OnlineWindow;
        return db.SessionPresences
            .Where(p => p.LastSeenUtc >= cutoff && p.Session.ExpiresUtc > now)
            .Select(p => p.Session.UserId).Distinct().CountAsync();
    }
}

public record PresenceResponse(string Status, DateTime ServerTimeUtc, DateTime SessionExpiresUtc,
    int OnlinePlayers);
public record OnlinePlayersResponse(DateTime ServerTimeUtc, int OnlinePlayers);
