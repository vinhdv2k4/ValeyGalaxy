using System;

public enum FarmerIntroMode { ExistingSave, Watch, Skip }

[Serializable]
public sealed class CreatedFarmer
{
    public string name;
    public string farmName;
    public string favoriteThing;
    public int gender;
    public int hair;
    public int skin;
    public int shirt;
    public int pants;
    public int farmType;
    public int pet;
    public int hairColor;
    public int eyeColor;
    public int accessory = -1;
    public int petBreed;
    // 0 means an older profile; otherwise the high byte marks a packed RGB colour.
    public int hairColorRgb;
    public int eyeColorRgb;
    public int pantsColorRgb;
    public FarmerIntroMode introMode;
}

