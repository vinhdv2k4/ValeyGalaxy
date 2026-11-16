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
    [SerializeField] private SpriteRenderer arms;
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
    private Sprite displayedItem;
    private Vector3 displayPosition;
    private Quaternion displayRotation;
    private Vector3 heldItemScale;
    private float displayScale;
    private SpriteRenderer accessory;
    private FarmerSpriteCatalog catalog;
    private Texture2D recoloredBody;
    private int accessoryStyle = -1;
    private bool uniqueLeftHair;
    private readonly Dictionary<int, Sprite> recoloredBodyFrames = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, Sprite> recoloredArmFrames = new Dictionary<int, Sprite>();
    private readonly List<Sprite> generatedHairSprites = new List<Sprite>();
    private Sprite selectedAccessory;
    private float itemDisplayUntil;
    internal static readonly Vector3 CatchOffsetFromFeet = new(-32f / 64f, 88f / 64f, 0f);

    // Vertical bob of the body frames used by the carry clips.
    private static readonly byte[] CarryFeatureY =
    {
        1, 2, 2, 0, 5, 6, 1, 2, 2, 1, 0, 2, 0, 1, 1, 0, 2, 2, 3, 3, 2, 2, 1, 1
    };

    // Opaque end pixels of the 48x48 FishingRod_Cast sprites (top-left origin).
    private static readonly Vector2[] SideRodTips =
    {
        new Vector2(16f, 25f), new Vector2(23f, 21f), new Vector2(40f, 9f),
        new Vector2(47f, 22f), new Vector2(47f, 35f)
    };
    private static readonly Vector2[] VerticalUpRodTips =
    {
        new Vector2(19f, 7f), new Vector2(23f, 5f), new Vector2(24f, 20f),
        new Vector2(24f, 27f), new Vector2(24f, 30f)
    };
    private static readonly Vector2[] VerticalDownRodTips =
    {
        new Vector2(19f, 15f), new Vector2(23f, 20f), new Vector2(24f, 26f),
        new Vector2(24f, 35f), new Vector2(24f, 41f)
    };

    [SerializeField] private UnityEngine.Rendering.SortingGroup sortingGroup;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<CharacterMotor>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimationController>();
        if (hair == null) hair = transform.Find("Hair")?.GetComponent<SpriteRenderer>();
        if (shirt == null) shirt = transform.Find("Shirt")?.GetComponent<SpriteRenderer>();
        if (body == null) body = transform.Find("Body")?.GetComponent<SpriteRenderer>();
        if (pants == null) pants = transform.Find("Pants")?.GetComponent<SpriteRenderer>();
        if (arms == null) arms = transform.Find("Arms")?.GetComponent<SpriteRenderer>();
        if (heldItem == null) heldItem = transform.Find("Tool")?.GetComponent<SpriteRenderer>();
        if (heldItem != null) heldItemScale = heldItem.transform.localScale;
        if (toolSwipe == null) toolSwipe = transform.Find("ToolSwipe")?.GetComponent<SpriteRenderer>();

        if (sortingGroup == null) sortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sortingGroup == null) sortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
    }

    private void LateUpdate()
    {
        float footY = transform.position.y - 0.75f;
        int playerBaseOrder = WorldSortOrder.GetOrder(footY);
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = playerBaseOrder;
        }
        else
        {
            if (body != null) body.sortingOrder = playerBaseOrder + 1;
            if (pants != null) pants.sortingOrder = playerBaseOrder + 2;
            if (shirt != null) shirt.sortingOrder = playerBaseOrder + 3;
            if (hair != null) hair.sortingOrder = playerBaseOrder + 5;
        }

        Vector2 facing = motor == null ? Vector2.down : motor.Facing;
        int direction = facing.y > 0.5f ? 2 : facing.x < -0.5f ? 3 : facing.x > 0.5f ? 1 : 0;
        bool left = facing.x < -0.5f;
        if (shirtStyleIndex != cachedShirtStyle) SelectShirtStyle();
        if (direction != lastDirection || shirtStyleIndex != cachedShirtStyle)
        {
            if (hair != null && hairSprites != null && hairSprites.Length > direction) hair.sprite = hairSprites[direction];
            if (shirt != null && selectedShirtSprites != null && selectedShirtSprites.Length > direction) shirt.sprite = selectedShirtSprites[direction];
            lastDirection = direction;
            if (accessory != null)
            {
                if (selectedAccessory != null) Destroy(selectedAccessory);
                int stardewDir = direction == 0 ? 2 : direction == 2 ? 0 : direction;
                selectedAccessory = catalog == null ? null : catalog.Accessory(accessoryStyle, stardewDir);
                accessory.sprite = selectedAccessory;
                accessory.flipX = left;
                accessory.enabled = selectedAccessory != null;
                accessory.color = accessoryStyle < 6 && hair != null ? hair.color : Color.white;
                accessory.sortingOrder = hair != null ? hair.sortingOrder + (accessoryStyle < 8 ? -1 : 1) : 6;
            }
        }

        UpdateRecoloredBody();
        UpdatePants();
        if (accessory != null && hair != null)
            accessory.transform.localPosition = hair.transform.localPosition + Vector3.up * 0.5f;

        if (hair != null) hair.flipX = left && !uniqueLeftHair;
        if (shirt != null) shirt.flipX = false;

        InventorySlot item = inventory == null ? null : inventory.SelectedItem;
        int amount = item == null ? 0 : item.amount;
        if (heldItem != null)
        {
            if (displayedItem != null && Time.time < itemDisplayUntil)
            {
                heldItem.enabled = true;
                heldItem.sprite = displayedItem;
                heldItem.transform.localPosition = displayPosition;
                heldItem.transform.localRotation = displayRotation;
                heldItem.transform.localScale = heldItemScale * displayScale;
                heldItem.sortingOrder = sortingGroup != null ? 30 : playerBaseOrder + 10;
                heldItem.flipX = false;
                heldItem.flipY = false;
                return;
            }
            displayedItem = null;
            heldItem.transform.localScale = heldItemScale;
            bool isUsingSelectedItem = item != null
                && item.hasUseAnimation
                && playerAnimation != null
                && (playerAnimation.ActiveAction == item.ResolvedUseAnimation
                    || (item.kind == PlayerItemKind.FishingRod && playerAnimation.ActiveAction == FarmerAnimationAction.FishingDone));

            bool isTool = IsTool(item);
            Sprite selectedSprite = item == null ? null : isTool ? item.GetHeldSprite(direction)
                : item.itemData != null ? item.itemData.GetIcon() : item.inventoryIcon;
            if (selectedSprite == null && item != null && !isTool)
                selectedSprite = item.inventoryIcon != null ? item.inventoryIcon : item.GetHeldSprite(direction);
            bool canShowCarriedItem = playerAnimation == null || !playerAnimation.ActiveAction.HasValue
                || playerAnimation.ActiveAction == FarmerAnimationAction.CarryWalk
                || playerAnimation.ActiveAction == FarmerAnimationAction.CarryRun;
            bool shouldShow = item != null && amount > 0 && selectedSprite != null
                && (isTool ? isUsingSelectedItem : canShowCarriedItem);

            heldItem.enabled = shouldShow;
            if (shouldShow)
            {
                IToolVisualStrategy strategy = isUsingSelectedItem ? ToolVisualStrategyFactory.GetStrategy(item) : null;
                ToolPose pose = isUsingSelectedItem
                    ? strategy is GenericToolVisualStrategy swinging
                        ? swinging.CalculatePose(direction, playerAnimation.ActionProgress, item, playerAnimation.ToolPower)
                        : strategy.CalculatePose(direction, playerAnimation.ActionProgress, item)
                    : isTool ? EquippedPose(item, direction) : new ToolPose
                    {
                        OverrideSprite = selectedSprite,
                        LocalPosition = CarryPosition(selectedSprite),
                        LocalRotation = Quaternion.identity,
                        SortingOrder = 30
                    };

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

    private Vector3 CarryPosition(Sprite carriedSprite)
    {
        Sprite bodySprite = body != null ? body.sprite : null;
        if (bodySprite == null) return Vector3.up;

        int featureY = CarryFeatureY[0];
        string frameName = bodySprite.name;
        if (frameName.StartsWith("Body_") && int.TryParse(frameName.Substring(5), out int frame) &&
            frame >= 0 && frame < CarryFeatureY.Length)
            featureY = CarryFeatureY[frame];

        // A carried item sits 8 sprite pixels above the body's top edge plus the frame bob.
        // Taller sprites (32 px) share the same bottom edge as a 16 px item.
        float bodyPpu = bodySprite.pixelsPerUnit;
        var topLeft = new Vector3(-bodySprite.pivot.x / bodyPpu,
            (bodySprite.rect.height - bodySprite.pivot.y + 8 - featureY) / bodyPpu, 0f);
        Vector3 anchor = heldItem.transform.parent.InverseTransformPoint(body.transform.TransformPoint(topLeft));
        Vector2 pivot = carriedSprite.pivot / carriedSprite.pixelsPerUnit;
        return anchor + new Vector3(pivot.x, pivot.y - 16f / carriedSprite.pixelsPerUnit, 0f);
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

    // Positions below convert Stardew offsets from who.Position (4x pixels, Y down) to this
    // transform, whose origin is the body centre at Position + (32, -32): ((x - 32) / 64, -(y + 32) / 64).
    // Each value is the centre of the drawn sprite, which is how the swipe sprites are pivoted.

    /// <summary>Farmer.showToolSwipeEffect centres: animations sheet 18 up, 19 down, 15 sideways.</summary>
    private static readonly Vector3[] ToolSwipeCentres =
    {
        new(28f / 64f, 32f / 64f, 0f),   // Down: 128x128 at (-4, -128)
        new(52f / 64f, 36f / 64f, 0f),   // Right: 128x128 at (20, -132)
        new(0f, 68f / 64f, 0f),          // Up: 64x64 at (0, -132)
        new(-60f / 64f, 36f / 64f, 0f)   // Left: 128x128 at (-92, -132), flipped
    };

    /// <summary>Farmer.showSwordSwipe slash centres (Cursors 23x31, or 42x17 facing down, at 4x).</summary>
    private static readonly Vector3[] SwordSlashCentres =
    {
        new(-12f / 64f, -58f / 64f, 0f), // Down: 42x17 at (-64, -8)
        new(30f / 64f, -46f / 64f, 0f),  // Right: at (16, -48)
        new(14f / 64f, 34f / 64f, 0f),   // Up: at (0, -128), rotated 225 degrees
        new(-46f / 64f, -46f / 64f, 0f)  // Left: at (-60, -48), flipped
    };

    private static Sprite swordSlashSide, swordSlashDown;
    private NetworkCharacterStats stats;

    private void UpdateToolSwipe(InventorySlot item, int direction)
    {
        if (toolSwipe == null) return;
        toolSwipe.enabled = false;
        toolSwipe.color = Color.white;
        toolSwipe.transform.localRotation = Quaternion.identity;
        if (item == null || item.amount <= 0 || playerAnimation == null) return;

        bool isSwingingTool = ((item.itemData is ToolData && item.kind != PlayerItemKind.WateringCan) || item.kind == PlayerItemKind.Hoe || item.kind == PlayerItemKind.Axe || item.kind == PlayerItemKind.Pickaxe)
            && playerAnimation.ActiveAction == FarmerAnimationAction.Tool;
        bool isSlashingSword = (item.itemData is MeleeWeaponData || item.kind == PlayerItemKind.Sword)
            && playerAnimation.ActiveAction == FarmerAnimationAction.SwordSwipe;

        // FishingRod casts (FarmerSprite 295-298) fire the same swipe as frame 2 starts (140 ms of
        // 460). The waiting hold sits at 0.98 progress, past the swipe.
        bool isCastingRod = item.kind == PlayerItemKind.FishingRod &&
            playerAnimation.ActiveAction == FarmerAnimationAction.Fishing;

        if (isSwingingTool) ShowToolSwipe(direction, direction == 0 ? 150f : 100f, GenericToolVisualStrategy.ClipMilliseconds);
        else if (isCastingRod) ShowToolSwipe(direction, 140f, 460f + 1000f / 60f);
        else if (isSlashingSword) ShowSwordSlash(item, direction);
    }

    /// <summary>
    /// Farmer.showToolSwipeEffect from <paramref name="start"/> ms into the clip (tools: frame 1,
    /// rod casts: frame 2), four frames of 40 ms (50 ms facing up), twice as slow once exhausted.
    /// </summary>
    private void ShowToolSwipe(int direction, float start, float clipMilliseconds)
    {
        float elapsed = playerAnimation.ActionProgress * clipMilliseconds;
        stats ??= GetComponent<NetworkCharacterStats>();
        bool exhausted = stats != null && stats.ExactStamina <= 0f;
        float interval = direction == 2 ? (exhausted ? 100f : 50f) : (exhausted ? 80f : 40f);
        int frame = Mathf.FloorToInt((elapsed - start) / interval);
        if (frame < 0 || frame >= 4) return;

        // The sprites are named after their art: "Up" is the tall downward slash (sheet 19) and
        // "Down" the small burst above the head (sheet 18), so facing down uses swipeUp.
        Sprite[] frames = direction == 0 ? swipeUp : direction == 2 ? swipeDown : swipeSide;
        if (frames == null || frames.Length <= frame || frames[frame] == null) return;
        toolSwipe.sprite = frames[frame];
        toolSwipe.enabled = true;
        toolSwipe.flipX = direction == 3;
        // Facing up the burst sits behind the farmer (standingY - 9); otherwise in front.
        SetSwipeOrder(behind: direction == 2);
        toolSwipe.transform.localPosition = ToolSwipeCentres[direction];
    }

    /// <summary>
    /// Farmer.showSwordSwipe: a half-transparent slash from Cursors on the last swing frame
    /// (starts at 175 ms and lasts that frame), pink for the Galaxy Sword.
    /// </summary>
    private void ShowSwordSlash(InventorySlot item, int direction)
    {
        float elapsed = playerAnimation.ActionProgress * 275f;
        if (elapsed < 175f || elapsed >= 275f) return;
        Texture2D cursors = StardewUiAtlas.Cursors;
        if (cursors == null) return;
        // Source rectangles use a top-left origin; Unity textures start at the bottom.
        swordSlashSide ??= AtlasSprites.Get(cursors, new Rect(518, cursors.height - 274 - 31, 23, 31),
            new Vector2(0.5f, 0.5f), 16f);
        swordSlashDown ??= AtlasSprites.Get(cursors, new Rect(503, cursors.height - 256 - 17, 42, 17),
            new Vector2(0.5f, 0.5f), 16f);

        toolSwipe.sprite = direction == 0 ? swordSlashDown : swordSlashSide;
        toolSwipe.enabled = true;
        toolSwipe.flipX = direction == 3;
        bool galaxy = item.itemData is MeleeWeaponData weapon && weapon.initialParentTileIndex == 4;
        toolSwipe.color = galaxy ? new Color(1f, 105f / 255f, 180f / 255f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);
        // XNA rotation is clockwise; facing up the slash turns 3.926991 rad.
        if (direction == 2) toolSwipe.transform.localRotation = Quaternion.Euler(0f, 0f, -225f);
        SetSwipeOrder(behind: direction == 2);
        toolSwipe.transform.localPosition = SwordSlashCentres[direction];
    }

    private void SetSwipeOrder(bool behind)
    {
        int pOrder = WorldSortOrder.GetOrder(transform.position.y - 0.75f);
        toolSwipe.sortingOrder = sortingGroup != null ? (behind ? 19 : 27) : pOrder + (behind ? -1 : 7);
    }

    public void SetOutfit(int shirtStyle, int pantsStyle, bool femaleCut)
    {
        shirtStyleIndex = Mathf.Clamp(shirtStyle, 0, 303);
        pantsStyleIndex = Mathf.Clamp(pantsStyle, 0, 19);
        femalePants = femaleCut;
    }

    internal (Texture2D Body, Sprite Hair, Color SkinColor, Color HairColor, Color PantsColor,
        int Shirt, int Pants, bool Female, int Accessory) GetPortraitAppearance() =>
        (recoloredBody, hairSprites != null && hairSprites.Length > 0 ? hairSprites[0] : null,
            body != null ? body.color : Color.white, hair != null ? hair.color : Color.white,
            pants != null ? pants.color : Color.white, shirtStyleIndex, pantsStyleIndex, femalePants, accessoryStyle);

    public void ShowHarvestItem(Sprite itemSprite, float duration = 0.85f) =>
        DisplayItem(itemSprite, duration, Vector3.up * 1.45f);

    public void ShowHarvestItem(ItemData itemData, float duration = 0.85f)
    {
        ShowHarvestItem(itemData != null ? itemData.GetIcon() : null, duration);
    }

    internal void ShowRewardPose(Sprite itemSprite)
    {
        motor?.SetMovement(Vector2.zero);
        motor?.FaceTowards(Vector2.down);
        playerAnimation?.HoldReward();
        Vector3 feet = motor != null ? motor.StandingPosition : transform.position + Vector3.down * 0.75f;
        DisplayItem(itemSprite, float.PositiveInfinity,
            transform.InverseTransformPoint(feet + Vector3.up * (124f / 64f)));
    }

    internal void ShowCatchPose(Sprite fish, int fishId)
    {
        motor?.FaceTowards(Vector2.down);
        // FishingRod shows frame 84 and draws the fish at 3x instead of 4x.
        playerAnimation?.Play(FarmerAnimationAction.ShowHoldingEdible);
        Vector3 feet = motor != null ? motor.StandingPosition : transform.position + Vector3.down * 0.75f;
        float angle = fishId is 149 or 151 or 798 or 800 ? 0f : -135f;
        DisplayItem(fish, float.PositiveInfinity, transform.InverseTransformPoint(feet + CatchOffsetFromFeet), angle, 0.75f);
    }

    internal void ClearDisplayedItem() => displayedItem = null;

    private void DisplayItem(Sprite sprite, float duration, Vector3 position, float angle = 0f, float scale = 1f)
    {
        displayedItem = sprite;
        itemDisplayUntil = Time.time + duration;
        displayPosition = position;
        displayRotation = Quaternion.Euler(0f, 0f, angle);
        displayScale = scale;
    }

    public void ApplyCreatedFarmer(CreatedFarmer farmer)
    {
        if (farmer == null) return;
        catalog = FarmerSpriteCatalog.Load();
        SetOutfit(farmer.shirt, farmer.pants, farmer.gender == 1);
        if (catalog != null)
        {
            shirtSheet = catalog.shirts;
            pantsSheet = catalog.pants;
            foreach (Sprite sprite in generatedHairSprites) if (sprite != null) Destroy(sprite);
            generatedHairSprites.Clear();
            hairSprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                // AnimationController uses Down, Right, Up, Left; the
                // Character sprite atlas uses Up, Right, Down, Left.
                int stardewDirection = i == 0 ? 2 : i == 2 ? 0 : i;
                hairSprites[i] = catalog.Hair(farmer.hair, stardewDirection);
                if (hairSprites[i] != null) generatedHairSprites.Add(hairSprites[i]);
            }
            foreach (Sprite sprite in recoloredBodyFrames.Values) if (sprite != null) Destroy(sprite);
            foreach (Sprite sprite in recoloredArmFrames.Values) if (sprite != null) Destroy(sprite);
            recoloredBodyFrames.Clear();
            recoloredArmFrames.Clear();
            if (recoloredBody != null) Destroy(recoloredBody);
            Color eye = FarmerSpriteCatalog.DecodeColor(farmer.eyeColorRgb,
                EyePalette[Mathf.Clamp(farmer.eyeColor, 0, EyePalette.Length - 1)]);
            recoloredBody = catalog.RecolorBody(farmer.gender, Mathf.Clamp(farmer.skin, 0, 23), eye);
            uniqueLeftHair = farmer.hair >= 100 && farmer.hair <= 117;
            accessoryStyle = Mathf.Clamp(farmer.accessory, -1, 18);
            if (accessory == null)
            {
                Transform existing = transform.Find("Accessory");
                accessory = existing != null ? existing.GetComponent<SpriteRenderer>() :
                    new GameObject("Accessory").AddComponent<SpriteRenderer>();
                if (existing == null)
                {
                    accessory.transform.SetParent(transform, false);
                    accessory.transform.localPosition = hair != null ? hair.transform.localPosition + Vector3.up * 0.5f : Vector3.zero;
                    accessory.sortingOrder = hair != null ? hair.sortingOrder + 1 : 6;
                }
            }
            lastDirection = -1;
        }
        Color[] hairPalette =
        {
            new Color(0.47f, 0.28f, 0.17f), new Color(0.21f, 0.20f, 0.24f),
            new Color(0.88f, 0.70f, 0.29f), new Color(0.72f, 0.28f, 0.20f),
            new Color(0.27f, 0.53f, 0.79f), new Color(0.32f, 0.67f, 0.35f)
        };
        Color[] skinPalette =
        {
            Color.white, new Color(0.94f, 0.79f, 0.68f), new Color(0.83f, 0.63f, 0.49f),
            new Color(0.70f, 0.48f, 0.37f), new Color(0.56f, 0.36f, 0.27f),
            new Color(0.42f, 0.26f, 0.20f)
        };
        if (hair != null) hair.color = FarmerSpriteCatalog.DecodeColor(farmer.hairColorRgb,
            hairPalette[Mathf.Clamp(farmer.hairColor, 0, hairPalette.Length - 1)]);
        if (pants != null) pants.color = FarmerSpriteCatalog.DecodeColor(farmer.pantsColorRgb, Color.white);
        if (body != null) body.color = recoloredBody != null ? Color.white :
            skinPalette[Mathf.Clamp(farmer.skin, 0, skinPalette.Length - 1)];
        if (arms != null) arms.color = body != null ? body.color : Color.white;
    }

    private static readonly Color[] EyePalette =
    {
        new Color(0.36f, 0.22f, 0.13f), new Color(0.24f, 0.45f, 0.78f),
        new Color(0.26f, 0.57f, 0.28f), new Color(0.48f, 0.51f, 0.55f),
        new Color(0.68f, 0.40f, 0.14f), new Color(0.49f, 0.34f, 0.62f)
    };

    private void UpdateRecoloredBody()
    {
        if (catalog == null || recoloredBody == null || body == null || body.sprite == null) return;
        Sprite animated = body.sprite;
        if (animated.name.StartsWith("Body_") && int.TryParse(animated.name.Substring(5), out int frame))
        {
            if (!recoloredBodyFrames.TryGetValue(frame, out Sprite replacement))
            {
                replacement = catalog.Body(recoloredBody, frame);
                recoloredBodyFrames[frame] = replacement;
            }
            if (replacement != null) body.sprite = replacement;
        }
        if (arms == null || arms.sprite == null) return;
        string armName = arms.sprite.name;
        bool alternate = armName.StartsWith("SecondaryArms_");
        int prefix = alternate ? 14 : 5;
        if (!(alternate || armName.StartsWith("Arms_")) || !int.TryParse(armName.Substring(prefix), out int armFrame)) return;
        int key = armFrame + (alternate ? 1000 : 0);
        if (!recoloredArmFrames.TryGetValue(key, out Sprite armReplacement))
        {
            armReplacement = catalog.Body(recoloredBody, armFrame, true, alternate);
            recoloredArmFrames[key] = armReplacement;
        }
        if (armReplacement != null) arms.sprite = armReplacement;
    }

    private void SelectShirtStyle()
    {
        foreach (var sprite in generatedShirtSprites) if (sprite != null) Destroy(sprite);
        generatedShirtSprites.Clear();
        selectedShirtSprites = shirtSprites;
        if (shirtSheet != null)
        {
            int x = shirtStyleIndex % 16 * 8;
            int top = shirtStyleIndex / 16 * 32;
            selectedShirtSprites = new[]
            {
                Slice(shirtSheet, x, top, 8, 8),
                Slice(shirtSheet, x, top + 8, 8, 8),
                Slice(shirtSheet, x, top + 24, 8, 8),
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
        foreach (var sprite in generatedHairSprites) if (sprite != null) Destroy(sprite);
        foreach (var sprite in recoloredBodyFrames.Values) if (sprite != null) Destroy(sprite);
        foreach (var sprite in recoloredArmFrames.Values) if (sprite != null) Destroy(sprite);
        if (selectedAccessory != null) Destroy(selectedAccessory);
        if (recoloredBody != null) Destroy(recoloredBody);
    }
}
