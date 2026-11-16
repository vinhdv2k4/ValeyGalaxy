using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public interface ILandPlotService
{
    Task<LandPlotDto?> GetPlotBySecureIdAsync(string secureHashId, Guid requesterUserId);
    Task<IReadOnlyList<LandPlotDto>> GetPlotsByOwnerAsync(Guid ownerId);
    Task<(bool Success, string? Error, LandPlotDto? Plot)> ClaimPlotAsync(Guid ownerId, ClaimPlotRequest request);
    Task<(bool Success, string? Error, LandPlotDto? Plot)> TillPlotAsync(string secureHashId, Guid requesterUserId, string? toolId);
    Task<(bool Success, string? Error, LandPlotDto? Plot)> WaterPlotAsync(string secureHashId, Guid requesterUserId, string? toolId);
    Task<(bool Success, string? Error, LandPlotDto? Plot)> PlantCropAsync(string secureHashId, Guid requesterUserId, string seedItemId);
}

/// <summary>
/// Service implementing high-performance two-tier O(1) hash lookups and cryptographic anti-IDOR security.
/// </summary>
public sealed class LandPlotService : ILandPlotService
{
    private readonly GameDb _db;
    private readonly ISecureIdHasher _hasher;
    private readonly ILandTypeCatalog _landTypes;
    private readonly IItemCatalog _items;

    // L1 In-Memory Hash Cache: key = SecureLookupHash, value = PlayerLandPlot
    // Provides sub-microsecond O(1) reads without DB roundtrips.
    private static readonly ConcurrentDictionary<string, PlayerLandPlot> L1Cache = new(StringComparer.Ordinal);

    public LandPlotService(GameDb db, ISecureIdHasher hasher, ILandTypeCatalog landTypes, IItemCatalog items)
    {
        _db = db;
        _hasher = hasher;
        _landTypes = landTypes;
        _items = items;
    }

    public async Task<LandPlotDto?> GetPlotBySecureIdAsync(string secureHashId, Guid requesterUserId)
    {
        // 1. Security Gate: Validate HMAC signature against requester in constant-time
        if (!_hasher.TryDecodeAndVerifyPlotId(secureHashId, requesterUserId, out var plotId))
        {
            // Tampered ID or IDOR attempt: reject immediately without querying database
            return null;
        }

        var lookupHash = _hasher.ComputePlotLookupHash(plotId, requesterUserId);

        // 2. L1 Cache Check: O(1) Hash Map
        if (L1Cache.TryGetValue(lookupHash, out var cachedPlot) && cachedPlot.OwnerId == requesterUserId)
        {
            return MapToDto(cachedPlot, secureHashId);
        }

        // 3. L2 Database Query: Hash index lookup with strict owner isolation
        var plot = await _db.LandPlots
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.SecureLookupHash == lookupHash && p.OwnerId == requesterUserId);

        if (plot == null) return null;

