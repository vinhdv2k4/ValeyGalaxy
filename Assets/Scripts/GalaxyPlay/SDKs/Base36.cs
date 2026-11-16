#if false
using System;

namespace GameSource.SDKs
{
  public class Base36
  {
    private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const ulong Base = 36;

    public static string Encode(ulong value)
    {
      string str = "";
      if (value == 0UL)
        return "0";
      while (value != 0UL)
      {
        int index = (int) (value % 36UL);
        value /= 36UL;
        str = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[index].ToString() + str;
      }
      return str;
    }

    public static ulong Decode(string value)
    {
      value = value.ToUpper();
      ulong num1 = 0;
      foreach (char ch in value)
      {
        ulong num2 = num1 * 36UL;
        int num3 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(ch);
        if (num3 == -1)
          throw new FormatException(value);
        num1 = num2 + (ulong) num3;
      }
      return num1;
    }
  }
}
#endif
