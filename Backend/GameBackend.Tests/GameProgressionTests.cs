using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class GameProgressionTests
{
    [Fact]
    public void SkillLevelsFollowFarmerThresholds_AndXpCanContinuePastLevelTen()
    {
        Assert.Equal(0, GameProgression.LevelFromExperience(99));
        Assert.Equal(1, GameProgression.LevelFromExperience(100));
        Assert.Equal(5, GameProgression.LevelFromExperience(2150));
        Assert.Equal(10, GameProgression.LevelFromExperience(15000));
        Assert.Equal(10, GameProgression.LevelFromExperience(25000));
        var experience = new[] { 14_999, 0, 0, 0, 0 };
        GameProgression.AddExperience(experience, 0, 10);
        Assert.Equal(15_009, experience[0]);
        GameProgression.AddExperience(experience, 5, 100);
        Assert.Equal(15_009, experience[0]);
    }

    [Fact]
    public void ServerEventsAwardOnlyReachedAchievements_AndSurviveSerialization()
    {
        var progress = new PlayerProgress();
        GameProgression.Evaluate(progress, 14_999);
        Assert.Empty(progress.Achievements);
        GameProgression.RecordFish(progress, "fish_128");
        GameProgression.RecordFish(progress, "fish_128");
        GameProgression.RecordShipment(progress, "parsnip", 300);
        progress.FriendshipPoints["Robin"] = 1250;
        GameProgression.Evaluate(progress, 15_000);
        Assert.Contains(0, progress.Achievements);
        Assert.Contains(6, progress.Achievements);
        Assert.Contains(32, progress.Achievements);
        Assert.DoesNotContain(24, progress.Achievements);
        Assert.DoesNotContain(7, progress.Achievements);

        var saved = PlayerProgress.FromJson(JsonSerializer.Serialize(progress));
        Assert.Equal(2, saved.FishCaught["128"]);
        Assert.Contains(32, saved.Achievements);
        GameProgression.Evaluate(saved, 15_000);
        Assert.Equal(progress.Achievements.Count, saved.Achievements.Count);
    }

    [Fact]
    public async Task ShippingUsesNativeObjectEligibilityAndPaysBasePricesOvernight()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-shipping-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "shipping.db"),
                "ShippingTestsSecret_2026_MustBeLongEnough!");
            using var client = factory.CreateClient();
            string username = "shipping_" + Guid.NewGuid().ToString("N")[..8];
            using var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, username + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", account.GetProperty("token").GetString());
            _ = await State();

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
                string inventory = JsonSerializer.Serialize(new StackEntry[]
                {
                    new() { Slot = 0, ItemId = "fish_145", Amount = 1 },
                    new() { Slot = 1, ItemId = "seeds_parsnip", Amount = 3 },
                    new() { Slot = 2, ItemId = "weapon_0", Amount = 1 },
                    new() { Slot = 3, ItemId = "mixed_seeds", Amount = 1 },
                    new() { Slot = 4, ItemId = "object_788", Amount = 1 },
                    new() { Slot = 5, ItemId = "coal", Amount = 3 },
                    new() { Slot = 6, ItemId = "object_334", Amount = 1 },
                    new() { Slot = 7, ItemId = "object_460", Amount = 1 }
                });
                await db.PlayerStates.Where(state => state.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.InventoryJson, inventory));
            }
            var before = await State();
            Assert.Equal(new[] { "fish_145", "seeds_parsnip", "mixed_seeds", "coal", "object_334" },
                before.ShippableItems);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await Deposit(2, "weapon_0", 1)).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await Deposit(4, "object_788", 1)).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await Deposit(7, "object_460", 1)).StatusCode);
            (await Deposit(0, "fish_145", 1)).EnsureSuccessStatusCode();
            (await Deposit(1, "seeds_parsnip", 2)).EnsureSuccessStatusCode();
            var pending = await State();
            Assert.Equal(before.Money, pending.Money);
            Assert.Equal(new[] { new ShippingEntry("fish_145", 1, 30), new ShippingEntry("seeds_parsnip", 2, 10) },
                pending.Shipping);
            (await client.PostAsJsonAsync("/api/game/shipping/retrieve", new BackpackRequest(Guid.NewGuid())))
                .EnsureSuccessStatusCode();
            Assert.Equal(3, Assert.Single((await State()).Inventory, stack => stack.ItemId == "seeds_parsnip").Amount);
            (await Deposit(1, "seeds_parsnip", 2)).EnsureSuccessStatusCode();
            (await Deposit(3, "mixed_seeds", 1)).EnsureSuccessStatusCode();
            (await Deposit(5, "coal", 1)).EnsureSuccessStatusCode();
            (await Deposit(6, "object_334", 1)).EnsureSuccessStatusCode();
            Assert.Equal(0, Assert.Single((await State()).Shipping, entry => entry.ItemId == "mixed_seeds").UnitPrice);
            (await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), 2)))
                .EnsureSuccessStatusCode();
            var settled = await State();
            Assert.Equal(before.Money + 125, settled.Money);
            Assert.Empty(settled.Shipping);

            Task<HttpResponseMessage> Deposit(int slot, string item, int amount) =>
                client.PostAsJsonAsync("/api/game/shipping/deposit", new SellRequest(Guid.NewGuid(), slot, item, amount));
            async Task<GameSnapshot> State() => (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoneyAchievementPersistsForOnlyTheAccountThatEarnedIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-achievements-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "achievements.db"),
                "GameProgressionTestsSecret_2026_MustBeLongEnough!");
            using var client = factory.CreateClient();
            async Task<string> Register(string name)
            {
                var response = await client.PostAsJsonAsync("/api/auth/register",
                    new RegisterRequest(name, name + "@example.com", "Password123!", "Password123!"));
                response.EnsureSuccessStatusCode();
                var account = await response.Content.ReadFromJsonAsync<JsonElement>();
                return account.GetProperty("token").GetString()!;
            }

            string nameA = "winner_" + Guid.NewGuid().ToString("N")[..8];
            string tokenA = await Register(nameA);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == nameA).Select(u => u.Id).SingleAsync();
                await db.PlayerStates.Where(s => s.UserId == userId).ExecuteUpdateAsync(setters =>
                    setters.SetProperty(s => s.TotalEarnings, 14_999L));
            }
            Guid requestId = Guid.NewGuid();
            var sold = await client.PostAsJsonAsync("/api/game/sell", new SellRequest(requestId, 11, "wood", 1));
            sold.EnsureSuccessStatusCode();
            var afterSale = await sold.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(afterSale.GetProperty("achievements").EnumerateArray(),
                value => value.GetInt32() == 0);
            var replay = await client.PostAsJsonAsync("/api/game/sell", new SellRequest(requestId, 11, "wood", 1));
            replay.EnsureSuccessStatusCode();
            var afterReplay = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(afterSale.GetProperty("totalEarnings").GetInt64(),
                afterReplay.GetProperty("totalEarnings").GetInt64());
            Assert.Empty(afterSale.GetProperty("itemsShipped").EnumerateArray());

            var deposit = await client.PostAsJsonAsync("/api/game/shipping/deposit",
                new SellRequest(Guid.NewGuid(), 11, "wood", 2));
            deposit.EnsureSuccessStatusCode();
            var beforeSettlement = await deposit.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Empty(beforeSettlement.GetProperty("itemsShipped").EnumerateArray());
            var day = await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), 2));
            day.EnsureSuccessStatusCode();
            var settlement = await day.Content.ReadFromJsonAsync<JsonElement>();
            var shipment = Assert.Single(settlement.GetProperty("itemsShipped").EnumerateArray());
            Assert.Equal("wood", shipment.GetProperty("itemId").GetString());
            Assert.Equal(2, shipment.GetProperty("amount").GetInt32());

            var organized = await client.PostAsJsonAsync("/api/game/inventory/organize", new BackpackRequest(Guid.NewGuid()));
            organized.EnsureSuccessStatusCode();
            var organizedState = await organized.Content.ReadFromJsonAsync<JsonElement>();
            var sorted = organizedState.GetProperty("inventory").EnumerateArray().OrderBy(item => item.GetProperty("slot").GetInt32()).ToArray();
            Assert.Equal("axe", sorted[0].GetProperty("itemId").GetString());
            Assert.Equal("scythe", sorted[4].GetProperty("itemId").GetString());
            Assert.Equal(97, Assert.Single(sorted, item => item.GetProperty("itemId").GetString() == "wood")
                .GetProperty("amount").GetInt32());

            string nameB = "newcomer_" + Guid.NewGuid().ToString("N")[..8];
            string tokenB = await Register(nameB);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
            var secondAccount = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Empty(secondAccount.GetProperty("achievements").EnumerateArray());
            Assert.Empty(secondAccount.GetProperty("itemsShipped").EnumerateArray());
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == nameB).Select(u => u.Id).SingleAsync();
                db.NpcFriendships.Add(new NpcFriendship
                    { UserId = userId, NpcName = "Robin", Points = 1250 });
                for (int id = 128; id < 138; id++)
                    db.FishingAttempts.Add(new FishingAttempt
                    {
                        Id = Guid.NewGuid(), UserId = userId, FishItemId = "fish_" + id,
                        ClaimedUtc = DateTime.UtcNow
                    });
                await db.SaveChangesAsync();
            }
            secondAccount = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Contains(secondAccount.GetProperty("achievements").EnumerateArray(),
                value => value.GetInt32() == 6);
            Assert.Contains(secondAccount.GetProperty("achievements").EnumerateArray(),
                value => value.GetInt32() == 24);
            Assert.Contains(secondAccount.GetProperty("fishCaught").EnumerateArray(), fish =>
                fish.GetProperty("itemId").GetString() == "128" && fish.GetProperty("amount").GetInt32() == 1);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            var firstAccount = await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            Assert.Contains(firstAccount.GetProperty("achievements").EnumerateArray(),
                value => value.GetInt32() == 0);
            Assert.Empty(firstAccount.GetProperty("fishCaught").EnumerateArray());
            Assert.Equal(2, Assert.Single(firstAccount.GetProperty("itemsShipped").EnumerateArray())
                .GetProperty("amount").GetInt32());

            var recipe = new CraftItemRequest(Guid.NewGuid(), "Stone Fence");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/game/craft", recipe)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == nameA).Select(u => u.Id).SingleAsync();
                var state = await db.PlayerStates.SingleAsync(row => row.UserId == userId);
                var stacks = JsonSerializer.Deserialize<List<StackEntry>>(state.InventoryJson)!;
                stacks.Add(new StackEntry { Slot = 11, ItemId = "stone", Amount = 2 });
                state.InventoryJson = JsonSerializer.Serialize(stacks);
                state.SkillsJson = "[380,0,0,0,0]";
                await db.SaveChangesAsync();
            }
            var crafted = await client.PostAsJsonAsync("/api/game/craft", recipe with { RequestId = Guid.NewGuid() });
            crafted.EnsureSuccessStatusCode();
            var inventory = (await crafted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("inventory");
            Assert.Contains(inventory.EnumerateArray(), stack => stack.GetProperty("itemId").GetString() == "object_323");
            Assert.DoesNotContain(inventory.EnumerateArray(), stack => stack.GetProperty("itemId").GetString() == "stone");
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
