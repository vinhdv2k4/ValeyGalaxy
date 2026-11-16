/// <summary>
/// Database Entity: Stores all Items and Tools directly in the database.
/// Includes properties for weapons/tools, pricing, stacks, and cryptographic lookup hash.
/// </summary>
public sealed class CatalogItemEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "General"; // Tool, Seeds, Crop, Resource
    public bool IsTool { get; set; }
    public int MaxStack { get; set; } = 999;
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    public int Edibility { get; set; }
    public bool IsDrink { get; set; }
    public string? ToolAction { get; set; } // Chop, Till, Water, Mine, Harvest, Fish
    public int PowerTier { get; set; } = 1;
    public int EnergyCost { get; set; }
    public string? HarvestItemId { get; set; } // Thành phẩm thu hoạch liên kết (vd: parsnip cho seeds_parsnip)
    public int GrowthDays { get; set; }        // Số ngày lớn (phaseDays)
    public int RegrowDays { get; set; }        // Số ngày tái thu hoạch (vd: 3 ngày đối với đậu que)
    public string LookupHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Database Entity: Stores all predefined types of land plots that players can own.
/// </summary>
public sealed class LandTypeEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double GrowthSpeedMultiplier { get; set; } = 1.0;
    public double WaterRetentionChance { get; set; }
    public bool AllowsAllSeasons { get; set; }
    public double BonusHarvestChance { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
