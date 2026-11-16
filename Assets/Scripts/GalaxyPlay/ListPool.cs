#if false
using System.Collections.Generic;

namespace GameSource
{
  /// <summary>
  /// JCF: I added this type from my own codebase.
  /// 
  ///      This is NOT threadsafe. If more than one thread could potentially call Get/Return
  ///      then put a lock around those calls (just lock around Get/Return, you do not need to
  ///      lock around whatever work you do).
  /// 
  ///      Protip: Use a finally block to ensure the list gets returned even if an exception occurs
  ///              and is handled somewhere higher in the callstack.
  /// </summary>
  public class ListPool<T>
  {
    private readonly Stack<List<T>> _in;

    public ListPool()
    {
      this._in = new Stack<List<T>>();
      this._in.Push(new List<T>());
    }

    public List<T> Get()
    {
      if (this._in.Count == 0)
        this._in.Push(new List<T>());
      return this._in.Pop();
    }

    public void Return(List<T> list)
    {
      list.Clear();
      this._in.Push(list);
    }
  }
}
#endif
