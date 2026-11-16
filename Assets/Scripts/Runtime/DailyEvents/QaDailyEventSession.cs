#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DailyEvents;
using UnityEngine;

internal sealed class QaDailyEventSession : IDailyEventSession, IDisposable
{
    private readonly DailyEventCalendar calendar = DailyEventAssets.Calendar;
    private readonly Dictionary<string, int> stamps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemData> sampleItems = new(StringComparer.Ordinal);
    private readonly FarmDate date;

    public OnlineGameSnapshot Snapshot { get; }
    internal DailyEventDefinition Selected { get; }

    internal QaDailyEventSession(int day, DailyEventDefinition selected)
    {
        date = new FarmDate(day);
        Selected = selected;
        Snapshot = new OnlineGameSnapshot
        {
            gameDay = day,
            dailyEvents = new OnlineDailyEventStatus { secretRecipient = "Abigail", secretGiver = "Lewis", fairScore = 75 }
        };
        if (selected != null)
        {
            for (var next = selected; !string.IsNullOrEmpty(next.requires);)
            {
                var prerequisite = calendar.Find(next.requires);
                stamps[prerequisite.Key(date)] = Math.Max(1, day - next.daysAfter);
                next = prerequisite;
            }
        }
        UpdateSnapshot();
    }

    public void Act(StartupApiClient.DailyEventAction action, Action<string> done)
    {
        var entry = calendar.Find(action.eventId);
        if (entry == null || action.day != date.Day)
        {
            done("Mục QA không thuộc ngày đang xem.");
            return;
        }
        string key = entry.Key(date);
        switch (action.action)
        {
            case "read":
            case "meet":
            case "activity":
                break;
            case "visit": key += "/visit/" + date.Day; break;
            case "find": key += "/found"; break;
            case "deliver":
                if (entry.HasLostItem && !stamps.ContainsKey(key + "/found"))
                {
                    done("Tương tác với vật phẩm trên bản đồ trước khi xem bước trả.");
                    return;
                }
                key += "/delivered";
                break;
            case "forage": key += $"/{date.Day}/{action.location}/{action.x}/{action.y}"; break;
            case "coffee":
            case "painting": key += $"/{action.action}/{date.Day}"; break;
            case "pearl": key = "night_market/pearl"; break;
            case "strawberry":
            case "brew":
                done(null);
                return;
            default:
                done("Thao tác này chưa có dữ liệu mẫu.");
                return;
        }
        stamps[key] = date.Day;
        UpdateSnapshot();
        done(null);
    }

    private void UpdateSnapshot()
    {
        Snapshot.version++;
        Snapshot.dailyEvents.completed = stamps.Select(pair => new OnlineDailyEventStamp
            { key = pair.Key, day = pair.Value }).ToArray();
    }

    public ItemData ResolveItem(string id)
    {
        if (sampleItems.TryGetValue(id, out var item)) return item;
        if (!id.StartsWith("object_", StringComparison.Ordinal) || !int.TryParse(id.Substring(7), out int index))
            return null;
        item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = id;
        item.icon = ItemData.CreateAtlasSprite(Resources.Load<Texture2D>("Maps/springobjects"), index);
        sampleItems.Add(id, item);
        return item;
    }

    public void Dispose()
    {
        foreach (var item in sampleItems.Values)
        {
            UnityEngine.Object.Destroy(item);
        }
        sampleItems.Clear();
    }
}
#endif
