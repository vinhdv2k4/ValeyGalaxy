using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class AccountFlowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StartupArrivalPersistsWithoutAdvancingDay(int introMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-arrival-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", Path.Combine(directory, "accounts.db"));
        try
        {
            using var factory = CreateServer();
            using var client = factory.CreateClient();
            await Register(client, "arrival", "arrival@example.com");
            // A state fetched before character creation may still carry the old demo kit.
            var before = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Contains(before.GetProperty("inventory").EnumerateArray(),
                slot => slot.GetProperty("itemId").GetString() == "wood");
            var farmer = new FarmerDto("Farmer", "River", "Rain", 0, 0, 0, 0, 0, 1, 0, 0, 0,
                IntroMode: introMode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/farmer", farmer)).StatusCode);

            var arrival = new MilestoneRequest(Guid.NewGuid(), "complete", "60367", 1, 600, "FarmHouse");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/milestones",
                arrival with { RequestId = Guid.NewGuid(), Location = "BusStop" })).StatusCode);
            var expected = introMode == 0 ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/game/milestones", arrival)).StatusCode);
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/game/milestones", arrival)).StatusCode);

            using var nextSession = factory.CreateClient();
            await Login(nextSession, "arrival", "StrongPass123");
            var account = await nextSession.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(introMode, account.GetProperty("farmer").GetProperty("introMode").GetInt32());
            var state = await nextSession.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Equal(1, state.GetProperty("gameDay").GetInt32());
            Assert.Equal(introMode != 0, state.GetProperty("milestones").GetProperty("seen")
                .EnumerateArray().Any(id => id.GetString() == "60367"));
            if (introMode != 0)
            {
                Assert.Equal(12, state.GetProperty("bagSize").GetInt32());
                var tools = state.GetProperty("inventory").EnumerateArray().ToArray();
                Assert.Equal(new[] { "axe", "hoe", "watering_can", "pickaxe", "scythe" },
                    tools.OrderBy(slot => slot.GetProperty("slot").GetInt32())
                        .Select(slot => slot.GetProperty("itemId").GetString()));
                Assert.All(tools, slot => Assert.Equal(1, slot.GetProperty("amount").GetInt32()));

                int expectedLimit = 15;
                if (introMode == 2)
                {
                    using var scope = factory.Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                    await db.PlayerStates.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Money, 600L));
                    using var rental = await nextSession.PostAsJsonAsync("/api/tv/land/rent",
                        new TvRentRequest(Guid.NewGuid(), "rent-001"));
                    rental.EnsureSuccessStatusCode();
                    expectedLimit = 30;
                }
                var gift = new MilestoneRequest(Guid.NewGuid(), "claim", "starter_gift", 1, 600, "FarmHouse");
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await nextSession.PostAsJsonAsync("/api/game/milestones", gift)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await nextSession.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(4.5f, -6.5f, 0f, 1f, "FarmHouse"))).StatusCode);
                using var claim = await nextSession.PostAsJsonAsync("/api/game/milestones", gift);
                claim.EnsureSuccessStatusCode();
                Assert.Equal(expectedLimit, (await claim.Content.ReadFromJsonAsync<GameSnapshot>())!.FarmTileLimit);
                using var replay = await nextSession.PostAsJsonAsync("/api/game/milestones", gift);
                replay.EnsureSuccessStatusCode();
                Assert.Equal(expectedLimit, (await replay.Content.ReadFromJsonAsync<GameSnapshot>())!.FarmTileLimit);
                Assert.Equal(HttpStatusCode.OK, (await nextSession.PostAsJsonAsync("/api/game/milestones",
                    gift with { RequestId = Guid.NewGuid() })).StatusCode);

                var afterGift = await nextSession.GetFromJsonAsync<JsonElement>("/api/game/state");
                var seeds = afterGift.GetProperty("inventory").EnumerateArray()
                    .Single(slot => slot.GetProperty("itemId").GetString() == "seeds_parsnip");
                Assert.Equal(15, seeds.GetProperty("amount").GetInt32());
                Assert.Contains(afterGift.GetProperty("milestones").GetProperty("seen").EnumerateArray(),
                    id => id.GetString() == "starter_gift");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MineGemPickupRequiresMineProximityAndCannotBeCollectedTwice()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-mine-loot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", Path.Combine(directory, "accounts.db"));
        try
        {
            using var factory = CreateServer();
            using var client = factory.CreateClient();
            await Register(client, "mine_loot", "mine_loot@example.com");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/game/inventory/discard",
                new { requestId = Guid.NewGuid(), slot = 11, itemId = "wood" })).StatusCode);
            var distant = new { requestId = Guid.NewGuid(), location = "1", tileX = 10, tileY = 10,
                itemId = "object_72", amount = 1 };
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/game/resources/collect", distant)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(10.5f, -10.5f, 0f, -1f, "1"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/resources/collect",
                new { requestId = Guid.NewGuid(), location = "Farm", tileX = 10, tileY = 10,
                    itemId = "object_72", amount = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/game/resources/collect", distant)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/game/resources/collect", distant)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/resources/collect",
                new { requestId = Guid.NewGuid(), location = "1", tileX = 10, tileY = 10,
                    itemId = "object_72", amount = 1 })).StatusCode);
            var state = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Equal(1, state.GetProperty("inventory").EnumerateArray()
                .Single(slot => slot.GetProperty("itemId").GetString() == "object_72").GetProperty("amount").GetInt32());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("coal")]
    [InlineData("gold_ore")]
    public async Task MineMaterialPickupPreservesCatalogId(string itemId)
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-mine-material-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", Path.Combine(directory, "accounts.db"));
        try
        {
            using var factory = CreateServer();
            using var client = factory.CreateClient();
            await Register(client, "mine_material", "mine_material@example.com");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/game/inventory/discard",
                new { requestId = Guid.NewGuid(), slot = 11, itemId = "wood" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(10.5f, -10.5f, 0f, -1f, "81"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/game/resources/collect",
                new { requestId = Guid.NewGuid(), location = "81", tileX = 10, tileY = 10, itemId, amount = 2 })).StatusCode);

            var state = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Equal(2, state.GetProperty("inventory").EnumerateArray()
                .Single(slot => slot.GetProperty("itemId").GetString() == itemId).GetProperty("amount").GetInt32());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FarmerCanOnlyBeCreatedOncePerAccount()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-farmer-once-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "accounts.db");
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
        try
        {
            using var factory = CreateServer();
            using var firstSession = factory.CreateClient();
            await Register(firstSession, "one_farmer", "one_farmer@example.com");
            var original = new { name = "Original", farmName = "Original Farm", favoriteThing = "Rain",
                gender = 0, hair = 0, skin = 0, shirt = 0, pants = 0, farmType = 1,
                pet = 0, petBreed = 0, accessory = -1, hairColor = 0, eyeColor = 0,
                hairColorRgb = 0, eyeColorRgb = 0, pantsColorRgb = 0 };
            var replacement = new { name = "Replacement", farmName = "Other Farm", favoriteThing = "Sun",
                gender = 1, hair = 16, skin = 1, shirt = 1, pants = 1, farmType = 1,
                pet = 1, petBreed = 1, accessory = 0, hairColor = 0, eyeColor = 0,
                hairColorRgb = 0, eyeColorRgb = 0, pantsColorRgb = 0 };

            Assert.Equal(HttpStatusCode.OK,
                (await firstSession.PutAsJsonAsync("/api/farmer", original)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await firstSession.PutAsJsonAsync("/api/farmer", replacement)).StatusCode);

            using var secondSession = factory.CreateClient();
            await Login(secondSession, "one_farmer", "StrongPass123");
            var account = await secondSession.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal("Original", account.GetProperty("farmer").GetProperty("name").GetString());
            Assert.Equal("Original Farm", account.GetProperty("farmer").GetProperty("farmName").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NewAccountDoesNotInheritAnotherAccountsFarmer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-farmer-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "accounts.db");
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
        try
        {
            using var factory = CreateServer();
            using var first = factory.CreateClient();
            using var second = factory.CreateClient();
            await Register(first, "isolated_first", "isolated_first@example.com");
            var farmer = new { name = "First", farmName = "First Farm", favoriteThing = "Rain",
                gender = 0, hair = 0, skin = 0, shirt = 0, pants = 0, farmType = 1,
                pet = 0, petBreed = 0, accessory = -1, hairColor = 0, eyeColor = 0,
                hairColorRgb = 0, eyeColorRgb = 0, pantsColorRgb = 0 };
            Assert.Equal(HttpStatusCode.OK, (await first.PutAsJsonAsync("/api/farmer", farmer)).StatusCode);

            var newRegistration = await second.PostAsJsonAsync("/api/auth/register", new
            {
                username = "isolated_second", email = "isolated_second@example.com",
                password = "StrongPass123", confirmPassword = "StrongPass123"
            });
            Assert.Equal(HttpStatusCode.OK, newRegistration.StatusCode);
            var registration = await newRegistration.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, registration.GetProperty("farmer").ValueKind);
            second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", registration.GetProperty("token").GetString());
            var secondAccount = await second.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal("isolated_second", secondAccount.GetProperty("username").GetString());
            Assert.Equal(JsonValueKind.Null, secondAccount.GetProperty("farmer").ValueKind);

            var firstAccount = await first.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal("First", firstAccount.GetProperty("farmer").GetProperty("name").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task TwoClientsSharePresenceAndLogoutRemovesOneSession()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-presence-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "accounts.db");
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
        try
        {
            using (var factory = CreateServer())
            using (var first = factory.CreateClient())
            using (var second = factory.CreateClient())
            {
                Assert.Equal(HttpStatusCode.OK,
                    (await first.GetAsync("/api/health/ready")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await first.PostAsync("/api/presence/heartbeat", null)).StatusCode);
                await Register(first, "presence_a", "presence_a@example.com");
                await Register(second, "presence_b", "presence_b@example.com");

                var firstHeartbeat = await first.PostAsync("/api/presence/heartbeat", null);
                Assert.Equal(HttpStatusCode.OK, firstHeartbeat.StatusCode);
                Assert.Equal(1, (await firstHeartbeat.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("onlinePlayers").GetInt32());

                var secondHeartbeat = await second.PostAsync("/api/presence/heartbeat", null);
                Assert.Equal(HttpStatusCode.OK, secondHeartbeat.StatusCode);
                Assert.Equal(2, (await secondHeartbeat.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("onlinePlayers").GetInt32());
                Assert.Equal(2, (await first.GetFromJsonAsync<JsonElement>("/api/presence/online"))
                    .GetProperty("onlinePlayers").GetInt32());

                await using (var connection = new SqliteConnection("Data Source=" + database))
                {
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "UPDATE SessionPresences SET LastSeenUtc = '2000-01-01 00:00:00' " +
                        "WHERE SessionId = (SELECT Id FROM Sessions WHERE UserId = " +
                        "(SELECT Id FROM Users WHERE Username = 'presence_a'))";
                    Assert.Equal(1, await command.ExecuteNonQueryAsync());
                }
                Assert.Equal(1, (await second.GetFromJsonAsync<JsonElement>("/api/presence/online"))
                    .GetProperty("onlinePlayers").GetInt32());
                Assert.Equal(HttpStatusCode.OK,
                    (await first.PostAsync("/api/presence/heartbeat", null)).StatusCode);
                Assert.Equal(2, (await second.GetFromJsonAsync<JsonElement>("/api/presence/online"))
                    .GetProperty("onlinePlayers").GetInt32());

                Assert.Equal(HttpStatusCode.NoContent,
                    (await second.PostAsync("/api/auth/logout", null)).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await second.PostAsync("/api/presence/heartbeat", null)).StatusCode);
                Assert.Equal(1, (await first.GetFromJsonAsync<JsonElement>("/api/presence/online"))
                    .GetProperty("onlinePlayers").GetInt32());
            }

            using (var factory = CreateServer())
            using (var client = factory.CreateClient())
            {
                await Login(client, "presence_a", "StrongPass123");
                Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/presence/online"))
                    .GetProperty("onlinePlayers").GetInt32());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task Register(HttpClient client, string username, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username, email, password = "StrongPass123", confirmPassword = "StrongPass123"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task Login(HttpClient client, string identity, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { identity, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task RegistrationLoginAndFarmerSurviveServerRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "doan-auth-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "accounts.db");
        var previousDatabase = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
        try
        {
            using (var factory = CreateServer())
            using (var client = factory.CreateClient())
            {
                var register = new { username = "farmer_1", email = "farmer@example.com",
                    password = "StrongPass123", confirmPassword = "StrongPass123" };
                var created = await client.PostAsJsonAsync("/api/auth/register", register);
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var token = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
                Assert.False(string.IsNullOrEmpty(token));
                Assert.Equal(HttpStatusCode.Conflict,
                    (await client.PostAsJsonAsync("/api/auth/register", register)).StatusCode);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var farmer = new { name = "Alex", farmName = "Oak Farm", favoriteThing = "Rain",
                    gender = 0, hair = 117, skin = 21, shirt = 203, pants = 19, farmType = 2,
                    pet = 1, petBreed = 2, accessory = 18, hairColor = 1, eyeColor = 4,
                    hairColorRgb = 0x16789ab, eyeColorRgb = 0x1123456, pantsColorRgb = 0x1abcdef };
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/farmer", farmer)).StatusCode);
                var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
                Assert.Equal("Alex", me.GetProperty("farmer").GetProperty("name").GetString());
                Assert.Equal(1, me.GetProperty("farmer").GetProperty("farmType").GetInt32());
                Assert.Equal(117, me.GetProperty("farmer").GetProperty("hair").GetInt32());
                Assert.Equal(18, me.GetProperty("farmer").GetProperty("accessory").GetInt32());
                var firstState = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
                Assert.Equal(500, firstState.GetProperty("money").GetInt64());
                Assert.Equal("axe", firstState.GetProperty("inventory")[0].GetProperty("itemId").GetString());
                Assert.Equal(100, firstState.GetProperty("inventory").EnumerateArray()
                    .Single(stack => stack.GetProperty("slot").GetInt32() == 11).GetProperty("amount").GetInt32());
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.PostAsJsonAsync("/api/game/chests",
                        new { requestId = Guid.NewGuid(), name = "Unplaced" })).StatusCode);
                var createRequest = new { requestId = Guid.NewGuid(), name = "Home chest",
                                          location = "FarmHouse", tileX = 4, tileY = 7 };
                var createdChest = await client.PostAsJsonAsync("/api/game/chests", createRequest);
                Assert.Equal(HttpStatusCode.OK, createdChest.StatusCode);
                var retriedChest = await client.PostAsJsonAsync("/api/game/chests", createRequest);
                Assert.Equal(HttpStatusCode.OK, retriedChest.StatusCode);
                var retryState = await retriedChest.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(1, retryState.GetProperty("chests").GetArrayLength());
                Assert.Equal(50, retryState.GetProperty("inventory").EnumerateArray()
                    .Single(stack => stack.GetProperty("slot").GetInt32() == 11).GetProperty("amount").GetInt32());
                var chestState = await createdChest.Content.ReadFromJsonAsync<JsonElement>();
                var chestId = chestState.GetProperty("chests")[0].GetProperty("id").GetGuid();
                Assert.Equal(1, chestState.GetProperty("chests").GetArrayLength());
                Assert.Equal("FarmHouse", chestState.GetProperty("chests")[0].GetProperty("location").GetString());
                Assert.Equal(4, chestState.GetProperty("chests")[0].GetProperty("tileX").GetInt32());
                Assert.Equal(50, chestState.GetProperty("inventory").EnumerateArray()
                    .Single(stack => stack.GetProperty("slot").GetInt32() == 11).GetProperty("amount").GetInt32());
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.PostAsJsonAsync("/api/game/chests",
                        new { requestId = Guid.NewGuid(), name = "Overlap",
                              location = "FarmHouse", tileX = 4, tileY = 7 })).StatusCode);
                // Simulate a chest from the older storage-only build and place it.
                await using (var connection = new SqliteConnection("Data Source=" + database))
                {
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "UPDATE PlayerStates SET ChestsJson = $json";
                    command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(new[]
                    {
                        new { Id = chestId, Name = "Home chest", Slots = Array.Empty<object>() }
                    }));
                    Assert.Equal(1, await command.ExecuteNonQueryAsync());
                }
                var placedOldChest = await client.PostAsJsonAsync(
                    $"/api/game/chests/{chestId}/place",
                    new { requestId = Guid.NewGuid(), location = "FarmHouse", tileX = 4, tileY = 7 });
                Assert.Equal(HttpStatusCode.OK, placedOldChest.StatusCode);
                var relocated = await placedOldChest.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(4, relocated.GetProperty("chests")[0].GetProperty("tileX").GetInt32());
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.PostAsJsonAsync($"/api/game/chests/{chestId}/place",
                        new { requestId = Guid.NewGuid(), location = "FarmHouse",
                              tileX = 5, tileY = 7 })).StatusCode);
                var transferRequest = new { requestId = Guid.NewGuid(), sourceChestId = (Guid?)null,
                    sourceSlot = 6, targetChestId = (Guid?)chestId, targetSlot = 0, amount = 5 };
                var transfer = await client.PostAsJsonAsync("/api/game/transfer", transferRequest);
                Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
                Assert.Equal(HttpStatusCode.OK,
                    (await client.PostAsJsonAsync("/api/game/transfer", transferRequest)).StatusCode);
                var afterTransfer = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
                Assert.Equal(15, afterTransfer.GetProperty("inventory").EnumerateArray()
                    .Single(stack => stack.GetProperty("slot").GetInt32() == 6).GetProperty("amount").GetInt32());
                Assert.Equal(5, afterTransfer.GetProperty("chests")[0].GetProperty("slots")[0].GetProperty("amount").GetInt32());
                var buyRequest = new { requestId = Guid.NewGuid(), itemId = "seeds_parsnip",
                    amount = 2, shop = "Pierre" };
                Assert.Equal(HttpStatusCode.OK,
                    (await client.PostAsJsonAsync("/api/game/buy", buyRequest)).StatusCode);
                Assert.Equal(HttpStatusCode.OK,
                    (await client.PostAsJsonAsync("/api/game/buy", buyRequest)).StatusCode);
                var afterBuy = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
                Assert.Equal(460, afterBuy.GetProperty("money").GetInt64());
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.PostAsJsonAsync("/api/game/sell", new
                        { requestId = Guid.NewGuid(), slot = 0, amount = 1 })).StatusCode);
                var other = await client.PostAsJsonAsync("/api/auth/register", new
                {
                    username = "farmer_2", email = "other@example.com",
                    password = "StrongPass123", confirmPassword = "StrongPass123"
                });
                Assert.Equal(HttpStatusCode.OK, other.StatusCode);
                var otherToken = (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/transfer", new
                {
                    requestId = Guid.NewGuid(), sourceChestId = chestId, sourceSlot = 0,
                    targetChestId = (Guid?)null, targetSlot = 11, amount = 1
                })).StatusCode);
                Assert.True(File.Exists(database));
            }

            using (var factory = CreateServer())
            using (var client = factory.CreateClient())
            {
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await client.PostAsJsonAsync("/api/auth/login", new { identity = "farmer_1", password = "WrongPass123" })).StatusCode);
                var login = await client.PostAsJsonAsync("/api/auth/login",
                    new { identity = "FARMER@EXAMPLE.COM", password = "StrongPass123" });
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var response = await login.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Oak Farm", response.GetProperty("farmer").GetProperty("farmName").GetString());
                Assert.Equal(1, response.GetProperty("farmer").GetProperty("farmType").GetInt32());
                Assert.Equal(2, response.GetProperty("farmer").GetProperty("petBreed").GetInt32());
                Assert.Equal(0x1abcdef, response.GetProperty("farmer").GetProperty("pantsColorRgb").GetInt32());
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.GetProperty("token").GetString());
                var persistentState = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
                Assert.Equal(460, persistentState.GetProperty("money").GetInt64());
                Assert.Equal(5, persistentState.GetProperty("chests")[0].GetProperty("slots")[0].GetProperty("amount").GetInt32());
                Assert.Equal(7, persistentState.GetProperty("chests")[0].GetProperty("tileY").GetInt32());
                Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previousDatabase);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateServer() =>
        new WebApplicationFactory<Program>();
}
