"""Reproducible calculations for Thiet_Ke_Cong_Thuc_Kinh_Te_Dua_Tren_Project.md.

Run: python docs/balance_economy.py
All prices are design proposals. Crop timings and potato extra yield match current assets.
"""

from dataclasses import dataclass
from math import floor


@dataclass(frozen=True)
class Crop:
    seed: int
    sell: int
    grow_days: int
    expected_yield: float = 1.0
    regrow_days: int = 0


CROPS = {
    "parsnip": Crop(20, 38, 4),
    "potato": Crop(50, 65, 6, 1.25),
    "green_bean": Crop(60, 30, 10, 1.0, 3),
    "cauliflower": Crop(80, 145, 12),
}

START_CASH = 1200
BASE_RENT = 150
EXPANSION_RENT = 85
EXPANSION_FEE = 900
HALF_SHIFT = 55  # 24 valid farming actions
FULL_SHIFT = 90  # 48 valid farming actions
SPRINKLER_PRICE = 2200
SPRINKLER_DAILY = 12


def harvest_days(crop: Crop, plant_day: int, last_day: int) -> list[int]:
    first = plant_day + crop.grow_days
    if first > last_day:
        return []
    if not crop.regrow_days:
        return [first]
    return list(range(first, last_day + 1, crop.regrow_days))


def crop_margin(crop: Crop, plots: int, harvests: int = 1) -> float:
    return plots * (harvests * crop.sell * crop.expected_yield - crop.seed)


def expected_yield(min_harvest: int, max_harvest: int, extra_chance: float) -> float:
    """Matches CropPlot's uniform base yield plus up to 20 chained extras."""
    base = (min_harvest + max_harvest) / 2
    extras = sum(extra_chance**k for k in range(1, 21))
    return base + extras


def round_g(value: float, step: int = 1) -> int:
    return floor(value / step + 0.5) * step


def suggest_crop_prices(
    grow_days: int,
    regrow_days: int,
    min_harvest: int,
    max_harvest: int,
    extra_chance: float,
    tier: int,
    season_days: int = 28,
    first_available_day: int = 1,
) -> tuple[int, int, float, int]:
    """Return seed, NPC sell, expected yield, harvest count for an early-season crop.

    These are candidate prices, not automatic live-server prices. Designers must
    review season restrictions, actual sale limits, and any upgrades/quality buffs.
    """
    if grow_days < 1 or tier < 0 or not 0 <= extra_chance < 1 or first_available_day < 1:
        raise ValueError("Invalid crop profile")
    profile = Crop(0, 0, grow_days, regrow_days=regrow_days)
    count = len(harvest_days(profile, first_available_day, season_days))
    if count == 0:
        raise ValueError("Crop cannot mature in the allowed season")
    yield_per_harvest = expected_yield(min_harvest, max_harvest, extra_chance)
    target_per_day = min(6.5, max(3.5, 4.5 + 0.64 * tier + 0.035 * (grow_days - 4)
                                  - (0.42 if regrow_days else 0)))
    horizon = season_days - first_available_day + 1 if regrow_days else grow_days
    target_margin = target_per_day * horizon

    if regrow_days:
        seed_share = 0.33
    elif yield_per_harvest > 1.05:
        seed_share = 0.62
    elif grow_days >= 10:
        seed_share = 0.55
    else:
        seed_share = 0.53

    seed = max(5, round_g(target_margin * seed_share / (1 - seed_share), 5))
    sell = max(1, round_g((seed + target_margin) / (yield_per_harvest * count)))
    return seed, sell, yield_per_harvest, count


def fmt(amount: float) -> str:
    return f"{amount:,.2f}".rstrip("0").rstrip(".")


