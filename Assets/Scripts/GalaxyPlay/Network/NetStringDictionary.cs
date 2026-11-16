#if false
using Netcode;
using System.Collections.Generic;
using System.IO;

namespace GameSource.Network
{
  public class NetStringDictionary<T, TField> : 
    NetFieldDictionary<string, T, TField, SerializableDictionary<string, T>, NetStringDictionary<T, TField>>
    where TField : NetField<T, TField>, new()
  {
    public NetStringDictionary()
    {
    }

    public NetStringDictionary(IEnumerable<KeyValuePair<string, T>> dict)
      : base(dict)
    {
    }

    protected override string ReadKey(BinaryReader reader) => reader.ReadString();

    protected override void WriteKey(BinaryWriter writer, string key) => writer.Write(key);
  }
}
#endif
