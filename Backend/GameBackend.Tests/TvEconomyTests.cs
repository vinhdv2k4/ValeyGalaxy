using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class TvEconomyTests
{
    [Fact]
    public async Task TvPrograms_RankRentReturnAndSettleAnAuction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-tv-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "tv.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "TvEconomyTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var alice = factory.CreateClient();
            using var bob = factory.CreateClient();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            await Register(alice, "alice_" + suffix);
            await Register(bob, "bob_" + suffix);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Assert.Equal(Path.GetFullPath(database), Path.GetFullPath(db.Database.GetDbConnection().DataSource));
            }

            await CreateFarmer(alice, "Alice");
            var initial = await alice.GetFromJsonAsync<JsonElement>("/api/tv/leaderboard");
            var initialEntries = initial.GetProperty("entries");
            Assert.Equal(2, initialEntries.GetArrayLength());
            // Equal scores share a rank, and a missing farmer never leaks the login username.
            Assert.All(initialEntries.EnumerateArray(), entry => Assert.Equal(1, entry.GetProperty("rank").GetInt32()));
            Assert.Contains(initialEntries.EnumerateArray(),
                entry => entry.GetProperty("farmerName").GetString() == "Unnamed farmer");
            Assert.DoesNotContain(initialEntries.EnumerateArray(),
                entry => entry.GetProperty("farmerName").GetString() == "bob_" + suffix);
            await CreateFarmer(bob, "Bob");

            async Task<TvLandResponse> Land() => (await alice.GetFromJsonAsync<TvLandResponse>("/api/tv/land"))!;
            var initialLand = await Land();
            Assert.Equal(600, initialLand.RentPrice);
            Assert.Equal(300, initialLand.BaseFarmRentPrice);
            Assert.Equal(15, initialLand.FarmTileLimit);
            Assert.Equal(15, initialLand.TilesPerParcel);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/tv/land/rent",
                new TvRentRequest(Guid.NewGuid(), "rent-001", 600))).StatusCode);
            Assert.Equal(500, (await Land()).Money);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                await db.PlayerStates.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 3000L));
            }
            async Task<TvLandResponse> Rent(string parcel, long price)
            {
                using var response = await alice.PostAsJsonAsync("/api/tv/land/rent",
                    new TvRentRequest(Guid.NewGuid(), parcel, price));
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<TvLandResponse>())!;
            }
            var first = await Rent("rent-001", 600);
            Assert.Equal(2400, first.Money);
            Assert.Equal(30, first.FarmTileLimit);
            Assert.Equal(900, first.RentPrice);
            Assert.False(Assert.Single(first.Holdings).IsPermanent);
            Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/tv/land/rent",
                new TvRentRequest(Guid.NewGuid(), "rent-002", 600))).StatusCode);
            Assert.Equal(2400, (await Land()).Money);
            var second = await Rent("rent-002", 900);
            Assert.Equal(1500, second.Money);
            Assert.Equal(45, second.FarmTileLimit);
            Assert.Equal(1200, second.RentPrice);
            var third = await Rent("rent-003", 1200);
            Assert.Equal(300, third.Money);
            Assert.Equal(60, third.FarmTileLimit);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/tv/land/rent",
                new TvRentRequest(Guid.NewGuid(), "rent-004"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/tv/land/return",
                new TvReturnRequest("rent-001"))).StatusCode);
            foreach (var holding in third.Holdings)
                (await alice.PostAsJsonAsync("/api/tv/land/return", new TvReturnRequest(holding.ParcelId)))
                    .EnsureSuccessStatusCode();
            var returned = await Land();
            Assert.Equal(300, returned.Money);
            Assert.Equal(15, returned.FarmTileLimit);
            Assert.Equal(600, returned.RentPrice);
            await bob.GetFromJsonAsync<JsonElement>("/api/game/state");

            // Spending changes the wallet, while only earned gold and unpaid base rent change rank.
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var aliceId = await db.Users.Where(u => u.Username == "alice_" + suffix)
                    .Select(u => u.Id).SingleAsync();
                var bobId = await db.Users.Where(u => u.Username == "bob_" + suffix)
                    .Select(u => u.Id).SingleAsync();
                await db.PlayerStates.Where(s => s.UserId == aliceId).ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Money, 100L).SetProperty(s => s.TotalEarnings, 1000L));
                await db.PlayerStates.Where(s => s.UserId == bobId).ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Money, 900L).SetProperty(s => s.TotalEarnings, 900L));
            }
            var earnedLeaders = await alice.GetFromJsonAsync<JsonElement>("/api/tv/leaderboard");
            Assert.Equal("Alice",
                earnedLeaders.GetProperty("entries")[0].GetProperty("farmerName").GetString());
            Assert.Equal(1000, earnedLeaders.GetProperty("entries")[0].GetProperty("score").GetInt64());
            Assert.Equal(2, earnedLeaders.GetProperty("entries")[1].GetProperty("rank").GetInt32());

            Guid dayEightRequest = Guid.Empty;
            for (int day = 2; day <= 8; day++)
            {
                var requestId = Guid.NewGuid();
                if (day == 8) dayEightRequest = requestId;
                Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/game/day/advance",
                    new AdvanceDayRequest(requestId, day))).StatusCode);
            }
            // The rent is taken overnight: Alice's 100g goes, and 200g stays owed.
            var due = await alice.GetFromJsonAsync<JsonElement>("/api/tv/land");
            Assert.Equal(0, due.GetProperty("money").GetInt64());
            Assert.Equal(200, due.GetProperty("rentDebt").GetInt64());
            var morning = (await alice.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
            Assert.Equal(new RentCharge(8, 300, 100, 200), morning.RentCharge);
            Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(dayEightRequest, 8))).StatusCode);
            Assert.Equal(200, (await alice.GetFromJsonAsync<JsonElement>("/api/tv/land"))
                .GetProperty("rentDebt").GetInt64());
            var penalized = await alice.GetFromJsonAsync<JsonElement>("/api/tv/leaderboard");
            Assert.Contains(penalized.GetProperty("entries").EnumerateArray(), entry =>
                entry.GetProperty("farmerName").GetString() == "Alice" &&
                entry.GetProperty("score").GetInt64() == 800L);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/tv/land/rent",
                new TvRentRequest(Guid.NewGuid(), "rent-001"))).StatusCode);
            var firstPayment = new FarmRentPaymentRequest(Guid.NewGuid());
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/game/farm/pay-rent",
                firstPayment)).StatusCode);
            (await alice.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(68.5f, -16.5f, 0, 1, "Farm"))).EnsureSuccessStatusCode();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                await db.PlayerStates.Where(s => s.TotalEarnings == 1000L)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 50L));
            }
            var partial = await alice.PostAsJsonAsync("/api/game/farm/pay-rent", firstPayment);
            partial.EnsureSuccessStatusCode();
            var partialState = (await partial.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(0, partialState.Money);
            Assert.Equal(150, partialState.RentDebt);
            var replayedPayment = await alice.PostAsJsonAsync("/api/game/farm/pay-rent", firstPayment);
            replayedPayment.EnsureSuccessStatusCode();
            Assert.Equal(150, (await replayedPayment.Content.ReadFromJsonAsync<GameSnapshot>())!.RentDebt);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                await db.PlayerStates.Where(s => s.TotalEarnings == 1000L)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 200L));
            }
            var payment = await alice.PostAsJsonAsync("/api/game/farm/pay-rent",
                new FarmRentPaymentRequest(Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
            Assert.Equal(0, (await payment.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("rentDebt").GetInt64());
            var restored = await alice.GetFromJsonAsync<JsonElement>("/api/tv/leaderboard");
            Assert.Equal(1000, restored.GetProperty("entries")[0].GetProperty("score").GetInt64());

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                await db.PlayerStates.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 3000L));
            }
            Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/tv/land-auction/bid",
                new TvBidRequest(Guid.NewGuid(), "auction-001", 1000))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/tv/land-auction/bid",
                new TvBidRequest(Guid.NewGuid(), "auction-001", 1200))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync("/api/tv/land-auction/bid",
                new TvBidRequest(Guid.NewGuid(), "auction-001", 1200))).StatusCode);
            Assert.Equal(3000, (await alice.GetFromJsonAsync<JsonElement>("/api/tv/land"))
                .GetProperty("money").GetInt64());
            Assert.Equal(1800, (await bob.GetFromJsonAsync<JsonElement>("/api/tv/land"))
                .GetProperty("money").GetInt64());

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                await db.TvLandAuctions.ExecuteUpdateAsync(setters =>
                    setters.SetProperty(a => a.EndsAtUtc, DateTime.UtcNow.AddMinutes(-1)));
            }
            var next = await bob.GetFromJsonAsync<JsonElement>("/api/tv/land-auction");
            Assert.Equal(2, next.GetProperty("round").GetInt32());
            var bobLand = await bob.GetFromJsonAsync<JsonElement>("/api/tv/land");
            Assert.True(bobLand.GetProperty("holdings")[0].GetProperty("isPermanent").GetBoolean());
            var leaders = await alice.GetFromJsonAsync<JsonElement>("/api/tv/leaderboard");
            Assert.Equal("Alice", leaders.GetProperty("entries")[0].GetProperty("farmerName").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public async Task FarmSoilLimitTracksRentAndPreservesExistingCrops()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-farm-land-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "farm.db"),
                "FarmLandTestsSecret_2026_MustBeLongEnough!");
            using var client = factory.CreateClient();
            var username = "farm_land_" + Guid.NewGuid().ToString("N")[..8];
            await Register(client, username);
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                var state = await db.PlayerStates.SingleAsync(s => s.UserId == user.Id);
                var progress = PlayerProgress.FromJson(state.ProgressJson);
                for (int x = 0; x < 14; x++)
                    progress.FarmSoil.Add(40 * 513 + x, new FarmSoilTile(x, 40));
                state.ProgressJson = JsonSerializer.Serialize(progress);
                state.Money = 1000;
                await db.SaveChangesAsync();
            }
            async Task Move(float x, float y)
            {
                (await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(x, y, 0, 1, "Farm"))).EnsureSuccessStatusCode();
            }
            Task<HttpResponseMessage> Soil(int x, int y, bool clear = false) =>
                client.PostAsJsonAsync("/api/game/farm/soil", new FarmSoilRequest(Guid.NewGuid(), x, y, clear));
            Task<HttpResponseMessage> Sow(int x, int y) =>
                client.PostAsJsonAsync("/api/game/farm/plant",
                    new PlantFarmCropRequest(Guid.NewGuid(), x, y, 6, "seeds_parsnip"));
            async Task<TvLandResponse> Land() => (await client.GetFromJsonAsync<TvLandResponse>("/api/tv/land"))!;

            await Move(5.5f, -5.5f);
            Assert.Equal(15, (await Land()).FarmTileLimit);
            Assert.Equal(HttpStatusCode.BadRequest, (await Sow(5, 5)).StatusCode);
            (await Soil(5, 5)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Soil(6, 5)).StatusCode);
            Assert.Equal(15, (await Land()).FarmTilesUsed);

            (await client.PostAsJsonAsync("/api/game/farm/water",
                new WaterFarmCropRequest(Guid.NewGuid(), 5, 5))).EnsureSuccessStatusCode();
            var saved = (await client.GetFromJsonAsync<FarmCropListResponse>("/api/game/farm/crops"))!;
            Assert.True(Assert.Single(saved.Soil, tile => tile.TileX == 5 && tile.TileY == 5).IsWatered);
            (await Sow(5, 5)).EnsureSuccessStatusCode();
            Assert.Equal(15, (await Land()).FarmTilesUsed);

            using var rented = await client.PostAsJsonAsync("/api/tv/land/rent",
                new TvRentRequest(Guid.NewGuid(), "rent-001"));
            rented.EnsureSuccessStatusCode();
            Assert.Equal(30, (await rented.Content.ReadFromJsonAsync<TvLandResponse>())!.FarmTileLimit);
            Assert.Equal(30, (await Land()).FarmTileLimit);
            (await Soil(6, 5)).EnsureSuccessStatusCode();

            (await client.PostAsJsonAsync("/api/tv/land/return", new TvReturnRequest("rent-001")))
                .EnsureSuccessStatusCode();
            Assert.Equal(15, (await Land()).FarmTileLimit);
            Assert.Equal(16, (await Land()).FarmTilesUsed);
            Assert.Equal(HttpStatusCode.BadRequest, (await Sow(6, 5)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Soil(5, 5, clear: true)).StatusCode);
            (await Soil(6, 5, clear: true)).EnsureSuccessStatusCode();
            var remaining = (await client.GetFromJsonAsync<FarmCropListResponse>("/api/game/farm/crops"))!;
            Assert.True(Assert.Single(remaining.Crops).IsWatered);
            Assert.Equal(15, (await Land()).FarmTilesUsed);

            // A farmer with no gold overnight keeps the rent as debt, which blocks new soil.
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 0L));
            }
            for (int day = 2; day <= 8; day++)
                (await client.PostAsJsonAsync("/api/game/day/advance",
                    new AdvanceDayRequest(Guid.NewGuid(), day))).EnsureSuccessStatusCode();
            await Move(5.5f, -5.5f);
            Assert.Equal(300, (await Land()).RentDebt);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 1000L));
            }
            var weather = (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!.Weather!;
            if (!DailyEvents.FarmWeather.IsRain(weather.Today))
                (await client.PostAsJsonAsync("/api/game/farm/water",
                    new WaterFarmCropRequest(Guid.NewGuid(), 5, 5))).EnsureSuccessStatusCode();
            await Move(0.5f, -40.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Sow(0, 40)).StatusCode);
            (await Soil(0, 40, clear: true)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Soil(0, 40)).StatusCode);
            await Move(68.5f, -16.5f);
            (await client.PostAsJsonAsync("/api/game/farm/pay-rent", new FarmRentPaymentRequest(Guid.NewGuid())))
                .EnsureSuccessStatusCode();
            await Move(0.5f, -40.5f);
            (await Soil(0, 40)).EnsureSuccessStatusCode();
            (await Sow(0, 40)).EnsureSuccessStatusCode();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static async Task CreateFarmer(HttpClient client, string name)
    {
        var response = await client.PutAsJsonAsync("/api/farmer",
            new FarmerDto(name, name + " Farm", "Rain", 0, 0, 0, 0, 0, 1, 0, 0, 0));
        response.EnsureSuccessStatusCode();
    }

    private static async Task Register(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            username, username + "@example.com", "Password123!", "Password123!"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            json.GetProperty("token").GetString());
    }
}
