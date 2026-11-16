#if false
using Netcode;
using GameSource.Menus;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameSource.Network
{
  public class NetBundles : 
    NetDictionary<int, bool[], NetArray<bool, NetBool>, SerializableDictionary<int, bool[]>, NetBundles>
  {
    protected override int ReadKey(BinaryReader reader)
    {
      int num = reader.ReadInt32();
      if (!(Game1.activeClickableMenu is JunimoNoteMenu))
        return num;
      (Game1.activeClickableMenu as JunimoNoteMenu).bundlesChanged = true;
      return num;
    }

    protected override void WriteKey(BinaryWriter writer, int key) => writer.Write(key);

    protected override void setFieldValue(NetArray<bool, NetBool> field, int key, bool[] value) => field.Set((IList<bool>) value);

    protected override bool[] getFieldValue(NetArray<bool, NetBool> field) => field.ToArray<bool>();

    protected override bool[] getFieldTargetValue(NetArray<bool, NetBool> field) => field.ToArray<bool>();
  }
}
#endif
