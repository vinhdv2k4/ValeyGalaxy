#if false
using Microsoft.Xna.Framework.Input;

namespace GameSource
{
  public interface IKeyboardSubscriber
  {
    void RecieveTextInput(char inputChar);

    void RecieveTextInput(string text);

    void RecieveCommandInput(char command);

    void RecieveSpecialInput(Keys key);

    bool Selected { get; set; }
  }
}
#endif
