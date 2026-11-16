using System;
using System.Collections.Generic;
using UnityEngine;

internal static class NpcSprites
{
    private const int CharacterWidth = 16;
    private const int CharacterHeight = 32;
    private const int PortraitSize = 64;
    private const float WorldPixelsPerUnit = 16f;
    internal static readonly Vector3 FeetOffset = Vector3.down * 0.25f;

    private static readonly Dictionary<Texture2D, Sprite[]> CharacterSheets = new();
    private static readonly Dictionary<(Texture2D Texture, int Index), Sprite> Portraits = new();
    private static readonly Dictionary<Sprite, Sprite> WorldSprites = new();

    internal static Sprite[] CharacterFrames(Sprite[] importedFrames)
    {
        if (importedFrames == null || importedFrames.Length == 0 || importedFrames[0] == null)
            return Array.Empty<Sprite>();
        Texture2D sheet = importedFrames[0].texture;
        if (sheet == null || sheet.width < CharacterWidth || sheet.height < CharacterHeight)
            return importedFrames;
        if (CharacterSheets.TryGetValue(sheet, out var cached)) return cached;

        // AnimatedSprite counts every cell, including blank and transparently padded frames.
        int columns = sheet.width / CharacterWidth;
        int rows = sheet.height / CharacterHeight;
        var frames = new Sprite[columns * rows];
        for (int index = 0; index < frames.Length; index++)
        {
            var rect = Cell(sheet, index, columns, CharacterWidth, CharacterHeight);
            frames[index] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0f), WorldPixelsPerUnit);
            frames[index].name = $"{sheet.name}_{index}";
        }
        CharacterSheets.Add(sheet, frames);
        return frames;
    }

    internal static Sprite CharacterIdle(Sprite[] importedFrames)
    {
        var frames = CharacterFrames(importedFrames);
        return frames.Length > 0 ? frames[0] : null;
    }

    internal static Sprite PortraitFrame(Sprite source, int index)
    {
        if (source == null) return null;
        Texture2D sheet = source.texture;
        if (sheet == null || sheet.width < PortraitSize || sheet.height < PortraitSize) return source;
        int columns = sheet.width / PortraitSize;
        index = Mathf.Clamp(index, 0, columns * (sheet.height / PortraitSize) - 1);
        var key = (sheet, index);
        if (Portraits.TryGetValue(key, out var frame)) return frame;

        frame = Sprite.Create(sheet, Cell(sheet, index, columns, PortraitSize, PortraitSize),
            new Vector2(0.5f, 0.5f), PortraitSize, 0, SpriteMeshType.FullRect);
        Portraits.Add(key, frame);
        return frame;
    }

    internal static Sprite AtWorldScale(Sprite source)
    {
        if (source == null || Mathf.Approximately(source.pixelsPerUnit, WorldPixelsPerUnit)) return source;
        if (WorldSprites.TryGetValue(source, out var sprite)) return sprite;
        Rect rect = source.rect;
        var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        sprite = Sprite.Create(source.texture, rect, pivot, WorldPixelsPerUnit);
        sprite.name = source.name;
        WorldSprites.Add(source, sprite);
        return sprite;
    }

    private static Rect Cell(Texture2D sheet, int index, int columns, int width, int height) =>
        new(index % columns * width, sheet.height - (index / columns + 1) * height, width, height);
}
