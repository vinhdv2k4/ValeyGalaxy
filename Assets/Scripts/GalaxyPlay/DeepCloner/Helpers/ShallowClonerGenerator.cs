#if false
using System;

namespace Force.DeepCloner.Helpers
{
  internal static class ShallowClonerGenerator
  {
    public static T CloneObject<T>(T obj)
    {
      if ((object) obj is ValueType)
        return typeof (T) == obj.GetType() ? obj : (T) ShallowObjectCloner.CloneObject((object) obj);
      if ((object) obj == null)
        return (T) null;
      return DeepClonerSafeTypes.CanReturnSameObject(obj.GetType()) ? obj : (T) ShallowObjectCloner.CloneObject((object) obj);
    }
  }
}
#endif
