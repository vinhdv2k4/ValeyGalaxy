using System.Globalization;
using System.Text;
using System.Text.Json;
using DailyEvents;
using FarmCutscenes;

Console.OutputEncoding = Encoding.UTF8;
string command = args.FirstOrDefault() ?? "help";
if (command is "help" or "--help")
{
    Console.WriteLine("DailyEvents.Cli validate");
    Console.WriteLine("DailyEvents.Cli day <absolute-day>");
    Console.WriteLine("DailyEvents.Cli year <year>");
    Console.WriteLine("DailyEvents.Cli cutscenes validate|list");
    return 0;
}

if (command == "cutscenes" && args is [_, "validate" or "list"])
{
    try
    {
        var script = JsonSerializer.Deserialize<EventScriptFile>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Milestones.json")),
            new JsonSerializerOptions { IncludeFields = true }) ??
            throw new InvalidDataException("Milestones.json is empty.");
        var repository = new EventRepository(script, new EventConditionFactory(), new EventCommandFactory());
        Console.WriteLine($"OK · {repository.Events.Count} cutscenes");
        if (args[1] == "list")
            foreach (var entry in repository.Events) Console.WriteLine($"{entry.Id} · {entry.Title}");
        return 0;
    }
    catch (Exception error) when (error is ArgumentException or JsonException or IOException)
    {
        Console.Error.WriteLine(error.Message);
        return 1;
    }
}

if ((command != "validate" || args.Length != 1) &&
    (command is not ("day" or "year") || args.Length != 2))
{
    Console.Error.WriteLine("Invalid command. Run with --help.");
    return 2;
}

try
{
    var file = JsonSerializer.Deserialize<DailyEventFile>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Calendar.json")),
        new JsonSerializerOptions { IncludeFields = true });
    var calendar = new DailyEventCalendar(file ??
        throw new InvalidDataException("Calendar.json is empty."));
    if (command == "validate")
    {
        Console.WriteLine($"OK · {calendar.Events.Count} events · {calendar.Villagers.Count} villagers");
        return 0;
    }
    if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 1 ||
        command == "year" && number > int.MaxValue / FarmDate.DaysPerYear)
    {
        Console.Error.WriteLine("Expected a positive day or year within the calendar range.");
        return 2;
    }
    int first = command == "day" ? number : (number - 1) * FarmDate.DaysPerYear + 1;
    int days = command == "day" ? 1 : FarmDate.DaysPerYear;
    for (int offset = 0; offset < days; offset++)
    {
        var date = new FarmDate(first + offset);
        DailyEventDefinition[] entries = calendar.On(date).ToArray();
        if (entries.Length == 0 && command == "year") continue;
        Console.WriteLine($"Year {date.Year} · {date.Season} {date.DayOfSeason} · day {date.Day}");
        foreach (var entry in entries)
        {
            string detail = entry.kind == "festival"
                ? $" · {entry.location} {entry.start:0000}–{entry.end:0000}"
                : "";
            Console.WriteLine($"  {entry.id} [{entry.kind}] {entry.title}{detail}");
            if (entry.people.Length > 0) Console.WriteLine($"    {string.Join(", ", entry.people)}");
        }
        if (entries.Length == 0) Console.WriteLine("  No scheduled events.");
    }
    return 0;
}
catch (Exception error) when (error is ArgumentException or JsonException or IOException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
