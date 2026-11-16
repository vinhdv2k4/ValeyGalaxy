using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

[Collection("Database")]
public sealed class LandAndItemHashSecurityTests
{
    private sealed class DummyHostEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "GameBackend";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void ProductionRequiresPrivatePlotSigningSecret()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SECURITY_HASH_SECRET"] = "" }).Build();
        var environment = new DummyHostEnv { EnvironmentName = "Production" };
        Assert.Throws<InvalidOperationException>(() => new SecureIdHasher(configuration, environment));
    }

    [Fact]
    public void CatalogItemsAndTools_IndexedWithFastO1HashLookup()
    {
        var root = FindProjectRoot();
        var env = new DummyHostEnv { ContentRootPath = Path.Combine(root, "Backend", "GameBackend") };
        var config = new ConfigurationBuilder().Build();
        var hasher = new SecureIdHasher(config);
        var catalog = new JsonItemCatalog(env, hasher);

        // 1. Direct O(1) item lookup
        Assert.True(catalog.TryGet("parsnip", out var parsnip));
        Assert.Equal("parsnip", parsnip.Id);
        Assert.Equal("Crop", parsnip.Category);
        Assert.False(parsnip.IsTool);
        Assert.NotEmpty(parsnip.LookupHash);

        // 2. Direct O(1) tool lookup
        Assert.True(catalog.TryGetTool("axe", out var axe));
        Assert.Equal("axe", axe.Id);
        Assert.Equal("Chop", axe.ActionKind);
        Assert.True(catalog.TryGet("axe", out var axeItem));
        Assert.True(axeItem.IsTool);

        // 3. Fast lookup by hash
        Assert.True(catalog.TryGetByHash(parsnip.LookupHash, out var retrievedByHash));
        Assert.Equal(parsnip.Id, retrievedByHash.Id);

        // 4. Non-existent items return false
        Assert.False(catalog.TryGet("non_existent_item", out _));
        Assert.False(catalog.TryGetTool("non_existent_tool", out _));
        Assert.False(catalog.TryGetByHash("invalid_hash", out _));
    }

    [Fact]
    public void LandTypeCatalog_HasSupportedSoilTypesWithO1Lookup()
    {
        var catalog = new LandTypeCatalog();

        Assert.True(catalog.TryGet("normal", out var normal));
        Assert.Equal("Đất thường", normal.Name);
        Assert.Equal(1.0, normal.GrowthSpeedMultiplier);

        Assert.True(catalog.TryGet("fertile", out var fertile));
        Assert.Equal("Đất màu mỡ", fertile.Name);
        Assert.Equal(1.25, fertile.GrowthSpeedMultiplier);

        Assert.True(catalog.TryGet("greenhouse", out var greenhouse));
        Assert.True(greenhouse.AllowsAllSeasons);

        Assert.False(catalog.TryGet("volcanic_lava", out _));
    }

    [Fact]
    public void SecureIdHasher_PreventsIDORAndTampering()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SECURITY_HASH_SECRET"] = "MySuperSecretKeyForTesting123456789!"
            })
            .Build();
        var hasher = new SecureIdHasher(config);

        var plotId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        // 1. Generate Secure Hashed ID for owner
        var secureId = hasher.GenerateSecurePlotId(plotId, ownerId);
        Assert.NotEmpty(secureId);
        Assert.Contains('.', secureId);

        // 2. Owner can decode and verify their own plot ID
        var valid = hasher.TryDecodeAndVerifyPlotId(secureId, ownerId, out var decodedPlotId);
        Assert.True(valid);
        Assert.Equal(plotId, decodedPlotId);

        // 3. Anti-IDOR: Attacker cannot decode/verify with their user ID
        var attackerAttempt = hasher.TryDecodeAndVerifyPlotId(secureId, attackerId, out var attackerDecoded);
        Assert.False(attackerAttempt, "Attacker should NEVER be able to verify another player's secure ID.");
        Assert.Equal(Guid.Empty, attackerDecoded);

        // 4. Anti-Tampering: Modifying any character invalidates the signature
        var tampered = secureId[..10] + (secureId[10] == 'A' ? 'B' : 'A') + secureId[11..];
        var tamperedAttempt = hasher.TryDecodeAndVerifyPlotId(tampered, ownerId, out _);
        Assert.False(tamperedAttempt, "Tampered signature must be rejected immediately.");

        // 5. Lookup Hash is 64 hex characters
        var lookupHash = hasher.ComputePlotLookupHash(plotId, ownerId);
        Assert.Equal(64, lookupHash.Length);
    }

    [Fact]
    public async Task EndToEndApi_ItemsToolsAndPlayerPlotsWithSecurity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-land-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "land_test.db");

        try
        {
            using var factory = CreateServer(database);
            using var clientA = factory.CreateClient();
            using var clientB = factory.CreateClient();

            // 1. Test public catalog items & tools
            var itemsResponse = await clientA.GetAsync("/api/catalog/items");
            Assert.Equal(HttpStatusCode.OK, itemsResponse.StatusCode);

            var axeResponse = await clientA.GetAsync("/api/catalog/tools/axe");
            Assert.Equal(HttpStatusCode.OK, axeResponse.StatusCode);
            var axeJson = await axeResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("axe", axeJson.GetProperty("id").GetString());

            var landTypesResponse = await clientA.GetAsync("/api/lands/types");
            Assert.Equal(HttpStatusCode.OK, landTypesResponse.StatusCode);

            // 2. Register Player A and Player B with unique credentials
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var tokenA = await RegisterAndGetToken(clientA, $"farmer_alice_{suffix}", $"alice_{suffix}@example.com");
            var tokenB = await RegisterAndGetToken(clientB, $"farmer_bob_{suffix}", $"bob_{suffix}@example.com");

            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            // 3. Player A claims a fertile plot at (10, 20)
            var claimResponse = await clientA.PostAsJsonAsync("/api/lands/claim", new ClaimPlotRequest("fertile", "farm_main", 10, 20));
            Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
            var plotJson = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
            var secureId = plotJson.GetProperty("secureId").GetString()!;
            Assert.NotEmpty(secureId);
            Assert.Equal("fertile", plotJson.GetProperty("landTypeId").GetString());

            // 4. Player A queries their plot by secureId (Fast O(1) hash lookup) -> Success
            var getPlotA = await clientA.GetAsync($"/api/lands/{secureId}");
            Assert.Equal(HttpStatusCode.OK, getPlotA.StatusCode);

            // 5. Anti-IDOR: Player B tries to query Player A's plot using Player A's secureId -> 404 NotFound
            var getPlotB = await clientB.GetAsync($"/api/lands/{secureId}");
            Assert.Equal(HttpStatusCode.NotFound, getPlotB.StatusCode);

            // 6. Player A tills the plot using "hoe"
            var tillResponse = await clientA.PostAsJsonAsync($"/api/lands/{secureId}/till", new TillPlotRequest("hoe"));
            Assert.Equal(HttpStatusCode.OK, tillResponse.StatusCode);
            var tilledJson = await tillResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(tilledJson.GetProperty("isTilled").GetBoolean());

            // 7. Player A waters the plot using "watering_can"
            var waterResponse = await clientA.PostAsJsonAsync($"/api/lands/{secureId}/water", new WaterPlotRequest("watering_can"));
            Assert.Equal(HttpStatusCode.OK, waterResponse.StatusCode);
            var wateredJson = await waterResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(wateredJson.GetProperty("isWatered").GetBoolean());

            // 8. Player A plants "seeds_parsnip"
            var plantResponse = await clientA.PostAsJsonAsync($"/api/lands/{secureId}/plant", new PlantCropRequest("seeds_parsnip"));
            Assert.Equal(HttpStatusCode.OK, plantResponse.StatusCode);
            var plantedJson = await plantResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("seeds_parsnip", plantedJson.GetProperty("plantedItemId").GetString());
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public async Task WorldMultiplayerPresence_SyncsMultiplePlayersAutomatically()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var directory = Path.Combine(Path.GetTempPath(), "world_test_" + suffix);
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "game.db");

        try
        {
            using var factory = CreateServer(dbPath);
            using var clientA = factory.CreateClient();
            using var clientB = factory.CreateClient();

            // 1. Session is active by default without manual host action
            var sessionRes = await clientA.GetAsync("/api/world/session");
            sessionRes.EnsureSuccessStatusCode();
            var sessionJson = await sessionRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(sessionJson.GetProperty("isActive").GetBoolean());
            Assert.Equal(16, sessionJson.GetProperty("maxPlayers").GetInt32());

            // A public host heartbeat is disabled unless the server owner configures a secret.
            var hostHeartbeat = await clientA.PostAsJsonAsync("/api/world/host-heartbeat",
                new HostHeartbeatRequest("guess", true, null, "127.0.0.1", 7777, "Test", 16));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, hostHeartbeat.StatusCode);

            // 2. Register Player A and Player B
            var tokenA = await RegisterAndGetToken(clientA, $"player_a_{suffix}", $"player_a_{suffix}@test.com");
            var tokenB = await RegisterAndGetToken(clientB, $"player_b_{suffix}", $"player_b_{suffix}@test.com");

            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            var invalidLocation = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(0f, 0f, 0f, -1f, "../Farm"));
            Assert.Equal(HttpStatusCode.BadRequest, invalidLocation.StatusCode);

            // 3. Public maps are shared between accounts.
            var syncResA = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(15.5f, -10.0f, 0f, -1f, "Town"));
            syncResA.EnsureSuccessStatusCode();
            var syncJsonA = await syncResA.Content.ReadFromJsonAsync<JsonElement>();
            // No other players yet
            Assert.Empty(syncJsonA.GetProperty("players").EnumerateArray());

            // 4. Player B sees Player A in Town.
            var syncResB = await clientB.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(20.0f, -8.5f, 1f, 0f, "Town"));
            syncResB.EnsureSuccessStatusCode();
            var syncJsonB = await syncResB.Content.ReadFromJsonAsync<JsonElement>();
            // Player B should see Player A!
            var playersSeenByB = syncJsonB.GetProperty("players").EnumerateArray().ToList();
            Assert.Single(playersSeenByB);
            Assert.Equal($"player_a_{suffix}", playersSeenByB[0].GetProperty("username").GetString());
            Assert.Equal(15.5f, playersSeenByB[0].GetProperty("x").GetSingle(), 0.01f);
            Assert.Equal(-10.0f, playersSeenByB[0].GetProperty("y").GetSingle(), 0.01f);

            // 5. Player A syncs again and now sees Player B!
            var syncResA2 = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(16.0f, -10.0f, 1f, 0f, "Town"));
            syncResA2.EnsureSuccessStatusCode();
            var syncJsonA2 = await syncResA2.Content.ReadFromJsonAsync<JsonElement>();
            var playersSeenByA = syncJsonA2.GetProperty("players").EnumerateArray().ToList();
            Assert.Single(playersSeenByA);
            Assert.Equal($"player_b_{suffix}", playersSeenByA[0].GetProperty("username").GetString());
            Assert.Equal(20.0f, playersSeenByA[0].GetProperty("x").GetSingle(), 0.01f);

            // Private maps use the signed-in account as their implicit instance key.
            // Two players reporting the same map name must never receive each other.
            foreach (var privateMap in new[] { "Farm", "FarmHouse", "FarmCave", "Mine", "1", "SkullCave" })
            {
                var a = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(2f, 3f, 0f, -1f, privateMap));
                a.EnsureSuccessStatusCode();
                var b = await clientB.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(2f, 3f, 0f, -1f, privateMap));
                b.EnsureSuccessStatusCode();
                Assert.Empty((await a.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("players").EnumerateArray());
                Assert.Empty((await b.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("players").EnumerateArray());
            }

            var farmSync = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(16f, -10f, 1f, 0f, "Farm"));
            farmSync.EnsureSuccessStatusCode();
            var farmJson = await farmSync.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Empty(farmJson.GetProperty("players").EnumerateArray());

            // 6. Player B leaves
            var leaveResB = await clientB.PostAsync("/api/world/presence/leave", null);
            leaveResB.EnsureSuccessStatusCode();

            // 7. Player A syncs again and Player B is immediately gone
            var syncResA3 = await clientA.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(16.0f, -10.0f, 1f, 0f, "Farm"));
            var syncJsonA3 = await syncResA3.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Empty(syncJsonA3.GetProperty("players").EnumerateArray());
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }


    private static WebApplicationFactory<Program> CreateServer(string databasePath) =>
        DatabaseTestServer.Create(databasePath, "IntegrationTestSecretKey_2026_MustBeLongAndSafe!",
            builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["WORLD_HOST_SECRET"] = "" })));

    private static async Task<string> RegisterAndGetToken(HttpClient client, string username, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, email, "Password123!", "Password123!"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null;
             directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "Backend", "GameBackend")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Project root not found.");
    }
}
