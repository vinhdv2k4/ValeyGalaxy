public sealed record RanchProduct(Guid Id, string ItemId, string Location, int X, int Y);
public sealed record RanchAnimalRequest(Guid RequestId, string? AnimalId);
public sealed record RanchBuildingRequest(Guid RequestId, string? BuildingId, int Trough);
public sealed record RanchProductRequest(Guid RequestId, string? ProductId);
public sealed record RanchFieldRequest(Guid RequestId, string? AnimalId, int TileX, int TileY);

public static partial class GameStateEndpoints
{
    private const string HayId = "object_178";
    private const string MilkPailId = "tool_MilkPail";
    private const string ShearsId = "tool_Shears";

    private static RanchOffer SpeciesOf(RanchAnimal animal) =>
        RanchStock.Value.Animals.Single(offer => offer.Id == animal.Species);

    /// <summary>Location name of a building's interior, e.g. "Coop2_&lt;id&gt;".</summary>
    private static string RanchRoomName(RanchBuilding home, int day)
    {
        int level = Math.Max(1, home.LevelOn(day));
        return home.Kind + (level > 1 ? level.ToString() : "") + "_" + home.Id;
    }

    private static int Season(int day) => (Math.Max(1, day) - 1) / 28 % 4;

    private static void MapAnimalCare(WebApplication app)
    {
        app.MapPost("/api/game/ranch/pet", async (HttpRequest request, RanchAnimalRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => PetAnimal(doc, input)));
        app.MapPost("/api/game/ranch/harvest", async (HttpRequest request, RanchAnimalRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => HarvestAnimal(doc, input, catalog)));
        app.MapPost("/api/game/ranch/hay", async (HttpRequest request, RanchBuildingRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => PutHayInTrough(doc, input)));
        app.MapPost("/api/game/ranch/door", async (HttpRequest request, RanchBuildingRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => ToggleAnimalDoor(doc, input)));
        app.MapPost("/api/game/ranch/graze", async (HttpRequest request, RanchFieldRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => GrazeAnimal(doc, input)));
        app.MapPost("/api/game/ranch/dig", async (HttpRequest request, RanchFieldRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => DigTruffle(doc, input)));
        app.MapPost("/api/game/ranch/hopper", async (HttpRequest request, RanchBuildingRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => TakeHayFromHopper(doc, input, catalog)));
        app.MapPost("/api/game/ranch/collect", async (HttpRequest request, RanchProductRequest input, GameDb db,
            IItemCatalog catalog) => await Change(request, db, input.RequestId, doc => CollectRanchProduct(doc, input, catalog)));
    }

    private static (int Index, RanchAnimal? Animal, RanchBuilding? Home) FindAnimal(StateDocument doc, string? id)
    {
        if (!Guid.TryParse(id, out Guid animalId)) return (-1, null, null);
        int index = doc.Progress.RanchAnimals.FindIndex(animal => animal.Id == animalId);
        if (index < 0) return (-1, null, null);
        var animal = doc.Progress.RanchAnimals[index];
        return (index, animal, doc.Progress.RanchHomes.FirstOrDefault(home => home.Id == animal.Home));
    }

