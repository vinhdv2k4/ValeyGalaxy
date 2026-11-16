#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  internal class DummyAudioEngine : IAudioEngine, IDisposable
  {
    private IAudioCategory category = (IAudioCategory) new DummyAudioCategory();

    public void Update()
    {
    }

    public bool IsDisposed => true;

    public IAudioCategory GetCategory(string name) => this.category;

    public int GetCategoryIndex(string name) => -1;

    public void Dispose()
    {
    }

    public AudioEngine Engine => (AudioEngine) null;
  }
}
#endif
