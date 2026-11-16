#if false
using Netcode;
using System;
using System.Collections.Generic;

namespace GameSource.Minigames
{
  public class NetLeaderboards : INetObject<NetFields>
  {
    public NetObjectList<NetLeaderboardsEntry> entries = new NetObjectList<NetLeaderboardsEntry>();
    public NetInt maxEntries = new NetInt(10);

    public NetFields NetFields { get; } = new NetFields();

    public void InitNetFields() => this.NetFields.AddFields((INetSerializable) this.entries, (INetSerializable) this.maxEntries);

    public NetLeaderboards() => this.InitNetFields();

    public void AddScore(string name, int score)
    {
      List<NetLeaderboardsEntry> list = new List<NetLeaderboardsEntry>((IEnumerable<NetLeaderboardsEntry>) this.entries);
      list.Add(new NetLeaderboardsEntry(name, score));
      list.Sort((Comparison<NetLeaderboardsEntry>) ((a, b) => a.score.Value.CompareTo(b.score.Value)));
      list.Reverse();
      while (list.Count > this.maxEntries.Value)
        list.RemoveAt(list.Count - 1);
      this.entries.Set((IList<NetLeaderboardsEntry>) list);
    }

    public List<KeyValuePair<string, int>> GetScores()
    {
      List<KeyValuePair<string, int>> scores = new List<KeyValuePair<string, int>>();
      foreach (NetLeaderboardsEntry entry in (NetList<NetLeaderboardsEntry, NetRef<NetLeaderboardsEntry>>) this.entries)
        scores.Add(new KeyValuePair<string, int>(entry.name.Value, entry.score.Value));
      scores.Sort((Comparison<KeyValuePair<string, int>>) ((a, b) => a.Value.CompareTo(b.Value)));
      scores.Reverse();
      return scores;
    }

    public void LoadScores(List<KeyValuePair<string, int>> scores)
    {
      this.entries.Clear();
      foreach (KeyValuePair<string, int> score in scores)
        this.AddScore(score.Key, score.Value);
    }
  }
}
#endif
