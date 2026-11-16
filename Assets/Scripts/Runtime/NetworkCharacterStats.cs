using UnityEngine;

public sealed class NetworkCharacterStats : MonoBehaviour
{
    private sealed class ActiveFoodBuff
    {
        public ItemData Item;
        public FoodBuffModifiers Modifiers;
        public float EndsAt;
    }

    [SerializeField, Min(1)] private int maxStamina = 100;
    // Float like Farmer.Stamina: tool costs fall by 0.1 per skill level, and work goes on below 0
    // (exhausted) until -15, when the farmer passes out.
    [SerializeField] private float stamina = 100;
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int health = 100;
    private ActiveFoodBuff foodBuff;
    private ActiveFoodBuff drinkBuff;
    private bool hasMineStardrop;
    private int combatHealthBonus, fishingSkill;
    private float mineHitCooldownUntil;

    public int Stamina => (int)stamina;
    public float ExactStamina => stamina;
    /// <summary>Farmer.exhausted: energy fell to 0 or below today; tomorrow starts at half energy.</summary>
    public bool IsExhausted { get; private set; }
    /// <summary>Energy reached -15 (Farmer.checkForExhaustion): the farmer passes out.</summary>
    public event System.Action PassedOut;
    private const float PassOutStamina = -15f;
    private int[] professions = System.Array.Empty<int>();
    public int MaxStamina => maxStamina + (hasMineStardrop ? 34 : 0) + Bonus(m => m.maxStamina);
    public int Health => health;
    public int MaxHealth => maxHealth + combatHealthBonus;
    public int FishingBonus => fishingSkill + Bonus(m => m.fishing);
    public int FarmingBonus => Bonus(m => m.farming);
    public int MiningBonus => Bonus(m => m.mining);
    public int ForagingBonus => Bonus(m => m.foraging);
    public int CraftingBonus => Bonus(m => m.crafting);
    public int SpeedBonus => Bonus(m => m.speed);
    public int LuckBonus => Bonus(m => m.luck);
    public int MagneticRadiusBonus => Bonus(m => m.magneticRadius);
    public int DefenseBonus => Bonus(m => m.defense);
    public int AttackBonus => Bonus(m => m.attack);

    private int Bonus(System.Func<FoodBuffModifiers, int> select) =>
        (foodBuff == null ? 0 : select(foodBuff.Modifiers)) +
        (drinkBuff == null ? 0 : select(drinkBuff.Modifiers));

    private void Update()
    {
        if (foodBuff != null && Time.time >= foodBuff.EndsAt) foodBuff = null;
        if (drinkBuff != null && Time.time >= drinkBuff.EndsAt) drinkBuff = null;
        stamina = Mathf.Min(stamina, MaxStamina);
    }

    internal bool HasProfession(int profession) => System.Array.IndexOf(professions, profession) >= 0;

    public void ApplyFood(ItemData item)
    {
        if (item == null || !item.IsEdible) return;
        // Uses staminaRecoveredOnConsumption and healthRecoveredOnConsumption formulas.
        if (item.foodBuff != null && item.foodBuff.HasAny && item.buffDurationMinutes > 0)
        {
            float secondsPerMinute = FarmDayClock.Instance == null ? 0.7f : FarmDayClock.Instance.SecondsPerGameMinute;
            var active = new ActiveFoodBuff
            {
                Item = item,
                Modifiers = item.foodBuff,
                EndsAt = Time.time + item.buffDurationMinutes * secondsPerMinute
            };
            // BuffsDisplay keeps one food buff and one drink buff; a new one replaces its category.
            if (item.isDrink) drinkBuff = active;
            else foodBuff = active;
        }
        int energy = Mathf.CeilToInt(item.edibility * 2.5f);
        RestoreStamina(energy);
        RestoreHealth(Mathf.FloorToInt(energy * 0.45f));
    }
    /// <summary>
    /// Spends energy like Tool.DoFunction and Farmer.checkForExhaustion: work is allowed until
    /// -15; dropping to 0 or below exhausts the farmer, reaching -15 makes them pass out.
    /// </summary>
    public bool TrySpendStamina(float amount)
    {
        if (amount < 0f || stamina <= PassOutStamina) return false;
        float before = stamina;
        stamina -= amount;
        if (stamina <= 0f && before > 0f && !IsExhausted)
        {
            IsExhausted = true;
            NpcConversation.ShowNotice("Bạn đã kiệt sức! Hãy ăn gì đó hoặc đi ngủ để hồi sức.");
        }
        // Stamina never goes below -16; passes out at -15.
        stamina = Mathf.Max(stamina, PassOutStamina - 1f);
        if (stamina <= PassOutStamina) PassedOut?.Invoke();
        return true;
    }

    /// <summary>Tool energy (2 * (power + 1) for tools, 8 for the rod) less 0.1 per level of its skill.</summary>
    internal static float ToolStaminaCost(float baseCost, int skillLevel) => Mathf.Max(0f, baseCost - skillLevel * 0.1f);

    /// <summary>
    /// Farmer.dayupdate: full energy, or half (+1) after being exhausted; going to bed after midnight
    /// costs up to half the cap; energy never drops below what the farmer went to bed with.
    /// </summary>
    internal void WakeUp(int bedtime)
    {
        float before = stamina;
        float morning = MaxStamina;
        if (IsExhausted) morning = MaxStamina / 2 + 1;
        IsExhausted = false;
        if (bedtime > 2400)
            morning -= (1f - (2600 - Mathf.Min(2600, bedtime)) / 200f) * (MaxStamina / 2);
        if (before > morning) morning = before;
        stamina = Mathf.Min(morning, MaxStamina);
        health = MaxHealth;
    }

