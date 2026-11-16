#if false
using Netcode;
using System.Xml;

namespace GameSource.Characters
{
  public class BotchedNetLong : BotchedNetField<long, NetLong>
  {
    public BotchedNetLong() => this.netField = new NetLong();

    public BotchedNetLong(long default_value) => this.netField = new NetLong(default_value);

    protected override object _ParseValue(XmlReader reader) => (object) long.Parse(reader.Value);
  }
}
#endif
