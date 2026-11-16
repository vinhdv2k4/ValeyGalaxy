#if false
using Microsoft.Xna.Framework;
using System.Xml;
using System.Xml.Serialization;

namespace GameSource
{
  public class Vector2Writer : XmlSerializationWriter
  {
    public void WriteVector2(Vector2 vec)
    {
      XmlWriter writer = this.Writer;
      writer.WriteStartElement("Vector2");
      writer.WriteStartElement("X");
      writer.WriteValue(vec.X);
      writer.WriteEndElement();
      writer.WriteStartElement("Y");
      writer.WriteValue(vec.Y);
      writer.WriteEndElement();
      writer.WriteEndElement();
    }

    protected override void InitCallbacks()
    {
    }
  }
}
#endif
