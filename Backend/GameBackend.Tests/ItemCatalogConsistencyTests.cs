using System.Text.RegularExpressions;

public sealed class ItemCatalogConsistencyTests
{
    [Fact]
    public void EveryServerItemHasOneMatchingUnityAssetAndStackLimit()
    {
        var root = FindProjectRoot();
        var catalog = System.Text.Json.JsonSerializer.Deserialize<CatalogFile>(
            File.ReadAllText(Path.Combine(root, "Backend", "GameBackend", "item-catalog.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotNull(catalog.Items);
        var server = catalog.Items!.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var assets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(Path.Combine(root, "Assets", "Resources", "OnlineItems"), "*.asset"))
        {
            var yaml = File.ReadAllText(path);
            var id = Regex.Match(yaml, @"(?m)^  itemId: (.+)$").Groups[1].Value.Trim();
            var max = Regex.Match(yaml, @"(?m)^  maxStackSize: (\d+)\r?$").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(id), path);
            Assert.True(int.TryParse(max, out var stack), path);
            Assert.True(assets.TryAdd(id, stack), $"Duplicate Unity item ID: {id}");
        }

        Assert.Equal(server.Keys.Order(StringComparer.Ordinal), assets.Keys.Order(StringComparer.Ordinal));
        foreach (var (id, definition) in server)
            Assert.Equal(definition.MaxStack, assets[id]);
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null;
             directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "Resources", "OnlineItems")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Unity project root not found.");
    }

    private sealed record CatalogFile(ItemDefinition[]? Items);
}
