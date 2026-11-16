using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class PrivateCutsceneTests
{
    [Fact]
    public async Task AFarmerWatchingACutsceneIsHiddenFromOthersButKeepsTheirSpot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-cutscene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "cutscene.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "PrivateCutsceneTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var watcher = factory.CreateClient();
            using var passerby = factory.CreateClient();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            await Register(watcher, "watcher_" + suffix);
            await Register(passerby, "passer_" + suffix);

            async Task<JsonElement> Sync(HttpClient client, bool inCutscene)
            {
                var response = await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(52.5f, -26.5f, 0, 1, "Town", inCutscene));
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("players");
            }

            await Sync(watcher, inCutscene: true);
            Assert.Equal(0, (await Sync(passerby, inCutscene: false)).GetArrayLength());
            Assert.True(WorldSessionEndpoints.IsPlayerAtLocation(
                UserId(factory, "watcher_" + suffix), "Town"));

            await Sync(watcher, inCutscene: false);
            Assert.Equal(1, (await Sync(passerby, inCutscene: false)).GetArrayLength());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static Guid UserId(WebApplicationFactory<Program> factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        return db.Users.Single(u => u.Username == username).Id;
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
