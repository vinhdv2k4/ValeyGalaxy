using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Farm.GetStarterShippingBinLocation and the shipping ItemGrabMenu.</summary>
public sealed class FarmShippingBin : MonoBehaviour
{
    private const int TileX = 71;
    private const int TileY = 14;
    private const float Reach = 1.75f;
    private static FarmShippingBin openBin;
    private Sprite sprite;
    private BoxCollider2D hitbox;
    private OnlineGameState online;
    private PlayerInventory playerInventory;
    private bool busy;
    private int openedFrame;

    public static bool IsMenuOpen => openBin != null;
    internal static void CloseMenu() => openBin?.Close();

    public static void Attach(GameLocation farm)
    {
        if (farm == null || farm.LocationName != "Farm" ||
            farm.GetComponentInChildren<FarmShippingBin>(true) != null) return;
        var go = new GameObject("ShippingBin");
        go.transform.SetParent(farm.transform, false);
        go.transform.localPosition = new Vector3(TileX, -TileY - 1f, 0f);
        go.AddComponent<FarmShippingBin>();
    }

    private void Awake()
    {
        var texture = Resources.Load<Texture2D>("Startup/ShippingBin");
        if (texture == null) { Debug.LogError("Shipping bin texture is missing."); return; }
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            Vector2.zero, 16f);
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y + 1f);
        hitbox = gameObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = sprite.bounds.size;
        hitbox.offset = sprite.bounds.center;
    }

    internal bool IsInReach(Vector2 standing) => hitbox != null &&
        Vector2.Distance(standing, hitbox.ClosestPoint(standing)) <= Reach;

    public static bool TryInteract(Vector2? pointer, Vector2 standing, Vector2 facing,
        OnlineGameState onlineState, PlayerInventory inventory)
    {
        var farm = LocationManager.Instance?.CurrentLocation;
        if (farm == null || farm.LocationName != "Farm") return false;
        var bin = farm.GetComponentInChildren<FarmShippingBin>();
        if (bin == null || !bin.IsInReach(standing)) return false;
        if (pointer.HasValue)
        {
            if (!bin.hitbox.OverlapPoint(pointer.Value)) return false;
        }
        else if (Vector2.Dot((Vector2)bin.hitbox.bounds.center - standing, facing) < 0f)
        {
            return false;
        }

        bin.online = onlineState;
        bin.playerInventory = inventory;
        bin.openedFrame = Time.frameCount;
        openBin = bin;
        GameAudio.Play("shwip");
        InputController.SuppressWorldUseAfterModal();
        return true;
    }

    private void Update()
    {
        if (openBin == this && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    private void OnGUI()
    {
        if (openBin != this) return;
        GUI.depth = -50;
        const float width = 864f;
        const float height = 438f;
        float scale = Mathf.Min(1f, (Screen.width - 32f) / width, (Screen.height - 32f) / height);
        if (scale <= 0f) return;
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f,
            (Screen.height - height * scale) / 2f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        try { DrawMenu(); }
        finally { GUI.matrix = previous; }
    }

    private void DrawMenu()
    {
        const float width = 864f;
        StardewUiAtlas.DrawGameMenuFrame(new Rect(0, 168, width, 270));
        Rect close = new(800, 128, 48, 48);
        StardewUiAtlas.DrawCursorsSprite(close, StardewUiAtlas.UpperRightCloseButtonRect);
        if (GUI.Button(close, GUIContent.none, GUIStyle.none)) { Close(); return; }

        var shipping = online?.Snapshot?.shipping;
        var last = shipping != null && shipping.Length > 0 ? shipping[^1] : null;
        Rect latest = new(384, 40, 96, 96);
        StardewUiAtlas.DrawStardewDialogueBox(new Rect(274, 0, 316, 160));
        NpcBitmapText.Draw(StardewUiAtlas.FontBold, new Rect(286, 10, 292, 25),
            last == null ? "Ô gửi bán" : "Món vừa gửi", 1.7f, true);
        StardewUiAtlas.DrawCursorsSprite(latest, new Rect(293, 360, 24, 24));
        Event evt = Event.current;
        bool overLatest = latest.Contains(evt.mousePosition);
        bool canInteract = !busy && !NpcConversation.IsOpen && Time.frameCount != openedFrame;
        if (last != null)
        {
            Rect icon = new(latest.x + 16, latest.y + 16, 64, 64);
            DrawIcon(online.ResolveItem(last.itemId)?.GetIcon(), icon, Color.white);
            StardewUiAtlas.DrawStackCount(icon, last.amount, last.itemId);
            if (canInteract && GUI.Button(latest, GUIContent.none, GUIStyle.none))
            {
                busy = true;
                online.RetrieveLastShipped(error => Finish(error, "coin"));
            }
        }

        string hover = last != null && overLatest ? "Lấy lại món vừa gửi" : null;
        for (int i = 0; i < InventoryModel.MaxCapacity; i++)
        {
            Rect cell = ItemSlotRect(i);
            StardewUiAtlas.DrawMenuTilesSprite(cell, StardewUiAtlas.SlotNormalRect);
            if (i >= (playerInventory?.UnlockedSlots ?? 0))
            {
                StardewUiAtlas.DrawMenuTilesSprite(cell, StardewUiAtlas.SlotLockedRect,
                    new Color(1, 1, 1, 0.5f));
                continue;
            }
            var slot = playerInventory.GetSlot(i) as InventorySlot;
            if (slot == null || slot.IsEmpty || slot.itemData == null) continue;
            bool canShip = CanShip(slot);
            DrawIcon(slot.itemData.GetIcon(), new Rect(cell.x + 8, cell.y + 8, 48, 48),
                canShip ? Color.white : new Color(1, 1, 1, 0.35f));
            StardewUiAtlas.DrawStackCount(cell, slot.amount, slot.itemData?.itemId);
            if (!cell.Contains(evt.mousePosition)) continue;
            hover = canShip ? slot.itemData.displayName : slot.itemData.displayName + " · Không gửi bán được";
            if (!canShip || !canInteract || evt.type != EventType.MouseDown || evt.button is not (0 or 1)) continue;
            busy = true;
            online.Ship(i, slot.itemData.itemId, evt.button == 1 ? 1 : slot.amount, error => Finish(error, "Ship"));
            evt.Use();
            break;
        }
        NpcBitmapText.Draw(StardewUiAtlas.FontBold, new Rect(32, 174, 768, 27),
            hover ?? "Chuột phải gửi 1 · Chuột trái gửi cả chồng", 1.7f, true);
    }

    private static Rect ItemSlotRect(int index)
    {
        Rect cell = BackpackGridUI.InventorySlotRect(index);
        cell.y += 148;
        return cell;
    }

    private bool CanShip(InventorySlot slot)
    {
        var eligible = online?.Snapshot?.shippableItems;
        return eligible != null && !slot.IsEmpty && slot.itemData != null &&
            System.Array.IndexOf(eligible, slot.itemData.itemId) >= 0;
    }

    private static void DrawIcon(Sprite icon, Rect bounds, Color tint)
    {
        if (icon == null || Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = tint;
        Rect rect = icon.textureRect;
        GUI.DrawTextureWithTexCoords(bounds, icon.texture, new Rect(rect.x / icon.texture.width,
            rect.y / icon.texture.height, rect.width / icon.texture.width, rect.height / icon.texture.height));
        GUI.color = previous;
    }

    private void Finish(string error, string sound)
    {
        busy = false;
        if (string.IsNullOrEmpty(error)) GameAudio.Play(sound);
        else NpcConversation.ShowNotice(error switch
        {
            "Item cannot be shipped." => "Vật phẩm này không gửi bán được. Hãy kiểm tra lại túi đồ.",
            "Not enough room in the backpack." => "Túi đồ đã đầy. Hãy chừa chỗ để lấy lại hàng.",
            _ => error
        });
    }

    private void Close()
    {
        if (openBin != this) return;
        openBin = null;
        GameAudio.Play("bigDeSelect");
        InputController.SuppressWorldUseAfterModal();
    }

    private void OnDisable() => Close();

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
    }
}
