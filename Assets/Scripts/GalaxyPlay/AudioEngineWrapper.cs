#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  internal class AudioEngineWrapper : IAudioEngine, IDisposable
  {
    private AudioEngine audioEngine;

    public AudioEngineWrapper(AudioEngine engine) => this.audioEngine = engine;

    public bool IsDisposed => this.audioEngine.IsDisposed;

    public void Dispose() => this.audioEngine.Dispose();

    public IAudioCategory GetCategory(string name) => (IAudioCategory) new AudioCategoryWrapper(this.audioEngine.GetCategory(name));

    public int GetCategoryIndex(string name) => this.audioEngine.GetCategoryIndex(name);

    public void Update() => this.audioEngine.Update();

    public AudioEngine Engine => this.audioEngine;
  }
}
#endif
