using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmCutscenes
{
    public abstract class EventCommandBase : IEventCommand
    {
        public abstract Task ExecuteAsync(EventExecutionContext context, CancellationToken ct);
    }

    public sealed class SequentialCommandGroup : EventCommandBase
    {
        private readonly IEventCommand[] commands;
        public SequentialCommandGroup(IEnumerable<IEventCommand> commands) => this.commands = commands.ToArray();
        public override async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            foreach (var command in commands)
            {
                ct.ThrowIfCancellationRequested();
                await command.ExecuteAsync(context, ct);
            }
        }
    }

    public sealed class ParallelCommandGroup : EventCommandBase
    {
        private readonly IEventCommand[] commands;
        public ParallelCommandGroup(IEnumerable<IEventCommand> commands) => this.commands = commands.ToArray();
        public override async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            using var siblings = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tasks = new Task[commands.Length];
            for (int i = 0; i < commands.Length; i++) tasks[i] = Run(commands[i]);
            await Task.WhenAll(tasks);

            async Task Run(IEventCommand command)
            {
                try { await command.ExecuteAsync(context, siblings.Token); }
                catch { siblings.Cancel(); throw; }
            }
        }
    }

    public sealed class MoveActorCommand : EventCommandBase
    {
        private readonly string actor;
        private readonly TilePoint tile;
        private readonly float speed;
        public MoveActorCommand(string actor, TilePoint tile, float speed)
        {
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            this.actor = actor;
            this.tile = tile;
            this.speed = speed;
        }
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct) =>
            context.Actor(actor).MoveToAsync(tile, speed, ct);
    }

    public sealed class FaceDirectionCommand : EventCommandBase
    {
        private readonly string actor;
        private readonly Direction direction;
        public FaceDirectionCommand(string actor, Direction direction) { this.actor = actor; this.direction = direction; }
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            context.Actor(actor).FaceDirection(direction);
            return Task.CompletedTask;
        }
    }

    public sealed class SpeakCommand : EventCommandBase
    {
        private readonly string actor, text;
        private readonly int portrait;
        public SpeakCommand(string actor, string text, int portrait)
        { this.actor = actor; this.text = text; this.portrait = portrait; }
        public override async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            using (context.PauseForDialogue())
                await context.DialogueService.SpeakAsync(actor, text, portrait, ct);
        }
    }

    public sealed class EmoteCommand : EventCommandBase
    {
        private readonly string actor;
        private readonly EmoteType emote;
        private readonly float seconds;
        public EmoteCommand(string actor, EmoteType emote, float seconds)
        { this.actor = actor; this.emote = emote; this.seconds = seconds; }
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct) =>
            context.Actor(actor).ShowEmoteAsync(emote, seconds, ct);
    }

    public sealed class FadeScreenCommand : EventCommandBase
    {
        private readonly bool toBlack;
        private readonly float seconds;
        public FadeScreenCommand(bool toBlack, float seconds) { this.toBlack = toBlack; this.seconds = seconds; }
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct) =>
            context.CameraService.FadeAsync(toBlack, seconds, ct);
    }

    public sealed class PlaySoundCommand : EventCommandBase
    {
        private readonly string cue;
        public PlaySoundCommand(string cue) => this.cue = cue;
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            context.AudioService.PlaySound(cue);
            return Task.CompletedTask;
        }
    }

    public sealed class PlayMusicCommand : EventCommandBase
    {
        private readonly string cue;
        public PlayMusicCommand(string cue) => this.cue = cue;
        public override Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            context.AudioService.PlayMusic(cue);
            return Task.CompletedTask;
        }
    }

    public sealed class QuestionBranchCommand : EventCommandBase
    {
        private readonly string question, npc;
        private readonly string[] labels, choices;
        private readonly int[] deltas;
        private readonly IEventCommand[] branches;
        internal QuestionBranchCommand(EventNode node, Func<EventNode, IEventCommand> build)
        {
            if (node.options.Length is < 2 or > 4 ||
                node.options.Any(option => string.IsNullOrWhiteSpace(option.label) || string.IsNullOrWhiteSpace(option.value)) ||
                node.options.Select(option => option.value).Distinct(StringComparer.Ordinal).Count() != node.options.Length)
                throw new ArgumentException("A question requires 2–4 distinct choices.");
            question = node.text;
            npc = node.friendshipNpc;
            labels = node.options.Select(option => option.label).ToArray();
            choices = node.options.Select(option => option.value).ToArray();
            deltas = node.options.Select(option => option.friendshipDelta).ToArray();
            branches = node.options.Select(option => new SequentialCommandGroup(option.commands.Select(build))).ToArray();
        }
        internal async Task<int> ChooseAsync(EventExecutionContext context, CancellationToken ct)
        {
            int index;
            using (context.PauseForDialogue()) index = await context.DialogueService.ChooseAsync(question, labels, ct);
            if (index < 0 || index >= choices.Length) throw new InvalidOperationException("Invalid dialogue choice.");
            context.SelectBranch(choices[index], npc, deltas[index]);
            return index;
        }
        public override async Task ExecuteAsync(EventExecutionContext context, CancellationToken ct)
        {
            int index = await ChooseAsync(context, ct);
            await branches[index].ExecuteAsync(context, ct);
        }
    }

    public sealed class EventCommandFactory
    {
        private readonly Dictionary<string, Func<EventNode, IEventCommand>> builders = new(StringComparer.Ordinal);
        public EventCommandFactory()
        {
            Register("sequence", node => new SequentialCommandGroup(node.children.Select(Create)));
            Register("parallel", node => new ParallelCommandGroup(node.children.Select(Create)));
            Register("move", node => new MoveActorCommand(node.actor, new TilePoint(node.x, node.y), node.speed));
            Register("face", node => new FaceDirectionCommand(node.actor, Enum.Parse<Direction>(node.value, true)));
            Register("speak", node => new SpeakCommand(node.actor, node.text, node.portrait));
            Register("emote", node => new EmoteCommand(node.actor, Enum.Parse<EmoteType>(node.value, true), node.seconds));
            Register("fade", node => new FadeScreenCommand(node.value == "out", node.seconds));
            Register("sound", node => new PlaySoundCommand(node.value));
            Register("music", node => new PlayMusicCommand(node.value));
            Register("question", node => new QuestionBranchCommand(node, Create));
        }
        public void Register(string type, Func<EventNode, IEventCommand> builder) => builders.Add(type, builder);
        public IEventCommand Create(EventNode node) => builders.TryGetValue(node.type, out var build)
            ? build(node) : throw new ArgumentException("Unknown event command " + node.type);
    }
}
