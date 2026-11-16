#if false
using Microsoft.Xna.Framework;
using Netcode;
using GameSource.Tools;
using System;

namespace GameSource.Objects
{
  public class ObjectFactory
  {
    public const byte regularObject = 0;
    public const byte bigCraftable = 1;
    public const byte weapon = 2;
    public const byte specialItem = 3;
    public const byte regularObjectRecipe = 4;
    public const byte bigCraftableRecipe = 5;

    public static ItemDescription getDescriptionFromItem(Item i)
    {
      switch (i)
      {
        case GameSource.Object _ when (bool) (NetFieldBase<bool, NetBool>) (i as GameSource.Object).bigCraftable:
          return new ItemDescription((byte) 1, (i as GameSource.Object).ParentSheetIndex, i.Stack);
        case GameSource.Object _:
          return new ItemDescription((byte) 0, (i as GameSource.Object).ParentSheetIndex, i.Stack);
        case MeleeWeapon _:
          return new ItemDescription((byte) 2, (i as MeleeWeapon).CurrentParentTileIndex, i.Stack);
        default:
          throw new Exception("ItemFactory trying to create item description from unknown item");
      }
    }

    public static Item getItemFromDescription(byte type, int index, int stack)
    {
      switch (type)
      {
        case 0:
          return (Item) new GameSource.Object(Vector2.Zero, index, stack);
        case 1:
          return (Item) new GameSource.Object(Vector2.Zero, index);
        case 2:
          return (Item) new MeleeWeapon(index);
        case 4:
          return (Item) new GameSource.Object(index, stack, true);
        case 5:
          return (Item) new GameSource.Object(Vector2.Zero, index, true);
        default:
          throw new Exception("ItemFactory trying to create item from unknown description");
      }
    }
  }
}
#endif
