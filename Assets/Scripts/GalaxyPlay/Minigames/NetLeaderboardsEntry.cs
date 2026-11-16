#if false
using Netcode;

namespace GameSource.Minigames
{
  public class NetLeaderboardsEntry : INetObject<NetFields>
  {
    public readonly NetString name = new NetString("");
    public readonly NetInt score = new NetInt(0);

    public NetFields NetFields { get; } = new NetFields();

    public void InitNetFields() => this.NetFields.AddFields((INetSerializable) this.name, (INetSerializable) this.score);

    public NetLeaderboardsEntry() => this.InitNetFields();

    public NetLeaderboardsEntry(string new_name, int new_score)
    {
      this.InitNetFields();
      this.name.Value = new_name;
      this.score.Value = new_score;
    }
  }
}
#endif
