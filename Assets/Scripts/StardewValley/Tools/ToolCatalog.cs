using System.Collections.Generic;

/// <summary>One registration point for runtime tool implementations.</summary>
public static class ToolCatalog
{
    private static readonly Dictionary<PlayerItemKind, Tool> Tools = new()
    {
        { PlayerItemKind.Hoe, new HoeTool() },
        { PlayerItemKind.Axe, new AxeTool() },
        { PlayerItemKind.Pickaxe, new PickaxeTool() },
        { PlayerItemKind.WateringCan, new WateringCanTool() },
        { PlayerItemKind.FishingRod, new Tool(PlayerItemKind.FishingRod, -1,
            FarmerAnimationAction.Fishing, new FishingRodVisualStrategy()) },
        { PlayerItemKind.Sword, new MeleeWeapon() },
        { PlayerItemKind.Shears, new Tool(PlayerItemKind.Shears, -1,
            FarmerAnimationAction.Shear, new DefaultHoldVisualStrategy()) },
        { PlayerItemKind.MilkPail, new Tool(PlayerItemKind.MilkPail, -1,
            FarmerAnimationAction.Milk, new DefaultHoldVisualStrategy()) },
        { PlayerItemKind.Pan, new Tool(PlayerItemKind.Pan, -1,
            FarmerAnimationAction.Pan, new DefaultHoldVisualStrategy()) }
    };
    private static readonly Dictionary<MeleeWeaponData, MeleeWeapon> Weapons = new();

    public static Tool Resolve(PlayerItemKind kind)
        => Tools.TryGetValue(kind, out var tool) ? tool : null;

    public static Tool Resolve(ItemData item)
    {
        if (item is MeleeWeaponData weapon)
        {
            if (!Weapons.TryGetValue(weapon, out var profile))
            {
                profile = new MeleeWeapon(weapon.kind, weapon.initialParentTileIndex,
                    new MeleeWeaponVisualStrategy(), (int)weapon.weaponType,
                    weapon.minDamage, weapon.maxDamage)
                {
                    Knockback = weapon.knockback,
                    Speed = weapon.speed,
                    CritChance = weapon.critChance,
                    CritMultiplier = weapon.critMultiplier,
                    AddedDefense = weapon.addedDefense
                };
                Weapons.Add(weapon, profile);
            }
            return profile;
        }
        return item == null ? null : Resolve(item.kind);
    }

    public static Tool Resolve(InventorySlot slot)
        => slot == null ? null : slot.itemData != null
            ? Resolve(slot.itemData) : Resolve(slot.kind);

    public static void Register(PlayerItemKind kind, Tool tool)
    {
        if (tool != null) Tools[kind] = tool;
    }
}
