#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FarmCutscenes
{
    public enum Weather { Clear, Rain, Snow }
    public enum Direction { Up, Right, Down, Left }
    public enum EmoteType { Heart, Exclamation, Sweat, Happy }
    public enum GameMode { NormalGameplay, CutscenePlaying, DialoguePaused }

    public readonly struct TilePoint
    {
        public readonly float X, Y;
        public TilePoint(float x, float y) { X = x; Y = y; }
    }

    public readonly struct EventEvaluationContext
    {
        public readonly int Day, Time, CopperDay, CompletedRooms;
        public readonly long Earnings;
        public readonly string Location, Season;
        public readonly Weather Weather;
        public readonly IReadOnlyDictionary<string, int> Friendship;
        public readonly IEventProgressTracker Progress;

        public EventEvaluationContext(int day, int time, string location, string season, Weather weather,
            long earnings, int copperDay, int completedRooms, IReadOnlyDictionary<string, int> friendship,
            IEventProgressTracker progress)
        {
            Day = day;
            Time = time;
            Location = location;
            Season = season;
            Weather = weather;
            Earnings = earnings;
            CopperDay = copperDay;
            CompletedRooms = completedRooms;
            Friendship = friendship;
            Progress = progress;
        }
    }

    public interface IEventCondition { bool IsMet(EventEvaluationContext context); }
    public interface IEventCommand { Task ExecuteAsync(EventExecutionContext context, CancellationToken ct); }
    public interface ICutsceneControllable
    {
        Task MoveToAsync(TilePoint tile, float speed, CancellationToken ct);
        void FaceDirection(Direction direction);
        Task ShowEmoteAsync(EmoteType emote, float seconds, CancellationToken ct);
    }
    public interface ICutsceneActor : ICutsceneControllable
    {
        string ActorId { get; }
        TilePoint Position { get; }
    }
    public interface IDialogueDisplayService
    {
        Task SpeakAsync(string speaker, string text, int portrait, CancellationToken ct);
        Task<int> ChooseAsync(string title, IReadOnlyList<string> options, CancellationToken ct);
        Task<string> AskNameAsync(string title, string initialName, CancellationToken ct);
    }
    public interface IAudioService
    {
        void PlaySound(string cue);
        void PlayMusic(string cue);
    }
    public interface ICameraService { Task FadeAsync(bool toBlack, float seconds, CancellationToken ct); }
    public interface IEventProgressTracker
    {
        bool HasSeen(string id);
        bool HasMail(string id);
        Task CompleteAsync(string id, string choice, string name, string friendshipNpc, int friendshipDelta,
            CancellationToken ct);
    }
    public interface IEventService
    {
        bool IsBusy { get; }
        GameMode Mode { get; }
        void Skip();
    }

    [Serializable]
    public sealed class EventNode
    {
        public string type = "", actor = "", text = "", value = "", friendshipNpc = "";
        public int min, max, portrait;
        public float x, y, speed = 2f, seconds = 0.4f;
        public EventNode[] children = Array.Empty<EventNode>();
        public EventOption[] options = Array.Empty<EventOption>();
    }

    [Serializable]
    public sealed class EventOption
    {
        public string label = "", value = "";
        public int friendshipDelta;
        public EventNode[] commands = Array.Empty<EventNode>();
    }

    [Serializable]
    public sealed class ActorCue
    {
        public string id = "";
        public float x, y;
        public bool hidden;
    }

    [Serializable]
    public sealed class EventExitTile
    {
        public int x, y;
    }

    public enum EventEnding { Interrupted, Completed, Skipped }

    public readonly struct ActorPlacement
    {
        public string Id { get; }
        public TilePoint Tile { get; }
        /// <summary>Starts invisible until an "appear" command shows it.</summary>
        public bool Hidden { get; }
        internal ActorPlacement(ActorCue cue)
        {
            Id = cue.id;
            Tile = new TilePoint(cue.x, cue.y);
            Hidden = cue.hidden;
        }
    }

    [Serializable]
    public sealed class EventScript
    {
        public string id = "", title = "", defaultName = "";
        public EventExitTile? endPosition, skipPosition;
        public ActorCue[] actors = Array.Empty<ActorCue>();
        public EventNode[] conditions = Array.Empty<EventNode>();
        public EventNode[] commands = Array.Empty<EventNode>();
    }

    [Serializable]
    public sealed class EventScriptFile
    {
        public EventScript[] events = Array.Empty<EventScript>();
    }

    public sealed class EventDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<ActorPlacement> Actors { get; }
        public IEventCondition Condition { get; }
        public IEventCommand Commands { get; }
        internal QuestionBranchCommand? RequiredQuestion { get; }
        internal string DefaultName { get; }
        private readonly TilePoint? endPosition, skipPosition;

        public TilePoint? ExitTile(EventEnding ending) => ending switch
        {
            EventEnding.Completed => endPosition,
            EventEnding.Skipped => skipPosition ?? endPosition,
            _ => null
        };

        internal EventDefinition(EventScript script, IEventCondition condition, IEventCommand commands,
            QuestionBranchCommand? requiredQuestion)
        {
            Id = script.id;
            Title = script.title;
            Actors = Array.AsReadOnly(Array.ConvertAll(script.actors, cue => new ActorPlacement(cue)));
            Condition = condition;
            Commands = commands;
            RequiredQuestion = requiredQuestion;
            DefaultName = script.defaultName;
            if (script.endPosition != null) endPosition = new TilePoint(script.endPosition.x, script.endPosition.y);
            if (script.skipPosition != null) skipPosition = new TilePoint(script.skipPosition.x, script.skipPosition.y);
        }
    }
}
