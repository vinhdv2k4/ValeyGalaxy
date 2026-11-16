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
public sealed class JojaRouteTests
{
    [Fact]
    public async Task MembershipReplacesBundlesAndTheFormFinishesRoomsForGold()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-joja-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "joja.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "JojaRouteTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var client = factory.CreateClient();
            string username = "member_" + Guid.NewGuid().ToString("N")[..8];
            var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                username, username + "@example.com", "Password123!", "Password123!"));
            registered.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 60_000L));
            }
            async Task Stand(string location, float x, float y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(x, y, 0, 1, location))).EnsureSuccessStatusCode();
            Task<HttpResponseMessage> Post(string path, int project = -1) =>
                client.PostAsJsonAsync(path, new JojaRequest(Guid.NewGuid(), project));

            // Morris's desk is at (21, 25) in JojaMart.
            await Stand("Town", 5f, -5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/game/joja/join")).StatusCode);
            await Stand("JojaMart", 21.5f, -24.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/game/joja/project", 1)).StatusCode);
            var joined = await Post("/api/game/joja/join");
            joined.EnsureSuccessStatusCode();
            var member = (await joined.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(55_000, member.Money);
            Assert.Contains("JojaMember", member.Milestones.Seen);

            // Minecarts (boiler room) costs 15,000g and finishes that room the Joja way.
            var minecarts = await Post("/api/game/joja/project", 1);
            minecarts.EnsureSuccessStatusCode();
            var bought = (await minecarts.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(40_000, bought.Money);
            Assert.Contains(bought.Milestones.Rooms, room => room.Id == "boiler" && room.Done);
            Assert.Contains("joja_boiler", bought.Milestones.Seen);
            // Buying it again costs nothing; the Bus (40,000g) is still affordable, the next one is not.
            Assert.Equal(40_000, (await (await Post("/api/game/joja/project", 1)).Content
                .ReadFromJsonAsync<GameSnapshot>())!.Money);
            (await Post("/api/game/joja/project", 0)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/game/joja/project", 3)).StatusCode);

            // Members no longer donate to the Junimo bundles.
            await Stand("CommunityCenter", 30.5f, -17.5f);
            var donate = await client.PostAsJsonAsync("/api/game/bundles/donate",
                new BundleRequest(Guid.NewGuid(), 0, 0));
            Assert.Equal(HttpStatusCode.BadRequest, donate.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
