#if false
using Microsoft.Xna.Framework;
using System.Xml;
using System.Xml.Serialization;

namespace GameSource
{
  public class Vector2Serializer : XmlSerializer
  {
    private Vector2Reader _reader = new Vector2Reader();
    private Vector2Writer _writer = new Vector2Writer();

    protected override XmlSerializationReader CreateReader() => (XmlSerializationReader) this._reader;

    protected override XmlSerializationWriter CreateWriter() => (XmlSerializationWriter) this._writer;

    public override bool CanDeserialize(XmlReader xmlReader) => xmlReader.IsStartElement("Vector2");

    protected override void Serialize(object o, XmlSerializationWriter writer) => this._writer.WriteVector2((Vector2) o);

    protected override object Deserialize(XmlSerializationReader reader) => (object) this._reader.ReadVector2();
  }
}
#endif
