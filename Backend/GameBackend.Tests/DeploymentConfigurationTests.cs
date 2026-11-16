using Microsoft.Extensions.Configuration;
using Npgsql;

public sealed class DeploymentConfigurationTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ProductionCannotFallBackToEphemeralSqlite()
    {
        var configuration = Config(new());
        Assert.Null(DeploymentConfiguration.PostgresConnectionString(configuration, false));
        Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.PostgresConnectionString(configuration, true));
    }

    [Fact]
    public void RailwayReferencesHandleSpecialCharactersInPassword()
    {
        var configuration = Config(new()
        {
            ["PGHOST"] = "postgres.railway.internal", ["PGPORT"] = "5432",
            ["PGUSER"] = "postgres", ["PGPASSWORD"] = "special;password=with\"symbols",
            ["PGDATABASE"] = "railway"
        });
        var parsed = new NpgsqlConnectionStringBuilder(DeploymentConfiguration.PostgresConnectionString(configuration, true));
        Assert.Equal("postgres.railway.internal", parsed.Host);
        Assert.Equal(configuration["PGPASSWORD"], parsed.Password);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-port")]
    [InlineData("0")]
    [InlineData("65536")]
    public void PartialOrInvalidPostgresConfigurationCannotSilentlyUseDefaults(string? port)
    {
        var configuration = Config(new()
        {
            ["PGHOST"] = "postgres", ["PGPORT"] = port, ["PGUSER"] = "postgres",
            ["PGPASSWORD"] = "private", ["PGDATABASE"] = "railway"
        });
        Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.PostgresConnectionString(configuration, true));
    }

    [Fact]
    public void RailwayPortWinsOverLocalListenAddress()
    {
        var configuration = Config(new() { ["PORT"] = "4321", ["ASPNETCORE_URLS"] = "http://localhost:5055" });
        Assert.Equal("http://0.0.0.0:4321", DeploymentConfiguration.ListenUrl(configuration));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5055")]
    [InlineData("https://old-demo.trycloudflare.com")]
    public void ReleaseUsesShippedDomainDespiteOldPlayerPrefsAndLaunchArguments(string oldUrl)
    {
        Assert.Equal("https://game.up.railway.app", StartupApiUrl.Resolve("https://game.up.railway.app/",
            oldUrl, new[] { "Game.exe", "-api=" + oldUrl }, false));
        Assert.Equal(oldUrl, StartupApiUrl.Resolve("http://127.0.0.1:5055", "",
            new[] { "Game.exe", "-api=" + oldUrl }, true));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5055", false)]
    [InlineData("https://localhost", false)]
    [InlineData("https://game.up.railway.app/api", false)]
    [InlineData("https://user:secret@game.up.railway.app", false)]
    [InlineData("https://game.up.railway.app", true)]
    public void ReleaseBuildRequiresHttpsOrigin(string url, bool valid) =>
        Assert.Equal(valid, StartupApiUrl.IsProductionUrl(url));
}
