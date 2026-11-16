using Microsoft.EntityFrameworkCore;

public sealed class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<GameUser> Users => Set<GameUser>();
    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<SessionPresence> SessionPresences => Set<SessionPresence>();
    public DbSet<FarmerProfile> Farmers => Set<FarmerProfile>();
    public DbSet<PlayerState> PlayerStates => Set<PlayerState>();
    public DbSet<ProcessedGameRequest> ProcessedGameRequests => Set<ProcessedGameRequest>();
    public DbSet<FishingAttempt> FishingAttempts => Set<FishingAttempt>();
    public DbSet<ResourceClaim> ResourceClaims => Set<ResourceClaim>();
    public DbSet<FarmCropState> FarmCropStates => Set<FarmCropState>();
    public DbSet<NpcFriendship> NpcFriendships => Set<NpcFriendship>();
    public DbSet<PlayerLandPlot> LandPlots => Set<PlayerLandPlot>();
    public DbSet<TvLandHolding> TvLandHoldings => Set<TvLandHolding>();
    public DbSet<TvLandParcel> TvLandParcels => Set<TvLandParcel>();
    public DbSet<TvMoneyOperation> TvMoneyOperations => Set<TvMoneyOperation>();
    public DbSet<TvLandAuction> TvLandAuctions => Set<TvLandAuction>();
    public DbSet<CatalogItemEntity> CatalogItems => Set<CatalogItemEntity>();
    public DbSet<LandTypeEntity> LandTypes => Set<LandTypeEntity>();
    public DbSet<AdminCheckpoint> AdminCheckpoints => Set<AdminCheckpoint>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<FarmerProfile>().HasKey(f => f.UserId);
        model.Entity<PlayerState>().HasKey(s => s.UserId);
        model.Entity<ProcessedGameRequest>().HasKey(r => new { r.UserId, r.RequestId });
        model.Entity<FishingAttempt>().HasKey(a => a.Id);
        model.Entity<FishingAttempt>().HasIndex(a => new { a.UserId, a.StartedUtc });
        model.Entity<ResourceClaim>().HasKey(c => new { c.UserId, c.Day, c.Location, c.TileX, c.TileY, c.ItemId });
        model.Entity<FarmCropState>().HasKey(c => new { c.UserId, c.TileX, c.TileY });
        model.Entity<NpcFriendship>().HasKey(f => new { f.UserId, f.NpcName });
        model.Entity<GameUser>().HasIndex(u => u.UsernameKey).IsUnique();
        model.Entity<GameUser>().HasIndex(u => u.EmailKey).IsUnique();
        model.Entity<GameSession>().HasIndex(s => s.TokenHash).IsUnique();
        model.Entity<SessionPresence>().HasKey(p => p.SessionId);
        model.Entity<SessionPresence>().HasIndex(p => p.LastSeenUtc);
        model.Entity<SessionPresence>().HasOne(p => p.Session).WithOne()
            .HasForeignKey<SessionPresence>(p => p.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<GameUser>().HasOne(u => u.Farmer).WithOne(f => f.User).HasForeignKey<FarmerProfile>(f => f.UserId);
        model.Entity<GameSession>().HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);

        model.Entity<PlayerLandPlot>().HasKey(p => p.Id);
        model.Entity<PlayerLandPlot>().HasIndex(p => p.OwnerId);
        model.Entity<PlayerLandPlot>().HasIndex(p => p.SecureLookupHash);
        model.Entity<PlayerLandPlot>().HasIndex(p => new { p.OwnerId, p.FarmLocation, p.TileX, p.TileY }).IsUnique();
        model.Entity<PlayerLandPlot>().HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<TvLandHolding>().HasKey(p => p.Id);
        model.Entity<TvLandHolding>().HasIndex(p => p.OwnerId);
        model.Entity<TvLandHolding>().HasIndex(p => p.ParcelId);
        model.Entity<TvLandParcel>().HasKey(p => p.Id);
        model.Entity<TvMoneyOperation>().HasKey(op => op.RequestId);
        model.Entity<TvLandAuction>().HasKey(a => a.Id);

        model.Entity<CatalogItemEntity>().HasKey(i => i.Id);
        model.Entity<CatalogItemEntity>().HasIndex(i => i.LookupHash);
        model.Entity<CatalogItemEntity>().HasIndex(i => i.Category);

        model.Entity<LandTypeEntity>().HasKey(l => l.Id);
        model.Entity<AdminCheckpoint>().HasKey(c => new { c.UserId, c.Name });
    }
}

