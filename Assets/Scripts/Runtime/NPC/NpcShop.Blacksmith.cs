using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class NpcShop
{
    private enum BlacksmithMode { Store, Upgrade, Geodes, Collect }

    private const int GeodeFee = 25;
    private const float HammerImpact = .58f;
    private const float GeodeReveal = HammerImpact + .7f;
    private const float GeodeDuration = 2.7f;
    private static readonly Rect Anvil = new(28f, 25f, 560f, 312f);
    private static readonly (int Frame, float End)[] HammerFrames =
    {
        (8, .30f), (9, .50f), (10, HammerImpact), (11, .78f), (12, .88f), (8, 1.18f)
    };
    private static readonly PlayerItemKind[] UpgradeOrder =
        { PlayerItemKind.Axe, PlayerItemKind.WateringCan, PlayerItemKind.Pickaxe, PlayerItemKind.Hoe };
    private const int UpgradeBarCount = 5;
    private const int FirstUpgradeBar = 334;

    private readonly List<(int Slot, ToolData Tool, ItemData Upgrade)> upgradeOffers = new();
    private readonly (Sprite Icon, int Amount)[] beforeCrack = new (Sprite, int)[PlayerInventory.MaxCapacity];
    private BlacksmithMode blacksmithMode;
    private Texture2D clintSheet, impactSheet;
    private int serviceOpenedFrame;
    private int serviceSelection;
    private int geodeSlot = -1;
    private Sprite geodeIcon;
    private ItemData geodeLoot;
    private int geodeAmount, geodeEffectRow;
    private float geodeStarted = -1f;
    private bool hammerPlayed, revealPlayed;

    private static bool OpenCounter(NpcActor actor, PlayerInventory playerInventory)
    {
        if (actor.DisplayName is "Marnie" or "Robin") return OpenRanchCounter(actor, playerInventory);
        if (actor.DisplayName != "Clint") return Open(actor.DisplayName, actor, playerInventory, false);
        if (IsOpen || NpcConversation.IsOpen || playerInventory == null) return false;

        var online = playerInventory.GetComponent<OnlineGameState>();
        var state = online?.Snapshot;
        if (state == null)
        {
            NpcConversation.ShowServiceNotice(actor, "Đăng nhập để sử dụng dịch vụ của thợ rèn.");
            return true;
        }

        var order = state.ActiveToolUpgrade;
        if (order != null)
        {
            if (state.gameDay < order.readyDay)
            {
                NpcConversation.ShowServiceNotice(actor, $"Tôi vẫn đang nâng cấp công cụ của bạn. Hãy quay lại ngày {order.readyDay}.");
                return true;
            }
            if (order.itemId.EndsWith("TrashCan", StringComparison.Ordinal))
            {
                if (Open("Clint", actor, playerInventory, false, BlacksmithMode.Collect))
                    instance.CollectTrashCan(online);
                return true;
            }
            var tool = online.ResolveItem(order.itemId);
            if (tool == null || !playerInventory.CanAccept(tool))
            {
                NpcConversation.ShowServiceNotice(actor, "Công cụ của bạn đã xong. Hãy chừa một ô trống trong túi.");
                return true;
            }
            if (Open("Clint", actor, playerInventory, false, BlacksmithMode.Collect))
                instance.CollectUpgrade(online, tool);
            return true;
        }

        var choices = new List<(string Label, Action Choose)>
        {
            ("Cửa hàng", () => Open("Clint", actor, playerInventory, false)),
            ("Nâng cấp công cụ", () => Open("Clint", actor, playerInventory, false, BlacksmithMode.Upgrade))
        };
        for (int slot = 0; slot < playerInventory.UnlockedSlots; slot++)
        {
            if (GeodeAt(playerInventory, slot) == null) continue;
            choices.Add(("Đập geode", () => Open("Clint", actor, playerInventory, false, BlacksmithMode.Geodes)));
            break;
        }
        choices.Add(("Rời đi", () => { }));
        NpcConversation.ShowServiceChoices(choices);
        return true;
    }

    private void ResetBlacksmithMode(BlacksmithMode mode)
    {
        blacksmithMode = mode;
        serviceOpenedFrame = Time.frameCount;
        serviceSelection = 0;
        geodeSlot = -1;
        geodeStarted = -1f;
        geodeIcon = null;
        geodeLoot = null;
        Array.Clear(beforeCrack, 0, beforeCrack.Length);
        upgradeOffers.Clear();
        if (mode == BlacksmithMode.Upgrade) BuildUpgradeOffers();
        if (mode != BlacksmithMode.Geodes) return;
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        var frames = catalog?.characters.Find(character => character.name == "Clint")?.animationFrames;
        clintSheet = frames is { Length: > 0 } ? frames[0]?.texture : null;
        impactSheet = catalog?.animationsAtlas;
    }

    private void BuildUpgradeOffers()
    {
        foreach (var kind in UpgradeOrder)
        {
            for (int slot = 0; slot < inventory.UnlockedSlots; slot++)
            {
                if (inventory.GetSlot(slot)?.Data is not ToolData tool || tool.kind != kind ||
                    tool.upgradeLevel is < 0 or >= 4) continue;
                string suffix = kind == PlayerItemKind.WateringCan ? "WateringCan" : kind.ToString();
                var next = ResolveUpgradedTool($"tool_{ToolUpgradeTiers[tool.upgradeLevel]}{suffix}");
                if (next != null) upgradeOffers.Add((slot, tool, next));
                break;
            }
        }
    }

    private void UpdateBlacksmith()
    {
        if (geodeStarted >= 0f) UpdateGeodeCrack();
        if (purchasePending || Time.frameCount == serviceOpenedFrame) return;
        var key = Keyboard.current;
        var pad = Gamepad.current;
        if (key?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true)
        {
            Close();
            return;
        }
        bool up = key?.upArrowKey.wasPressedThisFrame == true || pad?.dpad.up.wasPressedThisFrame == true;
        bool down = key?.downArrowKey.wasPressedThisFrame == true || pad?.dpad.down.wasPressedThisFrame == true;
        bool confirm = key?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true;
        if (blacksmithMode == BlacksmithMode.Upgrade && upgradeOffers.Count > 0)
        {
            if (up || down)
                serviceSelection = (serviceSelection + (up ? -1 : 1) + upgradeOffers.Count) % upgradeOffers.Count;
            if (confirm) OrderToolUpgrade(serviceSelection);
        }
        if (blacksmithMode == BlacksmithMode.Geodes)
        {
            bool left = key?.leftArrowKey.wasPressedThisFrame == true || pad?.dpad.left.wasPressedThisFrame == true;
            bool right = key?.rightArrowKey.wasPressedThisFrame == true || pad?.dpad.right.wasPressedThisFrame == true;
            if (up || down || left || right || (confirm && geodeSlot < 0))
                SelectNextGeode(up || left ? -1 : 1);
            else if (confirm) CrackSelectedGeode();
        }
    }

    private void SelectNextGeode(int direction)
    {
        int slots = inventory.UnlockedSlots;
        int first = geodeSlot >= 0 ? geodeSlot : direction > 0 ? slots - 1 : 0;
        for (int i = 1; i <= slots; i++)
        {
            int slot = (first + slots + i * direction) % slots;
            if (GeodeAt(inventory, slot) == null) continue;
            geodeSlot = slot;
            feedback = "";
            GameAudio.Play("smallSelect");
            return;
        }
    }

    private void DrawBlacksmith(Rect panel)
    {
        var cursors = StardewUiAtlas.Cursors;
        var font = StardewUiAtlas.FontBold;
        if (blacksmithMode == BlacksmithMode.Collect)
        {
            Rect message = new(242f, 274f, 548f, 96f);
            StardewUiAtlas.DrawTextureBox(message, cursors, ShopFrame, Color.white);
            GameText.Draw(new Rect(274f, 304f, 484f, 50f), "Clint đang lấy công cụ của bạn...", GameText.Face.Dialogue);
            return;
        }

        StardewUiAtlas.DrawTextureBox(new Rect(0f, 0f, panel.width, 444f), cursors, ShopFrame, Color.white);
        Rect close = new(panel.width - 36f, -8f, 48f, 48f);
        StardewUiAtlas.DrawCursorsSprite(close, StardewUiAtlas.UpperRightCloseButtonRect,
            purchasePending ? new Color(1f, 1f, 1f, .4f) : Color.white);
        if (!purchasePending && InvisibleButton(close)) { Close(); return; }

        if (blacksmithMode == BlacksmithMode.Upgrade) DrawUpgradeOffers(panel, cursors, font);
        else DrawGeodeMenu(panel, cursors, font);
    }

    private void DrawUpgradeOffers(Rect panel, Texture2D cursors, Texture2D font)
    {
        for (int index = 0; index < upgradeOffers.Count; index++)
        {
            var offer = upgradeOffers[index];
            int tier = offer.Tool.upgradeLevel;
            Rect row = new(24f, 57f + index * 78f, panel.width - 48f, 74f);
            bool hover = row.Contains(Event.current.mousePosition);
            StardewUiAtlas.DrawTextureBox(row, cursors, StockRow,
                hover || index == serviceSelection ? new Color(1f, .92f, .72f) : Color.white, 4f, false);
            Sprite icon = offer.Upgrade.GetIcon();
            if (icon != null) DrawIcon(new Rect(row.x + 22f, row.y + 10f, 52f, 52f), icon);
            DrawText(font, new Rect(row.x + 92f, row.y + 13f, 434f, 40f), offer.Upgrade.displayName, 2.5f);
            springObjectsTexture ??= Resources.Load<Texture2D>("Maps/springobjects");
            if (springObjectsTexture != null)
            {
                int tile = FirstUpgradeBar + tier;
                int columns = springObjectsTexture.width / 16;
                DrawBlacksmithAtlas(springObjectsTexture, new Rect(row.x + 667f, row.y + 15f, 44f, 44f),
                    new Rect(tile % columns * 16, tile / columns * 16, 16f, 16f));
            }
            DrawText(font, new Rect(row.x + 715f, row.y + 21f, 64f, 40f), UpgradeBarCount.ToString(), 2.3f);
            DrawText(font, new Rect(row.xMax - 178f, row.y + 21f, 120f, 40f),
                ToolUpgradePrices[tier].ToString(), 2.3f);
            StardewUiAtlas.DrawCursorsSprite(new Rect(row.xMax - 49f, row.y + 20f, 27f, 30f), GoldIcon);
            if (!purchasePending && InvisibleButton(row))
            {
                serviceSelection = index;
                OrderToolUpgrade(index);
                return;
            }
        }
        int trashLevel = inventory.GetComponent<OnlineGameState>()?.Snapshot?.trashCanLevel ?? 4;
        if (trashLevel < 4)
        {
            Rect row = new(24f, 57f + upgradeOffers.Count * 78f, panel.width - 48f, 74f);
            bool hover = row.Contains(Event.current.mousePosition);
            StardewUiAtlas.DrawTextureBox(row, cursors, StockRow, hover ? new Color(1f, .92f, .72f) : Color.white, 4f, false);
            StardewUiAtlas.DrawCursorsSprite(new Rect(row.x + 30f, row.y + 8f, 36f, 52f), StardewUiAtlas.TrashCanBody(trashLevel + 1));
            DrawText(font, new Rect(row.x + 92f, row.y + 13f, 434f, 40f), TrashCanNames[trashLevel], 2.5f);
            springObjectsTexture ??= Resources.Load<Texture2D>("Maps/springobjects");
            if (springObjectsTexture != null)
            {
                int tile = FirstUpgradeBar + trashLevel;
                int columns = springObjectsTexture.width / 16;
                DrawBlacksmithAtlas(springObjectsTexture, new Rect(row.x + 667f, row.y + 15f, 44f, 44f),
                    new Rect(tile % columns * 16, tile / columns * 16, 16f, 16f));
            }
            DrawText(font, new Rect(row.x + 715f, row.y + 21f, 64f, 40f), UpgradeBarCount.ToString(), 2.3f);
            DrawText(font, new Rect(row.xMax - 178f, row.y + 21f, 120f, 40f), TrashCanPrices[trashLevel].ToString(), 2.3f);
            StardewUiAtlas.DrawCursorsSprite(new Rect(row.xMax - 49f, row.y + 20f, 27f, 30f), GoldIcon);
            if (!purchasePending && InvisibleButton(row)) { OrderTrashCan(trashLevel); return; }
        }
        DrawText(font, new Rect(34f, 20f, 884f, 32f), "Nâng cấp công cụ", 2.25f);
        string message = feedback.Length > 0 ? feedback : upgradeOffers.Count == 0
            ? "Bạn chưa có công cụ có thể nâng cấp." : "Chọn công cụ để giao cho Clint.";
        StardewUiAtlas.DrawMoneyBox(new Vector2(-36f, panel.height - 196f), GetCurrentFunds());
        GameText.Draw(new Rect(34f, 386f, panel.width - 70f, 46f), message, GameText.Face.Dialogue);
        DrawInventory(panel, cursors, allowSales: false);
    }

    private static readonly int[] TrashCanPrices = { 1000, 2500, 5000, 12500 };
    private static readonly string[] TrashCanNames =
        { "Thùng rác đồng", "Thùng rác thép", "Thùng rác vàng", "Thùng rác iridium" };

    /// <summary>Trash can upgrades: each level gives back 15% more of a discarded item's price.</summary>
    private void OrderTrashCan(int level)
    {
        var online = inventory.GetComponent<OnlineGameState>();
        if (online?.Snapshot == null || purchasePending) return;
        if (online.Snapshot.money < TrashCanPrices[level]) { feedback = $"Bạn cần {TrashCanPrices[level]}g."; return; }
        purchasePending = true;
        int version = openingVersion;
        online.UpgradeTool(-1, "trash_can", false, error =>
        {
            if (!IsOpen || openingVersion != version) return;
            purchasePending = false;
            if (error != null) { feedback = error; return; }
            var actor = keeper;
            Close();
            NpcConversation.ShowServiceNotice(actor, "Tôi sẽ nâng cấp thùng rác cho bạn. Hãy quay lại sau hai ngày.");
            GameAudio.Play("hammer");
        });
    }

    private void CollectTrashCan(OnlineGameState online)
    {
        purchasePending = true;
        int version = openingVersion;
        online.UpgradeTool(-1, null, true, error =>
        {
            if (!IsOpen || openingVersion != version) return;
            var actor = keeper;
            Close();
            NpcConversation.ShowServiceNotice(actor, error ?? "Thùng rác của bạn đã được nâng cấp. Đồ bỏ đi giờ được hoàn lại một phần tiền.");
        });
    }

    private void OrderToolUpgrade(int offerIndex)
    {
        var online = inventory.GetComponent<OnlineGameState>();
        if (online?.Snapshot == null || purchasePending) return;
        var offer = upgradeOffers[offerIndex];
        int tier = offer.Tool.upgradeLevel;
        if (online.Snapshot.money < ToolUpgradePrices[tier])
        {
            feedback = $"Bạn cần {ToolUpgradePrices[tier]}g.";
            return;
        }
        string bar = "object_" + (FirstUpgradeBar + tier);
        int bars = 0;
        foreach (var entry in online.Snapshot.inventory ?? Array.Empty<OnlineStack>())
            if (entry != null && entry.itemId == bar) bars += entry.amount;
        if (bars < UpgradeBarCount) { feedback = "Bạn cần đủ 5 thỏi cùng loại."; return; }
        purchasePending = true;
        int version = openingVersion;
        online.UpgradeTool(offer.Slot, offer.Tool.itemId, false, error =>
        {
            if (!IsOpen || openingVersion != version) return;
            purchasePending = false;
            if (error != null) { feedback = error; return; }
            var actor = keeper;
            Close();
            NpcConversation.ShowServiceNotice(actor, "Tôi sẽ nâng cấp công cụ cho bạn. Hãy quay lại sau hai ngày.");
            GameAudio.Play("hammer");
        });
    }

    private void CollectUpgrade(OnlineGameState online, ItemData tool)
    {
        purchasePending = true;
        int version = openingVersion;
        online.UpgradeTool(-1, null, true, error =>
        {
            if (!IsOpen || openingVersion != version) return;
            var actor = keeper;
            var player = inventory.GetComponent<NetworkPlayerController>();
            Close();
            if (error != null)
            {
                if (error == "Your tool is not ready.") online.Reload();
                NpcConversation.ShowServiceNotice(actor, error == "Your tool is not ready."
                    ? "Chưa có công cụ nào sẵn sàng để nhận." : error);
                return;
            }
            string message = $"Đã nhận {tool.displayName} sau khi nâng cấp.";
            if (player != null) player.PresentReward(tool.GetIcon(), message);
            else NpcConversation.ShowServiceNotice(actor, message);
        });
    }

    private static InventorySlot GeodeAt(PlayerInventory inventory, int slot)
    {
        if (slot < 0 || slot >= inventory.UnlockedSlots || inventory.GetSlot(slot) is not InventorySlot item ||
            item.IsEmpty || item.itemData?.itemId is not ("object_535" or "object_536" or "object_537" or "object_749"))
            return null;
        return item;
    }

    private void DrawGeodeMenu(Rect panel, Texture2D cursors, Texture2D font)
    {
        StardewUiAtlas.DrawCursorsSprite(Anvil, new Rect(0f, 512f, 140f, 78f));
        float elapsed = geodeStarted < 0f ? 0f : Time.unscaledTime - geodeStarted;
        Rect stone = new(Anvil.x + 360f, Anvil.y + 160f, 64f, 64f);
        if (geodeStarted >= 0f)
        {
            if (elapsed < HammerImpact)
            {
                if (geodeIcon != null) DrawIcon(stone, geodeIcon);
            }
            else if (elapsed < GeodeReveal + .1f)
            {
                int impactFrame = Mathf.Clamp((int)((elapsed - HammerImpact) * 10f), 0, 7);
                DrawBlacksmithAtlas(impactSheet, stone, new Rect(impactFrame * 64f, geodeEffectRow, 64f, 64f));
            }
            if (elapsed >= GeodeReveal && geodeLoot?.GetIcon() is Sprite prize)
            {
                stone.y -= 48f * Mathf.Clamp01((elapsed - GeodeReveal) / .32f);
                DrawIcon(stone, prize);
                StardewUiAtlas.DrawStackCount(stone, geodeAmount);
            }
        }
        else if (GeodeAt(inventory, geodeSlot)?.itemData?.GetIcon() is Sprite selected)
        {
            DrawIcon(stone, selected);
        }
        if (clintSheet != null)
        {
            int frame = ClintFrame(elapsed);
            int columns = clintSheet.width / 32;
            DrawBlacksmithAtlas(clintSheet, new Rect(Anvil.x + 384f, Anvil.y + 64f, 128f, 192f),
                new Rect(frame % columns * 32f, frame / columns * 48f, 32f, 48f));
        }
        if (!purchasePending && InvisibleButton(Anvil)) CrackSelectedGeode();

        DrawText(font, new Rect(610f, 68f, 335f, 46f), "Đập geode", 2.7f);
        DrawText(font, new Rect(612f, 121f, 340f, 42f), $"Phí {GeodeFee}g mỗi viên", 2f);
        string message = feedback.Length > 0 ? feedback : "Chọn geode trong túi rồi đặt lên đe.";
        GameText.Draw(new Rect(612f, 185f, 350f, 155f), message, GameText.Face.Dialogue);
        DrawGeodeInventory(panel, cursors);
        StardewUiAtlas.DrawHudMoneyBox(GetCurrentFunds());
    }

    private void DrawGeodeInventory(Rect panel, Texture2D cursors)
    {
        const float left = 128f, top = 454f, size = 64f;
        StardewUiAtlas.DrawTextureBox(new Rect(left - 24f, top - 16f, 816f, panel.height - top + 16f),
            cursors, ShopFrame, Color.white);
        for (int slot = 0; slot < PlayerInventory.MaxCapacity; slot++)
        {
            Rect cell = new(left + slot % 12 * size, top + slot / 12 * 68f, size, size);
            bool unlocked = slot < inventory.UnlockedSlots;
            bool selected = slot == geodeSlot && !purchasePending;
            StardewUiAtlas.DrawMenuTilesSprite(cell, unlocked
                ? (selected ? StardewUiAtlas.SlotSelectedRect : StardewUiAtlas.SlotNormalRect)
                : StardewUiAtlas.SlotLockedRect);
            if (!unlocked) continue;
            var item = inventory.GetSlot(slot) as InventorySlot;
            var snapshot = purchasePending ? beforeCrack[slot] : (item?.itemData?.GetIcon(), item?.amount ?? 0);
            bool usable = !purchasePending && GeodeAt(inventory, slot) != null;
            if (snapshot.Item1 != null && snapshot.Item2 > 0)
            {
                Color previous = GUI.color;
                if (!usable) GUI.color = new Color(1f, 1f, 1f, .38f);
                DrawIcon(new Rect(cell.x + 8f, cell.y + 8f, 48f, 48f), snapshot.Item1);
                StardewUiAtlas.DrawStackCount(cell, snapshot.Item2);
                GUI.color = previous;
            }
            if (usable && InvisibleButton(cell))
            {
                geodeSlot = slot;
                feedback = "";
            }
        }
    }

    private void CrackSelectedGeode()
    {
        if (purchasePending) return;
        var item = GeodeAt(inventory, geodeSlot);
        if (item == null) { feedback = "Hãy chọn một viên geode trong túi."; return; }
        var online = inventory.GetComponent<OnlineGameState>();
        if (online?.Snapshot == null) { feedback = "Đăng nhập để đập geode."; return; }
        if (online.Snapshot.money < GeodeFee) { feedback = $"Bạn cần {GeodeFee}g để đập geode."; return; }
        for (int slot = 0; slot < inventory.UnlockedSlots; slot++)
        {
            var stack = inventory.GetSlot(slot) as InventorySlot;
            beforeCrack[slot] = (stack?.itemData?.GetIcon(), stack?.amount ?? 0);
        }
        string id = item.itemData.itemId;
        geodeIcon = item.itemData.GetIcon();
        geodeEffectRow = id == "object_536" ? 512 : id == "object_537" ? 576 : 448;
        geodeLoot = null;
        geodeAmount = 0;
        hammerPlayed = revealPlayed = false;
        purchasePending = true;
        feedback = "Đang đặt geode lên đe...";
        int version = openingVersion;
        online.CrackGeode(geodeSlot, id, (reward, error) =>
        {
            if (!IsOpen || openingVersion != version) return;
            if (error != null)
            {
                purchasePending = false;
                feedback = error;
                return;
            }
            if (reward != null)
            {
                geodeLoot = online.ResolveItem(reward.itemId);
                geodeAmount = reward.amount;
            }
            geodeStarted = Time.unscaledTime;
            feedback = "Clint đang đập geode...";
            GameAudio.Play("stoneStep");
        });
    }

    private void UpdateGeodeCrack()
    {
        float elapsed = Time.unscaledTime - geodeStarted;
        if (!hammerPlayed && elapsed >= HammerImpact)
        {
            hammerPlayed = true;
            GameAudio.Play("hammer");
            GameAudio.Play("stoneCrack");
        }
        if (!revealPlayed && elapsed >= GeodeReveal)
        {
            revealPlayed = true;
            feedback = geodeLoot == null ? "Đã mở geode." : $"Đã nhận {geodeAmount} {geodeLoot.displayName}.";
            GameAudio.Play("discoverMineral");
        }
        if (elapsed < GeodeDuration) return;
        geodeStarted = -1f;
        purchasePending = false;
        if (GeodeAt(inventory, geodeSlot) == null) geodeSlot = -1;
    }

    private static int ClintFrame(float elapsed)
    {
        foreach (var step in HammerFrames)
            if (elapsed < step.End) return step.Frame;
        return 8;
    }

    private static void DrawBlacksmithAtlas(Texture2D atlas, Rect bounds, Rect source)
    {
        if (atlas != null)
            GUI.DrawTextureWithTexCoords(bounds, atlas, StardewUiAtlas.ToUv(source, atlas.width, atlas.height));
    }
}
