#if false
using Netcode;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameSource
{
  public class NetIntIntArrayDictionary : 
    NetDictionary<int, int[], NetArray<int, NetInt>, SerializableDictionary<int, int[]>, NetIntIntArrayDictionary>
  {
    protected override int ReadKey(BinaryReader reader) => reader.ReadInt32();

    protected override void WriteKey(BinaryWriter writer, int key) => writer.Write(key);

    protected override void setFieldValue(NetArray<int, NetInt> field, int key, int[] value) => field.Set((IList<int>) value);

    protected override int[] getFieldValue(NetArray<int, NetInt> field) => field.ToArray<int>();

    protected override int[] getFieldTargetValue(NetArray<int, NetInt> field) => field.ToArray<int>();
  }
}
#endif
