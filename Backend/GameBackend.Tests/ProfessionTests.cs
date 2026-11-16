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
public sealed class ProfessionTests
{
    [Fact]
    public async Task ProfessionsAreSavedOnTheAccountAndRaiseSalePrices()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-profession-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        try
        {
            var database = Path.Combine(directory, "profession.db");
            Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GAME_DB_PATH"] = database,
                        ["SECURITY_HASH_SECRET"] = "ProfessionTestsSecret_2026_MustBeLongEnough!"
                    }));
            });
            using var client = factory.CreateClient();
            string username = "farmer_" + Guid.NewGuid().ToString("N")[..8];
            var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                username, username + "@example.com", "Password123!", "Password123!"));
            registered.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");

            Task<HttpResponseMessage> Choose(int skill, int level, int profession) =>
                client.PostAsJsonAsync("/api/game/profession",
                    new ProfessionRequest(Guid.NewGuid(), skill, level, profession));

            // Not at Farming 5 yet.
            Assert.Equal(HttpStatusCode.BadRequest, (await Choose(0, 5, 1)).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                await db.PlayerStates.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.SkillsJson, "[2150,0,0,0,0]")
                    .SetProperty(s => s.InventoryJson, """[{"Slot":0,"ItemId":"parsnip","Amount":10}]"""));
            }

            // Level 10 needs the level 5 choice first, and only its own branch is offered.
            Assert.Equal(HttpStatusCode.BadRequest, (await Choose(0, 10, 4)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Choose(0, 5, 6)).StatusCode);
            var tiller = await Choose(0, 5, 1);
            tiller.EnsureSuccessStatusCode();
            var tillerState = (await tiller.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Equal(new[] { 1 }, tillerState.Professions);
            // Choosing again for the same level is refused unless it is the same choice.
            Assert.Equal(HttpStatusCode.BadRequest, (await Choose(0, 5, 0)).StatusCode);
            (await Choose(0, 5, 1)).EnsureSuccessStatusCode();

            // Tiller: crops sell for 10% more (Parsnip 35g -> 38g).
            var shipped = await client.PostAsJsonAsync("/api/game/shipping/deposit",
                new SellRequest(Guid.NewGuid(), 0, "parsnip", 10));
            shipped.EnsureSuccessStatusCode();
            var entry = Assert.Single((await shipped.Content.ReadFromJsonAsync<GameSnapshot>())!.Shipping);
            Assert.Equal(38, entry.UnitPrice);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