        // Populate L1 cache
        L1Cache[lookupHash] = plot;
        return MapToDto(plot, secureHashId);
    }

    public async Task<IReadOnlyList<LandPlotDto>> GetPlotsByOwnerAsync(Guid ownerId)
    {
        var plots = await _db.LandPlots
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .OrderBy(p => p.FarmLocation).ThenBy(p => p.TileX).ThenBy(p => p.TileY)
            .ToListAsync();

        var result = new List<LandPlotDto>(plots.Count);
        foreach (var plot in plots)
        {
            var secureId = _hasher.GenerateSecurePlotId(plot.Id, ownerId);
            L1Cache[plot.SecureLookupHash] = plot;
            result.Add(MapToDto(plot, secureId));
        }

        return result;
    }

    public async Task<(bool Success, string? Error, LandPlotDto? Plot)> ClaimPlotAsync(Guid ownerId, ClaimPlotRequest request)
    {
        var typeId = string.IsNullOrWhiteSpace(request.LandTypeId) ? "normal" : request.LandTypeId.Trim().ToLowerInvariant();
        if (!_landTypes.TryGet(typeId, out _))
            return (false, $"Loại mảnh đất không hợp lệ: '{typeId}'.", null);

        var location = string.IsNullOrWhiteSpace(request.FarmLocation) ? "farm_main" : request.FarmLocation.Trim();
        if (request.TileX < 0 || request.TileX > 500 || request.TileY < 0 || request.TileY > 500)
            return (false, "Tọa độ mảnh đất vượt quá phạm vi trang trại cho phép.", null);

        var plotId = Guid.NewGuid();
        var lookupHash = _hasher.ComputePlotLookupHash(plotId, ownerId);

        var plot = new PlayerLandPlot
        {
            Id = plotId,
            OwnerId = ownerId,
            LandTypeId = typeId,
            FarmLocation = location,
            TileX = request.TileX,
            TileY = request.TileY,
            IsTilled = false,
            IsWatered = false,
            WateredDaysLeft = 0,
            PlantedItemId = null,
            GrowthStage = 0,
            PlantedAtUtc = null,
            LastUpdatedUtc = DateTime.UtcNow,
            SecureLookupHash = lookupHash
        };

        try
        {
            _db.LandPlots.Add(plot);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (false, "Ô đất tại vị trí này đã được sở hữu.", null);
        }

        // Cache update
        L1Cache[lookupHash] = plot;
        var secureId = _hasher.GenerateSecurePlotId(plot.Id, ownerId);
        return (true, null, MapToDto(plot, secureId));
    }

    public async Task<(bool Success, string? Error, LandPlotDto? Plot)> TillPlotAsync(string secureHashId, Guid requesterUserId, string? toolId)
    {
        if (!_hasher.TryDecodeAndVerifyPlotId(secureHashId, requesterUserId, out var plotId))
            return (false, "Mảnh đất không tồn tại hoặc bạn không có quyền truy cập.", null);

        if (!string.IsNullOrWhiteSpace(toolId) && !_items.TryGetTool(toolId, out var tool))
            return (false, $"Công cụ không hợp lệ: '{toolId}'.", null);

        var plot = await _db.LandPlots.SingleOrDefaultAsync(p => p.Id == plotId && p.OwnerId == requesterUserId);
        if (plot == null) return (false, "Mảnh đất không tồn tại.", null);

        if (plot.IsTilled) return (false, "Mảnh đất này đã được cuốc đất rồi.", null);

        plot.IsTilled = true;
        plot.LastUpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        L1Cache[plot.SecureLookupHash] = plot;
        return (true, null, MapToDto(plot, secureHashId));
    }

    public async Task<(bool Success, string? Error, LandPlotDto? Plot)> WaterPlotAsync(string secureHashId, Guid requesterUserId, string? toolId)
    {
        if (!_hasher.TryDecodeAndVerifyPlotId(secureHashId, requesterUserId, out var plotId))
            return (false, "Mảnh đất không tồn tại hoặc bạn không có quyền truy cập.", null);

        if (!string.IsNullOrWhiteSpace(toolId) && !_items.TryGetTool(toolId, out var tool))
            return (false, $"Công cụ không hợp lệ: '{toolId}'.", null);

        var plot = await _db.LandPlots.SingleOrDefaultAsync(p => p.Id == plotId && p.OwnerId == requesterUserId);
        if (plot == null) return (false, "Mảnh đất không tồn tại.", null);

        if (!plot.IsTilled) return (false, "Mảnh đất chưa được cuốc đất, không thể tưới nước.", null);
        if (plot.IsWatered) return (false, "Mảnh đất đã được tưới nước hôm nay.", null);

        _landTypes.TryGet(plot.LandTypeId, out var landType);
        var retentionDays = landType != null && landType.WaterRetentionChance > 0.5 ? 2 : 1;

        plot.IsWatered = true;
        plot.WateredDaysLeft = retentionDays;
        plot.LastUpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        L1Cache[plot.SecureLookupHash] = plot;
        return (true, null, MapToDto(plot, secureHashId));
    }

    public async Task<(bool Success, string? Error, LandPlotDto? Plot)> PlantCropAsync(string secureHashId, Guid requesterUserId, string seedItemId)
    {
        if (!_hasher.TryDecodeAndVerifyPlotId(secureHashId, requesterUserId, out var plotId))
            return (false, "Mảnh đất không tồn tại hoặc bạn không có quyền truy cập.", null);

        if (!_items.TryGet(seedItemId, out var seed) || seed.Category != "Seeds")
            return (false, $"Vật phẩm không phải là hạt giống hợp lệ: '{seedItemId}'.", null);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var plot = await _db.LandPlots.SingleOrDefaultAsync(p => p.Id == plotId && p.OwnerId == requesterUserId);
        if (plot == null) return (false, "Mảnh đất không tồn tại.", null);

        if (!plot.IsTilled) return (false, "Mảnh đất chưa được cuốc, không thể gieo hạt.", null);
        if (!string.IsNullOrEmpty(plot.PlantedItemId)) return (false, "Mảnh đất này đã được gieo hạt rồi.", null);

        var state = await GameStateEndpoints.Load(_db, requesterUserId);
        var inventory = JsonSerializer.Deserialize<List<StackEntry>>(state.InventoryJson) ?? new();
        var stack = inventory.FirstOrDefault(s => s.ItemId == seedItemId && s.Amount > 0);
        if (stack == null) return (false, "Bạn không có hạt giống này trong túi đồ.", null);
        stack.Amount--;
        if (stack.Amount == 0) inventory.Remove(stack);
        string updatedInventory = JsonSerializer.Serialize(inventory);
        int changed = await _db.PlayerStates.Where(s => s.UserId == requesterUserId &&
            s.Version == state.Version).ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.InventoryJson, updatedInventory)
                .SetProperty(s => s.Version, s => s.Version + 1));
        if (changed != 1) return (false, "Túi đồ vừa thay đổi; hãy thử lại.", null);

        plot.PlantedItemId = seed.Id;
        plot.GrowthStage = 0;
        plot.PlantedAtUtc = DateTime.UtcNow;
        plot.LastUpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        L1Cache[plot.SecureLookupHash] = plot;
        return (true, null, MapToDto(plot, secureHashId));
    }

    private LandPlotDto MapToDto(PlayerLandPlot plot, string secureId)
    {
        _landTypes.TryGet(plot.LandTypeId, out var landType);
        return new LandPlotDto(
            SecureId: secureId,
            LandTypeId: plot.LandTypeId,
            LandTypeName: landType?.Name ?? "Đất cơ bản",
            FarmLocation: plot.FarmLocation,
            TileX: plot.TileX,
            TileY: plot.TileY,
            IsTilled: plot.IsTilled,
            IsWatered: plot.IsWatered,
            PlantedItemId: plot.PlantedItemId,
            GrowthStage: plot.GrowthStage,
            PlantedAtUtc: plot.PlantedAtUtc
        );
    }
}
