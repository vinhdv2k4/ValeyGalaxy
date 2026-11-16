using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>HTTP client for the GameBackend account and farmer endpoints.</summary>
public sealed class StartupApiClient
{
    [Serializable] private sealed class RegisterBody
    {
        public string username, email, password, confirmPassword;
    }
    [Serializable] private sealed class LoginBody
    {
        public string identity, password;
    }
    [Serializable] private sealed class AuthResult
    {
        public string token, username, email;
        public CreatedFarmer farmer;
    }
    [Serializable] private sealed class AccountResult
    {
        public string username, email;
        public CreatedFarmer farmer;
    }
    [Serializable] private sealed class ApiError { public string error; }
    [Serializable] private sealed class PresenceResult { public int onlinePlayers; }

    private readonly string baseUrl;
    private string token;
    public string UserName { get; private set; }
    public CreatedFarmer Farmer { get; private set; }
    public bool IsSignedIn => !string.IsNullOrEmpty(token);
    public bool IsServerConnected { get; private set; }
    public string ConnectionError { get; private set; }
    public int OnlinePlayers { get; private set; }

    public const string SessionExpiredMessage = "Session expired. Please log in again.";

    public StartupApiClient(string url) => baseUrl = (url ?? "").TrimEnd('/');

    public IEnumerator Register(string username, string email, string password, string confirmation,
                                Action<string> done)
    {
        var body = new RegisterBody { username = username, email = email, password = password,
                                      confirmPassword = confirmation };
        yield return Send("POST", "/api/auth/register", body, (json, error) =>
        {
            if (error == null) SetAuth(json);
            done(error);
        });
    }

    public IEnumerator Login(string identity, string password, Action<string> done)
    {
        yield return Send("POST", "/api/auth/login", new LoginBody { identity = identity, password = password },
            (json, error) => { if (error == null) SetAuth(json); done(error); });
    }

    /// <summary>Reload the authenticated account and its farmer from the database.</summary>
    public IEnumerator RefreshAccount(Action<string> done)
    {
        if (!IsSignedIn)
        {
            done("Sign in before loading a farmer.");
            yield break;
        }
        string expectedToken = token;
        yield return Send("GET", "/api/auth/me", null, (json, error) =>
        {
            if (error != null) { done(error); return; }
            if (token != expectedToken) { done("Account changed while loading the farmer."); return; }
            try
            {
                var account = JsonConvert.DeserializeObject<AccountResult>(json);
                if (account == null || string.IsNullOrWhiteSpace(account.username))
                {
                    done("The server returned an invalid account.");
                    return;
                }
                UserName = account.username;
                Farmer = account.farmer;
                done(null);
            }
            catch (Exception)
            {
                done("The server returned an invalid farmer profile.");
            }
        });
    }

    /// <summary>Server acknowledgment, including a validated session when signed in.</summary>
    public IEnumerator Probe(Action<string> done)
    {
        string path = IsSignedIn ? "/api/presence/heartbeat" : "/api/health/ready";
        string method = IsSignedIn ? "POST" : "GET";
        yield return Send(method, path, null, (json, error) =>
        {
            IsServerConnected = error == null;
            ConnectionError = error;
            OnlinePlayers = 0;
            if (error == null && IsSignedIn)
                OnlinePlayers = JsonUtility.FromJson<PresenceResult>(json).onlinePlayers;
            done?.Invoke(error);
        });
    }

    public IEnumerator SaveFarmer(CreatedFarmer farmer, Action<string> done)
    {
        yield return Send("PUT", "/api/farmer", farmer, (json, error) =>
        {
            if (error == null) Farmer = JsonUtility.FromJson<CreatedFarmer>(json);
            done(error);
        });
    }

