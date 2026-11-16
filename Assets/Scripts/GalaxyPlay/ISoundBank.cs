#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  public interface ISoundBank : IDisposable
  {
    bool IsInUse { get; }

    ICue GetCue(string name);

    void PlayCue(string name);

    void PlayCue(string name, AudioListener listener, AudioEmitter emitter);

    void AddCue(CueDefinition cue_definition);

    CueDefinition GetCueDefinition(string name);

    bool IsDisposed { get; }
  }
}
#endif