    private static bool NearAnimal(StateDocument doc, RanchBuilding home) =>
        WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, RanchRoomName(home, doc.GameDay)) ||
        WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, "Farm");

    private static string? PetAnimal(StateDocument doc, RanchAnimalRequest input)
    {
        var (index, animal, home) = FindAnimal(doc, input.AnimalId);
        if (animal == null || home == null) return "Không tìm thấy vật nuôi.";
        if (!NearAnimal(doc, home)) return "Hãy lại gần vật nuôi.";
        if (animal.Petted) return null;
        doc.Progress.RanchAnimals[index] = animal with
        {
            Petted = true,
            Friendship = Math.Min(1000, animal.Friendship + 15),
            Happiness = Math.Min(255, animal.Happiness + 35)
        };
        return null;
    }

    private static string? HarvestAnimal(StateDocument doc, RanchAnimalRequest input, IItemCatalog catalog)
    {
        var (index, animal, home) = FindAnimal(doc, input.AnimalId);
        if (animal == null || home == null) return "Không tìm thấy vật nuôi.";
        if (!NearAnimal(doc, home)) return "Hãy lại gần vật nuôi.";
        var species = SpeciesOf(animal);
        string? tool = species.Harvest switch { "pail" => MilkPailId, "shears" => ShearsId, _ => null };
        if (tool == null) return "Con vật này không thu hoạch bằng dụng cụ.";
        if (!doc.Inventory.Any(stack => stack.ItemId == tool && stack.Amount > 0))
            return tool == MilkPailId ? "Bạn cần xô vắt sữa." : "Bạn cần kéo xén lông.";
        if (animal.Produce == null) return "Chưa có gì để thu hoạch.";
        if (!catalog.TryGet(animal.Produce, out var item) ||
            !CanAdd(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize))
            return "Túi đồ đã đầy.";
        Add(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize);
        doc.Progress.RanchAnimals[index] = animal with
        {
            Produce = null,
            Friendship = Math.Min(1000, animal.Friendship + 5)
        };
        return null;
    }

    private static string? PutHayInTrough(StateDocument doc, RanchBuildingRequest input)
    {
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy chuồng.";
        int index = doc.Progress.RanchHomes.FindIndex(home => home.Id == id);
        if (index < 0) return "Không tìm thấy chuồng.";
        var home = doc.Progress.RanchHomes[index];
        if (!WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, RanchRoomName(home, doc.GameDay)))
            return "Hãy vào trong chuồng.";
        int troughs = Math.Max(1, home.LevelOn(doc.GameDay)) * 4;
        if (input.Trough < 0 || input.Trough >= troughs) return "Máng ăn không hợp lệ.";
        var hay = home.Hay ?? [];
        if (hay.Contains(input.Trough)) return "Máng này đã có cỏ khô.";
        if (!TakeItems(doc.Inventory, HayId, 1)) return "Bạn không có cỏ khô.";
        doc.Progress.RanchHomes[index] = home with { Hay = hay.Append(input.Trough).Order().ToArray() };
        return null;
    }

    private static string? ToggleAnimalDoor(StateDocument doc, RanchBuildingRequest input)
    {
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy chuồng.";
        int index = doc.Progress.RanchHomes.FindIndex(home => home.Id == id);
        if (index < 0) return "Không tìm thấy chuồng.";
        var home = doc.Progress.RanchHomes[index];
        if (home.LevelOn(doc.GameDay) < 1) return "Chuồng chưa xây xong.";
        var footprint = FootprintOf(home.Kind);
        int doorX = home.X + footprint.AnimalDoorX, doorY = home.Y + footprint.AnimalDoorY;
        if (!WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", doorX, doorY - 1, 2.5f))
            return "Hãy đứng cạnh cửa cho vật nuôi.";
        doc.Progress.RanchHomes[index] = home with { AnimalDoorOpen = !home.AnimalDoorOpen };
        return null;
    }

    private static bool CanGraze(StateDocument doc, RanchBuilding home) =>
        home.LevelOn(doc.GameDay) >= 1 && home.AnimalDoorOpen && Season(doc.GameDay) != 3 &&
        !DailyEvents.FarmWeather.IsRain(WeatherForDay(doc.UserId, doc.GameDay));

    /// <summary>An animal outside eats a grass tuft; the tuft regrows like cut grass.</summary>
    private static string? GrazeAnimal(StateDocument doc, RanchFieldRequest input)
    {
        var (index, animal, home) = FindAnimal(doc, input.AnimalId);
        if (animal == null || home == null) return "Không tìm thấy vật nuôi.";
        if (!CanGraze(doc, home)) return "Vật nuôi không ở ngoài đồng cỏ.";
        if (animal.Grazed) return null;
        if (input.TileX is < 0 or > 512 || input.TileY is < 0 or > 512) return "Ô cỏ không hợp lệ.";
        string key = $"Farm:{input.TileX}:{input.TileY}";
        var changes = doc.Progress.NaturalResources;
        if (changes.TryGetValue(key, out var previous) &&
            (previous.Kind != "grass" || previous.Stump || doc.GameDay < previous.RegrowDay))
            return "Ô cỏ này đã bị ăn hết.";
        changes[key] = new("Farm", input.TileX, input.TileY, "grass", false, 0, NextGrowingDay(doc.GameDay, 3));
        doc.Progress.RanchAnimals[index] = animal with
        {
            Grazed = true,
            Happiness = Math.Min(255, animal.Happiness + 20)
        };
        return null;
    }

    /// <summary>A grown pig outside roots up a truffle near its barn.</summary>
    private static string? DigTruffle(StateDocument doc, RanchFieldRequest input)
    {
        var (index, animal, home) = FindAnimal(doc, input.AnimalId);
        if (animal == null || home == null) return "Không tìm thấy vật nuôi.";
        var species = SpeciesOf(animal);
        if (species.Harvest != "dig" || animal.Age < species.Mature) return "Con vật này không đào nấm.";
        if (!CanGraze(doc, home)) return "Vật nuôi không ở ngoài đồng cỏ.";
        int limit = Math.Min(5, 1 + animal.Friendship / 250);
        if (animal.Dug >= limit) return "Hôm nay nó đã đào đủ rồi.";
        var footprint = FootprintOf(home.Kind);
        int doorX = home.X + footprint.AnimalDoorX;
        int x = input.TileX, y = input.TileY;
        if (x < doorX - 5 || x >= doorX + 6 || y < home.Y + footprint.Height || y >= home.Y + footprint.Height + 7)
            return "Vị trí đào không hợp lệ.";
        if (doc.Progress.RanchHomes.Any(other => InRanchBuilding(other, x, y) || DoorstepOf(other) == (x, y)) ||
            doc.Progress.RanchProducts.Any(product => product.Location == "Farm" && product.X == x && product.Y == y))
            return "Vị trí đào không hợp lệ.";
        doc.Progress.RanchProducts.Add(new(Guid.NewGuid(), species.Produce, "Farm", x, y));
        doc.Progress.RanchAnimals[index] = animal with { Dug = animal.Dug + 1 };
        return null;
    }

    /// <summary>Feed hopper: takes enough hay from the silos for the empty troughs.</summary>
    private static string? TakeHayFromHopper(StateDocument doc, RanchBuildingRequest input, IItemCatalog catalog)
    {
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy chuồng.";
        var home = doc.Progress.RanchHomes.FirstOrDefault(item => item.Id == id);
        if (home == null || home.Kind == "Silo") return "Không tìm thấy chuồng.";
        if (!WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, RanchRoomName(home, doc.GameDay)))
            return "Hãy vào trong chuồng.";
        if (HayCapacity(doc) == 0) return "Bạn cần xây Silo để chứa cỏ khô.";
        if (doc.Progress.PiecesOfHay <= 0) return "Silo đã hết cỏ khô.";
        int animals = doc.Progress.RanchAnimals.Count(animal => animal.Home == home.Id);
        int empty = Math.Max(1, home.LevelOn(doc.GameDay)) * 4 - (home.Hay?.Length ?? 0);
        int amount = Math.Min(Math.Min(Math.Max(1, animals), doc.Progress.PiecesOfHay), empty);
        if (amount <= 0) return "Các máng đã đầy cỏ khô.";
        if (!catalog.TryGet(HayId, out var hay) || !CanAdd(doc.Inventory, hay.Id, amount, hay.MaxStack, doc.BagSize))
            return "Túi đồ đã đầy.";
        Add(doc.Inventory, hay.Id, amount, hay.MaxStack, doc.BagSize);
        doc.Progress.PiecesOfHay -= amount;
        return null;
    }

    private static string? CollectRanchProduct(StateDocument doc, RanchProductRequest input, IItemCatalog catalog)
    {
        if (!Guid.TryParse(input.ProductId, out Guid id)) return "Không tìm thấy vật phẩm.";
        var product = doc.Progress.RanchProducts.FirstOrDefault(item => item.Id == id);
        if (product == null) return "Không tìm thấy vật phẩm.";
        bool near = product.Location == "Farm"
            ? WorldSessionEndpoints.IsPlayerNear(doc.UserId, "Farm", product.X, product.Y - 1, 2.5f)
            : doc.Progress.RanchHomes.FirstOrDefault(home => home.Id.ToString() == product.Location) is { } home &&
              WorldSessionEndpoints.IsPlayerAtLocation(doc.UserId, RanchRoomName(home, doc.GameDay));
        if (!near) return "Hãy lại gần vật phẩm.";
        if (!catalog.TryGet(product.ItemId, out var item) ||
            !CanAdd(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize))
            return "Túi đồ đã đầy.";
        Add(doc.Inventory, item.Id, 1, item.MaxStack, doc.BagSize);
        doc.Progress.RanchProducts.Remove(product);
        return null;
    }

    /// <summary>
    /// Overnight update for every animal: grazing or hay, growing up, mood, friendship and produce.
    /// <paramref name="day"/> is the day that just ended.
    /// </summary>
    private static void UpdateRanchAnimals(StateDocument doc, int day, bool rained)
    {
        var progress = doc.Progress;
        var random = new Random(HashCode.Combine(doc.UserId, day));
        bool winter = Season(day) == 3;
        for (int b = 0; b < progress.RanchHomes.Count; b++)
        {
            var home = progress.RanchHomes[b];
            int level = home.LevelOn(day);
            if (level < 1) continue;
            if (home.Kind == "Silo") continue;
            var hay = (home.Hay ?? []).ToList();
            var plan = RanchStock.Value.Plans.Single(plan => plan.Kind == home.Kind && plan.Level == level);
            for (int i = 0; i < progress.RanchAnimals.Count; i++)
            {
                var animal = progress.RanchAnimals[i];
                if (animal.Home != home.Id) continue;
                var species = SpeciesOf(animal);
                bool grazed = animal.Grazed && home.AnimalDoorOpen && !rained && !winter;
                bool fed = grazed;
                if (!fed && hay.Count > 0)
                {
                    hay.RemoveAt(0);
                    fed = true;
                }

                int friendship = animal.Friendship, happiness = animal.Happiness;
                if (!animal.Petted)
                {
                    friendship = Math.Max(0, friendship - (10 - friendship / 200));
                    happiness = Math.Max(0, happiness - 25);
                }
                int age = animal.Age;
                if (fed)
                {
                    age++;
                    happiness = Math.Min(255, happiness + (grazed ? 40 : 14));
                }
                else
                {
                    happiness = Math.Max(0, happiness - 100);
                    friendship = Math.Max(0, friendship - 20);
                }

                string? produce = animal.Produce;
                int sinceLay = animal.DaysSinceLay + 1;
                bool adult = age >= species.Mature;
                if (adult && animal.Age < species.Mature && species.Harvest == "shears")
                {
                    produce = species.Produce;
                    sinceLay = 0;
                }
                else if (adult && fed && sinceLay >= species.DaysToLay && random.NextDouble() < happiness / 70.0)
                {
                    string item = PickProduce(animal, species, friendship, happiness, random);
                    if (species.Harvest != "dig") item = WithQuality(item, ProduceQuality(friendship, happiness, random));
                    sinceLay = 0;
                    switch (species.Harvest)
                    {
                        case "pail" or "shears":
                            produce ??= item;
                            break;
                        case "dig":
                            break;
                        default:
                            DropInRoom(doc, home, plan, item, random);
                            break;
                    }
                }

                progress.RanchAnimals[i] = animal with
                {
                    Age = age, Friendship = friendship, Happiness = happiness, Petted = false,
                    DaysSinceLay = Math.Min(sinceLay, 99), Produce = produce, Hungry = !fed, Grazed = false, Dug = 0
                };
            }
            if (level >= 3)
            {
                int animalsHere = progress.RanchAnimals.Count(animal => animal.Home == home.Id);
                for (int trough = 0; trough < level * 4 && hay.Count < animalsHere && progress.PiecesOfHay > 0; trough++)
                {
                    if (hay.Contains(trough)) continue;
                    hay.Add(trough);
                    progress.PiecesOfHay--;
                }
                hay.Sort();
            }
            progress.RanchHomes[b] = home with { Hay = hay.ToArray() };
        }
    }

    private static string PickProduce(RanchAnimal animal, RanchOffer species, int friendship, int happiness, Random random)
    {
        bool alt = animal.Variant == 1 && species.AltProduce != null;
        string normal = alt ? species.AltProduce! : species.Produce;
        string deluxe = alt ? species.AltDeluxe ?? species.Deluxe : species.Deluxe;
        float mood = happiness > 200 ? happiness * 1.5f : happiness <= 100 ? happiness - 100 : 0f;
        double chance = species.Id switch
        {
            "duck" => (friendship + mood) / 4750.0,
            "rabbit" => (friendship + mood) / 5000.0,
            _ => friendship >= 200 ? (friendship + mood) / 1200.0 : 0.0
        };
        return random.NextDouble() < happiness / 150.0 && random.NextDouble() < chance ? deluxe : normal;
    }

    private static void DropInRoom(StateDocument doc, RanchBuilding home, RanchPlan plan, string item, Random random)
    {
        string location = home.Id.ToString();
        var taken = doc.Progress.RanchProducts.Where(product => product.Location == location)
            .Select(product => (product.X, product.Y)).ToHashSet();
        int[] area = plan.ProduceArea;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int x = area[0] + random.Next(area[2]), y = area[1] + random.Next(area[3]);
            if (taken.Contains((x, y))) continue;
            doc.Progress.RanchProducts.Add(new(Guid.NewGuid(), item, location, x, y));
            return;
        }
    }

    private static int ProduceQuality(int friendship, int happiness, Random random)
    {
        double chance = friendship / 1000.0 - (1.0 - happiness / 225.0);
        if (chance >= 0.95 && random.NextDouble() < chance / 2.0) return 4;
        if (random.NextDouble() < chance / 2.0) return 2;
        return random.NextDouble() < chance ? 1 : 0;
    }

    /// <summary>Quality rides on the item id: "object_176@2" is a gold-star egg.</summary>
    internal static string WithQuality(string itemId, int quality) => quality > 0 ? $"{itemId}@{quality}" : itemId;
}
