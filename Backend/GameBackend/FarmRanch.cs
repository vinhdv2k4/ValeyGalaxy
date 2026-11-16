using System.Text.Json;

public sealed record RanchConstruction(int Level, int TargetLevel, int ReadyDay);

public sealed record RanchPaintRegion(bool Custom, int Hue, int Saturation, int Lightness);

public sealed record RanchBuilding(Guid Id, string Kind, int X, int Y, int Level, int TargetLevel, int ReadyDay,
    RanchPaintRegion[]? Paint = null, bool AnimalDoorOpen = false, int[]? Hay = null)
{
    public int LevelOn(int day) => day >= ReadyDay ? TargetLevel : Level;
}

public sealed record RanchHomeStatus(Guid Id, string Kind, int X, int Y, int Width, int Height,
    int Level, int TargetLevel, int ReadyDay, int Capacity, RanchPaintRegion[] Paint, bool AnimalDoorOpen, int[] Hay,
    bool HasDoor);

public sealed record RanchAnimal(Guid Id, string Species, string Kind, string Name, int Variant, Guid Home = default,
    int Age = 0, int Friendship = 0, int Happiness = 255, bool Petted = false, int DaysSinceLay = 0,
    string? Produce = null, bool Hungry = false, bool Grazed = false, int Dug = 0);
public sealed record RanchBuildRequest(Guid RequestId, string? Kind, int Level, string? BuildingId, int X, int Y);
public sealed record RanchMoveRequest(Guid RequestId, string? BuildingId, int X, int Y);
public sealed record RanchDemolishRequest(Guid RequestId, string? BuildingId);
public sealed record RanchPaintRequest(Guid RequestId, string? BuildingId, RanchPaintRegion[]? Regions);
public sealed record RanchBuyRequest(Guid RequestId, string? Species, string? Name, string? Home);

public static partial class GameStateEndpoints
{
    private sealed record RanchOffer(string Id, string Name, string Kind, int Level, int Price, string Produce,
        string Deluxe, string? AltProduce, string? AltDeluxe, int DaysToLay, int Mature, string Harvest, int Variants);
    private sealed record RanchMaterial(string Id, int Amount);
    private sealed record RanchPlan(string Kind, int Level, string Name, int Price, int Wood, int Stone, int Days,
        int[] ProduceArea, RanchMaterial[]? Materials);
    private sealed record RanchFootprint(string Kind, int Width, int Height, int DoorX, int AnimalDoorX,
        int AnimalDoorY, int TroughX, int TroughY, int HopperX, int HopperY)
    {
        public bool HasDoor => DoorX >= 0;
    }

    private const int SiloCapacity = 240;
    private static readonly (int X, int Y) NoDoorstep = (-1000, -1000);

