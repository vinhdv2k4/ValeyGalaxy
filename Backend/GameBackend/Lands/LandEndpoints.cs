using Microsoft.EntityFrameworkCore;

public static class LandEndpoints
{
    public static async Task EnsureLandTables(GameDb db)
    {
        var pg = db.Database.IsNpgsql();
        await db.Database.ExecuteSqlRawAsync(pg
            ? """
              CREATE TABLE IF NOT EXISTS "LandPlots" (
                  "Id" uuid NOT NULL PRIMARY KEY,
                  "OwnerId" uuid NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
                  "LandTypeId" text NOT NULL,
                  "FarmLocation" text NOT NULL,
                  "TileX" integer NOT NULL,
                  "TileY" integer NOT NULL,
                  "IsTilled" boolean NOT NULL,
                  "IsWatered" boolean NOT NULL,
                  "WateredDaysLeft" integer NOT NULL,
                  "PlantedItemId" text,
                  "GrowthStage" integer NOT NULL,
                  "PlantedAtUtc" timestamp with time zone,
                  "LastUpdatedUtc" timestamp with time zone NOT NULL,
                  "SecureLookupHash" text NOT NULL
              )
              """
            : """
              CREATE TABLE IF NOT EXISTS "LandPlots" (
                  "Id" TEXT NOT NULL PRIMARY KEY,
                  "OwnerId" TEXT NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
                  "LandTypeId" TEXT NOT NULL,
                  "FarmLocation" TEXT NOT NULL,
                  "TileX" INTEGER NOT NULL,
                  "TileY" INTEGER NOT NULL,
                  "IsTilled" INTEGER NOT NULL,
                  "IsWatered" INTEGER NOT NULL,
                  "WateredDaysLeft" INTEGER NOT NULL,
                  "PlantedItemId" TEXT,
                  "GrowthStage" INTEGER NOT NULL,
                  "PlantedAtUtc" TEXT,
                  "LastUpdatedUtc" TEXT NOT NULL,
                  "SecureLookupHash" TEXT NOT NULL
              )
              """);

        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LandPlots_Owner_Coord" ON "LandPlots" ("OwnerId", "FarmLocation", "TileX", "TileY")""");

        // High-performance hash lookup index:
        // On PostgreSQL, use Hash index for true O(1) equality lookup. On SQLite, standard index.
        await db.Database.ExecuteSqlRawAsync(pg
            ? """CREATE INDEX IF NOT EXISTS "IX_LandPlots_SecureLookupHash" ON "LandPlots" USING HASH ("SecureLookupHash")"""
            : """CREATE INDEX IF NOT EXISTS "IX_LandPlots_SecureLookupHash" ON "LandPlots" ("SecureLookupHash")""");
    }

    public static void MapLandEndpoints(this WebApplication app)
    {
        // ── Danh mục các kiểu mảnh đất ────────────────────────────────
        app.MapGet("/api/lands/types", (ILandTypeCatalog catalog) =>
            Results.Ok(catalog.All));

        app.MapGet("/api/lands/types/{id}", (string id, ILandTypeCatalog catalog) =>
            catalog.TryGet(id, out var landType)
                ? Results.Ok(landType)
                : Results.NotFound(new ApiError($"Không tìm thấy kiểu đất với id '{id}'.")));

        // ── Truy vấn mảnh đất của người chơi ──────────────────────────
        app.MapGet("/api/lands/my-plots", async (HttpRequest request, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            var plots = await service.GetPlotsByOwnerAsync(userId.Value);
            return Results.Ok(plots);
        });

        // ── Tìm kiếm mảnh đất qua Secure Hash ID (O(1) & Bảo mật chống IDOR) ────
        app.MapGet("/api/lands/{secureHashId}", async (HttpRequest request, string secureHashId, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            // Tìm kiếm nhanh O(1) qua hash và verify HMAC chữ ký chống xem trộm đất người khác
            var plot = await service.GetPlotBySecureIdAsync(secureHashId, userId.Value);
            if (plot == null)
            {
                // Trả về 404 để chống information disclosure (không để hacker biết ID có tồn tại hay không)
                return Results.NotFound(new ApiError("Mảnh đất không tồn tại hoặc bạn không có quyền truy cập."));
            }

            return Results.Ok(plot);
        });

        // ── Sở hữu mảnh đất mới ──────────────────────────────────────
        app.MapPost("/api/lands/claim", async (HttpRequest request, ClaimPlotRequest input, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            var (success, error, plot) = await service.ClaimPlotAsync(userId.Value, input);
            return success ? Results.Ok(plot) : Results.BadRequest(new ApiError(error!));
        });

        // ── Thao tác cày xới đất ─────────────────────────────────────
        app.MapPost("/api/lands/{secureHashId}/till", async (HttpRequest request, string secureHashId, TillPlotRequest input, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            var (success, error, plot) = await service.TillPlotAsync(secureHashId, userId.Value, input.ToolId);
            return success ? Results.Ok(plot) : Results.BadRequest(new ApiError(error!));
        });

        // ── Thao tác tưới nước ───────────────────────────────────────
        app.MapPost("/api/lands/{secureHashId}/water", async (HttpRequest request, string secureHashId, WaterPlotRequest input, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            var (success, error, plot) = await service.WaterPlotAsync(secureHashId, userId.Value, input.ToolId);
            return success ? Results.Ok(plot) : Results.BadRequest(new ApiError(error!));
        });

        // ── Thao tác gieo hạt ────────────────────────────────────────
        app.MapPost("/api/lands/{secureHashId}/plant", async (HttpRequest request, string secureHashId, PlantCropRequest input, GameDb db, ILandPlotService service) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(input.SeedItemId))
                return Results.BadRequest(new ApiError("Mã hạt giống (SeedItemId) không được để trống."));

            var (success, error, plot) = await service.PlantCropAsync(secureHashId, userId.Value, input.SeedItemId);
            return success ? Results.Ok(plot) : Results.BadRequest(new ApiError(error!));
        });
    }
}
