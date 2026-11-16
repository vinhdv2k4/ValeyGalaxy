using Microsoft.EntityFrameworkCore;

public sealed class TvLandHolding
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string ParcelId { get; set; } = "";
    public bool IsPermanent { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class TvLandParcel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Rental";
    public Guid? CurrentOwnerId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
}

public sealed class TvMoneyOperation
{
    public Guid RequestId { get; set; }
    public Guid OwnerId { get; set; }
    public string ParcelId { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TvLandAuction
{
    public Guid Id { get; set; }
    public int Round { get; set; } = 1;
    public string ParcelId { get; set; } = "";
    public DateTime EndsAtUtc { get; set; }
    public long HighestBid { get; set; }
    public Guid? HighestBidderId { get; set; }
    public int Version { get; set; }
}

public record TvLeaderboardEntry(int Rank, string FarmerName, string FarmName,
    long TotalEarnings, long RentDebt, long Score, int LandCount);
public record TvLeaderboardResponse(IReadOnlyList<TvLeaderboardEntry> Entries);
public record TvHoldingDto(string Id, string ParcelId, string Name, bool IsPermanent, DateTime? ExpiresAtUtc);
public record TvParcelDto(string Id, string Name);
public record TvLandResponse(long Money, long RentDebt, long BaseFarmRentPrice, long NextBaseFarmRentDay,
    long RentPrice, int RentDays, int RentLimit, int FarmTilesUsed, int FarmTileLimit, int TilesPerParcel,
    IReadOnlyList<TvHoldingDto> Holdings, IReadOnlyList<TvParcelDto> AvailableParcels);
public record TvAuctionResponse(int Round, string ParcelId, string ParcelName, long HighestBid, long MinimumBid, string? HighestBidder,
    bool YouAreWinning, DateTime EndsAtUtc, long YourMoney);
public record TvRentRequest(Guid RequestId, string? ParcelId, long? ExpectedRentPrice = null);
public record TvReturnRequest(string? ParcelId);
public record TvBidRequest(Guid RequestId, string? ParcelId, long Amount);

public static class TvEconomyEndpoints
{
    private static readonly Guid AuctionId = Guid.Parse("2d792785-9b96-42a4-b351-ef24c491ba34");
    public const long BaseFarmRentPrice = 300;
    private const long RentPriceStep = 300;
    public const int BaseFarmRentDays = 7;
    private const int RentDays = 7;
    private const int RentLimit = 3;
    private const long OpeningBid = 1000;
    private const long BidIncrement = 100;
    private static readonly TvParcelDto[] RentalParcels = Enumerable.Range(1, 6)
        .Select(number => new TvParcelDto($"rent-{number:000}", $"Rental Plot {number:000}")).ToArray();
    private static readonly TvParcelDto[] AuctionParcels = Enumerable.Range(1, 24)
        .Select(number => new TvParcelDto($"auction-{number:000}", $"Auction Plot {number:000}")).ToArray();

    public static async Task EnsureTables(GameDb db)
    {
        bool pg = db.Database.IsNpgsql();
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "TvLandHoldings" ("Id" uuid NOT NULL PRIMARY KEY, "OwnerId" uuid NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE, "Name" text NOT NULL, "ParcelId" text NOT NULL DEFAULT '', "IsPermanent" boolean NOT NULL, "ExpiresAtUtc" timestamp with time zone)"""
            : """CREATE TABLE IF NOT EXISTS "TvLandHoldings" ("Id" TEXT NOT NULL PRIMARY KEY, "OwnerId" TEXT NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE, "Name" TEXT NOT NULL, "ParcelId" TEXT NOT NULL DEFAULT '', "IsPermanent" INTEGER NOT NULL, "ExpiresAtUtc" TEXT)""");
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_TvLandHoldings_OwnerId" ON "TvLandHoldings" ("OwnerId")""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "TvLandAuctions" ("Id" uuid NOT NULL PRIMARY KEY, "Round" integer NOT NULL, "ParcelId" text NOT NULL DEFAULT '', "EndsAtUtc" timestamp with time zone NOT NULL, "HighestBid" bigint NOT NULL, "HighestBidderId" uuid, "Version" integer NOT NULL)"""
            : """CREATE TABLE IF NOT EXISTS "TvLandAuctions" ("Id" TEXT NOT NULL PRIMARY KEY, "Round" INTEGER NOT NULL, "ParcelId" TEXT NOT NULL DEFAULT '', "EndsAtUtc" TEXT NOT NULL, "HighestBid" INTEGER NOT NULL, "HighestBidderId" TEXT, "Version" INTEGER NOT NULL)""");
        if (pg)
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "TvLandHoldings" ADD COLUMN IF NOT EXISTS "ParcelId" text NOT NULL DEFAULT ''""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "TvLandAuctions" ADD COLUMN IF NOT EXISTS "ParcelId" text NOT NULL DEFAULT ''""");
        }
        else
        {
            await EnsureSqliteColumn(db, "TvLandHoldings", "ParcelId");
            await EnsureSqliteColumn(db, "TvLandAuctions", "ParcelId");
        }
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "TvLandParcels" ("Id" text NOT NULL PRIMARY KEY, "Name" text NOT NULL, "Kind" text NOT NULL, "CurrentOwnerId" uuid, "LeaseExpiresAtUtc" timestamp with time zone)"""
            : """CREATE TABLE IF NOT EXISTS "TvLandParcels" ("Id" TEXT NOT NULL PRIMARY KEY, "Name" TEXT NOT NULL, "Kind" TEXT NOT NULL, "CurrentOwnerId" TEXT, "LeaseExpiresAtUtc" TEXT)""");
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE TABLE IF NOT EXISTS "TvMoneyOperations" ("RequestId" uuid NOT NULL PRIMARY KEY, "OwnerId" uuid NOT NULL, "ParcelId" text NOT NULL, "Action" text NOT NULL, "CreatedUtc" timestamp with time zone NOT NULL)"""
            : """CREATE TABLE IF NOT EXISTS "TvMoneyOperations" ("RequestId" TEXT NOT NULL PRIMARY KEY, "OwnerId" TEXT NOT NULL, "ParcelId" TEXT NOT NULL, "Action" TEXT NOT NULL, "CreatedUtc" TEXT NOT NULL)""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_TvLandHoldings_ParcelId" ON "TvLandHoldings" ("ParcelId")""");
        await db.Database.ExecuteSqlRawAsync("""UPDATE "TvLandHoldings" SET "ParcelId" = CAST("Id" AS text) WHERE "ParcelId" = ''""");
        var existingParcels = await db.TvLandParcels.AsNoTracking().Select(p => p.Id).ToListAsync();
        var parcelIds = existingParcels.ToHashSet(StringComparer.Ordinal);
        foreach (var parcel in RentalParcels)
            if (parcelIds.Add(parcel.Id)) db.TvLandParcels.Add(new TvLandParcel { Id = parcel.Id, Name = parcel.Name });
        foreach (var parcel in AuctionParcels)
            if (parcelIds.Add(parcel.Id)) db.TvLandParcels.Add(new TvLandParcel
                { Id = parcel.Id, Name = parcel.Name, Kind = "Auction" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""UPDATE "TvLandAuctions" SET "ParcelId" = 'auction-001' WHERE "ParcelId" = ''""");
        if (!await db.TvLandAuctions.AnyAsync())
        {
            db.TvLandAuctions.Add(new TvLandAuction { Id = AuctionId, ParcelId = AuctionParcels[0].Id,
                EndsAtUtc = DateTime.UtcNow.AddDays(1) });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        }
    }

    // The land market is shared with real players, so admin test accounts stay out of it.
    private const string AdminMarketClosed = "Tài khoản admin không được thuê hoặc đấu giá đất (dữ liệu dùng chung).";

    public static void MapTvEconomy(this WebApplication app)
    {
        app.MapGet("/api/tv/leaderboard", async (HttpRequest request, GameDb db) =>
        {
            if (await AuthTokens.ResolveUserId(request, db) == null) return Results.Unauthorized();
            // Test accounts never rank against real players.
            var adminKeys = AdminAccess.UsernameKeys;
            var leaders = await (from user in db.Users.AsNoTracking()
                                 where !adminKeys.Contains(user.UsernameKey)
                                 join state in db.PlayerStates.AsNoTracking() on user.Id equals state.UserId into states
                                 from state in states.DefaultIfEmpty()
                                 join farmer in db.Farmers.AsNoTracking() on user.Id equals farmer.UserId into farmers
                                 from farmer in farmers.DefaultIfEmpty()
                                 let score = state == null ? 0L : state.TotalEarnings > state.RentDebt
                                     ? state.TotalEarnings - state.RentDebt : 0L
                                 orderby score descending, user.Username
                                 select new { user.Id, user.Username, Farmer = farmer == null ? null : farmer.Name,
                                     Farm = farmer == null ? null : farmer.FarmName,
                                     TotalEarnings = state == null ? 0L : state.TotalEarnings,
                                     RentDebt = state == null ? 0L : state.RentDebt, Score = score })
                .Take(10).ToListAsync();
            var ids = leaders.Select(x => x.Id).ToArray();
            var now = DateTime.UtcNow;
            var counts = await db.TvLandHoldings.AsNoTracking()
                .Where(h => ids.Contains(h.OwnerId) && (h.IsPermanent || h.ExpiresAtUtc > now))
                .GroupBy(h => h.OwnerId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();
            var byOwner = counts.ToDictionary(x => x.Id, x => x.Count);
            // Standard competition ranking: equal scores share a rank (1, 1, 3).
            var entries = new List<TvLeaderboardEntry>(leaders.Count);
            int rank = 0;
            for (int index = 0; index < leaders.Count; index++)
            {
                var p = leaders[index];
                if (index == 0 || p.Score != leaders[index - 1].Score) rank = index + 1;
                // Never expose the login username; fall back to a neutral name.
                string name = string.IsNullOrWhiteSpace(p.Farmer) ? "Unnamed farmer" : p.Farmer;
                entries.Add(new TvLeaderboardEntry(rank, name, p.Farm ?? "",
                    p.TotalEarnings, p.RentDebt, p.Score, byOwner.GetValueOrDefault(p.Id)));
            }
            return Results.Ok(new TvLeaderboardResponse(entries));
        });

        app.MapGet("/api/tv/land", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await LandStatus(db, userId.Value));
        });

        app.MapPost("/api/tv/land/rent", async (HttpRequest request, TvRentRequest input, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            if (await AdminAccess.IsAdmin(db, userId.Value))
                return Results.BadRequest(new ApiError(AdminMarketClosed));
            if (input.RequestId == Guid.Empty)
                return Results.BadRequest(new ApiError("Mã giao dịch là bắt buộc."));
            if (string.IsNullOrWhiteSpace(input.ParcelId))
                return Results.BadRequest(new ApiError("Mã mảnh đất thuê là bắt buộc."));
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (await db.TvMoneyOperations.AnyAsync(op => op.RequestId == input.RequestId))
                return Results.Conflict(new ApiError("Giao dịch này đã được xử lý."));
            var now = DateTime.UtcNow;
            var parcel = await db.TvLandParcels.AsNoTracking().SingleOrDefaultAsync(p => p.Id == input.ParcelId);
            if (parcel == null || parcel.Kind != "Rental")
                return Results.BadRequest(new ApiError("Mảnh đất này không cho thuê."));
            var state = await GameStateEndpoints.Load(db, userId.Value);
            if (state.RentDebt > 0) return Results.BadRequest(new ApiError("Hãy thanh toán hóa đơn trong hộp thư trước khi thuê thêm đất."));
            int count = await db.TvLandHoldings.CountAsync(h => h.OwnerId == userId &&
                !h.IsPermanent && h.ExpiresAtUtc > now);
            if (count >= RentLimit) return Results.BadRequest(new ApiError("Bạn đã thuê tối đa 3 thửa đất."));
            long rentPrice = NextRentPrice(count);
            if (input.ExpectedRentPrice is long expectedPrice && expectedPrice != rentPrice)
                return Results.Conflict(new ApiError("Giá thuê đã thay đổi. Hãy tải lại kênh thuê đất."));
            if (state.Money < rentPrice) return Results.BadRequest(new ApiError("Không đủ tiền thuê đất."));
            int reserved = await db.TvLandParcels.Where(p => p.Id == input.ParcelId && p.Kind == "Rental" &&
                (p.CurrentOwnerId == null || p.LeaseExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.CurrentOwnerId, userId)
                    .SetProperty(p => p.LeaseExpiresAtUtc, now.AddDays(RentDays)));
            if (reserved != 1) return Results.Conflict(new ApiError("Mảnh đất đã được người khác thuê."));
            int changed = await db.PlayerStates.Where(s => s.UserId == userId && s.Version == state.Version && s.Money >= rentPrice)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, s => s.Money - rentPrice)
                    .SetProperty(s => s.Version, s => s.Version + 1));
            if (changed != 1) return Results.Conflict(new ApiError("Số dư đã thay đổi; hãy thử lại."));
            db.TvLandHoldings.Add(new TvLandHolding { Id = Guid.NewGuid(), OwnerId = userId.Value,
                ParcelId = parcel.Id, Name = parcel.Name, ExpiresAtUtc = now.AddDays(RentDays) });
            db.TvMoneyOperations.Add(new TvMoneyOperation { RequestId = input.RequestId,
                OwnerId = userId.Value, ParcelId = parcel.Id, Action = "Rent" });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new ApiError("Giao dịch thuê đã thay đổi.")); }
            await transaction.CommitAsync();
            return Results.Ok(await LandStatus(db, userId.Value));
        });

        app.MapPost("/api/tv/land/return", async (HttpRequest request, TvReturnRequest input, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(input.ParcelId))
                return Results.BadRequest(new ApiError("Mã thửa đất không hợp lệ."));
            await using var transaction = await db.Database.BeginTransactionAsync();
            var state = await GameStateEndpoints.Load(db, userId.Value);
            int removed = await db.TvLandHoldings.Where(h => h.ParcelId == input.ParcelId &&
                h.OwnerId == userId && !h.IsPermanent && h.ExpiresAtUtc > DateTime.UtcNow)
                .ExecuteDeleteAsync();
            if (removed != 1) return Results.NotFound(new ApiError("Không tìm thấy đất thuê đang hiệu lực."));
            await db.TvLandParcels.Where(p => p.Id == input.ParcelId && p.CurrentOwnerId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.CurrentOwnerId, (Guid?)null)
                    .SetProperty(p => p.LeaseExpiresAtUtc, (DateTime?)null));
            int changed = await db.PlayerStates.Where(s => s.UserId == userId && s.Version == state.Version)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Version, s => s.Version + 1));
            if (changed != 1) return Results.Conflict(new ApiError("Quyền sử dụng đất đã thay đổi; hãy thử lại."));
            await transaction.CommitAsync();
            return Results.Ok(await LandStatus(db, userId.Value));
        });

        app.MapGet("/api/tv/land-auction", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            await SettleAuction(db);
            return Results.Ok(await AuctionStatus(db, userId.Value));
        });

        app.MapPost("/api/tv/land-auction/bid", async (HttpRequest request, TvBidRequest input, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            if (input.RequestId == Guid.Empty)
                return Results.BadRequest(new ApiError("Mã giao dịch là bắt buộc."));
            if (await AdminAccess.IsAdmin(db, userId.Value))
                return Results.BadRequest(new ApiError(AdminMarketClosed));
            if (input.Amount < OpeningBid || input.Amount > 999_999_999)
                return Results.BadRequest(new ApiError("Giá đấu không hợp lệ."));
            await SettleAuction(db);
            var auction = await db.TvLandAuctions.AsNoTracking().SingleAsync();
            if (string.IsNullOrEmpty(auction.ParcelId) || input.ParcelId != auction.ParcelId)
                return Results.Conflict(new ApiError("Mã mảnh đất đấu giá đã thay đổi; hãy tải lại."));
            if (auction.EndsAtUtc <= DateTime.UtcNow)
                return Results.Conflict(new ApiError("Phiên đấu giá đã kết thúc; hãy tải lại."));
            if (auction.HighestBidderId == userId)
                return Results.BadRequest(new ApiError("Bạn đang dẫn đầu phiên đấu giá."));
            if (input.Amount < Math.Max(OpeningBid, auction.HighestBid + BidIncrement))
                return Results.BadRequest(new ApiError("Giá đấu phải cao hơn ít nhất 100g."));
            if (!await db.TvLandParcels.AnyAsync(p => p.Id == auction.ParcelId &&
                p.Kind == "Auction" && p.CurrentOwnerId == null))
                return Results.Conflict(new ApiError("Mảnh đất không còn khả dụng để đấu giá."));

            var state = await GameStateEndpoints.Load(db, userId.Value);
            if (state.Money < input.Amount) return Results.BadRequest(new ApiError("Không đủ tiền đặt giá."));
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (await db.TvMoneyOperations.AnyAsync(op => op.RequestId == input.RequestId))
                return Results.Conflict(new ApiError("Giao dịch này đã được xử lý."));
            int won = await db.TvLandAuctions.Where(a => a.Id == auction.Id && a.ParcelId == input.ParcelId &&
                a.Version == auction.Version && a.EndsAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.HighestBid, input.Amount)
                    .SetProperty(a => a.HighestBidderId, userId)
                    .SetProperty(a => a.Version, a => a.Version + 1));
            if (won != 1) return Results.Conflict(new ApiError("Giá đấu đã thay đổi; hãy tải lại."));
            int paid = await db.PlayerStates.Where(s => s.UserId == userId && s.Version == state.Version && s.Money >= input.Amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, s => s.Money - input.Amount)
                    .SetProperty(s => s.Version, s => s.Version + 1));
            if (paid != 1) return Results.Conflict(new ApiError("Số dư đã thay đổi; hãy thử lại."));
            if (auction.HighestBidderId.HasValue)
                await db.PlayerStates.Where(s => s.UserId == auction.HighestBidderId.Value)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, s => s.Money + auction.HighestBid)
                        .SetProperty(s => s.Version, s => s.Version + 1));
            db.TvMoneyOperations.Add(new TvMoneyOperation { RequestId = input.RequestId,
                OwnerId = userId.Value, ParcelId = auction.ParcelId, Action = "Bid" });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new ApiError("Giao dịch đấu giá đã thay đổi.")); }
            await transaction.CommitAsync();
            return Results.Ok(await AuctionStatus(db, userId.Value));
        });
    }

    private static long NextRentPrice(int activeRentals) => BaseFarmRentPrice + RentPriceStep * (activeRentals + 1);

    private static async Task<TvLandResponse> LandStatus(GameDb db, Guid ownerId)
    {
        var state = await GameStateEndpoints.Load(db, ownerId);
        var now = DateTime.UtcNow;
        var holdings = await db.TvLandHoldings.AsNoTracking()
            .Where(h => h.OwnerId == ownerId && (h.IsPermanent || h.ExpiresAtUtc > now))
            .OrderBy(h => h.IsPermanent).ThenBy(h => h.ExpiresAtUtc).ToListAsync();
        var available = await db.TvLandParcels.AsNoTracking()
            .Where(p => p.Kind == "Rental" && (p.CurrentOwnerId == null || p.LeaseExpiresAtUtc <= now))
            .OrderBy(p => p.Id).Select(p => new TvParcelDto(p.Id, p.Name)).ToListAsync();
        long nextRentDay = ((long)Math.Max(1, state.GameDay) - 1) / BaseFarmRentDays * BaseFarmRentDays
            + BaseFarmRentDays + 1;
        return new TvLandResponse(state.Money, state.RentDebt, BaseFarmRentPrice, nextRentDay,
            NextRentPrice(holdings.Count(h => !h.IsPermanent)), RentDays, RentLimit, GameStateEndpoints.FarmTilesUsed(db, state),
            GameStateEndpoints.FarmTileLimit(db, ownerId, now), GameStateEndpoints.TilesPerParcel,
            holdings.Select(h => new TvHoldingDto(h.Id.ToString(), h.ParcelId, h.Name,
                h.IsPermanent, h.ExpiresAtUtc)).ToArray(), available);
    }

    private static async Task<TvAuctionResponse> AuctionStatus(GameDb db, Guid userId)
    {
        var auction = await db.TvLandAuctions.AsNoTracking().SingleAsync();
        var state = await GameStateEndpoints.Load(db, userId);
        string? bidder = auction.HighestBidderId.HasValue
            ? await db.Users.Where(u => u.Id == auction.HighestBidderId.Value).Select(u => u.Username).SingleOrDefaultAsync()
            : null;
        string parcelName = await db.TvLandParcels.Where(p => p.Id == auction.ParcelId)
            .Select(p => p.Name).SingleOrDefaultAsync() ?? "No land available";
        return new TvAuctionResponse(auction.Round, auction.ParcelId, parcelName, auction.HighestBid,
            string.IsNullOrEmpty(auction.ParcelId) ? 0 : Math.Max(OpeningBid, auction.HighestBid + BidIncrement), bidder,
            auction.HighestBidderId == userId, auction.EndsAtUtc, state.Money);
    }

    private static async Task SettleAuction(GameDb db)
    {
        var auction = await db.TvLandAuctions.AsNoTracking().SingleAsync();
        if (string.IsNullOrEmpty(auction.ParcelId) || auction.EndsAtUtc > DateTime.UtcNow) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        string nextParcelId = auction.ParcelId;
        if (auction.HighestBidderId.HasValue)
        {
            nextParcelId = await db.TvLandParcels.AsNoTracking()
                .Where(p => p.Kind == "Auction" && p.CurrentOwnerId == null && p.Id != auction.ParcelId)
                .OrderBy(p => p.Id).Select(p => p.Id).FirstOrDefaultAsync() ?? "";
        }
        int changed = await db.TvLandAuctions.Where(a => a.Id == auction.Id && a.Version == auction.Version)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Round, a => a.Round + 1)
                .SetProperty(a => a.ParcelId, nextParcelId)
                .SetProperty(a => a.EndsAtUtc, DateTime.UtcNow.AddDays(1))
                .SetProperty(a => a.HighestBid, 0L)
                .SetProperty(a => a.HighestBidderId, (Guid?)null)
                .SetProperty(a => a.Version, a => a.Version + 1));
        if (changed == 1 && auction.HighestBidderId.HasValue)
        {
            int claimed = await db.TvLandParcels.Where(p => p.Id == auction.ParcelId && p.CurrentOwnerId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.CurrentOwnerId, auction.HighestBidderId));
            if (claimed != 1) throw new InvalidOperationException("Auction parcel was already assigned.");
            string parcelName = await db.TvLandParcels.Where(p => p.Id == auction.ParcelId)
                .Select(p => p.Name).SingleAsync();
            db.TvLandHoldings.Add(new TvLandHolding { Id = Guid.NewGuid(), OwnerId = auction.HighestBidderId.Value,
                ParcelId = auction.ParcelId, Name = parcelName, IsPermanent = true });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task EnsureSqliteColumn(GameDb db, string table, string column)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            bool exists = false;
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    if (reader.GetString(1) == column) exists = true;
            if (!exists)
            {
                string sql = table switch
                {
                    "TvLandHoldings" => """ALTER TABLE "TvLandHoldings" ADD COLUMN "ParcelId" TEXT NOT NULL DEFAULT ''""",
                    "TvLandAuctions" => """ALTER TABLE "TvLandAuctions" ADD COLUMN "ParcelId" TEXT NOT NULL DEFAULT ''""",
                    _ => throw new InvalidOperationException("Unexpected parcel table.")
                };
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
