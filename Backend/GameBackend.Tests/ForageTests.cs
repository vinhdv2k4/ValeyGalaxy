using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyEvents;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class ForageTests
{
    [Fact]
    public void ForageIsTheSameEverywhereAndFollowsTheSeason()
    {
        var tiles = ForageSpawns.ParseTiles(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "ForageTiles.txt")));
        var spring = ForageSpawns.ForDay(tiles, "Town", 5);
        Assert.InRange(spring.Count, 3, 6);
        Assert.Equal(spring, ForageSpawns.ForDay(tiles, "Town", 5));
        Assert.All(spring, spot => Assert.Contains(spot.ItemId, new[] { "object_16", "object_18", "object_20", "object_22" }));
        Assert.All(ForageSpawns.ForDay(tiles, "Town", 60), spot =>
            Assert.Contains(spot.ItemId, new[] { "object_404", "object_406", "object_408", "object_410" }));
        Assert.Empty(ForageSpawns.ForDay(tiles, "Saloon", 5));
    }

    [Fact]
    public async Task ForageAndBlackberriesAreGrantedByTheServer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-forage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "forage.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "ForageTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var client = factory.CreateClient();
            string username = "forager_" + Guid.NewGuid().ToString("N")[..8];
            var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                username, username + "@example.com", "Password123!", "Password123!"));
            registered.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            async Task SetDay(int day)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.GameDay, day).SetProperty(s => s.InventoryJson, "[]"));
            }
            async Task Stand(string location, int x, int y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(x + 0.5f, 0.5f - y, 0, 1, location))).EnsureSuccessStatusCode();

            await SetDay(6);
            var tiles = ForageSpawns.ParseTiles(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "ForageTiles.txt")));
            var spot = ForageSpawns.ForDay(tiles, "Town", 6)[0];
            Task<HttpResponseMessage> Pick(int x, int y) => client.PostAsJsonAsync("/api/game/forage/pick",
                new ForagePickRequest(Guid.NewGuid(), "Town", x, y));

            // Too far away, and an empty tile, are refused.
            await Stand("Town", spot.X + 10, spot.Y);
            Assert.Equal(HttpStatusCode.BadRequest, (await Pick(spot.X, spot.Y)).StatusCode);
            await Stand("Town", spot.X, spot.Y + 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await Pick(spot.X + 200, spot.Y)).StatusCode);
            var picked = await Pick(spot.X, spot.Y);
            picked.EnsureSuccessStatusCode();
            var state = (await picked.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Contains(state.Inventory, stack => stack.ItemId.StartsWith(spot.ItemId, StringComparison.Ordinal));
            Assert.Equal(7, state.SkillExperience[2]);
            Assert.Contains($"6/Town/{spot.X}/{spot.Y}", state.ForagePicked);
            // It is gone once picked.
            var again = (await (await Pick(spot.X, spot.Y)).Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(7, again.SkillExperience[2]);

            // Fall 8-11: a medium bush in the Forest gives Blackberries.
            await SetDay(64);
            await Stand("Forest", 40, 30);
            var berries = await client.PostAsJsonAsync("/api/game/events/action", new DailyEventRequest(
                Guid.NewGuid(), "blackberry_season", "forage", 64, "Forest", X: 40, Y: 30));
            Assert.True(berries.IsSuccessStatusCode, await berries.Content.ReadAsStringAsync());
            Assert.Contains((await berries.Content.ReadFromJsonAsync<GameSnapshot>())!.Inventory,
                stack => stack.ItemId == "object_410");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
