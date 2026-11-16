using UnityEngine;

/// <summary>
/// Light masks as sprite-light cookies. Drawing only
/// uses a mask's alpha: each light draws it in black over the ambient darkness, so the darkness left
/// is darkness * (1 - alpha). Here the alpha becomes a white-to-black cookie, one pixel per world pixel
/// (64 per tile), and the light's colour is the darkness it removes.
/// </summary>
internal static class LightMasks
{
    private static readonly Sprite[] Masks = new Sprite[9];

    /// <summary>The mask for textureIndex (4, the sconce, for unknown types).</summary>
    internal static Sprite For(int type)
    {
        if (type is < 0 or > 8 || type is 0 or 3) type = 4;
        if (Masks[type] != null) return Masks[type];
        Texture2D source = Source(type);
        if (source == null) return null;

        // The imported textures are not readable; copy them through a render texture.
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color32[] pixels = copy.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = pixels[i].a;
            pixels[i] = new Color32(a, a, a, a);
        }
        copy.SetPixels32(pixels);
        copy.filterMode = FilterMode.Bilinear;
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.Apply(false, true);
        Masks[type] = Sprite.Create(copy, new Rect(0, 0, copy.width, copy.height), new Vector2(0.5f, 0.5f), 64f);
        Masks[type].name = "LightMask_" + type;
        return Masks[type];
    }

    private static Texture2D Source(int type)
    {
        var textures = Resources.Load<NpcCatalog>("NpcCatalog")?.lightTextures;
        if (textures != null && type < textures.Length && textures[type] != null) return textures[type];
#if UNITY_EDITOR
        string name = type switch
        {
            1 => "lantern", 2 => "windowLight", 5 => "greenLight", 6 => "indoorWindowLight",
            7 => "projectorLight", 8 => "fishTankLight", _ => "sconceLight"
        };
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/Lighting/" + name + ".png");
#else
        return null;
#endif
    }
}
