using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class FarmRanchTests
{
    [Fact]
    public async Task MarnieChecksFinishedHousingTierAndCapacityAndSavesNamedAnimals()
    {
        string folder = Path.Combine(Path.GetTempPath(), "doan-ranch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(folder, "ranch.db"),
                "FarmRanchTestsSecret_2026_MustBeLongEnough!");
            using var client = factory.CreateClient();
            const string username = "rancher";
            const string password = "Password123!";
            using var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, "rancher@example.com", password, password));
            registration.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registration.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            var initial = await State(client);
            Assert.Empty(initial.RanchHomes);
            Assert.Empty(initial.RanchAnimals);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                string inventory = JsonSerializer.Serialize(new StackEntry[]
                {
                    new() { Slot = 0, ItemId = "wood", Amount = 900 },
                    new() { Slot = 1, ItemId = "stone", Amount = 500 }
                });
                await db.PlayerStates.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 50_000L)
                    .SetProperty(s => s.InventoryJson, inventory));
            }

            await Move("AnimalShop", 12.5f, -15.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Buy("chicken", "Nắng")).StatusCode);
            Assert.Equal(50_000, (await State(client)).Money);
            Assert.Contains((await client.GetFromJsonAsync<ShopOfferList>("/api/game/shop/Marnie"))!.Offers,
                offer => offer.ItemId == "object_178" && offer.Price == 50);
            await Move("Farm", 52.5f, -18.5f);
            (await Soil(clear: false)).EnsureSuccessStatusCode();
            await Move("ScienceHouse", 8.5f, -19.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Build(2)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Build(1)).StatusCode);
            Assert.Equal(50_000, (await State(client)).Money);
            await Move("Farm", 52.5f, -18.5f);
            (await Soil(clear: true)).EnsureSuccessStatusCode();
            await Move("ScienceHouse", 8.5f, -19.5f);
            using var order = await Build(1);
            order.EnsureSuccessStatusCode();
            var ordered = (await order.Content.ReadFromJsonAsync<GameSnapshot>())!;
            var building = Assert.Single(ordered.RanchHomes);
            Assert.Equal((0, 1, 4), (building.Level, building.TargetLevel, building.ReadyDay));
            Assert.Equal(46_000, ordered.Money);
            Assert.Equal(600, Assert.Single(ordered.Inventory, stack => stack.ItemId == "wood").Amount);
            Assert.Equal(400, Assert.Single(ordered.Inventory, stack => stack.ItemId == "stone").Amount);

            await Move("AnimalShop", 12.5f, -15.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await Buy("chicken", "Nắng")).StatusCode);
            await Advance(2, 4);
            Assert.Equal(4, Assert.Single((await State(client)).RanchHomes).Capacity);
            Assert.Equal(HttpStatusCode.BadRequest, (await Buy("duck", "Vịt")).StatusCode);
            var first = new RanchBuyRequest(Guid.NewGuid(), "chicken", "Nắng");
            for (int retry = 0; retry < 2; retry++)
                (await client.PostAsJsonAsync("/api/game/ranch/buy", first)).EnsureSuccessStatusCode();
            Assert.Single((await State(client)).RanchAnimals);
            Assert.Equal(HttpStatusCode.BadRequest, (await Buy("chicken", "nắng")).StatusCode);
            for (int i = 2; i <= 4; i++) (await Buy("chicken", $"Gà {i}")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Buy("chicken", "Gà 5")).StatusCode);
            Assert.Equal(42_800, (await State(client)).Money);

            await Move("ScienceHouse", 8.5f, -19.5f);
            (await Build(2)).EnsureSuccessStatusCode();
            Assert.Equal(4, Assert.Single((await State(client)).RanchHomes).Capacity);
            await Advance(5, 6);
            await Move("AnimalShop", 12.5f, -15.5f);
            (await Buy("duck", "Vịt")).EnsureSuccessStatusCode();

            using var reconnect = factory.CreateClient();
            using var login = await reconnect.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
            login.EnsureSuccessStatusCode();
            reconnect.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            var saved = await State(reconnect);
            Assert.Equal(8, Assert.Single(saved.RanchHomes).Capacity);
            Assert.Equal(5, saved.RanchAnimals.Length);
            Assert.Contains(saved.RanchAnimals, animal => animal.Name == "Nắng" && animal.Species == "chicken");
            Assert.Contains(saved.RanchAnimals, animal => animal.Name == "Vịt" && animal.Species == "duck");
            Assert.Equal(31_600, saved.Money);

            async Task Move(string location, float x, float y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync", new WorldPlayerSyncRequest(x, y, 0, 1, location)))
                    .EnsureSuccessStatusCode();
            Task<HttpResponseMessage> Build(int level) => client.PostAsJsonAsync("/api/game/ranch/build",
                new RanchBuildRequest(Guid.NewGuid(), "Coop", level));
            Task<HttpResponseMessage> Buy(string species, string name) => client.PostAsJsonAsync("/api/game/ranch/buy",
                new RanchBuyRequest(Guid.NewGuid(), species, name));
            Task<HttpResponseMessage> Soil(bool clear) => client.PostAsJsonAsync("/api/game/farm/soil",
                new FarmSoilRequest(Guid.NewGuid(), 52, 18, clear));
            async Task Advance(int firstDay, int lastDay)
            {
                for (int day = firstDay; day <= lastDay; day++)
                    (await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), day)))
                        .EnsureSuccessStatusCode();
            }
            static async Task<GameSnapshot> State(HttpClient connection) =>
                (await connection.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }
}
