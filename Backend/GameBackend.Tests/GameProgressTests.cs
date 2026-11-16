using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class GameProgressTests
{
    private const string TestSecret = "GameProgressTestsSecret_2026_MustBeLongEnough!";

    [Fact]
    public async Task RobinUnlocksPurchasedRecipesAndMarlonQuotesAndPaysForGearAtHisCounter()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-merchant-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "audit.db"), TestSecret);
            using var client = factory.CreateClient();
            using var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest("auditor", "auditor@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await registration.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            await Move("ScienceHouse", 8.5f, -19.5f);
            var stock = await Stock("Robin");
            Assert.Contains(new ShopOffer("recipe_object_328", 100), stock);
            Assert.Contains(new ShopOffer("recipe_big_craftable_143", 250), stock);
            Assert.DoesNotContain(stock, offer => offer.ItemId == "recipe_big_craftable_144");
            Assert.Contains(new ShopOffer("big_craftable_208", 2000), stock);
            Assert.DoesNotContain(stock, offer => offer.ItemId == "big_craftable_211");
            Assert.Equal(stock, await Stock("Robin"));
            Assert.Equal(HttpStatusCode.BadRequest, (await CraftFloor()).StatusCode);
            using var learned = await client.PostAsJsonAsync("/api/game/buy",
                new BuyRequest(Guid.NewGuid(), "recipe_object_328", 1, "Robin"));
            learned.EnsureSuccessStatusCode();
            var learnedState = (await learned.Content.ReadFromJsonAsync<GameSnapshot>())!;
            Assert.Contains("Wood Floor", learnedState.LearnedShopRecipes);
            Assert.Equal(400, learnedState.Money);
            Assert.DoesNotContain(await Stock("Robin"), offer => offer.ItemId == "recipe_object_328");

            await Move("AdventureGuild", 5.5f, -12.5f);
            Assert.DoesNotContain(await Stock("Marlon"), offer => offer.ItemId == "weapon_17");
            using var bought = await client.PostAsJsonAsync("/api/game/buy",
                new BuyRequest(Guid.NewGuid(), "weapon_12", 1, "Marlon"));
            bought.EnsureSuccessStatusCode();
            var purchase = (await bought.Content.ReadFromJsonAsync<GameSnapshot>())!;
            var weapon = Assert.Single(purchase.Inventory, stack => stack.ItemId == "weapon_12");
            Assert.Contains(new ShopOffer(weapon.ItemId, 50), purchase.GuildResale);
            var sale = new SellRequest(Guid.NewGuid(), weapon.Slot, weapon.ItemId, 1, "Marlon");
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/game/sell", sale with { Shop = "Pierre" })).StatusCode);
            await Move("Town", 5.5f, -12.5f);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/sell", sale)).StatusCode);
            await Move("AdventureGuild", 5.5f, -12.5f);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var sold = await client.PostAsJsonAsync("/api/game/sell", sale);
                sold.EnsureSuccessStatusCode();
                var snapshot = (await sold.Content.ReadFromJsonAsync<GameSnapshot>())!;
                Assert.Equal(200, snapshot.Money);
                Assert.Equal(50, snapshot.TotalEarnings);
                Assert.DoesNotContain(snapshot.Inventory, stack => stack.ItemId == weapon.ItemId);
            }
            using var crafted = await CraftFloor();
            crafted.EnsureSuccessStatusCode();
            Assert.Contains((await crafted.Content.ReadFromJsonAsync<GameSnapshot>())!.Inventory, stack => stack.ItemId == "object_328");

            async Task Move(string location, float x, float y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync", new WorldPlayerSyncRequest(x, y, 0, 1, location)))
                    .EnsureSuccessStatusCode();
            async Task<ShopOffer[]> Stock(string name) =>
                (await client.GetFromJsonAsync<ShopOfferList>("/api/game/shop/" + name))!.Offers;
            Task<HttpResponseMessage> CraftFloor() => client.PostAsJsonAsync("/api/game/craft",
                new CraftItemRequest(Guid.NewGuid(), "Wood Floor"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MerchantOffersGeodesAndHouseUpgradeUseServerPricesAndPersist()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-merchants-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "merchants.db"), TestSecret);
            using var client = factory.CreateClient();
            string user = "merchant_" + Guid.NewGuid().ToString("N")[..8];
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(user, user + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                account.GetProperty("token").GetString());
            await client.GetFromJsonAsync<JsonElement>("/api/game/state");

            var clint = await client.GetFromJsonAsync<ShopOfferList>("/api/game/shop/Clint");
            Assert.Equal(4, clint!.Offers.Length);
            Assert.Contains(clint.Offers, entry => entry.ItemId == "gold_ore" && entry.Price == 400);
            var robin = await client.GetFromJsonAsync<ShopOfferList>("/api/game/shop/Robin");
            Assert.Contains(robin!.Offers, entry => entry.ItemId == "furniture_2048" && entry.Price == 500);
            var marlon = await client.GetFromJsonAsync<ShopOfferList>("/api/game/shop/Marlon");
            Assert.Contains(new ShopOffer("weapon_12", 250), marlon!.Offers);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == user).Select(u => u.Id).SingleAsync();
                var state = await db.PlayerStates.SingleAsync(entry => entry.UserId == userId);
                state.Money = 13_000;
                state.InventoryJson = JsonSerializer.Serialize(new[]
                {
                    new StackEntry { Slot = 0, ItemId = "wood", Amount = 450 },
                    new StackEntry { Slot = 1, ItemId = "object_535", Amount = 1 },
                    new StackEntry { Slot = 3, ItemId = "axe", Amount = 1 },
                    new StackEntry { Slot = 4, ItemId = "object_334", Amount = 5 }
                });
                await db.SaveChangesAsync();
            }
            async Task Move(string location, float x, float y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(x, y, 0, 1, location))).EnsureSuccessStatusCode();

            var buySword = new BuyRequest(Guid.NewGuid(), "weapon_12", 1, "Marlon");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/buy", buySword)).StatusCode);
            await Move("AdventureGuild", 5.5f, -12.5f);
            var sword = await client.PostAsJsonAsync("/api/game/buy", buySword);
            sword.EnsureSuccessStatusCode();
            var bought = await sword.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Equal(12_750, bought!.Money);
            Assert.Contains(bought.Inventory, stack => stack.ItemId == "weapon_12" && stack.Amount == 1);
            Assert.Contains(new ShopOffer("weapon_12", 50), bought.GuildResale);

            await Move("Blacksmith", 3.5f, -14.5f);
            var geode = new SellRequest(Guid.NewGuid(), 1, "object_535", 1);
            var cracked = await client.PostAsJsonAsync("/api/game/blacksmith/geode", geode);
            cracked.EnsureSuccessStatusCode();
            var reward = await cracked.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Equal(12_725, reward!.Money);
            Assert.DoesNotContain(reward.Inventory, stack => stack.ItemId == "object_535");
            Assert.Equal(5, reward.Inventory.Count);
            Assert.NotNull(reward.GeodeReward);
            Assert.Contains(reward.Inventory, stack => stack.ItemId == reward.GeodeReward.ItemId &&
                stack.Amount == reward.GeodeReward.Amount);
            var crackedAgain = await client.PostAsJsonAsync("/api/game/blacksmith/geode", geode);
            crackedAgain.EnsureSuccessStatusCode();
            var afterReplay = await crackedAgain.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Equal(reward.Money, afterReplay!.Money);
            Assert.Contains(afterReplay.Inventory, stack => stack.ItemId == reward.GeodeReward.ItemId &&
                stack.Amount == reward.GeodeReward.Amount);
            Assert.Null(afterReplay.GeodeReward);

            var upgradeTool = await client.PostAsJsonAsync("/api/game/blacksmith/upgrade",
                new SellRequest(Guid.NewGuid(), 3, "axe", 1));
            upgradeTool.EnsureSuccessStatusCode();
            var atClint = await upgradeTool.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Equal(new ToolUpgradeOrder("tool_CopperAxe", 3), atClint!.ToolUpgrade);
            Assert.DoesNotContain(atClint.Inventory, stack => stack.ItemId is "axe" or "object_334");
            var early = await client.PostAsJsonAsync("/api/game/blacksmith/collect-upgrade", new BackpackRequest(Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

            await Move("ScienceHouse", 8.5f, -19.5f);
            var upgrade = new BackpackRequest(Guid.NewGuid());
            var order = await client.PostAsJsonAsync("/api/game/robin/upgrade-house", upgrade);
            order.EnsureSuccessStatusCode();
            var result = await order.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Equal(725, result!.Money);
            Assert.Equal(4, result.HouseUpgradeDay);
            Assert.DoesNotContain(result.Inventory, stack => stack.ItemId == "wood");
            var replay = await client.PostAsJsonAsync("/api/game/robin/upgrade-house", upgrade);
            replay.EnsureSuccessStatusCode();
            var reloaded = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Equal(725, reloaded!.Money);
            Assert.Equal(4, reloaded.HouseUpgradeDay);
            for (int day = 2; day <= 3; day++)
                (await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), day)))
                    .EnsureSuccessStatusCode();
            await Move("Blacksmith", 3.5f, -14.5f);
            var collect = await client.PostAsJsonAsync("/api/game/blacksmith/collect-upgrade", new BackpackRequest(Guid.NewGuid()));
            collect.EnsureSuccessStatusCode();
            var finished = await collect.Content.ReadFromJsonAsync<GameSnapshot>();
            Assert.Null(finished!.ToolUpgrade);
            Assert.Equal(3, finished.GameDay);
            Assert.Contains(finished.Inventory, stack => stack.ItemId == "tool_CopperAxe");
            var secondCollect = await client.PostAsJsonAsync("/api/game/blacksmith/collect-upgrade",
                new BackpackRequest(Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.BadRequest, secondCollect.StatusCode);
            var afterSecondCollect = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Equal(finished.Money, afterSecondCollect!.Money);
            Assert.Single(afterSecondCollect.Inventory, stack => stack.ItemId == "tool_CopperAxe");
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public async Task NaturalResourcesKeepTheirStateAcrossLoginAndRegrowOnGameDays()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-vegetation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "vegetation.db"), TestSecret);
            using var client = factory.CreateClient();
            string user = "vegetation_" + Guid.NewGuid().ToString("N")[..8];
            const string password = "Password123!";
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(user, user + "@example.com", password, password));
            registration.EnsureSuccessStatusCode();

            async Task Authorize(HttpResponseMessage response)
            {
                response.EnsureSuccessStatusCode();
                var account = await response.Content.ReadFromJsonAsync<JsonElement>();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                    account.GetProperty("token").GetString());
            }

            await Authorize(registration);
            await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            var grass = new NaturalResourceRequest(Guid.NewGuid(), "Farm", 14, 17, "grass", false);
            var tooFar = await client.PostAsJsonAsync("/api/game/resources/clear", grass);
            Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(14.5f, -17.5f, 0, 1, "Farm"))).EnsureSuccessStatusCode();

            async Task<GameSnapshot> Clear(NaturalResourceRequest input)
            {
                var response = await client.PostAsJsonAsync("/api/game/resources/clear", input);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<GameSnapshot>())!;
            }

            var cleared = await Clear(grass);
            Assert.Equal(new NaturalResourceState("Farm", 14, 17, "grass", false, 0, 4),
                Assert.Single(cleared.NaturalResources!));
            Assert.Equal(cleared.Version, (await Clear(grass)).Version);
            var tooEarly = await client.PostAsJsonAsync("/api/game/resources/clear", grass with { RequestId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);

            var tree = new NaturalResourceRequest(Guid.NewGuid(), "Farm", 15, 17, "tree", true);
            await Clear(tree);
            await Authorize(await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user, password)));
            var loaded = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Contains(loaded!.NaturalResources!, entry => entry.Kind == "grass" && entry.RegrowDay == 4);
            Assert.Contains(loaded.NaturalResources!, entry => entry.Kind == "tree" && entry.Stump && entry.RegrowDay == 0);

            var dug = await Clear(tree with { RequestId = Guid.NewGuid(), Stump = false });
            Assert.Contains(dug.NaturalResources!, entry => entry.Kind == "tree" && !entry.Stump &&
                entry.SaplingDay == 8 && entry.RegrowDay == 15);
            for (int day = 2; day <= 4; day++)
                (await client.PostAsJsonAsync("/api/game/day/advance",
                    new AdvanceDayRequest(Guid.NewGuid(), day))).EnsureSuccessStatusCode();
            var regrown = await Clear(grass with { RequestId = Guid.NewGuid() });
            Assert.Contains(regrown.NaturalResources!, entry => entry.Kind == "grass" && entry.RegrowDay == 7);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Guid userId = await db.Users.Where(entry => entry.Username == user).Select(entry => entry.Id).SingleAsync();
            await db.PlayerStates.Where(entry => entry.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.GameDay, 83));
            var beforeWinter = await Clear(grass with { RequestId = Guid.NewGuid() });
            Assert.Contains(beforeWinter.NaturalResources!, entry => entry.Kind == "grass" && entry.RegrowDay == 114);
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public async Task LegacyUtcCropCannotRipenBeforeAnyWateredGameDay()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-legacy-crop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "legacy.db"), TestSecret);
            using var client = factory.CreateClient();
            string name = "legacy_" + Guid.NewGuid().ToString("N")[..8];
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(name, name + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                account.GetProperty("token").GetString());
            await client.GetFromJsonAsync<JsonElement>("/api/game/state");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == name).Select(u => u.Id).SingleAsync();
                db.FarmCropStates.Add(new FarmCropState
                {
                    UserId = userId, TileX = 5, TileY = 5, SeedItemId = "seeds_parsnip",
                    HarvestItemId = "parsnip", DaysToGrow = 4, GrowthDays = 4,
                    ReadyGameDay = 0, ReadyUtc = DateTime.UtcNow.AddDays(-3)
                });
                db.FarmCropStates.Add(new FarmCropState
                {
                    UserId = userId, TileX = 6, TileY = 5, SeedItemId = "seeds_green_bean",
                    HarvestItemId = "green_bean", DaysToGrow = 10, GrowthDays = 10,
                    HasHarvested = true, RegrowDays = 3, RegrowDaysLeft = 0,
                    ReadyGameDay = 0, ReadyUtc = DateTime.UtcNow.AddDays(-3)
                });
                await db.SaveChangesAsync();
                await GameStateEndpoints.EnsureStateTable(db);
                await GameStateEndpoints.EnsureStateTable(db);
            }
            var crop = await client.GetFromJsonAsync<JsonElement>("/api/game/farm/crops");
            Assert.Equal(0, crop.GetProperty("crops")[0].GetProperty("growthDays").GetInt32());
            Assert.Equal(5, crop.GetProperty("crops")[0].GetProperty("readyGameDay").GetInt32());
            Assert.Equal(3, crop.GetProperty("crops")[1].GetProperty("regrowDaysLeft").GetInt32());
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [Fact]
    public async Task CropsOnlyGrowAfterWateredGameDay_AndGiftsRespectPersonalDay()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-progress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "progress.db"), TestSecret);
            using var client = factory.CreateClient();
            string username = "grower_" + Guid.NewGuid().ToString("N")[..8];
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, username + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                account.GetProperty("token").GetString());
            await client.GetFromJsonAsync<JsonElement>("/api/game/state");

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Guid userId = await db.Users.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
                foreach (int y in new[] { 5, 6 })
                    db.FarmCropStates.Add(new FarmCropState
                    {
                        UserId = userId, TileX = 5, TileY = y, SeedItemId = "seeds_parsnip",
                        HarvestItemId = "parsnip", DaysToGrow = 4,
                        ReadyUtc = DateTime.UtcNow.AddDays(-1)
                    });
                await db.SaveChangesAsync();
            }

            async Task<FarmCropDto> LoadCrop(int y)
            {
                var response = await client.GetFromJsonAsync<FarmCropListResponse>("/api/game/farm/crops");
                return Assert.Single(response!.Crops, crop => crop.TileX == 5 && crop.TileY == y);
            }

            Assert.Equal(0, (await LoadCrop(5)).GrowthDays);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 2))).StatusCode);
            var forecast = (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!.Weather;
            Assert.Equal(new FarmWeatherForecast(0, 1), forecast);
            Assert.Equal(0, (await LoadCrop(5)).GrowthDays);
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(5.5f, -5.5f, 0, -1, "Farm"))).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync("/api/game/farm/water",
                new WaterFarmCropRequest(Guid.NewGuid(), 5, 5))).EnsureSuccessStatusCode();

            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Password123!"));
            login.EnsureSuccessStatusCode();
            var loggedIn = await login.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                loggedIn.GetProperty("token").GetString());
            Assert.Equal(forecast, (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!.Weather);
            Assert.True((await LoadCrop(5)).IsWatered);
            Assert.False((await LoadCrop(6)).IsWatered);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 3))).StatusCode);
            var crop = await LoadCrop(5);
            Assert.Equal(1, crop.GrowthDays);
            Assert.True(crop.IsWatered);
            Assert.Equal(0, (await LoadCrop(6)).GrowthDays);
            Assert.True((await LoadCrop(6)).IsWatered);

            var gift = await client.PostAsJsonAsync("/api/game/npc/gift",
                new NpcGiftRequest(Guid.NewGuid(), "Robin", 11, "wood"));
            Assert.Equal(HttpStatusCode.OK, gift.StatusCode);
            var friends = await client.GetFromJsonAsync<JsonElement>("/api/game/npc/friendships");
            Assert.Single(friends.GetProperty("friends").EnumerateArray());
            Assert.Equal(3, friends.GetProperty("friends")[0].GetProperty("lastGiftDay").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/game/npc/gift",
                new NpcGiftRequest(Guid.NewGuid(), "Robin", 11, "wood"))).StatusCode);

            var presence = await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(5.5f, -5.5f, 0, -1, "Farm"));
            presence.EnsureSuccessStatusCode();
            foreach (var (x, y, slot, id, amount) in new[]
            {
                (4, 4, 6, "seeds_parsnip", 19), (5, 4, 7, "seeds_potato", 19),
                (6, 4, 8, "seeds_green_bean", 19), (4, 5, 9, "mixed_seeds", 11),
                (6, 5, 10, "seeds_cauliflower", 19)
            })
            {
                (await client.PostAsJsonAsync("/api/game/farm/soil",
                    new FarmSoilRequest(Guid.NewGuid(), x, y, false))).EnsureSuccessStatusCode();
                var sow = await client.PostAsJsonAsync("/api/game/farm/plant",
                    new PlantFarmCropRequest(Guid.NewGuid(), x, y, slot, id));
                sow.EnsureSuccessStatusCode();
                var state = await sow.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Contains(state.GetProperty("inventory").EnumerateArray(), stack =>
                    stack.GetProperty("slot").GetInt32() == slot &&
                    stack.GetProperty("amount").GetInt32() == amount);
            }
            var planted = await client.GetFromJsonAsync<JsonElement>("/api/game/farm/crops");
            var mixed = Assert.Single(planted.GetProperty("crops").EnumerateArray(),
                record => record.GetProperty("seedItemId").GetString() == "mixed_seeds");
            string? harvest = mixed.GetProperty("harvestItemId").GetString();
            Assert.Contains(harvest, new[] { "parsnip", "potato", "cauliflower" });
            var reloaded = await client.GetFromJsonAsync<JsonElement>("/api/game/farm/crops");
            Assert.Contains(reloaded.GetProperty("crops").EnumerateArray(), record =>
                record.GetProperty("seedItemId").GetString() == "mixed_seeds" &&
                record.GetProperty("harvestItemId").GetString() == harvest);

            (await client.PostAsJsonAsync("/api/game/farm/soil",
                new FarmSoilRequest(Guid.NewGuid(), 5, 7, false))).EnsureSuccessStatusCode();
            var rainyFarm = (await client.GetFromJsonAsync<FarmCropListResponse>("/api/game/farm/crops"))!;
            Assert.All(rainyFarm.Crops, tile => Assert.True(tile.IsWatered));
            Assert.Contains(rainyFarm.Soil, tile => tile.TileX == 5 && tile.TileY == 7 && tile.IsWatered);
            var morning = await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), 4));
            morning.EnsureSuccessStatusCode();
            Assert.Equal(0, (await morning.Content.ReadFromJsonAsync<GameSnapshot>())!.Weather!.Today);
            Assert.Equal(2, (await LoadCrop(5)).GrowthDays);
            Assert.Equal(1, (await LoadCrop(6)).GrowthDays);
            var dryFarm = (await client.GetFromJsonAsync<FarmCropListResponse>("/api/game/farm/crops"))!;
            Assert.All(dryFarm.Crops, tile => Assert.False(tile.IsWatered));
            Assert.All(dryFarm.Soil, tile => Assert.False(tile.IsWatered));
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
