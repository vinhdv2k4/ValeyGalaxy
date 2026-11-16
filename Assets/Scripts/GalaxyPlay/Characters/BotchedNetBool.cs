#if false
using Netcode;
using System.Xml;

namespace GameSource.Characters
{
  public class BotchedNetBool : BotchedNetField<bool, NetBool>
  {
    public BotchedNetBool() => this.netField = new NetBool();

    public BotchedNetBool(bool default_value) => this.netField = new NetBool(default_value);

    protected override object _ParseValue(XmlReader reader) => (object) bool.Parse(reader.Value);
  }
}
#endif
