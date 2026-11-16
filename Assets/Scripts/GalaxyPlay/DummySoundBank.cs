#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  public class DummySoundBank : ISoundBank, IDisposable
  {
    private ICue dummyCue = (ICue) new DummyCue();

    public bool IsInUse => false;

    public bool IsDisposed => true;

    public void Dispose()
    {
    }

    public ICue GetCue(string name) => this.dummyCue;

    public void PlayCue(string name)
    {
    }

    public void PlayCue(string name, AudioListener listener, AudioEmitter emitter)
    {
    }

    public void AddCue(CueDefinition cue_definition)
    {
    }

    public CueDefinition GetCueDefinition(string cue_name) => (CueDefinition) null;
  }
}
#endif
