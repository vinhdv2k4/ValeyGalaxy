#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmCutscenes
{
    public abstract class EventConditionBase : IEventCondition
    {
        public abstract bool IsMet(EventEvaluationContext context);
    }

    public sealed class TimeCondition : EventConditionBase
    {
        private readonly int min, max;
        public TimeCondition(int min, int max)
        {
            if (min < 0 || max <= min || max > 2700) throw new ArgumentException("Invalid time range.");
            this.min = min;
            this.max = max;
        }
        public override bool IsMet(EventEvaluationContext context) => context.Time >= min && context.Time < max;
    }

    public sealed class LocationCondition : EventConditionBase
    {
        private readonly string location;
        public LocationCondition(string location) => this.location = location;
        public override bool IsMet(EventEvaluationContext context) => context.Location == location;
    }

    public sealed class WeatherCondition : EventConditionBase
    {
        private readonly Weather weather;
        public WeatherCondition(Weather weather) => this.weather = weather;
        public override bool IsMet(EventEvaluationContext context) => context.Weather == weather;
    }

    public sealed class FriendshipCondition : EventConditionBase
    {
        private readonly string npc;
        private readonly int points;
        public FriendshipCondition(string npc, int hearts)
        {
            if (hearts < 0 || hearts > 14) throw new ArgumentOutOfRangeException(nameof(hearts));
            this.npc = npc;
            points = hearts * 250;
        }
        public override bool IsMet(EventEvaluationContext context) =>
            context.Friendship != null && context.Friendship.TryGetValue(npc, out int current) && current >= points;
    }

    public sealed class FlagCondition : EventConditionBase
    {
        private readonly string id;
        private readonly bool mail;
        public FlagCondition(string id, bool mail) { this.id = id; this.mail = mail; }
        public override bool IsMet(EventEvaluationContext context) =>
            context.Progress != null && (mail ? context.Progress.HasMail(id) : context.Progress.HasSeen(id));
    }

    public sealed class CompositeCondition : EventConditionBase
    {
        public enum Operator { And, Or, Not }
        private readonly Operator operation;
        private readonly IEventCondition[] children;

        public CompositeCondition(Operator operation, IEventCondition[] children)
        {
            if (operation == Operator.Not && children.Length != 1)
                throw new ArgumentException("NOT requires exactly one condition.");
            this.operation = operation;
            this.children = (IEventCondition[])children.Clone();
        }
        public override bool IsMet(EventEvaluationContext context)
        {
            if (operation == Operator.Not) return !children[0].IsMet(context);
            bool isAnd = operation == Operator.And;
            foreach (var child in children)
                if (child.IsMet(context) != isAnd) return !isAnd;
            return isAnd;
        }
    }

    internal sealed class ProgressCondition : EventConditionBase
    {
        private readonly string metric;
        private readonly int minimum;
        public ProgressCondition(string metric, int minimum)
        {
            if (metric is not ("earnings" or "rooms" or "day" or "copperMorning") || minimum < 0)
                throw new ArgumentException("Invalid progress condition " + metric);
            this.metric = metric;
            this.minimum = minimum;
        }

        public override bool IsMet(EventEvaluationContext context) => metric switch
        {
            "earnings" => context.Earnings >= minimum,
            "rooms" => context.CompletedRooms >= minimum,
            "day" => context.Day >= minimum,
            "copperMorning" => context.CopperDay > 0 && context.Day - context.CopperDay >= minimum,
            _ => false
        };
    }

    public sealed class EventConditionFactory
    {
        private readonly Dictionary<string, Func<EventNode, IEventCondition>> builders = new(StringComparer.Ordinal);

        public EventConditionFactory()
        {
            Register("time", node => new TimeCondition(node.min, node.max));
            Register("location", node => new LocationCondition(node.value));
            Register("weather", node => new WeatherCondition(Enum.Parse<Weather>(node.value, true)));
            Register("friendship", node => new FriendshipCondition(node.actor, node.min));
            Register("seen", node => new FlagCondition(node.value, false));
            Register("mail", node => new FlagCondition(node.value, true));
            Register("progress", node => new ProgressCondition(node.value, node.min));
            foreach (CompositeCondition.Operator operation in Enum.GetValues(typeof(CompositeCondition.Operator)))
                Register(operation.ToString().ToLowerInvariant(),
                    node => new CompositeCondition(operation, node.children.Select(Create).ToArray()));
        }

        public void Register(string type, Func<EventNode, IEventCondition> builder) => builders.Add(type, builder);
        public IEventCondition Create(EventNode node) => builders.TryGetValue(node.type, out var build)
            ? build(node) : throw new ArgumentException("Unknown event condition " + node.type);
    }

    public sealed class EventConditionEvaluator
    {
        public EventDefinition? FirstMatch(IReadOnlyList<EventDefinition> events, EventEvaluationContext context)
        {
            for (int i = 0; i < events.Count; i++)
            {
                EventDefinition entry = events[i];
                if (!context.Progress.HasSeen(entry.Id) && entry.Condition.IsMet(context)) return entry;
            }
            return null;
        }
    }
}
