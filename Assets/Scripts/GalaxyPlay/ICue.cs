#if false
using Microsoft.Xna.Framework.Audio;
using System;

namespace GameSource
{
  public interface ICue : IDisposable
  {
    void Play();

    void Pause();

    void Resume();

    void Stop(AudioStopOptions options);

    void SetVariable(string var, int val);

    void SetVariable(string var, float val);

    float GetVariable(string var);

    bool IsStopped { get; }

    bool IsStopping { get; }

    bool IsPlaying { get; }

    bool IsPaused { get; }

    string Name { get; }

    float Pitch { get; set; }

    float Volume { get; set; }

    bool IsPitchBeingControlledByRPC { get; }
  }
}
#endif
