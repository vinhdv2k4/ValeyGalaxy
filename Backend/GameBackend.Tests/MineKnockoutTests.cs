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
public sealed class MineKnockoutTests
{
    [Fact]
    public async Task KnockoutInTheMinesAndPassingOutAwayFromHomeCostGold()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-knockout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "knockout.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "MineKnockoutTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var client = factory.CreateClient();
            string username = "miner_" + Guid.NewGuid().ToString("N")[..8];
            var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                username, username + "@example.com", "Password123!", "Password123!"));
            registered.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            (await client.PutAsJsonAsync("/api/farmer",
                new FarmerDto("Miner", "Miner Farm", "Rain", 0, 0, 0, 0, 0, 1, 0, 0, 0))).EnsureSuccessStatusCode();
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 40_000L));
            }

            // Away from the floor the server refuses it.
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(5f, -5f, 0, 1, "Farm"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/mine/knockout",
                new MineKnockoutRequest(Guid.NewGuid(), 3))).StatusCode);

            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(5f, -5f, 0, 1, "3"))).EnsureSuccessStatusCode();
            var before = (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
            var knockout = new MineKnockoutRequest(Guid.NewGuid(), 3);
            var response = await client.PostAsJsonAsync("/api/game/mine/knockout", knockout);
            response.EnsureSuccessStatusCode();
            var after = (await response.Content.ReadFromJsonAsync<GameSnapshot>())!;
            long lost = before.Money - after.Money;
            // A twentieth to a quarter of 40 000g, capped at 5000g and rounded down to 100g.
            Assert.InRange(lost, 2000, 5000);
            Assert.Equal(0, lost % 100);
            // Tools are never lost.
            foreach (var tool in before.Inventory.Where(s => s.ItemId is "axe" or "hoe" or "pickaxe" or "watering_can" or "scythe"))
                Assert.Contains(after.Inventory, s => s.ItemId == tool.ItemId);
            // A retried request is not charged twice.
            var replay = await client.PostAsJsonAsync("/api/game/mine/knockout", knockout);
            replay.EnsureSuccessStatusCode();
            Assert.Equal(after.Money, (await replay.Content.ReadFromJsonAsync<GameSnapshot>())!.Money);

            // Passing out at 2 AM away from home costs a tenth of the gold, at most 1000g.
            var night = await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 2, "3"));
            night.EnsureSuccessStatusCode();
            var morning = (await night.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(after.Money - 1000, morning.Money);
            Assert.Equal(new PassOutCharge(2, 1000, "3"), morning.PassOutCharge);

            // Passing out at home costs nothing.
            var home = await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 3, "FarmHouse"));
            home.EnsureSuccessStatusCode();
            var homeMorning = (await home.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(morning.Money, homeMorning.Money);
            Assert.Null(homeMorning.PassOutCharge);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
