using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NpcDefinition
{
    public string name;
    public TextAsset schedule;
    public TextAsset dialogue;
    public Sprite portrait;
    public Sprite[] portraits;
    public Sprite[] animationFrames;
}

public sealed class NpcCatalog : ScriptableObject
{
    public List<NpcDefinition> characters = new();
    public Texture2D dialogueFrame;
    public Texture2D dialogueFont;
    public Texture2D cursors;
    public Texture2D buffsIcons;
    public Texture2D toolsAtlas, animationsAtlas;
    public TextAsset animationDescriptions;
    public NpcDefinition marlon;
    public Sprite gilPortrait;
    public Texture2D weaponsAtlas, furnitureAtlas;
    public Texture2D craftablesAtlas;
    public TextAsset furnitureDefinitions;
    public TextAsset adventureGuildStock;
    // Billboard: the town calendar and the Help Wanted board.
    public Texture2D billboard;
    // Joja Community Development form.
    public Texture2D jojaCDForm;
    // Lighting masks, indexed by textureIndex (0 and 3 unused).
    public Texture2D[] lightTextures = new Texture2D[9];
}
