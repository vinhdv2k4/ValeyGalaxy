#if false
using System;

namespace GameSource
{
  /// <summary>
  /// When specified, this static field will be remain static for split screen multiplayer.
  /// </summary>
  [AttributeUsage(AttributeTargets.Field)]
  public class NonInstancedStatic : Attribute
  {
  }
}
#endif
