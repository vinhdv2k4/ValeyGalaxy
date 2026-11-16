using System;

/// <summary>Where the player left the game on the current server day (see /api/game/session).</summary>
[Serializable]
public sealed class OnlinePlayerSession
{
    public int day;
    public string location;
    public float x, y, facingX, facingY;
    public int time, stamina, health;

    // JsonUtility always creates nested objects, so an absent session arrives with day 0.
    public bool IsFor(int gameDay) => day > 0 && day == gameDay && !string.IsNullOrEmpty(location);
}

/// <summary>Farm rent the server took overnight on <see cref="day"/> (day 0 when nothing was taken).</summary>
[Serializable]
public sealed class OnlineRentCharge
{
    public int day;
    public long billed, paid, stillOwed;
}

/// <summary>Gold lost passing out away from home, charged on the morning of <see cref="day"/>.</summary>
[Serializable]
public sealed class OnlinePassOutCharge
{
    public int day;
    public long goldLost;
    public string location;
}

[Serializable]
public sealed class OnlineGameSnapshot
{
    public OnlinePlayerSession session;
    public long money;
    public long totalEarnings;
    public long rentDebt;
    public OnlineRentCharge rentCharge;
    public OnlinePassOutCharge passOutCharge;
    public int bagSize;
    public int farmTileLimit;
    public int version;
    public OnlineStack[] inventory;
    public OnlineChest[] chests;
    public OnlineShippingEntry[] shipping;
    public string[] shippableItems;
    public int gameDay;
    public int deepestMineFloor;
    public OnlineFarmWeather weather;
    public int[] skillExperience;
    public int[] skillLevels;
    public int[] achievements;
    public OnlineCollectionCount[] itemsShipped;
    public OnlineCollectionCount[] fishCaught;
    public int houseUpgradeDay;
    public OnlineToolUpgrade toolUpgrade;
    public OnlineGeodeReward geodeReward;
    public OnlineNaturalResource[] naturalResources;
    public int[] claimedMineChests;
    public OnlineDailyEventStatus dailyEvents;
    public OnlineMilestoneStatus milestones;
    public OnlineRanchHome[] ranchHomes;
    public OnlineRanchAnimal[] ranchAnimals;
    public OnlineRanchProduct[] ranchProducts;
    public int piecesOfHay, hayCapacity;
    public OnlinePlacedObject[] placedObjects;
    public int trashCanLevel;
    /// <summary>LevelUpMenu professions chosen (skill * 6 + choice), kept on the server.</summary>
    public int[] professions;
    /// <summary>Today's picked forage, as "day/location/x/y".</summary>
    public string[] foragePicked;

    public bool HasProfession(int id) => professions != null && Array.IndexOf(professions, id) >= 0;
    public OnlineBundleStatus bundles;
    public OnlineGuildPrice[] guildResale;
    public string[] learnedShopRecipes;

    internal OnlineRanchHome RanchHomeById(string id) => ranchHomes == null || id == null ? null :
        Array.Find(ranchHomes, home => home.id == id);

    public bool HasClaimedMineChest(int floor) =>
        claimedMineChests != null && Array.IndexOf(claimedMineChests, floor) >= 0;

    // JsonUtility can materialize null JSON objects as empty inline instances.
    internal OnlineToolUpgrade ActiveToolUpgrade =>
        toolUpgrade != null && toolUpgrade.readyDay > 0 && !string.IsNullOrEmpty(toolUpgrade.itemId) ? toolUpgrade : null;
}

[Serializable]
public sealed class OnlineBundleStatus
{
    public OnlineBundleArea[] areas;
    public int[] pendingRewards;
}

[Serializable]
public sealed class OnlineBundleArea
{
    public int area;
    public string roomId, name;
    public OnlineBundle[] bundles;
    public bool complete;
}

[Serializable]
public sealed class OnlineBundle
{
    public int index;
    public string name;
    public int color, required;
    public string rewardId;
    public int rewardAmount, gold;
    public OnlineBundleIngredient[] ingredients;
    public bool complete;
}

[Serializable]
public sealed class OnlineBundleIngredient
{
    public string itemId;
    public int amount, quality;
    public bool done;
}

[Serializable]
public sealed class OnlineGuildPrice
{
    public string itemId;
    public int price;
}

[Serializable]
public sealed class OnlineFarmWeather
{
    public int today, tomorrow;
}

[Serializable]
public sealed class OnlineNaturalResource
{
    public string location, kind;
    public int tileX, tileY, saplingDay, regrowDay;
    public bool stump;
}

[Serializable]
public sealed class OnlineMilestoneStatus
{
    public string[] seen;
    public int firstCopperDay, caveHarvestDay, candles;
    public string pet, petName, cave;
    public OnlineBundleRoom[] rooms;
    public OnlineMilestoneFriend[] friends;
    public bool HasSeen(string id) => seen != null && Array.IndexOf(seen, id) >= 0;
}

[Serializable]
public sealed class OnlineBundleRoom
{
    public string id, title;
    public int gold;
    public bool done;
    public OnlineBundleItem[] items;
}

[Serializable]
public sealed class OnlineBundleItem
{
    public string itemId;
    public int amount;
}

[Serializable]
public sealed class OnlineMilestoneFriend
{
    public string npc;
    public int points;
}

[Serializable]
public sealed class OnlineDailyEventStatus
{
    public OnlineDailyEventStamp[] completed;
    public string secretRecipient;
    public string secretGiver;
    public int fairScore;

    public int CompletedOn(string key)
    {
        foreach (var stamp in completed ?? Array.Empty<OnlineDailyEventStamp>())
            if (stamp.key == key) return stamp.day;
        return 0;
    }
}

[Serializable]
public sealed class OnlineDailyEventStamp
{
    public string key;
    public int day;
}

[Serializable]
public sealed class OnlineToolUpgrade
{
    public string itemId;
    public int readyDay;
}

[Serializable]
public sealed class OnlineGeodeReward
{
    public string itemId;
    public int amount;
}

[Serializable]
public sealed class OnlineCollectionCount
{
    public string itemId;
    public int amount;
}

[Serializable]
public sealed class OnlineShippingEntry
{
    public string itemId;
    public int amount;
    public int unitPrice;
}

[Serializable]
public sealed class OnlineStack
{
    public int slot;
    public string itemId;
    public int amount;
}

[Serializable]
public sealed class OnlineChest
{
    public string id;
    public string name;
    public string location;
    public int tileX;
    public int tileY;
    public OnlineStack[] slots;
}
