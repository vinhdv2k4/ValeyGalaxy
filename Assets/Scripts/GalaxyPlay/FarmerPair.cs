#if false
using System;

namespace GameSource
{
  public struct FarmerPair
  {
    public long Farmer1;
    public long Farmer2;

    public static FarmerPair MakePair(long f1, long f2) => new FarmerPair()
    {
      Farmer1 = Math.Min(f1, f2),
      Farmer2 = Math.Max(f1, f2)
    };

    public bool Contains(long f) => this.Farmer1 == f || this.Farmer2 == f;

    public long GetOther(long f) => this.Farmer1 == f ? this.Farmer2 : this.Farmer1;

    public bool Equals(FarmerPair other) => this.Farmer1 == other.Farmer1 && this.Farmer2 == other.Farmer2;

    public override bool Equals(object obj) => obj is FarmerPair other && this.Equals(other);

    public override int GetHashCode() => this.Farmer1.GetHashCode() ^ this.Farmer2.GetHashCode() << 16;
  }
}
#endif