    public void RestoreStamina(int amount)
    {
        stamina = Mathf.Clamp(stamina + Mathf.Max(0, amount), PassOutStamina - 1f, MaxStamina);
        if (stamina > 0f) IsExhausted = false;
    }
    internal void ApplyMineStardrop(bool claimed)
    {
        if (!claimed || hasMineStardrop) return;
        hasMineStardrop = true;
        stamina = MaxStamina;
    }
    internal void ApplySkillProgress(int[] levels, string account, int[] chosenProfessions)
    {
        professions = chosenProfessions ?? System.Array.Empty<int>();
        fishingSkill = NightResultsMenu.ShownSkillLevel(account, levels, 1);
        int combat = NightResultsMenu.ShownSkillLevel(account, levels, 4);
        int previousMaxHealth = MaxHealth;
        combatHealthBonus = (combat - combat / 5) * 5;
        // LevelUpMenu.getImmediateProfessionPerk: Fighter +15, Defender +25 max health.
        if (HasProfession(24)) combatHealthBonus += 15;
        if (HasProfession(27)) combatHealthBonus += 25;
        health = Mathf.Clamp(health + MaxHealth - previousMaxHealth, 0, MaxHealth);
    }

    /// <summary>Resumes the energy and health saved when the player left the game.</summary>
    internal void RestoreVitals(int savedStamina, int savedHealth)
    {
        stamina = Mathf.Clamp(savedStamina, 0, MaxStamina);
        IsExhausted = savedStamina <= 0;
        health = Mathf.Clamp(savedHealth, 1, MaxHealth);
    }

    public void RestoreHealth(int amount) => health = Mathf.Clamp(health + Mathf.Max(0, amount), 0, MaxHealth);
    public void TakeDamage(int amount) => health = Mathf.Clamp(health - Mathf.Max(0, amount - DefenseBonus), 0, MaxHealth);

    internal bool TryTakeMineHit(int amount)
    {
        if (amount <= 0 || health <= 0 || Time.time < mineHitCooldownUntil) return false;
        health = Mathf.Max(0, health - Mathf.Max(1, amount - DefenseBonus));
        mineHitCooldownUntil = Time.time + 1.2f;
        return true;
    }

    // Only the farmer this client controls draws buff icons; avatars lose the controller.
    private NetworkPlayerController controller;
    private bool controllerLookedUp;

    private void OnGUI()
    {
        if (!controllerLookedUp)
        {
            controller = GetComponent<NetworkPlayerController>();
            controllerLookedUp = true;
        }
        // A destroyed controller compares equal to null, so avatars stop here too.
        if (controller == null || StardewUiAtlas.Cursors == null) return;
        GUI.depth = -35;
        float x = Screen.width - 454f;
        if (foodBuff != null) DrawBuff(foodBuff, x, 16f);
        if (drinkBuff != null) DrawBuff(drinkBuff, x + (foodBuff == null ? 0f : 64f), 16f);
    }

    private static void DrawBuff(ActiveFoodBuff buff, float x, float y)
    {
        var iconBox = new Rect(x, y, 56f, 56f);
        StardewUiAtlas.DrawTextureBox(iconBox, StardewUiAtlas.Cursors,
            new Rect(375, 357, 3, 3), Color.white, 4f, false);
        Sprite icon = buff.Item.GetIcon();
        if (icon != null && icon.texture != null)
        {
            Rect source = icon.rect;
            Texture2D texture = icon.texture;
            GUI.DrawTextureWithTexCoords(new Rect(x + 8f, y + 8f, 40f, 40f), texture,
                new Rect(source.x / texture.width, source.y / texture.height,
                    source.width / texture.width, source.height / texture.height));
        }
        int remaining = Mathf.CeilToInt(Mathf.Max(0f, buff.EndsAt - Time.time));
        NpcBitmapText.Draw(StardewUiAtlas.FontBold, new Rect(x, y + 58f, 62f, 25f),
            $"{remaining / 60}:{remaining % 60:00}", 1.4f);
        if (Event.current == null || !iconBox.Contains(Event.current.mousePosition)) return;
        float tooltipX = Mathf.Clamp(x - 120f, 8f, Screen.width - 290f);
        var tooltip = new Rect(tooltipX, y + 84f, 282f, 104f);
        StardewUiAtlas.DrawStardewDialogueBox(tooltip);
        NpcBitmapText.Draw(StardewUiAtlas.FontBold,
            new Rect(tooltip.x + 18f, tooltip.y + 20f, tooltip.width - 36f, 74f),
            buff.Item.displayName + "\n" + Describe(buff.Modifiers), 1.7f);
    }

    private static string Describe(FoodBuffModifiers buff)
    {
        var parts = new System.Collections.Generic.List<string>();
        Add(parts, "Speed", buff.speed);
        Add(parts, "Fishing", buff.fishing);
        Add(parts, "Luck", buff.luck);
        Add(parts, "Defense", buff.defense);
        Add(parts, "Attack", buff.attack);
        Add(parts, "Energy", buff.maxStamina);
        Add(parts, "Magnet", buff.magneticRadius);
        Add(parts, "Farming", buff.farming);
        Add(parts, "Mining", buff.mining);
        Add(parts, "Foraging", buff.foraging);
        Add(parts, "Crafting", buff.crafting);
        return string.Join(" ", parts);
    }

    private static void Add(System.Collections.Generic.List<string> parts, string label, int value)
    {
        if (value != 0) parts.Add(label + " " + (value > 0 ? "+" : "") + value);
    }
}
