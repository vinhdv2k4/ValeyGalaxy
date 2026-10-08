using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws the farmer's clothing and the item selected in the inventory.</summary>
public sealed class PlayerAppearance : MonoBehaviour
{
    [SerializeField] private CharacterMotor motor;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerAnimationController playerAnimation;
    [SerializeField] private SpriteRenderer hair;
    [SerializeField] private SpriteRenderer shirt;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer pants;
    [SerializeField] private SpriteRenderer heldItem;
    [SerializeField] private SpriteRenderer toolSwipe;
    [SerializeField] private Sprite[] swipeDown;
    [SerializeField] private Sprite[] swipeSide;
    [SerializeField] private Sprite[] swipeUp;
    // Down, Right, Up, Left. The left hairstyle mirrors the right source frame.
    [SerializeField] private Sprite[] hairSprites;
    [SerializeField] private Sprite[] shirtSprites;
    [SerializeField] private Texture2D shirtSheet;
    [SerializeField] private Texture2D pantsSheet;
    [SerializeField, Range(0, 303)] private int shirtStyleIndex;
    [SerializeField, Range(0, 19)] private int pantsStyleIndex;
    [SerializeField] private bool femalePants;
    private int lastDirection = -1;
    private int cachedShirtStyle = -1;
    private int cachedPantsStyle = -1;
    private bool cachedFemalePants;
    private Sprite[] selectedShirtSprites;
    private readonly List<Sprite> generatedShirtSprites = new List<Sprite>();
    private readonly Dictionary<int, Sprite> selectedPantsFrames = new Dictionary<int, Sprite>();
    private Sprite defaultPantsFrame;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<CharacterMotor>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimationController>();
        if (hair == null) hair = transform.Find("Hair")?.GetComponent<SpriteRenderer>();
        if (shirt == null) shirt = transform.Find("Shirt")?.GetComponent<SpriteRenderer>();
        if (body == null) body = transform.Find("Body")?.GetComponent<SpriteRenderer>();
        if (pants == null) pants = transform.Find("Pants")?.GetComponent<SpriteRenderer>();
        if (heldItem == null) heldItem = transform.Find("Tool")?.GetComponent<SpriteRenderer>();
        if (toolSwipe == null) toolSwipe = transform.Find("ToolSwipe")?.GetComponent<SpriteRenderer>();

    }

    private void LateUpdate()
    {
        Vector2 facing = motor == null ? Vector2.down : motor.Facing;
        int direction = facing.y > 0.5f ? 2 : facing.x < -0.5f ? 3 : facing.x > 0.5f ? 1 : 0;
        bool left = facing.x < -0.5f;
        if (shirtStyleIndex != cachedShirtStyle) SelectShirtStyle();
        if (direction != lastDirection || shirtStyleIndex != cachedShirtStyle)
        {
            if (hair != null && hairSprites != null && hairSprites.Length > direction) hair.sprite = hairSprites[direction];
            if (shirt != null && selectedShirtSprites != null && selectedShirtSprites.Length > direction) shirt.sprite = selectedShirtSprites[direction];
            lastDirection = direction;
        }

        UpdatePants();

        if (hair != null) hair.flipX = left;
        if (shirt != null) shirt.flipX = false;

        InventorySlot item = inventory == null ? null : inventory.SelectedItem;
        int amount = item == null ? 0 : item.amount;
        if (heldItem != null)
        {
            bool isUsingSelectedItem = item != null
                && item.hasUseAnimation
                && playerAnimation != null
                && playerAnimation.ActiveAction == item.ResolvedUseAnimation;

            bool isTool = IsTool(item);
            bool shouldShow = item != null && amount > 0 && item.GetHeldSprite(direction) != null
                && (!isTool || isUsingSelectedItem);

            heldItem.enabled = shouldShow;
            if (shouldShow)
            {
                ToolPose pose = isUsingSelectedItem
                    ? ToolVisualStrategyFactory.GetStrategy(item).CalculatePose(direction, playerAnimation.ActionProgress, item)
                    : EquippedPose(item, direction);

                heldItem.sprite = pose.OverrideSprite;
                heldItem.enabled = !pose.Hide && pose.OverrideSprite != null;
                heldItem.transform.localPosition = pose.LocalPosition;
                heldItem.transform.localRotation = pose.LocalRotation;
                heldItem.sortingOrder = pose.SortingOrder;
                heldItem.flipX = pose.FlipX;
                heldItem.flipY = pose.FlipY;
            }
        }
        UpdateToolSwipe(item, direction);
    }

    public static bool IsTool(InventorySlot item)
    {
        if (item == null) return false;
        if (item.itemData is ToolData || item.itemData is MeleeWeaponData) return true;
        return item.kind == PlayerItemKind.Hoe
            || item.kind == PlayerItemKind.Axe
            || item.kind == PlayerItemKind.Pickaxe
            || item.kind == PlayerItemKind.WateringCan
            || item.kind == PlayerItemKind.Sword
            || item.kind == PlayerItemKind.FishingRod
            || item.kind == PlayerItemKind.Shears
            || item.kind == PlayerItemKind.MilkPail
            || item.kind == PlayerItemKind.Pan;
    }

    private static ToolPose EquippedPose(InventorySlot item, int direction)
    {
        Vector3 pos;
        float angle;
        int sortingOrder;
        bool flipX = direction == 3;

        switch (direction)
        {
            case 0: // Facing Down: Held naturally in right hand at side
                pos = new Vector3(0.24f, -0.15f, 0f);
                angle = -10f;
                sortingOrder = 26;
                break;
            case 2: // Facing Up: Held at back/side behind torso
                pos = new Vector3(-0.20f, -0.05f, 0f);
                angle = 10f;
                sortingOrder = 20; // Behind farmer body
                break;
            case 1: // Facing Right: Held at right hand side
                pos = new Vector3(0.18f, -0.15f, 0f);
                angle = 15f;
                sortingOrder = 26;
                break;
            case 3: // Facing Left: Mirrored at left hand side
                pos = new Vector3(-0.18f, -0.15f, 0f);
                angle = -15f;
                sortingOrder = 26;
                break;
            default:
                pos = Vector3.zero;
                angle = 0f;
                sortingOrder = 26;
                break;
        }

        return new ToolPose
        {
            OverrideSprite = item.GetHeldSprite(direction),
            LocalPosition = pos,
            LocalRotation = Quaternion.Euler(0f, 0f, angle),
            SortingOrder = sortingOrder,
            FlipX = flipX
        };
    }

    private void UpdateToolSwipe(InventorySlot item, int direction)
    {
        if (toolSwipe == null) return;
        toolSwipe.enabled = false;
        if (item == null || item.amount <= 0 || playerAnimation == null) return;

        bool isSwingingTool = ((item.itemData is ToolData && item.kind != PlayerItemKind.WateringCan) || item.kind == PlayerItemKind.Hoe || item.kind == PlayerItemKind.Axe || item.kind == PlayerItemKind.Pickaxe)
            && playerAnimation.ActiveAction == FarmerAnimationAction.Tool;
        bool isSlashingSword = (item.itemData is MeleeWeaponData || item.kind == PlayerItemKind.Sword)
            && playerAnimation.ActiveAction == FarmerAnimationAction.SwordSwipe;

        if (!isSwingingTool && !isSlashingSword) return;

        float totalDuration = isSlashingSword ? 275f : GenericToolVisualStrategy.ClipMilliseconds;
        float elapsed = playerAnimation.ActionProgress * totalDuration;
        float start = isSlashingSword ? (direction == 0 ? 55f : 45f) : (direction == 0 ? 190f : 140f);
        float interval = isSlashingSword ? 35f : (direction == 0 ? 50f : 40f);
        int frame = Mathf.FloorToInt((elapsed - start) / interval);
        if (frame < 0 || frame >= 4) return;

        Sprite[] frames = direction == 0 ? swipeDown : direction == 2 ? swipeUp : swipeSide;
        if (frames == null || frames.Length <= frame || frames[frame] == null) return;
        toolSwipe.sprite = frames[frame];
        toolSwipe.enabled = true;
        toolSwipe.flipX = direction == 3;
        toolSwipe.sortingOrder = 27;
        toolSwipe.transform.localPosition = direction switch
        {
            0 => new Vector3(0f, -0.45f, 0f),
            1 => new Vector3(0.50f, 0.05f, 0f),
            2 => new Vector3(0f, 0.50f, 0f),
            _ => new Vector3(-0.50f, 0.05f, 0f)
        };
    }

    public void SetOutfit(int shirtStyle, int pantsStyle, bool femaleCut)
    {
        shirtStyleIndex = Mathf.Clamp(shirtStyle, 0, 303);
        pantsStyleIndex = Mathf.Clamp(pantsStyle, 0, 19);
        femalePants = femaleCut;
    }

    private void SelectShirtStyle()
    {
        foreach (var sprite in generatedShirtSprites) if (sprite != null) Destroy(sprite);
        generatedShirtSprites.Clear();
        selectedShirtSprites = shirtSprites;
        if (shirtStyleIndex != 0 && shirtSheet != null)
        {
            int x = shirtStyleIndex % 16 * 8;
            int top = shirtStyleIndex / 16 * 32;
            selectedShirtSprites = new[]
            {
                Slice(shirtSheet, x, top, 8, 8),
                Slice(shirtSheet, x, top + 8, 8, 8),
                Slice(shirtSheet, x, top, 8, 8),
                Slice(shirtSheet, x, top + 16, 8, 8)
            };
            foreach (var sprite in selectedShirtSprites)
                if (sprite != null) generatedShirtSprites.Add(sprite);
        }
        cachedShirtStyle = shirtStyleIndex;
        lastDirection = -1;
    }

    private void UpdatePants()
    {
        if (body == null || pants == null || pantsSheet == null || body.sprite == null) return;
        if (cachedPantsStyle != pantsStyleIndex || cachedFemalePants != femalePants)
        {
            foreach (var frame in selectedPantsFrames.Values) if (frame != null) Destroy(frame);
            selectedPantsFrames.Clear();
            cachedPantsStyle = pantsStyleIndex;
            cachedFemalePants = femalePants;
        }
        if (pants.sprite != null && pants.sprite.name.StartsWith("Pants_")) defaultPantsFrame = pants.sprite;
        if (pantsStyleIndex == 0 && !femalePants)
        {
            if (defaultPantsFrame != null) pants.sprite = defaultPantsFrame;
            return;
        }
        string name = body.sprite.name;
        if (!name.StartsWith("Body_") || !int.TryParse(name.Substring(5), out int frameIndex)) return;
        if (!selectedPantsFrames.TryGetValue(frameIndex, out var selected))
        {
            int x = pantsStyleIndex % 10 * 192 + (femalePants ? 96 : 0) + frameIndex % 6 * 16;
            int top = pantsStyleIndex / 10 * 688 + frameIndex / 6 * 32;
            selected = Slice(pantsSheet, x, top, 16, 32);
            selectedPantsFrames.Add(frameIndex, selected);
        }
        if (selected != null) pants.sprite = selected;
    }

    private static Sprite Slice(Texture2D sheet, int x, int top, int width, int height)
    {
        if (x < 0 || top < 0 || x + width > sheet.width || top + height > sheet.height) return null;
        return Sprite.Create(sheet, new Rect(x, sheet.height - top - height, width, height),
            new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
    }

    private void OnDestroy()
    {
        foreach (var sprite in generatedShirtSprites) if (sprite != null) Destroy(sprite);
        foreach (var sprite in selectedPantsFrames.Values) if (sprite != null) Destroy(sprite);
    }
}
