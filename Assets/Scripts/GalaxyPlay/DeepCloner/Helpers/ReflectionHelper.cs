#if false
using System;
using System.Linq;
using System.Reflection;

namespace Force.DeepCloner.Helpers
{
  internal static class ReflectionHelper
  {
    public static bool IsEnum(this Type t) => t.GetTypeInfo().IsEnum;

    public static bool IsValueType(this Type t) => t.GetTypeInfo().IsValueType;

    public static bool IsClass(this Type t) => t.GetTypeInfo().IsClass;

    public static Type BaseType(this Type t) => t.GetTypeInfo().BaseType;

    public static FieldInfo[] GetAllFields(this Type t) => t.GetTypeInfo().DeclaredFields.Where<FieldInfo>((Func<FieldInfo, bool>) (x => !x.IsStatic)).ToArray<FieldInfo>();

    public static PropertyInfo[] GetPublicProperties(this Type t) => t.GetTypeInfo().DeclaredProperties.ToArray<PropertyInfo>();

    public static FieldInfo[] GetDeclaredFields(this Type t) => t.GetTypeInfo().DeclaredFields.Where<FieldInfo>((Func<FieldInfo, bool>) (x => !x.IsStatic)).ToArray<FieldInfo>();

    public static ConstructorInfo[] GetPrivateConstructors(this Type t) => t.GetTypeInfo().DeclaredConstructors.ToArray<ConstructorInfo>();

    public static ConstructorInfo[] GetPublicConstructors(this Type t) => t.GetTypeInfo().DeclaredConstructors.ToArray<ConstructorInfo>();

    public static MethodInfo GetPrivateMethod(this Type t, string methodName) => t.GetTypeInfo().GetDeclaredMethod(methodName);

    public static MethodInfo GetMethod(this Type t, string methodName) => t.GetTypeInfo().GetDeclaredMethod(methodName);

    public static MethodInfo GetPrivateStaticMethod(this Type t, string methodName) => t.GetTypeInfo().GetDeclaredMethod(methodName);

    public static FieldInfo GetPrivateField(this Type t, string fieldName) => t.GetTypeInfo().GetDeclaredField(fieldName);

    public static bool IsSubclassOfTypeByName(this Type t, string typeName)
    {
      for (; t != (Type) null; t = t.BaseType())
      {
        if (t.Name == typeName)
          return true;
      }
      return false;
    }

    public static bool IsAssignableFrom(this Type from, Type to) => from.GetTypeInfo().IsAssignableFrom(to.GetTypeInfo());

    public static bool IsInstanceOfType(this Type from, object to) => from.IsAssignableFrom(to.GetType());

    public static Type[] GenericArguments(this Type t) => t.GetTypeInfo().GenericTypeArguments;
  }
}
#endif
