#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  public class SoundBankWrapper : ISoundBank, IDisposable
  {
    private SoundBank soundBank;

    public bool IsInUse => this.soundBank.IsInUse;

    public SoundBankWrapper(SoundBank soundBank) => this.soundBank = soundBank;

    public ICue GetCue(string name) => (ICue) new CueWrapper(this.soundBank.GetCue(name));

    public void PlayCue(string name) => this.soundBank.PlayCue(name);

    public void PlayCue(string name, AudioListener listener, AudioEmitter emitter) => this.soundBank.PlayCue(name, listener, emitter);

    public bool IsDisposed => this.soundBank.IsDisposed;

    public void Dispose() => this.soundBank.Dispose();

    public void AddCue(CueDefinition cue_definition) => this.soundBank.AddCue(cue_definition);

    public CueDefinition GetCueDefinition(string name) => this.soundBank.GetCueDefinition(name);
  }
}
#endif
