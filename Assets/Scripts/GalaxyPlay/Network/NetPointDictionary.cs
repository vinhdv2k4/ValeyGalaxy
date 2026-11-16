#if false
using Microsoft.Xna.Framework;
using Netcode;
using System.Collections.Generic;
using System.IO;

namespace GameSource.Network
{
  public class NetPointDictionary<T, TField> : 
    NetFieldDictionary<Point, T, TField, SerializableDictionary<Point, T>, NetPointDictionary<T, TField>>
    where TField : NetField<T, TField>, new()
  {
    public NetPointDictionary()
    {
    }

    public NetPointDictionary(IEnumerable<KeyValuePair<Point, T>> dict)
      : base(dict)
    {
    }

    protected override Point ReadKey(BinaryReader reader) => new Point(reader.ReadInt32(), reader.ReadInt32());

    protected override void WriteKey(BinaryWriter writer, Point key)
    {
      writer.Write(key.X);
      writer.Write(key.Y);
    }
  }
}
#endif
