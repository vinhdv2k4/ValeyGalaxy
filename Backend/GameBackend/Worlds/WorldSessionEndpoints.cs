using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// World session broker: allows players to automatically connect upon logging in
/// without needing any manual room code entry, and synchronizes real-time positions
/// and appearances of all online players in the world.
/// </summary>
public static partial class WorldSessionEndpoints
{
    private static readonly object SyncLock = new();
    private static readonly ConcurrentDictionary<Guid, WorldPlayerPresence> ActivePlayers = new();
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan HostHeartbeatTimeout = TimeSpan.FromSeconds(30);
    private static bool ExternalHostRegistered;

    public static bool IsPlayerNear(Guid userId, string location, int tileX, int tileY, float radius) =>
        IsPlayerNearPosition(userId, location, tileX + 0.5f, -tileY - 0.5f, radius);

    public static bool IsPlayerAtLocation(Guid userId, string location) =>
        ActivePlayers.TryGetValue(userId, out var presence) &&
        presence.LastSeenUtc >= DateTime.UtcNow.AddSeconds(-8) &&
        string.Equals(presence.Location, location, StringComparison.Ordinal);

    public static bool IsPlayerNearPosition(Guid userId, string location, float x, float y, float radius)
    {
        if (!ActivePlayers.TryGetValue(userId, out var presence) ||
            presence.LastSeenUtc < DateTime.UtcNow.AddSeconds(-8) ||
            !string.Equals(presence.Location, location, StringComparison.Ordinal)) return false;
        float dx = presence.X - x;
        float dy = presence.Y - y;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static WorldSessionInfo CurrentSession = new(
        IsActive: true,
        RelayJoinCode: null,
        ServerHost: "127.0.0.1",
        ServerPort: 7777,
        ServerName: "Thế giới Nông Trại Chung",
        MaxPlayers: 16,
        LastUpdatedUtc: DateTime.UtcNow
    );

    public static void MapWorldSession(this WebApplication app)
    {
        // ── Client: Tự động lấy thông tin kết nối máy chủ thế giới ─────────
        app.MapGet("/api/world/session", () =>
        {
            lock (SyncLock)
            {
                var isAlive = CurrentSession.IsActive &&
                    (!ExternalHostRegistered || DateTime.UtcNow - CurrentSession.LastUpdatedUtc <= HostHeartbeatTimeout);
                return Results.Ok(CurrentSession with { IsActive = isAlive });
            }
        });

        // ── Host/Server: Cập nhật thông tin phòng thế giới nếu dùng Relay hoặc Custom Host ────
        app.MapPost("/api/world/host-heartbeat", (HostHeartbeatRequest request, IConfiguration configuration) =>
        {
            var configuredSecret = configuration["WORLD_HOST_SECRET"];
            if (string.IsNullOrWhiteSpace(configuredSecret))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var expected = Encoding.UTF8.GetBytes(configuredSecret);
            var supplied = Encoding.UTF8.GetBytes(request.SecretKey ?? "");
            if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
            {
                return Results.Unauthorized();
            }

            lock (SyncLock)
            {
                CurrentSession = new WorldSessionInfo(
                    IsActive: request.IsActive,
                    RelayJoinCode: request.RelayJoinCode?.Trim(),
                    ServerHost: request.ServerHost?.Trim() ?? "127.0.0.1",
                    ServerPort: request.ServerPort > 0 ? request.ServerPort : 7777,
                    ServerName: string.IsNullOrWhiteSpace(request.ServerName) ? "Thế giới Nông Trại Chung" : request.ServerName.Trim(),
                    MaxPlayers: request.MaxPlayers > 0 ? request.MaxPlayers : 16,
                    LastUpdatedUtc: DateTime.UtcNow
                );
                ExternalHostRegistered = true;
            }

            return Results.Ok(new { status = "registered", timestamp = DateTime.UtcNow });
        });

        // ── Client: Đồng bộ vị trí, hướng nhìn, khu vực và ngoại hình thời gian thực ─────
        app.MapPost("/api/world/presence/sync", async (HttpRequest request, WorldPlayerSyncRequest payload, GameDb db) =>
        {
            var user = await AuthTokens.ResolveUser(request, db);
            if (user == null) return Results.Unauthorized();

            var location = payload.Location?.Trim();
            if (string.IsNullOrWhiteSpace(location) || location.Length > 64 ||
                !location.All(c => char.IsLetterOrDigit(c) || c is '_' or '-') ||
                !float.IsFinite(payload.X) || !float.IsFinite(payload.Y) ||
                !float.IsFinite(payload.FacingX) || !float.IsFinite(payload.FacingY) ||
                MathF.Abs(payload.X) > 10_000 || MathF.Abs(payload.Y) > 10_000 ||
                !float.IsFinite(payload.ActionFacingX) || !float.IsFinite(payload.ActionFacingY) ||
                payload.ActionSeq < 0 ||
                (payload.HeldItemId is { } held && (held.Length > 80 || held.Any(char.IsControl))) ||
                (payload.ActionName is { } action && (action.Length > 32 || !action.All(char.IsLetter))))
                return Results.BadRequest(new ApiError("Invalid world presence."));

            var now = DateTime.UtcNow;
            var playerPresence = new WorldPlayerPresence(
                UserId: user.Id,
                Username: user.Username,
                X: payload.X,
                Y: payload.Y,
                FacingX: payload.FacingX,
                FacingY: payload.FacingY,
                Location: location,
                Farmer: user.Farmer?.ToDto(),
                LastSeenUtc: now
            )
            {
                InCutscene = payload.InCutscene,
                // Farmer.CurrentTool / ActiveObject and the fireTool-style action counter: other
                // clients replay each new action once (NetEvent semantics).
                HeldItemId = string.IsNullOrEmpty(payload.HeldItemId) ? null : payload.HeldItemId,
                ActionSeq = payload.ActionSeq,
                ActionName = string.IsNullOrEmpty(payload.ActionName) ? null : payload.ActionName,
                ActionFacingX = payload.ActionFacingX,
                ActionFacingY = payload.ActionFacingY
            };

            List<WorldPlayerPresence> otherPlayers;
            FarmerGiftView? incomingGift, outgoingGift;
            lock (SyncLock)
            {
                if (!CurrentSession.IsActive ||
                    (ExternalHostRegistered && now - CurrentSession.LastUpdatedUtc > HostHeartbeatTimeout))
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                // Admission and snapshot must be atomic so concurrent joins cannot exceed capacity.
                var cutoff = now - InactivityTimeout;
                foreach (var kvp in ActivePlayers)
                {
                    if (kvp.Value.LastSeenUtc < cutoff)
                        ActivePlayers.TryRemove(kvp.Key, out _);
                }

                if (!ActivePlayers.ContainsKey(user.Id) && ActivePlayers.Count >= CurrentSession.MaxPlayers)
                    return Results.Conflict(new ApiError("World is full."));

                ActivePlayers[user.Id] = playerPresence;
                // Each farm and farm cave belongs to the signed-in farmer. Their
                // contents are private even though every client names them alike.
                otherPlayers = IsPrivateLocation(location) ? new List<WorldPlayerPresence>() :
                    // Cutscenes play for one farmer only: someone watching one is hidden from
                    // everyone else (their spot is still kept for position checks).
                    ActivePlayers.Values.Where(p => p.UserId != user.Id && !p.InCutscene &&
                        string.Equals(p.Location, location, StringComparison.OrdinalIgnoreCase)).ToList();
                (incomingGift, outgoingGift) = TakeGiftViews(user.Id, now);
            }

            return Results.Ok(new WorldSyncResponse(now, otherPlayers)
            {
                IncomingGift = incomingGift,
                OutgoingGift = outgoingGift
            });
        });

        // ── Client: Báo rời game để xóa ngay lập tức clone của nhân vật khỏi thế giới ───
        app.MapPost("/api/world/presence/leave", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId != null)
            {
                ActivePlayers.TryRemove(userId.Value, out _);
            }
            return Results.Ok(new { status = "left" });
        });

