#if false
using System;

namespace GameSource
{
  public class CharacterEventArgs : EventArgs
  {
    private readonly char character;
    private readonly int lParam;

    public CharacterEventArgs(char character, int lParam)
    {
      this.character = character;
      this.lParam = lParam;
    }

    public char Character => this.character;

    public int Param => this.lParam;

    public int RepeatCount => this.lParam & (int) ushort.MaxValue;

    public bool ExtendedKey => (this.lParam & 16777216) > 0;

    public bool AltPressed => (this.lParam & 536870912) > 0;

    public bool PreviousState => (this.lParam & 1073741824) > 0;

    public bool TransitionState => (this.lParam & int.MinValue) > 0;
  }
}
#endif
