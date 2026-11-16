#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  public interface IAudioEngine : IDisposable
  {
    void Update();

    bool IsDisposed { get; }

    IAudioCategory GetCategory(string name);

    int GetCategoryIndex(string name);

    AudioEngine Engine { get; }
  }
}
#endif