        app.MapFarmerGifts();
    }

    private static bool IsAnimalInterior(string location) =>
        location.StartsWith("Coop", StringComparison.OrdinalIgnoreCase) ||
        location.StartsWith("Barn", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateLocation(string location) =>
        IsAnimalInterior(location) ||
        string.Equals(location, "Farm", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, "FarmHouse", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, "FarmCave", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, "Mine", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, "SkullCave", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, "SkullCaveAltar", StringComparison.OrdinalIgnoreCase) ||
        (int.TryParse(location, out var floor) && floor > 0);
}

public record WorldSessionInfo(
    bool IsActive,
    string? RelayJoinCode,
    string ServerHost,
    int ServerPort,
    string ServerName,
    int MaxPlayers,
    DateTime LastUpdatedUtc
);

public record HostHeartbeatRequest(
    string? SecretKey,
    bool IsActive,
    string? RelayJoinCode,
    string? ServerHost,
    int ServerPort,
    string? ServerName,
    int MaxPlayers
);

public record WorldPlayerSyncRequest(
    float X,
    float Y,
    float FacingX,
    float FacingY,
    string? Location,
    bool InCutscene = false,
    string? HeldItemId = null,
    int ActionSeq = 0,
    string? ActionName = null,
    float ActionFacingX = 0f,
    float ActionFacingY = 0f
);

public record WorldPlayerPresence(
    Guid UserId,
    string Username,
    float X,
    float Y,
    float FacingX,
    float FacingY,
    string Location,
    FarmerDto? Farmer,
    DateTime LastSeenUtc
)
{
    /// <summary>Watching their own cutscene; not shown to other players.</summary>
    public bool InCutscene { get; init; }
    /// <summary>Item in the farmer's hands, so others see the tool or object they hold.</summary>
    public string? HeldItemId { get; init; }
    /// <summary>Increments for every action animation; a new value means "play it once".</summary>
    public int ActionSeq { get; init; }
    public string? ActionName { get; init; }
    public float ActionFacingX { get; init; }
    public float ActionFacingY { get; init; }
}

public record WorldSyncResponse(
    DateTime ServerTimeUtc,
    List<WorldPlayerPresence> Players
)
{
    /// <summary>A gift waiting for this farmer's answer.</summary>
    public FarmerGiftView? IncomingGift { get; init; }
    /// <summary>This farmer's own offer: still pending, or its final answer (sent once).</summary>
    public FarmerGiftView? OutgoingGift { get; init; }
}
