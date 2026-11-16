using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public readonly struct NpcDialoguePage
{
    public readonly string Text;
    public readonly int PortraitIndex;

    public NpcDialoguePage(string text, int portraitIndex)
    {
        Text = text;
        PortraitIndex = portraitIndex;
    }
}

public static class NpcDialogue
{
    private static readonly Regex Emotion = new Regex(@"\$([ahsulk]|\d+)", RegexOptions.Compiled);
    private static readonly Regex Metadata = new Regex(@"#\$[^#]+#", RegexOptions.Compiled);
    private static readonly Regex Commands = new Regex(@"\$[A-Za-z0-9]+", RegexOptions.Compiled);

    public static List<NpcDialoguePage> Parse(string raw, string farmerName, bool male = true)
    {
        var pages = new List<NpcDialoguePage>();
        if (string.IsNullOrWhiteSpace(raw)) return pages;
        raw = ResolveVariant(raw);
        int portrait = 0;
        foreach (string segment in Regex.Split(raw, @"#\$[eb]#"))
        {
            string selected = SelectGenderVariant(segment, male);
            MatchCollection emotions = Emotion.Matches(selected);
            foreach (Match emotion in emotions) portrait = PortraitIndex(emotion.Groups[1].Value, portrait);
            string text = Metadata.Replace(selected, string.Empty);
            text = Commands.Replace(text, string.Empty);
            text = text.Replace("@", farmerName).Replace("%", string.Empty).Trim(' ', '#', '\n', '\r');
            if (!string.IsNullOrWhiteSpace(text)) pages.Add(new NpcDialoguePage(text, portrait));
        }

        return pages;
    }

    private static string SelectGenderVariant(string segment, bool male)
    {
        int separator = segment.IndexOf('^');
        if (separator < 0) separator = segment.IndexOf('¦');
        return separator < 0 ? segment : male
            ? segment.Substring(0, separator) : segment.Substring(separator + 1);
    }

    private static string ResolveVariant(string raw)
    {
        Match choice = Regex.Match(raw, @"^\$c\s+[^#]+#([^#]+)#([^#]+)");
        if (!choice.Success) return raw;
        return choice.Groups[1].Value + raw.Substring(choice.Length);
    }

    private static int PortraitIndex(string emotion, int previous)
    {
        switch (emotion)
        {
            case "h": return 1;
            case "s": return 2;
            case "u": return 3;
            case "l": return 4;
            case "a": return 5;
            case "k": return 0;
            default: return int.TryParse(emotion, out int index) ? Math.Max(0, index) : previous;
        }
    }
}
