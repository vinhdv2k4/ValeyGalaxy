#if false
using Microsoft.Xna.Framework.Audio;

namespace GameSource
{
  public class AudioCategoryWrapper : IAudioCategory
  {
    private AudioCategory audioCategory;

    public AudioCategoryWrapper(AudioCategory category) => this.audioCategory = category;

    public void SetVolume(float volume) => this.audioCategory.SetVolume(volume);
  }
}
#endif
