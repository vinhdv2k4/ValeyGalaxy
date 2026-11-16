using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Database")]
public sealed class DailyEventTests
{
    internal static DailyEventCalendar LoadCalendar() => new(JsonSerializer.Deserialize<DailyEventFile>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContentData", "DailyEvents.json")),
        new JsonSerializerOptions { IncludeFields = true })!);

    [Fact]
    public void DatesKeepSameDayEventsAndRepeatOnlySeasonalContent()
    {
        var catalog = LoadCalendar();
        Assert.Equal(49, catalog.Events.Count);
        Assert.Equal(new[] { "night_market", "winter_17_1" },
            catalog.On(new FarmDate(101)).Select(entry => entry.id));
        Assert.Single(catalog.On(new FarmDate(213)), entry => entry.id == "night_market");
        Assert.Empty(catalog.On(new FarmDate(115)));
        Assert.True(catalog.IsFestivalDay(new FarmDate(125)));
        Assert.False(catalog.IsFestivalDay(new FarmDate(99)));
        var summons = catalog.Find("wizardJunimoNote")!;
        int Completed(string id) => id == "junimo_scroll" ? 9 : 0;
        Assert.False(catalog.IsAvailable(summons, new FarmDate(9), Completed));
        Assert.True(catalog.IsAvailable(summons, new FarmDate(10), Completed));
    }

    [Fact]
    public void WeatherUsesTheOriginalCalendarOverrides()
    {
        var calendar = LoadCalendar();
        var saveId = Guid.Parse("0e16ccf2-875f-476c-85d9-3db74f9d2eb9");
        int Weather(int day) => FarmWeather.ForDay(day, saveId, calendar);
        Assert.Equal(FarmWeather.Clear, Weather(2));
        Assert.Equal(FarmWeather.Rain, Weather(3));
        Assert.Equal(FarmWeather.Clear, Weather(4));
        Assert.Equal(FarmWeather.Festival, Weather(13));
        Assert.Equal(FarmWeather.Clear, Weather(29));
        Assert.Equal(FarmWeather.Storm, Weather(41));
        Assert.Equal(FarmWeather.Storm, Weather(54));
        Assert.Equal(FarmWeather.Clear, Weather(98));
        Assert.Equal(FarmWeather.Clear, Weather(100));
        for (int day = 85; day <= 112; day++)
            Assert.False(FarmWeather.IsRain(Weather(day)));
    }

    [Fact]
    public void MailboxUsesActualDeliveryDayAndSkipsReadLetters()
    {
        var catalog = LoadCalendar();
        var completed = new Dictionary<string, int>();
        int Completed(string id) => completed.GetValueOrDefault(id);

        Assert.Null(catalog.FirstUnreadMail(new FarmDate(1), Completed));
        Assert.Equal("spring_2_1", catalog.FirstUnreadMail(new FarmDate(12), Completed)?.id);
        completed["spring_2_1"] = 12;
        completed["junimo_scroll"] = 11;
        Assert.Equal("landslideDone", catalog.FirstUnreadMail(new FarmDate(12), Completed)?.id);
        completed["landslideDone"] = 12;
        Assert.Equal("spring_11_1", catalog.FirstUnreadMail(new FarmDate(12), Completed)?.id);
        completed["spring_11_1"] = 12;
        Assert.Equal("wizardJunimoNote", catalog.FirstUnreadMail(new FarmDate(12), Completed)?.id);
    }

    [Fact]
    public async Task DailyActionsPersistAndRejectPrematureOrDuplicateRewards()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-events-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "events.db"),
                "DailyEventsTestsSecret_2026_MustBeLongEnough");
            using var client = factory.CreateClient();
            string username = "event_" + Guid.NewGuid().ToString("N")[..8];
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, username + "@example.com", "Password123!", "Password123!"));
            registration.EnsureSuccessStatusCode();
            var account = await registration.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", account.GetProperty("token").GetString());
            var first = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.DoesNotContain(first!.Inventory, stack => stack.ItemId == "fishing_rod");

            Task<HttpResponseMessage> Act(DailyEventRequest action) =>
                client.PostAsJsonAsync("/api/game/events/action", action);

            Assert.Equal(HttpStatusCode.BadRequest, (await Act(
                new(Guid.NewGuid(), "spring_2_1", "read", 1, "Farm"))).StatusCode);
            (await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 2))).EnsureSuccessStatusCode();
            var willy = LoadCalendar().Find("willy_rod")!;
            var encounter = new DailyEventRequest(Guid.NewGuid(), willy.id, "cutscene", 2, willy.location, 1700);
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new { location = willy.location, x = 34.5f, y = -30.5f, facingX = 0, facingY = -1 })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Act(encounter)).StatusCode);
            (await Act(new(Guid.NewGuid(), "spring_2_1", "read", 2, "Farm"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Act(encounter with { Action = "meet" })).StatusCode);
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(64.5f, -18.5f, 0, 1, "Farm"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Act(encounter)).StatusCode);
            (await client.PostAsJsonAsync("/api/world/presence/sync",
                new WorldPlayerSyncRequest(34.5f, -2.5f, 0, -1, "Beach"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Act(encounter with { Time = 1710 })).StatusCode);
            (await Act(encounter)).EnsureSuccessStatusCode();
            (await Act(encounter)).EnsureSuccessStatusCode();
            (await Act(encounter with { RequestId = Guid.NewGuid() })).EnsureSuccessStatusCode();
            var saved = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Single(saved!.Inventory, stack => stack.ItemId == "fishing_rod" && stack.Amount == 1);
            Assert.Contains(new DailyEventStamp("spring_2_1", 2), saved.DailyEvents.Completed);
            Assert.Contains(new DailyEventStamp("willy_rod", 2), saved.DailyEvents.Completed);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var userId = await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
                await db.PlayerStates.Where(state => state.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.GameDay, 102));
            }
            var before = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Empty(before!.DailyEvents.SecretRecipient);
            var letter = await Act(new(Guid.NewGuid(), "winter_18", "read", 102, "Farm"));
            letter.EnsureSuccessStatusCode();
            var assigned = (await letter.Content.ReadFromJsonAsync<GameSnapshot>())!.DailyEvents.SecretRecipient;
            Assert.Contains(assigned, LoadCalendar().Villagers);
            var after = await client.GetFromJsonAsync<GameSnapshot>("/api/game/state");
            Assert.Equal(assigned, after!.DailyEvents.SecretRecipient);
            Assert.Empty(after.DailyEvents.SecretGiver);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MineOpensOnDayFiveAndPersistsOnlySequentialFloorArrivals()
    {
        string directory = Path.Combine(Path.GetTempPath(), "doan-mine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = DatabaseTestServer.Create(Path.Combine(directory, "mine.db"),
                "MineProgressTestsSecret_2026_MustBeLongEnough");
            using var client = factory.CreateClient();
            string username = "mine_" + Guid.NewGuid().ToString("N")[..8];
            const string password = "Password123!";
            await SignIn(await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(username, username + "@example.com", password, password)));
            await Move("Mine");
            Assert.Equal(HttpStatusCode.BadRequest, (await Arrive(1)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Event("landslideDone", "read", 1)).StatusCode);
            await Move("FarmHouse");
            for (int day = 2; day <= 4; day++)
                (await client.PostAsJsonAsync("/api/game/day/advance",
                    new AdvanceDayRequest(Guid.NewGuid(), day))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await ClearLandslide(4, "FarmHouse")).StatusCode);
            (await client.PostAsJsonAsync("/api/game/day/advance",
                new AdvanceDayRequest(Guid.NewGuid(), 5))).EnsureSuccessStatusCode();
            (await Event("landslideDone", "read", 5)).EnsureSuccessStatusCode();
            await Move("Mountain");
            Assert.Equal(HttpStatusCode.BadRequest, (await ClearLandslide(5, "Mountain")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ClearLandslide(5, "FarmHouse", 900)).StatusCode);
            // The private dawn cutaway finishes even if world presence still shows the previous map.
            (await ClearLandslide(5, "FarmHouse")).EnsureSuccessStatusCode();
            (await ClearLandslide(5, "FarmHouse")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await Event("marlon_sword", "cutscene", 5)).StatusCode);
            await Move("Mine");
            (await Event("marlon_sword", "cutscene", 5)).EnsureSuccessStatusCode();
            (await Event("marlon_sword", "cutscene", 5)).EnsureSuccessStatusCode();

            await Move("120");
            Assert.Equal(0, (await State()).DeepestMineFloor);
            Assert.Equal(HttpStatusCode.BadRequest, (await Arrive(120)).StatusCode);
            await Move("Mine");
            for (int floor = 1; floor <= 6; floor++)
            {
                var response = await Arrive(floor);
                response.EnsureSuccessStatusCode();
                Assert.Equal(floor, (await response.Content.ReadFromJsonAsync<GameSnapshot>())!.DeepestMineFloor);
                await Move(floor.ToString());
            }
            await Move("Mine");
            await SignIn(await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password)));
            var saved = await State();
            Assert.Equal(6, saved.DeepestMineFloor);
            Assert.Single(saved.Inventory, stack => stack.ItemId == "weapon_0" && stack.Amount == 1);
            Assert.Single(saved.Milestones.Seen, id => id == "mine_landslide_clear");
            Assert.Contains(new DailyEventStamp("marlon_sword", 5), saved.DailyEvents.Completed);

            async Task SignIn(HttpResponseMessage response)
            {
                response.EnsureSuccessStatusCode();
                var account = await response.Content.ReadFromJsonAsync<JsonElement>();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", account.GetProperty("token").GetString());
            }
            async Task Move(string map) =>
                (await client.PostAsJsonAsync("/api/world/presence/sync",
                    new WorldPlayerSyncRequest(18.5f, -12.5f, 0, 1, map))).EnsureSuccessStatusCode();
            Task<HttpResponseMessage> Arrive(int floor) =>
                client.PostAsJsonAsync("/api/game/mine/arrive", new MineArrivalRequest(Guid.NewGuid(), floor));
            Task<HttpResponseMessage> Event(string id, string action, int day) =>
                client.PostAsJsonAsync("/api/game/events/action",
                    new DailyEventRequest(Guid.NewGuid(), id, action, day, "Mine", 900));
            Task<HttpResponseMessage> ClearLandslide(int day, string location, int time = 600) =>
                client.PostAsJsonAsync("/api/game/milestones",
                    new MilestoneRequest(Guid.NewGuid(), "complete", "mine_landslide_clear", day, time, location));
            async Task<GameSnapshot> State() => (await client.GetFromJsonAsync<GameSnapshot>("/api/game/state"))!;
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
