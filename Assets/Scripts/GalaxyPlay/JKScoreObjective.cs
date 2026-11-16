#if false
using System;

namespace GameSource
{
  public class JKScoreObjective : OrderObjective
  {
    protected override void _Register()
    {
      base._Register();
      this._order.onJKScoreAchieved += new Action<Farmer, int>(this.OnNewValue);
    }

    protected override void _Unregister()
    {
      base._Unregister();
      this._order.onJKScoreAchieved -= new Action<Farmer, int>(this.OnNewValue);
    }

    public virtual void OnNewValue(Farmer who, int new_value) => this.SetCount(Math.Min(Math.Max(new_value, this.currentCount.Value), this.GetMaxCount()));
  }
}
#endif
