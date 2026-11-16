using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FarmCutscenes;

internal sealed class UnityCutsceneDialogue : IDialogueDisplayService
{
    private readonly MilestoneSprites sprites;
    internal UnityCutsceneDialogue(MilestoneSprites sprites) => this.sprites = sprites;

    public Task SpeakAsync(string speaker, string text, int portrait, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        NpcConversation.ShowCutscenePage(speaker, text, portrait, sprites?.PortraitFor(speaker));
        return UntilClosed(ct);
    }

    public async Task<int> ChooseAsync(string title, IReadOnlyList<string> options, CancellationToken ct)
    {
        int selected = -1;
        var choices = new List<(string, System.Action)>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            choices.Add((options[i], () => selected = index));
        }
        do
        {
            ct.ThrowIfCancellationRequested();
            NpcConversation.ShowChoices(title, choices);
            await UntilClosed(ct);
        } while (selected < 0);
        return selected;
    }

    public async Task<string> AskNameAsync(string title, string initialName, CancellationToken ct)
    {
        string name = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            NpcConversation.ShowTextEntry(title, initialName, value => name = value);
            await UntilClosed(ct);
        } while (string.IsNullOrWhiteSpace(name));
        return name;
    }

    private static async Task UntilClosed(CancellationToken ct)
    {
        try
        {
            while (NpcConversation.IsOpen)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            if (ct.IsCancellationRequested && NpcConversation.IsOpen) NpcConversation.Close();
        }
    }
}