public sealed class PlayerState
{
    public Guid UserId { get; set; }
    public long Money { get; set; } = 500;
    public long TotalEarnings { get; set; }
    public long RentDebt { get; set; }
    public int BagSize { get; set; } = 12;
    public string InventoryJson { get; set; } = "[]";
    public string ChestsJson { get; set; } = "[]";
    public string RecentRequestsJson { get; set; } = "[]";
    public string ShippingJson { get; set; } = "[]";
    public string SkillsJson { get; set; } = "[0,0,0,0,0]";
    public string ProgressJson { get; set; } = "{}";
    public int GameDay { get; set; } = 1;
    public int DeepestMineFloor { get; set; }
    // Where the player was when they left (map, position, clock, energy). Written outside the
    // versioned state so frequent saves never conflict with gameplay requests.
    public string SessionJson { get; set; } = "";
    public int Version { get; set; }
}

public sealed class ProcessedGameRequest
{
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FishingAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FishItemId { get; set; } = "";
    public int Size { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedUtc { get; set; }
}

public sealed class ResourceClaim
{
    public Guid UserId { get; set; }
    public string Day { get; set; } = "";
    public string Location { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string ItemId { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class FarmCropState
{
    public Guid UserId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string SeedItemId { get; set; } = "";
    public string HarvestItemId { get; set; } = "";
    public DateTime ReadyUtc { get; set; }
    public int ReadyGameDay { get; set; }
    public int RegrowDays { get; set; }
    public bool HasHarvested { get; set; }
    public int DaysToGrow { get; set; }
    public int GrowthDays { get; set; }
    public int RegrowDaysLeft { get; set; }
    public bool IsWatered { get; set; }
}

public sealed class NpcFriendship
{
    public Guid UserId { get; set; }
    public string NpcName { get; set; } = "";
    public int Points { get; set; }
    public int LastGiftDay { get; set; }
    public int GiftWeek { get; set; } = -1;
    public int GiftsThisWeek { get; set; }
}

public sealed class GameUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string UsernameKey { get; set; } = "";
    public string Email { get; set; } = "";
    public string EmailKey { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public FarmerProfile? Farmer { get; set; }
}

public sealed class GameSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public GameUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}

public sealed class SessionPresence
{
    public Guid SessionId { get; set; }
    public GameSession Session { get; set; } = null!;
    public DateTime LastSeenUtc { get; set; }
}

public sealed class FarmerProfile
{
    public Guid UserId { get; set; }
    public GameUser User { get; set; } = null!;
    public string Name { get; set; } = "";
    public string FarmName { get; set; } = "";
    public string FavoriteThing { get; set; } = "";
    public int Gender { get; set; }
    public int Hair { get; set; }
    public int Skin { get; set; }
    public int Shirt { get; set; }
    public int Pants { get; set; }
    public int FarmType { get; set; }
    public int Pet { get; set; }
    public int HairColor { get; set; }
    public int EyeColor { get; set; }
    public int Accessory { get; set; } = -1;
    public int PetBreed { get; set; }
    public int HairColorRgb { get; set; }
    public int EyeColorRgb { get; set; }
    public int PantsColorRgb { get; set; }
    public int IntroMode { get; set; }

    public FarmerDto ToDto() => new(Name, FarmName, FavoriteThing, Gender, Hair, Skin, Shirt, Pants,
        1, Pet, HairColor, EyeColor, Accessory, PetBreed, HairColorRgb, EyeColorRgb, PantsColorRgb, IntroMode);
    public void CopyFrom(FarmerDto f)
    {
        Name = f.Name.Trim(); FarmName = f.FarmName.Trim(); FavoriteThing = f.FavoriteThing.Trim();
        Gender = f.Gender; Hair = f.Hair; Skin = f.Skin; Shirt = f.Shirt; Pants = f.Pants;
        FarmType = 1; Pet = f.Pet; HairColor = f.HairColor; EyeColor = f.EyeColor;
        Accessory = f.Accessory; PetBreed = f.PetBreed;
        HairColorRgb = f.HairColorRgb; EyeColorRgb = f.EyeColorRgb; PantsColorRgb = f.PantsColorRgb;
        IntroMode = f.IntroMode;
    }
}
