using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// ──────────────────────────────────────────────────────────────
// Composition Root (DIP): wires abstractions to implementations.
// No business logic lives here - only service registration and
// middleware pipeline configuration.
// ──────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────
var postgres = DeploymentConfiguration.PostgresConnectionString(builder.Configuration, builder.Environment.IsProduction());
var listenUrl = DeploymentConfiguration.ListenUrl(builder.Configuration);
if (listenUrl != null) builder.WebHost.UseUrls(listenUrl);
var sqlitePath = builder.Configuration["GAME_DB_PATH"] ??
    Path.Combine(builder.Environment.ContentRootPath, "data", "game.db");
if (string.IsNullOrWhiteSpace(postgres))
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);

builder.Services.AddDbContext<GameDb>(options =>
{
    if (string.IsNullOrWhiteSpace(postgres)) options.UseSqlite($"Data Source={sqlitePath}");
    else options.UseNpgsql(postgres);
});

// ── DI: Register services (DIP) ───────────────────────────────
builder.Services.AddSingleton<ISecureIdHasher, SecureIdHasher>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IItemCatalog, JsonItemCatalog>();
builder.Services.AddSingleton<ILandTypeCatalog, LandTypeCatalog>();
builder.Services.AddScoped<ILandPlotService, LandPlotService>();

// ── Rate limiting ──────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
AdminAccess.Configure(app.Configuration);
_ = app.Services.GetRequiredService<IItemCatalog>();
_ = app.Services.GetRequiredService<ILandTypeCatalog>();

// ── Database initialization ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    var catalog = scope.ServiceProvider.GetRequiredService<IItemCatalog>();
    await db.Database.EnsureCreatedAsync();
    await FarmerEndpoints.EnsureFarmerColumns(db);
    await GameStateEndpoints.EnsureStateTable(db);
    await PresenceEndpoints.EnsurePresenceTable(db);
    await LandEndpoints.EnsureLandTables(db);
    await TvEconomyEndpoints.EnsureTables(db);
    await GameStateEndpoints.EnsureAdminTables(db);
    await AdminAccess.SeedAsync(db, app.Configuration, app.Logger);
    await DatabaseSeeder.EnsureTablesAndSeedAsync(db, catalog);
}

app.UseRateLimiter();

// ── Endpoints (SRP: each module maps its own routes) ───────────
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health/ready", async (GameDb db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapAuth();
app.MapPresence();
app.MapFarmer();
app.MapGameState();
app.MapItemEndpoints();
app.MapLandEndpoints();
app.MapTvEconomy();
app.MapWorldSession();
app.MapAdmin();

app.Run();

// ── Shared DTOs and records ────────────────────────────────────
public partial class Program { }
public record ApiError(string Error);
public record RegisterRequest(string? Username, string? Email, string? Password, string? ConfirmPassword);
public record LoginRequest(string? Identity, string? Password);
public record AccountResponse(string Username, string Email, FarmerDto? Farmer);
public record AuthResponse(string Token, string Username, string Email, FarmerDto? Farmer);
public record FarmerDto(string Name, string FarmName, string FavoriteThing, int Gender, int Hair, int Skin,
    int Shirt, int Pants, int FarmType, int Pet, int HairColor, int EyeColor,
    int Accessory = -1, int PetBreed = 0, int HairColorRgb = 0, int EyeColorRgb = 0, int PantsColorRgb = 0,
    int IntroMode = 0);
