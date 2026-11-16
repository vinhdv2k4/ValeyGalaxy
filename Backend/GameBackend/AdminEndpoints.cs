using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Accounts named in configuration <c>Admin:Usernames</c> (comma separated, e.g. env
/// <c>Admin__Usernames=admin</c>). Admin tools only ever touch the admin's own rows, and the
/// shared TV land market is closed to admins so testing never moves real players' parcels.
/// </summary>
public static class AdminAccess
{
    private static HashSet<string> usernameKeys = new(StringComparer.Ordinal);

    public static void Configure(IConfiguration configuration)
    {
        usernameKeys = (configuration["Admin:Usernames"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Upper-case username keys of the admin accounts.</summary>
    public static string[] UsernameKeys => usernameKeys.ToArray();

    public static bool IsAdmin(GameUser? user) => user != null && usernameKeys.Contains(user.UsernameKey);

    public static async Task<bool> IsAdmin(GameDb db, Guid userId) => usernameKeys.Count > 0 &&
        await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && usernameKeys.Contains(user.UsernameKey));

    /// <summary>The admin's id, or null when the caller is not signed in as an admin.</summary>
    public static async Task<Guid?> Resolve(HttpRequest request, GameDb db)
    {
        if (usernameKeys.Count == 0) return null;
        var userId = await AuthTokens.ResolveUserId(request, db);
        return userId != null && await IsAdmin(db, userId.Value) ? userId : null;
    }

    /// <summary>
    /// Creates the first admin account when <c>Admin:Password</c> is set and the name is free.
    /// Development settings set it; production only does when the variable is configured.
    /// </summary>
    public static async Task SeedAsync(GameDb db, IConfiguration configuration, ILogger logger)
    {
        string? password = configuration["Admin:Password"];
        string? username = (configuration["Admin:Usernames"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(username)) return;
        string key = username.ToUpperInvariant();
        string email = configuration["Admin:Email"] ?? username.ToLowerInvariant() + "@admin.local";
        if (await db.Users.AnyAsync(user => user.UsernameKey == key || user.EmailKey == email.ToUpperInvariant())) return;
        db.Users.Add(new GameUser
        {
            Id = Guid.NewGuid(), Username = username, UsernameKey = key,
            Email = email, EmailKey = email.ToUpperInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Created admin account {Username}.", username);
    }
}

public sealed class AdminCheckpoint
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public int GameDay { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Json { get; set; } = "";
}

public record AdminAmountRequest(Guid RequestId, long Amount);
public record AdminItemRequest(Guid RequestId, string? ItemId, int Amount);
public record AdminDayRequest(Guid RequestId, int Day);
public record AdminWeatherRequest(Guid RequestId, int Weather, bool Tomorrow = false);
public record AdminFriendshipRequest(Guid RequestId, string? Npc, int Points);
public record AdminEventRequest(Guid RequestId, string? Id, bool Done);
public record AdminBundleRequest(Guid RequestId, int Area, bool Done);
/// <summary>Skill 0-4 (Farming, Fishing, Foraging, Mining, Combat) or -1 for all; Level 0-10.</summary>
public record AdminSkillRequest(Guid RequestId, int Skill, int Level);
public record AdminCheckpointRequest(string? Name);
public record AdminCheckpointInfo(string Name, int GameDay, DateTime CreatedUtc);
public record AdminStatusResponse(bool Admin, AdminCheckpointInfo[] Checkpoints);

public static partial class GameStateEndpoints
{
    private const int MaxCheckpoints = 20;
    private static readonly Regex CheckpointName = new("^[\\p{L}\\p{N} _.-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex EventId = new("^[A-Za-z0-9_./-]{1,64}$", RegexOptions.Compiled);

    // Admin weather overrides, mirrored from PlayerProgress.WeatherOverrides each time a state is decoded.
    private static readonly ConcurrentDictionary<(Guid User, int Day), int> AdminWeather = new();

    private static PlayerProgress SyncAdminWeather(Guid userId, PlayerProgress progress)
    {
        foreach (var key in AdminWeather.Keys.Where(key => key.User == userId &&
                     !progress.WeatherOverrides.ContainsKey(key.Day)).ToArray())
            AdminWeather.TryRemove(key, out _);
        foreach (var (day, weather) in progress.WeatherOverrides) AdminWeather[(userId, day)] = weather;
        return progress;
    }

    /// <summary>Everything a checkpoint restores. TV land holdings are shared and never included.</summary>
    private sealed record CheckpointData(PlayerState State, List<FarmCropState> Crops,
        List<NpcFriendship> Friends, List<ResourceClaim> Claims);

    public static async Task EnsureAdminTables(GameDb db)
    {
        var pg = db.Database.IsNpgsql();
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "AdminCheckpoints" ("UserId" uuid NOT NULL, "Name" text NOT NULL, "GameDay" integer NOT NULL, "CreatedUtc" timestamp with time zone NOT NULL, "Json" text NOT NULL, PRIMARY KEY ("UserId", "Name"))"""
            : """CREATE TABLE IF NOT EXISTS "AdminCheckpoints" ("UserId" TEXT NOT NULL, "Name" TEXT NOT NULL, "GameDay" INTEGER NOT NULL, "CreatedUtc" TEXT NOT NULL, "Json" TEXT NOT NULL, PRIMARY KEY ("UserId", "Name"))""");
    }

    private static IResult NotAdmin() =>
        Results.Json(new ApiError("Chỉ tài khoản admin mới dùng được lệnh này."), statusCode: StatusCodes.Status403Forbidden);

    /// <summary>Runs a state change for the admin only, through the normal versioned Change pipeline.</summary>
    private static async Task<IResult> AdminChange(HttpRequest request, GameDb db, Guid requestId,
        Func<StateDocument, string?> operation) =>
        await AdminAccess.Resolve(request, db) == null ? NotAdmin() : await Change(request, db, requestId, operation);

    public static void MapAdmin(this WebApplication app)
    {
        app.MapGet("/api/admin/status", async (HttpRequest request, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            var checkpoints = await db.AdminCheckpoints.AsNoTracking().Where(c => c.UserId == admin.Value)
                .OrderByDescending(c => c.CreatedUtc)
                .Select(c => new AdminCheckpointInfo(c.Name, c.GameDay, c.CreatedUtc)).ToArrayAsync();
            return Results.Ok(new AdminStatusResponse(true, checkpoints));
        });

        // ── Money and items ───────────────────────────────────────
        app.MapPost("/api/admin/money", async (HttpRequest request, AdminAmountRequest input, GameDb db) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                doc.Money = Math.Clamp(doc.Money + input.Amount, 0, MoneyLimit);
                return null;
            }));

        app.MapPost("/api/admin/item", async (HttpRequest request, AdminItemRequest input, GameDb db,
            IItemCatalog catalog) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                if (input.Amount is < 1 or > 999 || !catalog.TryGet(input.ItemId?.Trim() ?? "", out var item))
                    return "Mã vật phẩm hoặc số lượng không hợp lệ.";
                if (!CanAdd(doc.Inventory, item.Id, input.Amount, item.MaxStack, doc.BagSize))
                    return "Túi đồ đã đầy.";
                Add(doc.Inventory, item.Id, input.Amount, item.MaxStack, doc.BagSize);
                return null;
            }));

        // ── Time ──────────────────────────────────────────────────
        // Jumps the calendar without running the overnight update (crops do not grow). The saved
        // play session follows the jump so the farmer resumes on the same spot.
        app.MapPost("/api/admin/day", async (HttpRequest request, AdminDayRequest input, GameDb db) =>
        {
            if (input.Day is < 1 or > 10_000) return Results.BadRequest(new ApiError("Ngày không hợp lệ."));
            var result = await AdminChange(request, db, input.RequestId, doc =>
            {
                doc.GameDay = input.Day;
                return null;
            });
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId != null && result is IStatusCodeHttpResult { StatusCode: 200 })
            {
                var state = await Load(db, userId.Value);
                if (!string.IsNullOrEmpty(state.SessionJson))
                {
                    try
                    {
                        var session = JsonSerializer.Deserialize<PlayerSession>(state.SessionJson);
                        if (session != null)
                        {
                            string moved = JsonSerializer.Serialize(session with { Day = input.Day });
                            await db.PlayerStates.Where(s => s.UserId == userId.Value).ExecuteUpdateAsync(setters =>
                                setters.SetProperty(s => s.SessionJson, moved));
                        }
                    }
                    catch (JsonException) { }
                }
            }
            return result;
        });

        app.MapPost("/api/admin/weather", async (HttpRequest request, AdminWeatherRequest input, GameDb db) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                if (input.Weather is < DailyEvents.FarmWeather.Clear or > DailyEvents.FarmWeather.Snow)
                    return "Thời tiết không hợp lệ.";
                int day = input.Tomorrow ? doc.GameDay + 1 : doc.GameDay;
                doc.Progress.WeatherOverrides[day] = input.Weather;
                SyncAdminWeather(doc.UserId, doc.Progress);
                return null;
            }));

        // ── Progress and events ───────────────────────────────────
        app.MapPost("/api/admin/friendship", async (HttpRequest request, AdminFriendshipRequest input, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            string npc = input.Npc?.Trim() ?? "";
            if (!NpcGiftTastes.IsKnownNpc(npc)) return Results.BadRequest(new ApiError("Không có NPC này."));
            int points = Math.Clamp(input.Points, 0, 2500);
            var friendship = await db.NpcFriendships.SingleOrDefaultAsync(f => f.UserId == admin.Value && f.NpcName == npc);
            return await Change(request, db, input.RequestId, doc =>
            {
                if (friendship == null)
                {
                    friendship = new NpcFriendship { UserId = admin.Value, NpcName = npc };
                    db.NpcFriendships.Add(friendship);
                }
                friendship.Points = points;
                doc.Progress.FriendshipPoints[npc] = points;
                return null;
            });
        });

        // Sets a skill to exactly the experience its level needs (Farmer.experiencePoints).
        // Lowering a level drops the professions that level no longer allows.
        app.MapPost("/api/admin/skill", async (HttpRequest request, AdminSkillRequest input, GameDb db) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                if (input.Skill is < -1 or > 4 || input.Level is < 0 or > 10) return "Kỹ năng hoặc cấp không hợp lệ.";
                int xp = GameProgression.ExperienceForLevel(input.Level);
                for (int skill = 0; skill < 5; skill++)
                {
                    if (input.Skill != -1 && input.Skill != skill) continue;
                    doc.SkillExperience[skill] = xp;
                    int first = skill * 6;
                    if (input.Level < 10) doc.Progress.Professions.RemoveWhere(p => p >= first + 2 && p <= first + 5);
                    if (input.Level < 5) doc.Progress.Professions.RemoveWhere(p => p == first || p == first + 1);
                }
                return null;
            }));

        // Marks a calendar event or cutscene as done (seen) or not yet happened.
        app.MapPost("/api/admin/event", async (HttpRequest request, AdminEventRequest input, GameDb db) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                string id = input.Id?.Trim() ?? "";
                if (!EventId.IsMatch(id)) return "Mã sự kiện không hợp lệ.";
                var entry = EventCalendar.Value.Find(id);
                var daily = doc.Progress.DailyEvents;
                string key = entry?.Key(new DailyEvents.FarmDate(doc.GameDay)) ?? id;
                if (input.Done)
                {
                    if (entry != null) daily[key] = doc.GameDay;
                    doc.Progress.Milestones.Seen.Add(id);
                }
                else
                {
                    foreach (string done in daily.Keys.Where(k => k == id || k == key ||
                                 k.StartsWith(id + "/", StringComparison.Ordinal)).ToArray())
                        daily.Remove(done);
                    doc.Progress.Milestones.Seen.Remove(id);
                }
                return null;
            }));

        // Finishes every bundle of a room (restoring it) or clears the room again.
        app.MapPost("/api/admin/bundle", async (HttpRequest request, AdminBundleRequest input, GameDb db) =>
            await AdminChange(request, db, input.RequestId, doc =>
            {
                // Area -1 means every room at once.
                if (input.Area < -1 || input.Area >= BundleAreas.Length) return "Phòng không hợp lệ.";
                var areas = input.Area < 0 ? Enumerable.Range(0, BundleAreas.Length).ToArray() : new[] { input.Area };
                foreach (int area in areas)
                {
                    var bundles = BundleData.Value.Where(bundle => bundle.Area == area).ToArray();
                    if (input.Done)
                    {
                        foreach (var bundle in bundles)
                            if (!BundleComplete(doc, bundle)) FinishBundle(doc, bundle);
                        doc.Progress.Milestones.Rooms.Add(BundleAreas[area].RoomId);
                    }
                    else
                    {
                        foreach (var bundle in bundles)
                        {
                            doc.Progress.BundlesComplete.Remove(bundle.Index);
                            doc.Progress.BundleDonations.Remove(bundle.Index);
                            doc.Progress.BundleRewardsPending.RemoveAll(index => index == bundle.Index);
                        }
                        doc.Progress.Milestones.Rooms.Remove(BundleAreas[area].RoomId);
                        doc.Progress.Milestones.Seen.Remove("joja_" + BundleAreas[area].RoomId);
                    }
                }
                // With a room undone the center is no longer finished: the opening ceremony (which
                // switches to the refurbished map) has to be earned again.
                if (!input.Done) doc.Progress.Milestones.Seen.Remove("community_opening");
                // Resetting the whole center also undoes the Joja route, to test either path again.
                if (!input.Done && input.Area < 0)
                {
                    doc.Progress.Milestones.Seen.Remove(JojaMemberFlag);
                    doc.Progress.Milestones.Seen.Remove(JojaOpeningId);
                }
                return null;
            }));

        // ── Checkpoints ───────────────────────────────────────────
        app.MapPost("/api/admin/checkpoints/save", async (HttpRequest request, AdminCheckpointRequest input, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            string name = input.Name?.Trim() ?? "";
            if (!CheckpointName.IsMatch(name)) return Results.BadRequest(new ApiError("Tên điểm lưu không hợp lệ (1-40 ký tự)."));
            Guid id = admin.Value;
            var state = await Load(db, id);
            var data = new CheckpointData(state,
                await db.FarmCropStates.AsNoTracking().Where(c => c.UserId == id).ToListAsync(),
                await db.NpcFriendships.AsNoTracking().Where(f => f.UserId == id).ToListAsync(),
                await db.ResourceClaims.AsNoTracking().Where(c => c.UserId == id).ToListAsync());
            var existing = await db.AdminCheckpoints.SingleOrDefaultAsync(c => c.UserId == id && c.Name == name);
            if (existing == null)
            {
                if (await db.AdminCheckpoints.CountAsync(c => c.UserId == id) >= MaxCheckpoints)
                    return Results.BadRequest(new ApiError($"Tối đa {MaxCheckpoints} điểm lưu; hãy xóa bớt."));
                existing = new AdminCheckpoint { UserId = id, Name = name };
                db.AdminCheckpoints.Add(existing);
            }
            existing.GameDay = state.GameDay;
            existing.CreatedUtc = DateTime.UtcNow;
            existing.Json = JsonSerializer.Serialize(data);
            await db.SaveChangesAsync();
            return Results.Ok(new AdminCheckpointInfo(name, state.GameDay, existing.CreatedUtc));
        });

        app.MapPost("/api/admin/checkpoints/load", async (HttpRequest request, AdminCheckpointRequest input, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            var checkpoint = await db.AdminCheckpoints.AsNoTracking()
                .SingleOrDefaultAsync(c => c.UserId == admin.Value && c.Name == (input.Name ?? ""));
            if (checkpoint == null) return Results.NotFound(new ApiError("Không tìm thấy điểm lưu."));
            CheckpointData? data;
            try { data = JsonSerializer.Deserialize<CheckpointData>(checkpoint.Json); }
            catch (JsonException) { data = null; }
            if (data?.State == null) return Results.BadRequest(new ApiError("Điểm lưu bị hỏng."));
            await RestorePlayer(db, admin.Value, data);
            return Results.Ok();
        });

        app.MapPost("/api/admin/checkpoints/delete", async (HttpRequest request, AdminCheckpointRequest input, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            await db.AdminCheckpoints.Where(c => c.UserId == admin.Value && c.Name == (input.Name ?? ""))
                .ExecuteDeleteAsync();
            return Results.Ok();
        });

        // Back to a brand-new farm (the farmer's look and the checkpoints are kept).
        app.MapPost("/api/admin/reset", async (HttpRequest request, GameDb db) =>
        {
            var admin = await AdminAccess.Resolve(request, db);
            if (admin == null) return NotAdmin();
            var current = await Load(db, admin.Value);
            var fresh = new PlayerState { UserId = admin.Value, InventoryJson = NewFarmerInventory };
            await RestorePlayer(db, admin.Value, new CheckpointData(fresh, new(), new(), new()), current.Version);
            return Results.Ok();
        });
    }

    /// <summary>Replaces every per-player row of <paramref name="userId"/> with <paramref name="data"/>.</summary>
    private static async Task RestorePlayer(GameDb db, Guid userId, CheckpointData data, int? currentVersion = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        int version = currentVersion ?? (await Load(db, userId)).Version;
        var saved = data.State;
        await db.FarmCropStates.Where(c => c.UserId == userId).ExecuteDeleteAsync();
        await db.NpcFriendships.Where(f => f.UserId == userId).ExecuteDeleteAsync();
        await db.ResourceClaims.Where(c => c.UserId == userId).ExecuteDeleteAsync();
        await db.FishingAttempts.Where(a => a.UserId == userId).ExecuteDeleteAsync();
        // A newer version makes any open client reload instead of writing over the restore.
        await db.PlayerStates.Where(s => s.UserId == userId).ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.Money, saved.Money)
            .SetProperty(s => s.TotalEarnings, saved.TotalEarnings)
            .SetProperty(s => s.RentDebt, saved.RentDebt)
            .SetProperty(s => s.BagSize, saved.BagSize)
            .SetProperty(s => s.InventoryJson, saved.InventoryJson)
            .SetProperty(s => s.ChestsJson, saved.ChestsJson)
            .SetProperty(s => s.RecentRequestsJson, "[]")
            .SetProperty(s => s.ShippingJson, saved.ShippingJson)
            .SetProperty(s => s.SkillsJson, saved.SkillsJson)
            .SetProperty(s => s.ProgressJson, saved.ProgressJson)
            .SetProperty(s => s.GameDay, saved.GameDay)
            .SetProperty(s => s.DeepestMineFloor, saved.DeepestMineFloor)
            .SetProperty(s => s.SessionJson, saved.SessionJson)
            .SetProperty(s => s.Version, version + 1));
        foreach (var crop in data.Crops) { crop.UserId = userId; db.FarmCropStates.Add(crop); }
        foreach (var friend in data.Friends) { friend.UserId = userId; db.NpcFriendships.Add(friend); }
        foreach (var claim in data.Claims) { claim.UserId = userId; db.ResourceClaims.Add(claim); }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        db.ChangeTracker.Clear();
        SyncAdminWeather(userId, PlayerProgress.FromJson(saved.ProgressJson));
    }
}
