#if false
using Netcode;
using System.Xml;

namespace GameSource.Characters
{
  public class BotchedNetInt : BotchedNetField<int, NetInt>
  {
    public BotchedNetInt() => this.netField = new NetInt();

    public BotchedNetInt(int default_value) => this.netField = new NetInt(default_value);

    protected override object _ParseValue(XmlReader reader) => (object) int.Parse(reader.Value);
  }
}
#endif
