#if false
using Netcode;
using GameSource.Network;

namespace GameSource
{
  public class ModDataDictionary : NetStringDictionary<string, NetString>
  {
    public ModDataDictionary() => this.InterpolationWait = false;

    public virtual void SetFromSerialization(ModDataDictionary source)
    {
      this.Clear();
      if (source == null)
        return;
      foreach (string key in source.Keys)
        this[key] = source[key];
    }

    public ModDataDictionary GetForSerialization() => Game1.game1 != null && Game1.game1.IsSaving && this.Count() == 0 ? (ModDataDictionary) null : this;
  }
}
#endif
