using UnityEngine;

/// <summary>
/// Item quality travels in the item id ("object_176@2" is a gold-star egg). 1 = silver,
/// 2 = gold, 4 = iridium; plain ids are normal quality.
/// </summary>
internal static class ItemQuality
{
    private static readonly Rect Silver = new(338, 400, 8, 8);
    private static readonly Rect Gold = new(346, 400, 8, 8);
    private static readonly Rect Iridium = new(346, 392, 8, 8);

    internal static int Of(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        int split = itemId.LastIndexOf('@');
        return split > 0 && int.TryParse(itemId.Substring(split + 1), out int quality) &&
               quality is 1 or 2 or 4 ? quality : 0;
    }

    internal static string BaseId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return itemId;
        int split = itemId.LastIndexOf('@');
        return split > 0 ? itemId.Substring(0, split) : itemId;
    }

    /// <summary>Source rectangle of the star in Cursors, or null for normal quality.</summary>
    internal static Rect? StarSource(string itemId) => Of(itemId) switch
    {
        1 => Silver, 2 => Gold, 4 => Iridium, _ => null
    };

    /// <summary>Draws the quality star in the bottom-left corner of an item slot (IMGUI).</summary>
    internal static void DrawStar(Rect slot, string itemId)
    {
        Rect? source = StarSource(itemId);
        if (source == null || Event.current.type != EventType.Repaint) return;
        float size = Mathf.Max(12f, slot.width * 0.375f);
        StardewUiAtlas.DrawCursorsSprite(new Rect(slot.x + 2f, slot.yMax - size - 2f, size, size), source.Value);
    }
}