def main() -> None:
    parsnip = CROPS["parsnip"]
    potato = CROPS["potato"]
    bean = CROPS["green_bean"]
    cauliflower = CROPS["cauliflower"]

    assert harvest_days(parsnip, 1, 7) == [5]
    assert harvest_days(potato, 1, 7) == [7]
    assert harvest_days(bean, 1, 28) == [11, 14, 17, 20, 23, 26]
    assert harvest_days(cauliflower, 1, 28) == [13]

    normal = crop_margin(parsnip, 24) - BASE_RENT
    weak_price = 30  # rounded 80% of 38G
    weak = 24 * (weak_price - parsnip.seed) - BASE_RENT
    busy = crop_margin(parsnip, 48) - BASE_RENT
    potato_week = crop_margin(potato, 24) - BASE_RENT

    # New 24-cell plot: 72 actions on day 1 (hoe, plant, water),
    # then water 3 or 5 days, harvest once. 48+24 actions cost 90+55G.
    parsnip_hire = FULL_SHIFT + HALF_SHIFT + 3 * HALF_SHIFT + HALF_SHIFT
    potato_hire = FULL_SHIFT + HALF_SHIFT + 5 * HALF_SHIFT + HALF_SHIFT
    extra_parsnip = crop_margin(parsnip, 24) - EXPANSION_RENT - parsnip_hire
    extra_potato = crop_margin(potato, 24) - EXPANSION_RENT - potato_hire

    bean_season = crop_margin(bean, 1, len(harvest_days(bean, 1, 28)))
    sprinkler_saving_per_day = FULL_SHIFT - SPRINKLER_DAILY
    sprinkler_payback_days = SPRINKLER_PRICE / sprinkler_saving_per_day

    assert normal == 282
    assert weak == 90
    assert busy == 714
    assert potato_week == 600
    assert parsnip_hire == 365
    assert potato_hire == 475
    assert extra_parsnip == -18
    assert extra_potato == 190
    assert bean_season == 120
    assert round(sprinkler_payback_days, 2) == 28.21

    # One formula reproduces all four hand-calibrated launch prices.
    launch_profiles = {
        "parsnip": (4, 0, 1, 1, 0.0, 0),
        "potato": (6, 0, 1, 1, 0.2, 1),
        "green_bean": (10, 3, 1, 1, 0.0, 0),
        "cauliflower": (12, 0, 1, 1, 0.0, 1),
    }
    for name, inputs in launch_profiles.items():
        seed, sell, yield_per_harvest, count = suggest_crop_prices(*inputs)
        actual = CROPS[name]
        assert (seed, sell, count) == (
            actual.seed, actual.sell, len(harvest_days(actual, 1, 28))
        ), (name, seed, sell, count)
        assert abs(yield_per_harvest - actual.expected_yield) < 0.00001

    # A regrowing crop first sold mid-season must use its shorter sales window.
    assert suggest_crop_prices(9, 4, 1, 1, 0.0, 2, first_available_day=15)[3] == 2

    future_examples = {
        "late single-harvest": (8, 0, 1, 1, 0.0, 2),
        "late variable-yield": (7, 0, 1, 2, 0.1, 3),
        "late regrowing": (9, 4, 1, 1, 0.0, 2),
    }

    scenarios = {
        "24 parsnips, day 1-7": normal,
        "24 parsnips, 20% price shock": weak,
        "48 parsnips, day 1-7": busy,
        "24 potatoes, day 1-7, expected yield": potato_week,
        "new 24-cell plot, all NPC shifts, parsnips": extra_parsnip,
        "new 24-cell plot, all NPC shifts, potatoes": extra_potato,
    }
    for name, profit in scenarios.items():
        print(f"{name}: {fmt(profit)} G")
    print(f"Green bean, one plot, 28-day season: {fmt(bean_season)} G gross margin")
    print(f"48-cell sprinkler cash payback vs NPC watering: {fmt(sprinkler_payback_days)} days")
    for name, inputs in future_examples.items():
        seed, sell, yield_per_harvest, count = suggest_crop_prices(*inputs)
        grow_days, regrow_days = inputs[0], inputs[1]
        horizon = 28 if regrow_days else grow_days
        gross_margin_per_day = (count * sell * yield_per_harvest - seed) / horizon
        assert 3.5 <= gross_margin_per_day <= 6.75, name
        print(f"{name}: seed={seed} G, sell={sell} G, "
              f"yield={fmt(yield_per_harvest)}, harvests={count}, "
              f"gross_margin_per_plot_day={fmt(gross_margin_per_day)} G")


if __name__ == "__main__":
    main()
