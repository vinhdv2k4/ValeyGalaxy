using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmCutscenes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class MilestoneTests
{
    [Fact]
    public async Task PrivateRoomUnlockNeedsTwoHeartsAndPersistsAfterFriendshipDrops()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-room-door-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "room.db"),
                "PrivateRoomDoorTestsSecret_2026_LongEnough");
            using var client = factory.CreateClient();
            const string username = "roomtester";
            const string password = "Password123!";
            using var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, "roomtester@example.com", password, password));
            registration.EnsureSuccessStatusCode();
            var account = (await registration.Content.ReadFromJsonAsync<AuthResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
            var unlock = new MilestoneRequest(Guid.NewGuid(), "unlock_door", "Abigail", 1, 900, "SeedShop");
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(13.5f, -11.5f, 0, 1, "SeedShop"))).EnsureSuccessStatusCode();

            await SetFriendship(499);
            using var locked = await client.PostAsJsonAsync("/api/game/milestones", unlock);
            Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
            await SetFriendship(500);
            using var opened = await client.PostAsJsonAsync("/api/game/milestones", unlock);
            opened.EnsureSuccessStatusCode();
            Assert.Contains("doorUnlockAbigail", (await opened.Content.ReadFromJsonAsync<GameSnapshot>())!.Milestones.Seen);

            await SetFriendship(0);
            using var reconnected = factory.CreateClient();
            using var login = await reconnected.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
            login.EnsureSuccessStatusCode();
            reconnected.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            var saved = (await reconnected.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
            Assert.Single(saved.Milestones.Seen, id => id == "doorUnlockAbigail");
            Assert.DoesNotContain("doorUnlockPierre", saved.Milestones.Seen);

            async Task SetFriendship(int points)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(u => u.Username == username);
                var friend = await db.NpcFriendships.SingleOrDefaultAsync(f => f.UserId == user.Id && f.NpcName == "Abigail");
                if (friend == null)
                {
                    friend = new NpcFriendship { UserId = user.Id, NpcName = "Abigail" };
                    db.NpcFriendships.Add(friend);
                }
                friend.Points = points;
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SkippingRestoresTheStageBeforeAskingForTheRequiredBranch()
    {
        var services = new TestPresentation();
        var script = new EventScript
        {
            id = "choice", defaultName = "Milo",
            endPosition = new() { x = 42, y = 36 }, skipPosition = new() { x = 43, y = 36 },
            commands = [
                new() { type = "fade" },
                new() { type = "question", friendshipNpc = "Marnie",
                    options = [new() { label = "Dog", value = "dog", friendshipDelta = 50 },
                               new() { label = "Decline", value = "decline" }] }]
        };
        var repository = new EventRepository(new EventScriptFile { events = [script] },
            new EventConditionFactory(), new EventCommandFactory());
        var runner = new CutsceneRunner();
        var context = new EventExecutionContext(repository.Events[0], new Dictionary<string, ICutsceneActor>(),
            services, services, services, runner.State);

        Task run = runner.RunAsync(context, services, ending =>
        {
            Assert.Equal(EventEnding.Skipped, ending);
            Assert.Equal(43f, context.EventData.ExitTile(ending)?.X);
            services.Calls.Add("restore");
        }, CancellationToken.None);
        Assert.Equal(GameMode.CutscenePlaying, runner.Mode);
        runner.Skip();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "fade", "restore", "choose", "name", "choice:dog:Milo:Marnie:50" }, services.Calls);
        Assert.Equal(GameMode.NormalGameplay, runner.Mode);
        Assert.Equal(42f, context.EventData.ExitTile(EventEnding.Completed)?.X);
        Assert.Null(context.EventData.ExitTile(EventEnding.Interrupted));
    }

    [Fact]
    public async Task ServerMilestonesUseEarnedRevenueCopperDayAndSixPaidRooms()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-milestones-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "milestones.db");
        string? previous = Environment.GetEnvironmentVariable("GAME_DB_PATH");
        Environment.SetEnvironmentVariable("GAME_DB_PATH", database);
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
            using var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                Assert.Equal(database, db.Database.GetDbConnection().DataSource);
            }
            string username = "milestone_" + Guid.NewGuid().ToString("N")[..8];
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, username + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", account.GetProperty("token").GetString());
            var initial = await State();
            Assert.Empty(initial.Milestones.Seen);
            await Reject(Complete("pet_adoption", 1, 600, "Farm", "dog", "Milo"));
            await EditState(state => { state.TotalEarnings = 25_000; state.Money = 50_000; state.BagSize = 36; });
            await Reject(Complete("pet_adoption", 1, 600, "Farm", "dog", "Milo") with { Rain = true });
            var adopt = Complete("pet_adoption", 1, 600, "Farm", "dog", "Milo");
            await Apply(adopt);
            var replay = await Apply(adopt with { RequestId = Guid.NewGuid(), Choice = "cat", Name = "Luna" });
            Assert.Equal("dog", replay.Milestones.Pet);
            Assert.Equal("Milo", replay.Milestones.PetName);
            Assert.Equal(50, Assert.Single(replay.Milestones.Friends, friend => friend.Npc == "Marnie").Points);
            Assert.Equal("mushrooms", (await Apply(Complete("farm_cave", 1, 600, "Farm", "mushrooms"))).Milestones.Cave);
            await Presence("FarmCave", 5.5f, -5.5f);
            var harvest = new MilestoneRequest(Guid.NewGuid(), "harvest", "", 1, 600, "FarmCave");
            await Apply(harvest);
            Assert.Equal(1, (await Apply(harvest with { RequestId = Guid.NewGuid() })).Milestones.CaveHarvestDay);
            Assert.Single((await State()).Inventory, item => item.ItemId == "object_404" && item.Amount == 1);

            await Presence("1", 5.5f, -5.5f);
            var mined = await client.PostAsJsonAsync("/api/game/resources/collect",
                new CollectResourceRequest(Guid.NewGuid(), "1", 5, 5, "copper_ore", 2));
            mined.EnsureSuccessStatusCode();
            Assert.Equal(1, (await State()).Milestones.FirstCopperDay);
            await Reject(Complete("furnace_blueprint", 1, 600, "Farm"));
            (await client.PostAsJsonAsync("/api/game/day/advance", new AdvanceDayRequest(Guid.NewGuid(), 2)))
                .EnsureSuccessStatusCode();
            Assert.Contains("furnace_blueprint", (await Apply(Complete("furnace_blueprint", 2, 600, "Farm"))).Milestones.Seen);

            var bundleItems = initial.Milestones.Rooms.SelectMany(room => room.Items)
                .GroupBy(item => item.ItemId).Select((group, slot) => new StackEntry
                { Slot = slot, ItemId = group.Key, Amount = group.Sum(item => item.Amount) }).ToArray();
            var calendar = DailyEventTests.LoadCalendar();
            int openingDay = 5;
            await EditState(state =>
            {
                while (DailyEvents.FarmWeather.IsRain(DailyEvents.FarmWeather.ForDay(openingDay, state.UserId, calendar)))
                    openingDay++;
                state.GameDay = openingDay;
                state.InventoryJson = JsonSerializer.Serialize(bundleItems);
                var progress = PlayerProgress.FromJson(state.ProgressJson);
                progress.DailyEvents["community_center"] = 5;
                state.ProgressJson = JsonSerializer.Serialize(progress);
            });
            await Reject(Complete("community_opening", openingDay, 900, "CommunityCenter"));
            await Presence("CommunityCenter", 30.5f, -17.5f);
            foreach (var room in initial.Milestones.Rooms)
                await Apply(new MilestoneRequest(Guid.NewGuid(), "donate", room.Id, openingDay, 900, "CommunityCenter"));
            await Reject(Complete("community_opening", openingDay, 900, "Town"));
            var opened = await Apply(Complete("community_opening", openingDay, 900, "CommunityCenter"));
            Assert.All(opened.Milestones.Rooms, room => Assert.True(room.Done));
            Assert.Equal(7_500, opened.Money);
            var joja = await client.PostAsJsonAsync("/api/game/buy",
                new BuyRequest(Guid.NewGuid(), "seeds_parsnip", 1, "JojaMart"));
            Assert.Equal(HttpStatusCode.BadRequest, joja.StatusCode);

            await EditState(state => state.GameDay = 225);
            await Reject(Complete("grandpa_evaluation", 225, 1900, "Farm"));
            await EditState(state =>
            {
                state.TotalEarnings = 1_000_000;
                state.SkillsJson = "[15000,15000,15000,15000,15000]";
            });
            Assert.Equal(4, (await Apply(Complete("grandpa_evaluation", 225, 2000, "Farm"))).Milestones.Candles);

            async Task<GameSnapshot> State() => (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
            async Task<GameSnapshot> Apply(MilestoneRequest action)
            {
                if (action.Action == "complete") await Presence(action.Location!, 5.5f, -5.5f);
                var response = await client.PostAsJsonAsync("/api/game/milestones", action);
                Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
                return (await response.Content.ReadFromJsonAsync<GameSnapshot>())!;
            }
            async Task Reject(MilestoneRequest action)
            {
                if (action.Action == "complete") await Presence(action.Location!, 5.5f, -5.5f);
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await client.PostAsJsonAsync("/api/game/milestones", action)).StatusCode);
            }
            async Task Presence(string location, float x, float y) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync", new { location, x, y, facingX = 0, facingY = -1 }))
                .EnsureSuccessStatusCode();
            async Task EditState(Action<PlayerState> change)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var user = await db.Users.SingleAsync(user => user.Username == username);
                var state = await db.PlayerStates.SingleAsync(state => state.UserId == user.Id);
                change(state);
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAME_DB_PATH", previous);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MilestoneRequest Complete(string id, int day, int time, string location,
        string? choice = null, string? name = null) =>
        new(Guid.NewGuid(), "complete", id, day, time, location, Choice: choice, Name: name);

    private sealed class TestPresentation : IDialogueDisplayService, IAudioService, ICameraService, IEventProgressTracker
    {
        public List<string> Calls { get; } = new();
        public bool HasSeen(string id) => false;
        public bool HasMail(string id) => false;
        public Task SpeakAsync(string speaker, string text, int portrait, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ChooseAsync(string title, IReadOnlyList<string> options, CancellationToken ct)
        { Calls.Add("choose"); return Task.FromResult(0); }
        public Task<string> AskNameAsync(string title, string initialName, CancellationToken ct)
        { Calls.Add("name"); return Task.FromResult(initialName); }
        public void PlaySound(string cue) { }
        public void PlayMusic(string cue) { }
        public Task FadeAsync(bool toBlack, float seconds, CancellationToken ct)
        { Calls.Add("fade"); return Task.Delay(Timeout.Infinite, ct); }
        public Task CompleteAsync(string id, string choice, string name, string friendshipNpc,
            int friendshipDelta, CancellationToken ct)
        { Calls.Add($"{id}:{choice}:{name}:{friendshipNpc}:{friendshipDelta}"); return Task.CompletedTask; }
    }
}
