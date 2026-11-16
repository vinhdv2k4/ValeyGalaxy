using System.Collections.Frozen;

/// <summary>
/// Immutable definitions for types of land plots available in the game world.
/// </summary>
public sealed record LandTypeDefinition(
    string Id,
    string Name,
    string Description,
    double GrowthSpeedMultiplier,
    double WaterRetentionChance,
    bool AllowsAllSeasons,
    double BonusHarvestChance
);

/// <summary>
/// In-memory catalog of all supported land plot types, hashed with FrozenDictionary for O(1) retrieval.
/// </summary>
public interface ILandTypeCatalog
{
    bool TryGet(string id, out LandTypeDefinition landType);
    IReadOnlyCollection<LandTypeDefinition> All { get; }
}

public sealed class LandTypeCatalog : ILandTypeCatalog
{
    private static readonly LandTypeDefinition[] DefaultTypes =
    [
        new("normal", "Đất thường", "Đất canh tác cơ bản, cần tưới nước mỗi ngày.", 1.0, 0.0, false, 0.0),
        new("fertile", "Đất màu mỡ", "Giàu dưỡng chất, rút ngắn thời gian sinh trưởng của cây 25%.", 1.25, 0.15, false, 0.10),
        new("retaining", "Đất giữ ẩm", "Đất mùn có khả năng giữ ẩm cao, 70% không cần tưới lại vào ngày hôm sau.", 1.0, 0.70, false, 0.05),
        new("greenhouse", "Đất nhà kính", "Nhiệt độ tối ưu, cho phép gieo trồng tất cả các loại hạt bất kể mùa nào.", 1.15, 0.30, true, 0.15),
        new("mineral", "Đất khoáng sản", "Vùng đất trù phú chứa vi khoáng, có cơ hội nhân đôi sản lượng thu hoạch.", 1.0, 0.10, false, 0.25)
    ];

    private readonly FrozenDictionary<string, LandTypeDefinition> _byId;
    public IReadOnlyCollection<LandTypeDefinition> All { get; }

    public LandTypeCatalog()
    {
        _byId = DefaultTypes.ToFrozenDictionary(t => t.Id, StringComparer.Ordinal);
        All = Array.AsReadOnly(DefaultTypes);
    }

    public bool TryGet(string id, out LandTypeDefinition landType) =>
        _byId.TryGetValue(id ?? "", out landType!);
}

/// <summary>
/// EF Core Entity: Represents a specific plot of land owned by a player.
/// </summary>
public sealed class PlayerLandPlot
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public GameUser Owner { get; set; } = null!;

    public string LandTypeId { get; set; } = "normal";
    public string FarmLocation { get; set; } = "farm_main";
    public int TileX { get; set; }
    public int TileY { get; set; }

    public bool IsTilled { get; set; }
    public bool IsWatered { get; set; }
    public int WateredDaysLeft { get; set; }

    public string? PlantedItemId { get; set; }
    public int GrowthStage { get; set; }
    public DateTime? PlantedAtUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deterministic 64-character hash of (Id + OwnerId + Secret) for O(1) indexed database lookups.
    /// </summary>
    public string SecureLookupHash { get; set; } = "";
}

// ── DTOs ─────────────────────────────────────────────────────────────
public record LandPlotDto(
    string SecureId,
    string LandTypeId,
    string LandTypeName,
    string FarmLocation,
    int TileX,
    int TileY,
    bool IsTilled,
    bool IsWatered,
    string? PlantedItemId,
    int GrowthStage,
    DateTime? PlantedAtUtc
);

public record ClaimPlotRequest(string? LandTypeId, string? FarmLocation, int TileX, int TileY);
public record TillPlotRequest(string? ToolId);
public record WaterPlotRequest(string? ToolId);
public record PlantCropRequest(string? SeedItemId);
