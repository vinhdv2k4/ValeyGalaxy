#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmCutscenes
{
    public sealed class EventRepository
    {
        public IReadOnlyList<EventDefinition> Events { get; }
        public EventRepository(EventScriptFile source, EventConditionFactory conditions, EventCommandFactory commands)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Events = Array.AsReadOnly(source.events.Select(script =>
            {
                if (string.IsNullOrWhiteSpace(script.id) || !ids.Add(script.id) || script.commands.Length == 0)
                    throw new ArgumentException("Invalid or duplicated event id " + script.id);
                var actors = script.actors.Select(actor => actor.id).ToHashSet(StringComparer.Ordinal);
                if (actors.Count != script.actors.Length || script.actors.Any(actor => string.IsNullOrWhiteSpace(actor.id) ||
                    float.IsNaN(actor.x) || float.IsInfinity(actor.x) || float.IsNaN(actor.y) || float.IsInfinity(actor.y)))
                    throw new ArgumentException("Invalid or duplicated actor in " + script.id);
                foreach (var command in script.commands) ValidateCommand(command, actors, false, true);
                var questions = script.commands.Where(command => command.type == "question").ToArray();
                if (questions.Length > 1) throw new ArgumentException("Only one primary question per event.");
                var compiled = script.commands.Select(commands.Create).ToArray();
                return new EventDefinition(script,
                    new CompositeCondition(CompositeCondition.Operator.And, script.conditions.Select(conditions.Create).ToArray()),
                    new SequentialCommandGroup(compiled), compiled.OfType<QuestionBranchCommand>().SingleOrDefault());
            }).ToArray());
        }

        private static void ValidateCommand(EventNode node, HashSet<string> actors, bool parallel, bool root)
        {
            if (node.type is "move" or "face" or "emote" or "appear" or "vanish" or "dance" &&
                !actors.Contains(node.actor))
                throw new ArgumentException("Undeclared cutscene actor " + node.actor);
            if (node.type == "guide" &&
                (node.actor == node.value || !actors.Contains(node.actor) || !actors.Contains(node.value)))
                throw new ArgumentException("A guide command needs two distinct cast members.");
            if (node.type == "question" && (!root || parallel))
                throw new ArgumentException("Questions must be top-level commands.");
            if (node.type == "parallel")
            {
                var channels = new HashSet<string>(StringComparer.Ordinal);
                foreach (var child in node.children)
                    foreach (string channel in Channels(child).Distinct(StringComparer.Ordinal))
                        if (!channels.Add(channel))
                            throw new ArgumentException("Parallel commands compete for " + channel);
            }
            foreach (var child in node.children) ValidateCommand(child, actors, parallel || node.type == "parallel", false);
            foreach (var option in node.options)
                foreach (var child in option.commands) ValidateCommand(child, actors, parallel, false);
        }

        private static IEnumerable<string> Channels(EventNode node)
        {
            if (node.type is "speak" or "question") yield return "dialogue";
            if (node.type is "move" or "face" or "appear" or "vanish" or "dance") yield return "pose:" + node.actor;
            if (node.type == "guide")
            {
                yield return "pose:" + node.actor;
                yield return "pose:" + node.value;
            }
            if (node.type == "fade") yield return "screen";
            if (node.type == "music") yield return "music";
            foreach (var child in node.children)
                foreach (string channel in Channels(child)) yield return channel;
        }
    }

    public sealed class EventRuntimeState
    {
        private interface IState
        {
            GameMode Mode { get; }
            IState Begin();
            IState Pause();
            IState Resume();
        }
        private sealed class NormalGameplayState : IState
        {
            public GameMode Mode => GameMode.NormalGameplay;
            public IState Begin() => Playing;
            public IState Pause() => throw new InvalidOperationException("No cutscene is running.");
            public IState Resume() => this;
        }
        private sealed class CutscenePlayingState : IState
        {
            public GameMode Mode => GameMode.CutscenePlaying;
            public IState Begin() => throw new InvalidOperationException("A cutscene is already running.");
            public IState Pause() => Paused;
            public IState Resume() => this;
        }
        private sealed class DialoguePausedState : IState
        {
            public GameMode Mode => GameMode.DialoguePaused;
            public IState Begin() => throw new InvalidOperationException("A cutscene is already running.");
            public IState Pause() => throw new InvalidOperationException("Only one dialogue can be open.");
            public IState Resume() => Playing;
        }
        private static readonly IState Normal = new NormalGameplayState();
        private static readonly IState Playing = new CutscenePlayingState();
        private static readonly IState Paused = new DialoguePausedState();
        private IState state = Normal;
        public GameMode Mode => state.Mode;
        internal void Begin() => state = state.Begin();
        internal void Pause() => state = state.Pause();
        internal void Resume() => state = state.Resume();
        internal void Reset() => state = Normal;
    }

    public sealed class EventExecutionContext
    {
        private readonly EventRuntimeState state;
        public EventDefinition EventData { get; }
        public IReadOnlyDictionary<string, ICutsceneActor> Actors { get; }
        public IDialogueDisplayService DialogueService { get; }
        public IAudioService AudioService { get; }
        public ICameraService CameraService { get; }
        internal string Choice { get; private set; } = "";
        internal string FriendshipNpc { get; private set; } = "";
        internal int FriendshipDelta { get; private set; }

        public EventExecutionContext(EventDefinition definition, IDictionary<string, ICutsceneActor> actors,
            IDialogueDisplayService dialogue, IAudioService audio, ICameraService camera, EventRuntimeState state)
        {
            EventData = definition;
            Actors = new ReadOnlyDictionary<string, ICutsceneActor>(new Dictionary<string, ICutsceneActor>(actors));
            DialogueService = dialogue;
            AudioService = audio;
            CameraService = camera;
            this.state = state;
        }
        public ICutsceneActor Actor(string id) => Actors[id];
        public IDisposable PauseForDialogue()
        {
            state.Pause();
            return new DialogueScope(state);
        }
        internal void SelectBranch(string choice, string npc, int delta)
        { Choice = choice; FriendshipNpc = npc; FriendshipDelta = delta; }

        private sealed class DialogueScope : IDisposable
        {
            private readonly EventRuntimeState state;
            public DialogueScope(EventRuntimeState state) => this.state = state;
            public void Dispose() => state.Resume();
        }
    }

    public sealed class CutsceneRunner : IEventService
    {
        private CancellationTokenSource? presentation;
        public EventRuntimeState State { get; } = new EventRuntimeState();
        public GameMode Mode => State.Mode;
        public bool IsBusy => Mode != GameMode.NormalGameplay;
        public void Skip() => presentation?.Cancel();

        public async Task RunAsync(EventExecutionContext context, IEventProgressTracker progress,
            Action<EventEnding> restoreStage, CancellationToken lifetime)
        {
            State.Begin();
            try
            {
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime))
                {
                    presentation = cancellation;
                    EventEnding ending = EventEnding.Interrupted;
                    try
                    {
                        await context.EventData.Commands.ExecuteAsync(context, cancellation.Token);
                        lifetime.ThrowIfCancellationRequested();
                        ending = cancellation.IsCancellationRequested ? EventEnding.Skipped : EventEnding.Completed;
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested &&
                                                              !lifetime.IsCancellationRequested)
                    {
                        ending = EventEnding.Skipped;
                    }
                    finally
                    {
                        presentation = null;
                        restoreStage(ending);
                    }
                }
                lifetime.ThrowIfCancellationRequested();
                if (context.Choice.Length == 0 && context.EventData.RequiredQuestion != null)
                    await context.EventData.RequiredQuestion.ChooseAsync(context, lifetime);

                string name = "";
                if (context.EventData.DefaultName.Length > 0 && context.Choice is "dog" or "cat")
                {
                    using (context.PauseForDialogue())
                        name = await context.DialogueService.AskNameAsync("Đặt tên thú cưng",
                            context.EventData.DefaultName, lifetime);
                }
                await progress.CompleteAsync(context.EventData.Id, context.Choice, name, context.FriendshipNpc,
                    context.FriendshipDelta, lifetime);
            }
            finally { State.Reset(); }
        }
    }
}
