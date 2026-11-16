#if false
using Microsoft.Xna.Framework.Input;
using System;

namespace GameSource
{
  public class KeyEventArgs : EventArgs
  {
    private Keys keyCode;

    public KeyEventArgs(Keys keyCode) => this.keyCode = keyCode;

    public Keys KeyCode => this.keyCode;
  }
}
#endif
