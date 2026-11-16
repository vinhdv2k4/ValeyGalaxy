using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class NpcShop
{
    private enum RanchPage { None, Animals, Buildings }
    private RanchPage ranchPage;
    private static readonly Dictionary<string, string> CounterItemNames = new(StringComparer.Ordinal)
    {
        ["object_178"] = "Cỏ khô", ["big_craftable_208"] = "Workbench", ["big_craftable_211"] = "Wood Chipper",
        ["big_craftable_214"] = "Telephone", ["big_craftable_216"] = "Mini-Fridge", ["big_craftable_200"] = "Seasonal Plant",
        ["big_craftable_35"] = "Basic Log", ["big_craftable_46"] = "Log Section",
        ["big_craftable_143"] = "Wooden Brazier", ["big_craftable_144"] = "Stone Brazier",
        ["big_craftable_150"] = "Barrel Brazier", ["big_craftable_147"] = "Stump Brazier",
        ["big_craftable_145"] = "Gold Brazier", ["big_craftable_148"] = "Carved Brazier",
        ["big_craftable_149"] = "Skull Brazier", ["big_craftable_151"] = "Marble Brazier",
        ["big_craftable_152"] = "Wood Lamp-post", ["big_craftable_153"] = "Iron Lamp-post",
        ["object_328"] = "Wood Floor", ["object_840"] = "Rustic Plank Floor", ["object_329"] = "Stone Floor",
        ["object_293"] = "Brick Floor", ["object_841"] = "Stone Walkway Floor", ["object_415"] = "Stepping Stone Path",
        ["object_401"] = "Straw Floor", ["object_409"] = "Crystal Path"
    };

    private void ResetRanch(RanchPage page)
    {
        ranchPage = page;
        blueprintIndex = 0;
        farmTask = FarmTask.None;
        animalBeingPurchased = null;
        animalHome = null;
        namingAnimal = false;
        animalName = "";
    }

    private static bool OpenRanchCounter(NpcActor actor, PlayerInventory playerInventory)
    {
        if (IsOpen || NpcConversation.IsOpen || playerInventory == null) return false;
        bool marnie = actor.DisplayName == "Marnie";
        Vector2Int tile = NpcPathfinder.WorldToTile(actor.transform.position);
        if (marnie && (tile.y != 14 || tile.x is < 12 or > 13)) return false;
        if (!marnie && (tile - new Vector2Int(8, 19)).sqrMagnitude > 9) return false;
        string name = actor.DisplayName;
        var choices = new List<(string Label, Action Choose)>
        {
            (marnie ? "Mua vật tư" : "Cửa hàng", () => Open(name, actor, playerInventory, false))
        };
        var snapshot = playerInventory.GetComponent<OnlineGameState>()?.Snapshot;
        bool construction = snapshot != null && (snapshot.houseUpgradeDay > snapshot.gameDay ||
            snapshot.ranchHomes?.Any(home => home.readyDay > snapshot.gameDay) == true);
        if (!marnie && !construction && snapshot?.houseUpgradeDay == 0)
            choices.Add(("Nâng cấp nhà", () =>
            {
                if (Open(name, actor, playerInventory, false)) instance.confirmHouseUpgrade = true;
            }));
        if (marnie)
            choices.Add(("Mua động vật", () => Open(name, actor, playerInventory, false, ranch: RanchPage.Animals)));
        else
            choices.Add(("Xây dựng trang trại", () =>
            {
                if (RobinBusy(snapshot ?? playerInventory.GetComponent<OnlineGameState>()?.Snapshot))
                    NpcConversation.ShowNotice("Robin: Xin lỗi, mình đang bận thi công một công trình khác. Đợi xong rồi quay lại nhé!");
                else Open(name, actor, playerInventory, false, ranch: RanchPage.Buildings);
            }));
        choices.Add(("Rời đi", () => { }));
        NpcConversation.ShowServiceChoices(choices);
        return true;
    }

    private static ItemData ResolveCounterItem(string id)
    {
        bool recipe = id.StartsWith("recipe_", StringComparison.Ordinal);
        string itemId = recipe ? id[7..] : id;
        if (!CounterItemNames.TryGetValue(itemId, out string name)) return null;
        if (!int.TryParse(itemId[(itemId.LastIndexOf('_') + 1)..], out int tile)) return null;
        bool big = itemId.StartsWith("big_craftable_", StringComparison.Ordinal);
        Texture2D atlas = big ? Resources.Load<NpcCatalog>("NpcCatalog")?.craftablesAtlas :
            springObjectsTexture ??= Resources.Load<Texture2D>("Maps/springobjects");
        Sprite icon = null;
        if (atlas != null)
        {
            int columns = atlas.width / 16;
            int height = big ? 32 : 16;
            var rect = new Rect(tile % columns * 16, atlas.height - (tile / columns + 1) * height, 16, height);
            if (rect.yMin >= 0) icon = AtlasSprites.Get(atlas, rect, Vector2.one * .5f, 16f);
        }
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = id;
        item.displayName = recipe ? "Bản vẽ " + name : name;
        item.icon = icon;
        item.kind = PlayerItemKind.Material;
        item.maxStackSize = big || recipe ? 1 : 999;
        return item;
    }
}