    public IEnumerator LoadGameState(Action<OnlineGameSnapshot, string> done) =>
        Send("GET", "/api/game/state", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<OnlineGameSnapshot>(json) : null, error));

    public IEnumerator CreateChest(string name, string location, int tileX, int tileY,
                                   Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/chests", new ChestCreateBody
        {
            requestId = Guid.NewGuid().ToString(), name = name,
            location = location, tileX = tileX, tileY = tileY
        }, done);

    public IEnumerator PlaceExistingChest(string chestId, string location, int tileX, int tileY,
                                          Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/chests/" + Uri.EscapeDataString(chestId) + "/place", new ChestPlaceBody
        {
            requestId = Guid.NewGuid().ToString(), location = location,
            tileX = tileX, tileY = tileY
        }, done);

    public IEnumerator Transfer(string sourceChestId, int sourceSlot, string targetChestId,
                                int targetSlot, int amount, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/transfer", new TransferBody
        {
            requestId = Guid.NewGuid().ToString(), sourceChestId = sourceChestId,
            sourceSlot = sourceSlot, targetChestId = targetChestId,
            targetSlot = targetSlot, amount = amount
        }, done);

    public IEnumerator SwapInventory(int fromSlot, int toSlot, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/inventory/swap", new SwapSlotsBody
        {
            requestId = Guid.NewGuid().ToString(), fromSlot = fromSlot, toSlot = toSlot
        }, done);

    internal IEnumerator OrganizeInventory(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/inventory/organize", new RequestBody
        {
            requestId = Guid.NewGuid().ToString()
        }, done);

    public IEnumerator DiscardInventory(int slot, string itemId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/inventory/discard", new DiscardItemBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId
        }, done);

    public IEnumerator Craft(string recipeName, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/craft", new CraftBody
        {
            requestId = Guid.NewGuid().ToString(), recipeName = recipeName
        }, done);

    public IEnumerator CollectResource(string location, int tileX, int tileY,
                                       string itemId, int amount,
                                       Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/resources/collect", new CollectResourceBody
        {
            requestId = Guid.NewGuid().ToString(), location = location,
            tileX = tileX, tileY = tileY, itemId = itemId, amount = amount,
            visit = int.TryParse(location, out int floor) && floor > 0 ? MineManager.FloorVisit : 0
        }, done);

    internal IEnumerator ClearNaturalResource(string requestId, string location, int tileX, int tileY,
        string kind, bool stump, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/resources/clear", new ClearNaturalResourceBody
        {
            requestId = requestId, location = location, tileX = tileX, tileY = tileY, kind = kind, stump = stump
        }, done);

    public IEnumerator PlantFarmCrop(int tileX, int tileY, int seedSlot, string seedItemId,
                                     Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/plant", new PlantFarmCropBody
        {
            requestId = Guid.NewGuid().ToString(), tileX = tileX, tileY = tileY,
            seedSlot = seedSlot, seedItemId = seedItemId
        }, done);

    public IEnumerator WaterFarmCrop(int tileX, int tileY,
                                    Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/water", new WaterFarmCropBody
        {
            requestId = Guid.NewGuid().ToString(), tileX = tileX, tileY = tileY
        }, done);

    internal IEnumerator ChangeFarmSoilArea(int power, System.Collections.Generic.IReadOnlyList<Vector2Int> tiles,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/soil-area", AreaBody(power, tiles), done);

    internal IEnumerator WaterFarmArea(int power, System.Collections.Generic.IReadOnlyList<Vector2Int> tiles,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/water-area", AreaBody(power, tiles), done);

    private static FarmAreaBody AreaBody(int power, System.Collections.Generic.IReadOnlyList<Vector2Int> tiles)
    {
        var body = new FarmAreaBody
        {
            requestId = Guid.NewGuid().ToString(), power = power, tiles = new FarmTileBody[tiles.Count]
        };
        for (int i = 0; i < tiles.Count; i++) body.tiles[i] = new FarmTileBody { tileX = tiles[i].x, tileY = tiles[i].y };
        return body;
    }

    internal IEnumerator ChangeFarmSoil(int tileX, int tileY, bool clear, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/soil", new FarmSoilBody
        {
            requestId = Guid.NewGuid().ToString(), tileX = tileX, tileY = tileY, clear = clear
        }, done);

    internal IEnumerator PayBaseFarmRentDebt(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/pay-rent", new TvRequestBody { requestId = Guid.NewGuid().ToString() }, done);

    internal IEnumerator OrderRanchHome(string kind, int level, string buildingId, int x, int y,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/build", new RanchRequestBody
            { requestId = Guid.NewGuid().ToString(), kind = kind, level = level, buildingId = buildingId, x = x, y = y }, done);

    internal IEnumerator DemolishRanchHome(string buildingId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/demolish", new RanchRequestBody
            { requestId = Guid.NewGuid().ToString(), buildingId = buildingId }, done);

    internal IEnumerator PaintRanchHome(string buildingId, OnlineRanchPaint[] regions, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/paint", new RanchPaintBody
            { requestId = Guid.NewGuid().ToString(), buildingId = buildingId, regions = regions }, done);

    [Serializable] private sealed class RanchPaintBody { public string requestId, buildingId; public OnlineRanchPaint[] regions; }

    internal IEnumerator RanchCare(string action, string animalId, string buildingId, int trough, string productId,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/" + action, new RanchCareBody
        {
            requestId = Guid.NewGuid().ToString(), animalId = animalId, buildingId = buildingId,
            trough = trough, productId = productId
        }, done);

    [Serializable] private sealed class RanchCareBody { public string requestId, animalId, buildingId, productId; public int trough; }

    internal IEnumerator RanchField(string action, string animalId, int tileX, int tileY,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/" + action, new RanchFieldBody
            { requestId = Guid.NewGuid().ToString(), animalId = animalId, tileX = tileX, tileY = tileY }, done);

    [Serializable] private sealed class RanchFieldBody { public string requestId, animalId; public int tileX, tileY; }

    internal IEnumerator PlaceObject(string itemId, string location, int x, int y, string variant, int minute,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/objects/place", new PlaceObjectBody
        {
            requestId = Guid.NewGuid().ToString(), itemId = itemId, location = location, x = x, y = y,
            variant = variant, minute = minute
        }, done);

    internal IEnumerator ObjectAt(string action, string itemId, string location, int x, int y, int minute,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/objects/" + action, new ObjectActionBody
        {
            requestId = Guid.NewGuid().ToString(), itemId = itemId, location = location, x = x, y = y, minute = minute
        }, done);

    internal IEnumerator Fertilize(int tileX, int tileY, string itemId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/fertilize", new FertilizeBody
            { requestId = Guid.NewGuid().ToString(), tileX = tileX, tileY = tileY, itemId = itemId }, done);

    [Serializable] private sealed class FertilizeBody { public string requestId, itemId; public int tileX, tileY; }

    internal IEnumerator ObjectAction(string action, string objectId, string itemId, int minute,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/objects/" + action, new ObjectActionBody
            { requestId = Guid.NewGuid().ToString(), objectId = objectId, itemId = itemId, minute = minute }, done);

    [Serializable] private sealed class PlaceObjectBody { public string requestId, itemId, location, variant; public int x, y, minute; }
    [Serializable] private sealed class ObjectActionBody { public string requestId, objectId, itemId, location; public int minute, x, y; }

    internal IEnumerator ChestAction(string action, string chestId, bool fromChest, int slot, int amount,
        Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/chests/" + action, new ChestActionBody
            { requestId = Guid.NewGuid().ToString(), chestId = chestId, fromChest = fromChest, slot = slot, amount = amount }, done);

    internal IEnumerator BundleAction(string action, int bundle, int slot, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/bundles/" + action, new BundleActionBody
            { requestId = Guid.NewGuid().ToString(), bundle = bundle, slot = slot }, done);

    [Serializable] private sealed class BundleActionBody { public string requestId; public int bundle, slot; }

    [Serializable] private sealed class ChestActionBody { public string requestId, chestId; public bool fromChest; public int slot, amount; }

    internal IEnumerator MoveRanchHome(string buildingId, int x, int y, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/move", new RanchRequestBody
            { requestId = Guid.NewGuid().ToString(), buildingId = buildingId, x = x, y = y }, done);

    internal IEnumerator BuyRanchAnimal(string species, string name, string home, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/ranch/buy", new RanchRequestBody
            { requestId = Guid.NewGuid().ToString(), species = species, name = name, home = home }, done);

    [Serializable] private sealed class RanchRequestBody { public string requestId, kind, species, name, buildingId, home; public int level, x, y; }

    public IEnumerator HarvestFarmCrop(int tileX, int tileY,
                                       Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/farm/harvest", new HarvestFarmCropBody
        {
            requestId = Guid.NewGuid().ToString(), tileX = tileX, tileY = tileY
        }, done);

    public IEnumerator GiveNpcGift(string npcName, int slot, string itemId,
                                   Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/npc/gift", new NpcGiftBody
        {
            requestId = Guid.NewGuid().ToString(), npcName = npcName,
            slot = slot, itemId = itemId
        }, done);

    internal IEnumerator ActOnDailyEvent(DailyEventAction action, Action<OnlineGameSnapshot, string> done)
    {
        action.requestId = Guid.NewGuid().ToString();
        return SendState("/api/game/events/action", action, done);
    }

    internal IEnumerator ActOnMilestone(MilestoneAction action, Action<OnlineGameSnapshot, string> done)
    {
        action.requestId ??= Guid.NewGuid().ToString();
        return SendState("/api/game/milestones", action, done);
    }

    [Serializable]
    internal sealed class MilestoneAction
    {
        public string requestId, action, id, location, choice, name;
        public int day, time;
        public bool rain;
    }

    [Serializable]
    internal sealed class DailyEventAction
    {
        public string requestId, eventId, action, location, npc;
        public int day, time, slot = -1, x, y;
        public bool rain;
        public int[] slots;
    }

    [Serializable] public sealed class NpcFriendshipResponse
    {
        public NpcFriendshipRecord[] friends;
    }
    [Serializable] public sealed class NpcFriendshipRecord
    {
        public string npcName;
        public int points, lastGiftDay, giftsThisWeek;
    }
    public IEnumerator LoadNpcFriendships(Action<NpcFriendshipResponse, string> done) =>
        Send("GET", "/api/game/npc/friendships", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<NpcFriendshipResponse>(json) : null, error));

    [Serializable] public sealed class FarmCropsResponse
    {
        public FarmCropRecord[] crops;
        public FarmSoilRecord[] soil;
    }
    [Serializable] public sealed class FarmSoilRecord
    {
        public int tileX, tileY;
        public bool isWatered;
        public string fertilizer;
    }
    [Serializable] public sealed class FarmCropRecord
    {
        public int tileX, tileY, regrowDays, readyGameDay;
        public int daysToGrow, growthDays, regrowDaysLeft;
        public bool hasHarvested, isWatered;
        public string seedItemId, harvestItemId, readyUtc;
    }

    public IEnumerator LoadFarmCrops(Action<FarmCropsResponse, string> done) =>
        Send("GET", "/api/game/farm/crops", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<FarmCropsResponse>(json) : null, error));

    public IEnumerator Buy(string itemId, int amount, Action<OnlineGameSnapshot, string> done,
                           string shop = null) =>
        SendState("/api/game/buy", new BuyBody
        {
            requestId = Guid.NewGuid().ToString(), itemId = itemId, amount = amount,
            shop = shop
        }, done);

    public IEnumerator BuyBackpack(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/backpack", new RequestBody
        {
            requestId = Guid.NewGuid().ToString()
        }, done);

    public IEnumerator Sell(int slot, string itemId, int amount, Action<OnlineGameSnapshot, string> done, string shop = null) =>
        SendState("/api/game/sell", new SellBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId, amount = amount, shop = shop
        }, done);

    [Serializable] internal sealed class ShopOffer { public string itemId; public int price; }
    [Serializable] internal sealed class ShopOfferList { public ShopOffer[] offers; }

    internal IEnumerator LoadShop(string shop, Action<ShopOfferList, string> done) =>
        Send("GET", "/api/game/shop/" + shop, null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<ShopOfferList>(json) : null, error));

    [Serializable]
    private sealed class MineTreasureBody { public string requestId; public int floor; }

    internal IEnumerator ClaimMineTreasure(int floor, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/mine/treasure",
            new MineTreasureBody { requestId = Guid.NewGuid().ToString(), floor = floor }, done);

    internal IEnumerator CrackGeode(int slot, string itemId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/blacksmith/geode", new SellBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId, amount = 1
        }, done);

    internal IEnumerator UpgradeHouse(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/robin/upgrade-house", new RequestBody
        {
            requestId = Guid.NewGuid().ToString()
        }, done);

    internal IEnumerator UpgradeTool(int slot, string itemId, bool collect, Action<OnlineGameSnapshot, string> done) =>
        SendState(collect ? "/api/game/blacksmith/collect-upgrade" : "/api/game/blacksmith/upgrade", new SellBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId, amount = 1
        }, done);

    public IEnumerator Ship(int slot, string itemId, int amount, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/shipping/deposit", new SellBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId, amount = amount
        }, done);

    public IEnumerator RetrieveLastShipped(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/shipping/retrieve", new RequestBody
        {
            requestId = Guid.NewGuid().ToString()
        }, done);

    [Serializable] private sealed class AdvanceDayBody { public string requestId; public int day; public string passedOutAt; }
    [Serializable] private sealed class MineArrivalBody { public string requestId; public int floor; }

    /// <summary>Knocked out on a mine floor: the server takes the gold and items lost.</summary>
    internal IEnumerator MineKnockout(int floor, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/mine/knockout", new MineArrivalBody
        {
            requestId = Guid.NewGuid().ToString(), floor = floor
        }, done);

    [Serializable] private sealed class ProfessionBody { public string requestId; public int skill, level, profession; }

    [Serializable] private sealed class JojaBody { public string requestId; public int project = -1; }
    [Serializable] private sealed class ForageBody { public string requestId, location; public int x, y; }

    internal IEnumerator PickForage(string location, int x, int y, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/forage/pick", new ForageBody
        {
            requestId = Guid.NewGuid().ToString(), location = location, x = x, y = y
        }, done);

    internal IEnumerator JoinJoja(Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/joja/join", new JojaBody { requestId = Guid.NewGuid().ToString() }, done);

    internal IEnumerator BuyJojaProject(int project, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/joja/project", new JojaBody { requestId = Guid.NewGuid().ToString(), project = project }, done);

    internal IEnumerator ChooseProfession(int skill, int level, int profession, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/profession", new ProfessionBody
        {
            requestId = Guid.NewGuid().ToString(), skill = skill, level = level, profession = profession
        }, done);

    internal IEnumerator RecordMineArrival(int floor, string requestId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/mine/arrive", new MineArrivalBody { requestId = requestId, floor = floor }, done);

    public IEnumerator AdvanceGameDay(int day, Action<OnlineGameSnapshot, string> done, string passedOutAt = null) =>
        SendState("/api/game/day/advance", new AdvanceDayBody
        {
            requestId = Guid.NewGuid().ToString(), day = day, passedOutAt = passedOutAt
        }, done);

    public IEnumerator ConsumeFood(int slot, string itemId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/consume", new ConsumeFoodBody
        {
            requestId = Guid.NewGuid().ToString(), slot = slot, itemId = itemId
        }, done);

    [Serializable] public sealed class FishAttemptResult
    {
        public string attemptId, fishItemId;
        public int size;
    }

    public IEnumerator BeginFishAttempt(Action<FishAttemptResult, string> done) =>
        Send("POST", "/api/game/fish/start", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<FishAttemptResult>(json) : null, error));

    public IEnumerator SaveFishCatch(string attemptId, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/game/fish/catch", new CatchFishBody
        {
            requestId = Guid.NewGuid().ToString(), attemptId = attemptId
        }, done);

    [Serializable] public sealed class TvLeaderboard
    {
        public TvRank[] entries;
    }
    [Serializable] public sealed class TvRank
    {
        public int rank, landCount;
        public string farmerName, farmName;
        public long totalEarnings, rentDebt, score;
    }
    [Serializable] public sealed class TvLandStatus
    {
        public long money, rentDebt, baseFarmRentPrice, nextBaseFarmRentDay, rentPrice;
        public int rentDays, rentLimit, farmTilesUsed, farmTileLimit, tilesPerParcel;
        public TvHolding[] holdings;
        public TvParcel[] availableParcels;
    }
    [Serializable] public sealed class TvParcel { public string id, name; }
    [Serializable] public sealed class TvHolding
    {
        public string id, parcelId, name, expiresAtUtc;
        public bool isPermanent;
    }
    [Serializable] public sealed class TvAuctionStatus
    {
        public int round;
        public long highestBid, minimumBid, yourMoney;
        public string parcelId, parcelName, highestBidder, endsAtUtc;
        public bool youAreWinning;
    }
    [Serializable] private sealed class TvParcelBody { public string requestId, parcelId; }
    [Serializable] private sealed class TvRentBody { public string requestId, parcelId; public long expectedRentPrice; }
    [Serializable] private sealed class TvRequestBody { public string requestId; }
    [Serializable] private sealed class TvBidBody { public string requestId, parcelId; public long amount; }

    public IEnumerator GetTvLeaderboard(Action<TvLeaderboard, string> done) =>
        Send("GET", "/api/tv/leaderboard", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvLeaderboard>(json) : null, error));

    public IEnumerator GetTvLand(Action<TvLandStatus, string> done) =>
        Send("GET", "/api/tv/land", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvLandStatus>(json) : null, error));

    public IEnumerator RentTvLand(string parcelId, long expectedRentPrice, Action<TvLandStatus, string> done) =>
        Send("POST", "/api/tv/land/rent", new TvRentBody
            { requestId = Guid.NewGuid().ToString(), parcelId = parcelId, expectedRentPrice = expectedRentPrice }, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvLandStatus>(json) : null, error));

    public IEnumerator ReturnTvLand(string parcelId, Action<TvLandStatus, string> done) =>
        Send("POST", "/api/tv/land/return", new TvParcelBody
            { requestId = Guid.NewGuid().ToString(), parcelId = parcelId }, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvLandStatus>(json) : null, error));

    public IEnumerator GetTvAuction(Action<TvAuctionStatus, string> done) =>
        Send("GET", "/api/tv/land-auction", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvAuctionStatus>(json) : null, error));

    public IEnumerator BidTvLand(string parcelId, long amount, Action<TvAuctionStatus, string> done) =>
        Send("POST", "/api/tv/land-auction/bid", new TvBidBody
            { requestId = Guid.NewGuid().ToString(), parcelId = parcelId, amount = amount }, (json, error) =>
            done(error == null ? JsonUtility.FromJson<TvAuctionStatus>(json) : null, error));

    // ── Admin test tools (server only answers accounts listed in Admin:Usernames) ──
    [Serializable] internal sealed class AdminCheckpointInfo { public string name, createdUtc; public int gameDay; }
    [Serializable] internal sealed class AdminStatus { public bool admin; public AdminCheckpointInfo[] checkpoints; }
    [Serializable] internal sealed class AdminBody
    {
        public string requestId, itemId, npc, id, name;
        public long amount;
        public int day, weather, points, area, skill, level;
        public bool tomorrow, done;
    }

    internal IEnumerator GetAdminStatus(Action<AdminStatus, string> done) =>
        Send("GET", "/api/admin/status", null, (json, error) =>
            done(error == null ? JsonUtility.FromJson<AdminStatus>(json) : null, error));

    /// <summary>Admin command that returns the changed game snapshot.</summary>
    internal IEnumerator AdminState(string command, AdminBody body, Action<OnlineGameSnapshot, string> done)
    {
        body.requestId ??= Guid.NewGuid().ToString();
        return SendState("/api/admin/" + command, body, done);
    }

    /// <summary>Admin command without a snapshot (checkpoints, reset).</summary>
    internal IEnumerator AdminCommand(string command, AdminBody body, Action<string> done) =>
        Send("POST", "/api/admin/" + command, body, (_, error) => done(error));

    private IEnumerator SendState(string path, object body, Action<OnlineGameSnapshot, string> done) =>
        Send("POST", path, body, (json, error) =>
            done(error == null ? JsonUtility.FromJson<OnlineGameSnapshot>(json) : null, error));

    public IEnumerator Logout(Action done)
    {
        if (IsSignedIn) yield return Send("POST", "/api/auth/logout", null, (_, _) => { });
        token = null;
        UserName = null;
        Farmer = null;
        OnlinePlayers = 0;
        done();
    }

    private void SetAuth(string json)
    {
        var result = JsonConvert.DeserializeObject<AuthResult>(json);
        token = result.token;
        UserName = result.username;
        Farmer = result.farmer;
        IsServerConnected = false;
        ConnectionError = null;
    }

    [Serializable] private sealed class ChestCreateBody
    {
        public string requestId, name, location;
        public int tileX, tileY;
    }
    [Serializable] private sealed class ChestPlaceBody
    {
        public string requestId, location;
        public int tileX, tileY;
    }
    [Serializable] private sealed class TransferBody
    {
        public string requestId, sourceChestId, targetChestId;
        public int sourceSlot, targetSlot, amount;
    }
    [Serializable] private sealed class SwapSlotsBody
    {
        public string requestId;
        public int fromSlot, toSlot;
    }
    [Serializable] private sealed class DiscardItemBody
    {
        public string requestId, itemId;
        public int slot;
    }
    [Serializable] private sealed class CraftBody
    {
        public string requestId, recipeName;
    }
    [Serializable] private sealed class CollectResourceBody
    {
        public string requestId, location, itemId;
        public int tileX, tileY, amount, visit;
    }
    [Serializable] private sealed class ClearNaturalResourceBody
    {
        public string requestId, location, kind;
        public int tileX, tileY;
        public bool stump;
    }
    [Serializable] private sealed class PlantFarmCropBody
    {
        public string requestId, seedItemId;
        public int tileX, tileY, seedSlot;
    }
    [Serializable] private sealed class WaterFarmCropBody
    {
        public string requestId;
        public int tileX, tileY;
    }
    [Serializable] private sealed class FarmTileBody { public int tileX, tileY; }
    [Serializable] private sealed class FarmAreaBody
    {
        public string requestId;
        public int power;
        public FarmTileBody[] tiles;
    }
    [Serializable] private sealed class FarmSoilBody
    {
        public string requestId;
        public int tileX, tileY;
        public bool clear;
    }
    [Serializable] private sealed class HarvestFarmCropBody
    {
        public string requestId;
        public int tileX, tileY;
    }
    [Serializable] private sealed class NpcGiftBody
    {
        public string requestId, npcName, itemId;
        public int slot;
    }
    [Serializable] private sealed class BuyBody { public string requestId, itemId, shop; public int amount; }
    [Serializable] private sealed class RequestBody { public string requestId; }
    [Serializable] private sealed class SellBody { public string requestId, itemId, shop; public int slot, amount; }
    [Serializable] private sealed class ConsumeFoodBody { public string requestId, itemId; public int slot; }
    [Serializable] private sealed class CatchFishBody { public string requestId, attemptId; }

    public IEnumerator SaveSession(OnlinePlayerSession session, Action<string> done) =>
        Send("POST", "/api/game/session", session, (_, error) => done?.Invoke(error));

    /// <summary>
    /// Last-chance save while the application is closing, when coroutines no longer run.
    /// UnityWebRequest progresses natively, so waiting on the main thread is safe here.
    /// </summary>
    public bool SaveSessionBlocking(OnlinePlayerSession session, float timeoutSeconds)
    {
        if (!IsSignedIn || string.IsNullOrEmpty(baseUrl) || session == null) return false;
        using var request = new UnityWebRequest(baseUrl + "/api/game/session", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(session)));
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
        var operation = request.SendWebRequest();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!operation.isDone && watch.Elapsed.TotalSeconds < timeoutSeconds)
            System.Threading.Thread.Sleep(10);
        return operation.isDone && request.result == UnityWebRequest.Result.Success;
    }

    private IEnumerator Send(string method, string path, object body, Action<string, string> done)
    {
        if (string.IsNullOrEmpty(baseUrl)) { done(null, "API URL is not configured."); yield break; }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri address) ||
            (address.Scheme != Uri.UriSchemeHttps &&
             !(address.Scheme == Uri.UriSchemeHttp && address.IsLoopback)))
        {
            done(null, "Use HTTPS for a remote game server.");
            yield break;
        }
        using (var request = new UnityWebRequest(baseUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            string sentToken = IsSignedIn ? token : null;
            if (sentToken != null) request.SetRequestHeader("Authorization", "Bearer " + sentToken);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                done(request.downloadHandler.text, null);
                yield break;
            }
            string message = null;
            try { message = JsonUtility.FromJson<ApiError>(request.downloadHandler.text)?.error; }
            catch (Exception) { }
            bool isAuthCall = path == "/api/auth/login" || path == "/api/auth/register";
            if (request.responseCode == 429 && isAuthCall)
                message = "Too many attempts. Try again in a minute.";
            else if (request.responseCode == 401 && path == "/api/auth/login")
                message = "Wrong username or password.";
            else if (request.responseCode == 401)
            {
                message = SessionExpiredMessage;
                // Only the token this request carried is known dead: a late reply from before a
                // logout/login must not sign out the account that is signed in now.
                if (sentToken != null && token == sentToken)
                {
                    token = null;
                    UserName = null;
                    Farmer = null;
                }
            }
            if (string.IsNullOrEmpty(message))
                message = request.responseCode == 0 ? "Cannot reach game server at " + baseUrl :
                          "Server error (" + request.responseCode + ").";
            done(null, message);
        }
    }

    [Serializable]
    public sealed class WorldSessionResult
    {
        public bool isActive;
        public string relayJoinCode;
        public string serverHost;
        public int serverPort;
        public string serverName;
        public int maxPlayers;
    }

    public IEnumerator GetWorldSession(Action<WorldSessionResult, string> done)
    {
        yield return Send("GET", "/api/world/session", null, (json, error) =>
        {
            if (error != null) { done?.Invoke(null, error); return; }
            try
            {
                var result = JsonUtility.FromJson<WorldSessionResult>(json);
                done?.Invoke(result, null);
            }
            catch (Exception ex)
            {
                done?.Invoke(null, ex.Message);
            }
        });
    }

    [Serializable]
    public sealed class SyncPlayerBody
    {
        public float X, Y, FacingX, FacingY;
        public string Location;
        public bool InCutscene;
        public string HeldItemId;
        public int ActionSeq;
        public string ActionName;
        public float ActionFacingX, ActionFacingY;
    }

    [Serializable]
    public sealed class RemotePlayerDto
    {
        public string userId;
        public string username;
        public float x;
        public float y;
        public float facingX;
        public float facingY;
        public string location;
        public CreatedFarmer farmer;
        public string heldItemId;
        public int actionSeq;
        public string actionName;
        public float actionFacingX;
        public float actionFacingY;
    }

    /// <summary>A farmer-to-farmer gift; <c>other*</c> is the sender (incoming) or receiver (outgoing).</summary>
    [Serializable]
    public sealed class FarmerGiftDto
    {
        public string id;
        public string otherUserId;
        public string otherName;
        public string itemId;
        public string status;
        public string message;
        public bool IsPresent => !string.IsNullOrEmpty(id);
    }

    [Serializable]
    public sealed class WorldPresenceResponse
    {
        public string serverTimeUtc;
        public RemotePlayerDto[] players;
        public FarmerGiftDto incomingGift;
        public FarmerGiftDto outgoingGift;
    }

    public IEnumerator SyncWorldPresence(float x, float y, float facingX, float facingY, string location, Action<WorldPresenceResponse, string> done)
    {
        var body = new SyncPlayerBody
        {
            X = x,
            Y = y,
            FacingX = facingX,
            FacingY = facingY,
            Location = location,
            // A farmer in their own cutscene is hidden from everyone else.
            InCutscene = WorldMultiplayerManager.IsLocalCutscenePlaying
        };
        AddLocalAction(body);
        yield return Send("POST", "/api/world/presence/sync", body, (json, error) =>
        {
            if (error != null) { done?.Invoke(null, error); return; }
            try
            {
                var response = JsonUtility.FromJson<WorldPresenceResponse>(json);
                done?.Invoke(response, null);
            }
            catch (Exception ex)
            {
                done?.Invoke(null, ex.Message);
            }
        });
    }

    /// <summary>What the local farmer holds and their latest action, for other clients to replay.</summary>
    private static void AddLocalAction(SyncPlayerBody body)
    {
        CharacterMotor farmer = LocationManager.Instance != null ? LocationManager.Instance.LocalFarmer : null;
        if (farmer == null) return;
        var bag = farmer.GetComponent<PlayerInventory>();
        InventorySlot held = bag != null ? bag.SelectedItem : null;
        if (held != null && held.amount > 0 && held.itemData != null) body.HeldItemId = held.itemData.itemId;
        var animation = farmer.GetComponent<PlayerAnimationController>();
        if (animation == null || animation.ActionSequence == 0) return;
        body.ActionSeq = animation.ActionSequence;
        body.ActionName = animation.LastAction.ToString();
        body.ActionFacingX = animation.LastActionFacing.x;
        body.ActionFacingY = animation.LastActionFacing.y;
    }

    [Serializable] private sealed class FarmerGiftOfferBody
    {
        public string ReceiverId;
        public int Slot;
        public string ItemId;
    }
    [Serializable] private sealed class FarmerGiftOfferResult { public string proposalId; }
    [Serializable] private sealed class FarmerGiftRespondBody
    {
        public string ProposalId;
        public bool Accept;
    }

    /// <summary>Offers one item from a bag slot to a nearby farmer; returns the offer id.</summary>
    internal IEnumerator OfferFarmerGift(string receiverId, int slot, string itemId, Action<string, string> done) =>
        Send("POST", "/api/world/gift/offer",
            new FarmerGiftOfferBody { ReceiverId = receiverId, Slot = slot, ItemId = itemId },
            (json, error) => done(error == null ? JsonUtility.FromJson<FarmerGiftOfferResult>(json)?.proposalId : null, error));

    /// <summary>Answers an incoming gift; an accepted gift returns the receiver's new state.</summary>
    internal IEnumerator RespondFarmerGift(string proposalId, bool accept, Action<OnlineGameSnapshot, string> done) =>
        SendState("/api/world/gift/respond", new FarmerGiftRespondBody { ProposalId = proposalId, Accept = accept }, done);

    internal IEnumerator CancelFarmerGift(Action<string> done) =>
        Send("POST", "/api/world/gift/cancel", null, (_, error) => done(error));

    public IEnumerator LeaveWorld(Action<string> done = null)
    {
        yield return Send("POST", "/api/world/presence/leave", null, (json, error) => done?.Invoke(error));
    }
}