    private static int HayCapacity(StateDocument doc) =>
        doc.Progress.RanchHomes.Count(home => home.Kind == "Silo" && home.LevelOn(doc.GameDay) >= 1) * SiloCapacity;
    private sealed record RanchPaintRange(string Name, int Min, int Max);
    private sealed record RanchPaintable(string Building, RanchPaintRange[] Regions);
    private sealed record RanchCatalog(RanchOffer[] Animals, RanchPlan[] Plans, RanchFootprint[] Footprints,
        RanchPaintable[] Paint);
    private static readonly Lazy<RanchCatalog> RanchStock = new(() =>
        JsonSerializer.Deserialize<RanchCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "ContentData", "RanchShop.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("RanchShop catalog is missing."));

    private const int FarmWidth = 80, FarmHeight = 65;

    // Map-tile rectangles (x, y, width, height) that no animal building may cover.
    private static readonly (int X, int Y, int W, int H)[] FarmLandmarks =
    {
        (59, 11, 9, 7),   // farmhouse and its porch
        (71, 14, 2, 1),   // shipping bin
        (25, 10, 7, 7),   // greenhouse and its doorstep
    };

    private static RanchFootprint FootprintOf(string kind) => RanchStock.Value.Footprints.Single(f => f.Kind == kind);

    // The tile directly below the door must stay walkable.
    private static (int X, int Y) DoorstepOf(RanchBuilding building)
    {
        var footprint = FootprintOf(building.Kind);
        return footprint.HasDoor ? (building.X + footprint.DoorX, building.Y + footprint.Height) : NoDoorstep;
    }

    private static bool Covers(int x, int y, int w, int h, int tileX, int tileY) =>
        tileX >= x && tileX < x + w && tileY >= y && tileY < y + h;

    private static bool InRanchBuilding(RanchBuilding building, int mapX, int mapY)
    {
        var footprint = FootprintOf(building.Kind);
        return Covers(building.X, building.Y, footprint.Width, footprint.Height, mapX, mapY);
    }

    // Farm API rows are indexed from -worldY, one row above the map's cell index.
    private static bool InBuiltRanchPlot(StateDocument doc, int x, int y) =>
        doc.Progress.RanchHomes.Any(building => InRanchBuilding(building, x, y + 1) ||
            DoorstepOf(building) == (x, y + 1));

    private static bool RanchConstructionPending(StateDocument doc) =>
        doc.Progress.RanchHomes.Any(home => home.ReadyDay > doc.GameDay);

    private static void MapRanch(WebApplication app)
    {
        app.MapPost("/api/game/ranch/build", async (HttpRequest request, RanchBuildRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => OrderAnimalHousing(doc, input, db)));
        app.MapPost("/api/game/ranch/move", async (HttpRequest request, RanchMoveRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => MoveAnimalHousing(doc, input, db)));
        app.MapPost("/api/game/ranch/demolish", async (HttpRequest request, RanchDemolishRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => DemolishAnimalHousing(doc, input)));
        app.MapPost("/api/game/ranch/paint", async (HttpRequest request, RanchPaintRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => PaintAnimalHousing(doc, input)));
        MapAnimalCare(app);
        app.MapPost("/api/game/ranch/buy", async (HttpRequest request, RanchBuyRequest input, GameDb db) =>
            await Change(request, db, input.RequestId, doc => BuyRanchAnimal(doc, input)));
    }

    private static string? RanchSiteError(StateDocument doc, GameDb db, string kind, int x, int y, Guid ignore)
    {
        var footprint = FootprintOf(kind);
        (int doorX, int doorY) = footprint.HasDoor ? (x + footprint.DoorX, y + footprint.Height) : NoDoorstep;
        if (x < 1 || y < 1 || x + footprint.Width > FarmWidth - 1 || y + footprint.Height > FarmHeight - 2)
            return "Không thể xây ở đây.";

        bool Blocked(int tileX, int tileY) =>
            Covers(x, y, footprint.Width, footprint.Height, tileX, tileY) || (tileX, tileY) == (doorX, doorY);

        foreach (var mark in FarmLandmarks)
            for (int tx = mark.X; tx < mark.X + mark.W; tx++)
            for (int ty = mark.Y; ty < mark.Y + mark.H; ty++)
                if (Blocked(tx, ty)) return "Không thể xây ở đây.";

        foreach (var other in doc.Progress.RanchHomes)
        {
            if (other.Id == ignore) continue;
            var otherFootprint = FootprintOf(other.Kind);
            var step = DoorstepOf(other);
            if (Blocked(step.X, step.Y) ||
                Covers(other.X, other.Y, otherFootprint.Width, otherFootprint.Height, doorX, doorY))
                return "Không thể xây ở đây.";
            for (int tx = other.X; tx < other.X + otherFootprint.Width; tx++)
            for (int ty = other.Y; ty < other.Y + otherFootprint.Height; ty++)
                if (Blocked(tx, ty)) return "Không thể xây ở đây.";
        }

        // Soil, chests and crops use farm API rows (map row - 1).
        if (doc.Progress.FarmSoil.Values.Any(tile => Blocked(tile.TileX, tile.TileY + 1)) ||
            doc.Chests.Any(chest => chest.Location == "Farm" && Blocked(chest.TileX, chest.TileY + 1)) ||
            db.FarmCropStates.Where(crop => crop.UserId == doc.UserId).Select(crop => new { crop.TileX, crop.TileY })
                .AsEnumerable().Any(crop => Blocked(crop.TileX, crop.TileY + 1)))
            return "Hãy dọn đất đã cuốc, cây trồng và rương ở chỗ này trước.";
        return null;
    }

    private static string? OrderAnimalHousing(StateDocument doc, RanchBuildRequest input, GameDb db)
    {
        if (!AtMerchantCounter(doc.UserId, "Robin"))
            return "Hãy đứng ở quầy của Robin.";
        var plan = RanchStock.Value.Plans.SingleOrDefault(plan => plan.Kind == input.Kind && plan.Level == input.Level);
        if (plan == null) return "Bản vẽ chuồng không hợp lệ.";
        if (RanchConstructionPending(doc) || doc.Progress.HouseUpgradeDay > doc.GameDay)
            return "Robin đang thi công công trình khác.";
        if (doc.GameDay > int.MaxValue - plan.Days) return "Ngày hoàn tất công trình không hợp lệ.";

        var homes = doc.Progress.RanchHomes;
        int index = -1;
        if (!string.IsNullOrEmpty(input.BuildingId))
        {
            if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy chuồng cần nâng cấp.";
            index = homes.FindIndex(home => home.Id == id);
            if (index < 0 || homes[index].Kind != plan.Kind) return "Không tìm thấy chuồng cần nâng cấp.";
            if (homes[index].LevelOn(doc.GameDay) != plan.Level - 1) return "Hãy xây chuồng ở cấp liền trước.";
        }
        else
        {
            if (plan.Level != 1) return "Hãy chọn chuồng cần nâng cấp.";
            string? site = RanchSiteError(doc, db, plan.Kind, input.X, input.Y, Guid.Empty);
            if (site != null) return site;
        }

        if (doc.Money < plan.Price) return $"Bạn cần {plan.Price} G.";
        var materials = plan.Materials ?? [];
        if (materials.Any(material => doc.Inventory.Where(s => s.ItemId == material.Id).Sum(s => s.Amount) < material.Amount))
            return "Bạn chưa đủ vật liệu.";
        if (doc.Inventory.Where(s => s.ItemId == "wood").Sum(s => s.Amount) < plan.Wood || doc.Inventory.Where(s => s.ItemId == "stone").Sum(s => s.Amount) < plan.Stone)
            return $"Bạn cần {plan.Wood} gỗ và {plan.Stone} đá.";
        TakeItems(doc.Inventory, "wood", plan.Wood);
        TakeItems(doc.Inventory, "stone", plan.Stone);
        foreach (var material in materials) TakeItems(doc.Inventory, material.Id, material.Amount);
        doc.Money -= plan.Price;
        int readyDay = doc.GameDay + plan.Days;
        if (index >= 0)
            homes[index] = homes[index] with { Level = plan.Level - 1, TargetLevel = plan.Level, ReadyDay = readyDay };
        else
            homes.Add(new(Guid.NewGuid(), plan.Kind, input.X, input.Y, 0, 1, readyDay));
        return null;
    }

    private static string? MoveAnimalHousing(StateDocument doc, RanchMoveRequest input, GameDb db)
    {
        if (!AtMerchantCounter(doc.UserId, "Robin"))
            return "Hãy đứng ở quầy của Robin.";
        var homes = doc.Progress.RanchHomes;
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy công trình.";
        int index = homes.FindIndex(home => home.Id == id);
        if (index < 0) return "Không tìm thấy công trình.";
        if (homes[index].ReadyDay > doc.GameDay) return "Không thể di chuyển công trình đang thi công.";
        if (homes[index].X == input.X && homes[index].Y == input.Y) return null;
        string? site = RanchSiteError(doc, db, homes[index].Kind, input.X, input.Y, id);
        if (site != null) return site;
        homes[index] = homes[index] with { X = input.X, Y = input.Y };
        return null;
    }

    private static string? DemolishAnimalHousing(StateDocument doc, RanchDemolishRequest input)
    {
        if (!AtMerchantCounter(doc.UserId, "Robin"))
            return "Hãy đứng ở quầy của Robin.";
        var homes = doc.Progress.RanchHomes;
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy công trình.";
        int index = homes.FindIndex(home => home.Id == id);
        if (index < 0) return "Không tìm thấy công trình.";
        if (homes[index].ReadyDay > doc.GameDay) return "Không thể phá công trình đang thi công.";
        if (doc.Progress.RanchAnimals.Any(animal => animal.Home == id))
            return "Không thể phá chuồng khi vẫn còn vật nuôi sống ở đó.";
        homes.RemoveAt(index);
        doc.Progress.RanchProducts.RemoveAll(product => product.Location == id.ToString());
        doc.Progress.PiecesOfHay = Math.Min(doc.Progress.PiecesOfHay, HayCapacity(doc));
        return null;
    }

    private static string? PaintAnimalHousing(StateDocument doc, RanchPaintRequest input)
    {
        if (!AtMerchantCounter(doc.UserId, "Robin"))
            return "Hãy đứng ở quầy của Robin.";
        var homes = doc.Progress.RanchHomes;
        if (!Guid.TryParse(input.BuildingId, out Guid id)) return "Không tìm thấy công trình.";
        int index = homes.FindIndex(home => home.Id == id);
        if (index < 0) return "Không tìm thấy công trình.";
        var home = homes[index];
        if (home.ReadyDay > doc.GameDay) return "Không thể sơn công trình đang thi công.";
        string? name = RanchStock.Value.Plans.SingleOrDefault(plan => plan.Kind == home.Kind &&
            plan.Level == home.LevelOn(doc.GameDay))?.Name;
        var paintable = RanchStock.Value.Paint.SingleOrDefault(entry => entry.Building == name);
        if (paintable == null) return "Công trình này không sơn được.";
        var regions = input.Regions ?? [];
        if (regions.Length != paintable.Regions.Length) return "Dữ liệu màu sơn không hợp lệ.";
        for (int i = 0; i < regions.Length; i++)
        {
            var region = regions[i];
            if (region == null) return "Dữ liệu màu sơn không hợp lệ.";
            if (!region.Custom) continue;
            if (region.Hue is < 0 or > 360 || region.Saturation is < 0 or > 75 ||
                region.Lightness < paintable.Regions[i].Min || region.Lightness > paintable.Regions[i].Max)
                return "Dữ liệu màu sơn không hợp lệ.";
        }
        homes[index] = home with { Paint = regions.Any(region => region.Custom) ? regions : null };
        return null;
    }

    private static string? BuyRanchAnimal(StateDocument doc, RanchBuyRequest input)
    {
        if (!AtMerchantCounter(doc.UserId, "Marnie"))
            return "Hãy đứng ở quầy của Marnie.";
        var offer = RanchStock.Value.Animals.SingleOrDefault(offer => offer.Id == input.Species);
        if (offer == null) return "Loài vật này không có bán.";
        string name = input.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 18 || name.Any(char.IsControl))
            return "Tên thú nuôi cần từ 1 đến 18 ký tự.";
        var animals = doc.Progress.RanchAnimals;
        if (animals.Any(animal => animal.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return "Tên này đã được dùng cho một con vật khác.";

        Guid? wanted = Guid.TryParse(input.Home, out Guid homeId) ? homeId : null;
        var candidates = doc.Progress.RanchHomes
            .Where(home => home.Kind == offer.Kind && (wanted == null || home.Id == wanted))
            .ToList();
        if (candidates.All(home => home.LevelOn(doc.GameDay) < offer.Level))
            return "Bạn cần " + RanchStock.Value.Plans.Single(plan => plan.Kind == offer.Kind && plan.Level == offer.Level).Name + ".";
        var home = candidates.FirstOrDefault(home => home.LevelOn(doc.GameDay) >= offer.Level &&
            animals.Count(animal => animal.Home == home.Id) < home.LevelOn(doc.GameDay) * 4);
        if (home == null) return "Chuồng này đã đầy.";
        if (doc.Money < offer.Price) return $"Bạn cần {offer.Price} G.";
        doc.Money -= offer.Price;
        animals.Add(new(Guid.NewGuid(), offer.Id, offer.Kind, name, Random.Shared.Next(Math.Max(1, offer.Variants)), home.Id));
        return null;
    }

    private static RanchHomeStatus[] RanchHomeStatuses(StateDocument doc) => doc.Progress.RanchHomes.Select(home =>
    {
        var footprint = FootprintOf(home.Kind);
        int level = home.LevelOn(doc.GameDay);
        return new RanchHomeStatus(home.Id, home.Kind, home.X, home.Y, footprint.Width, footprint.Height,
            level, home.TargetLevel, home.ReadyDay, home.Kind == "Silo" ? 0 : level * 4, home.Paint ?? [],
            home.AnimalDoorOpen, home.Hay ?? [], footprint.HasDoor);
    }).ToArray();
}
