using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class DatabaseTestServer
{
    public static WebApplicationFactory<Program> Create(string databasePath, string secret,
        Action<IWebHostBuilder>? configure = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // Program selects the database before the web host's app configuration callbacks run.
            builder.UseSetting("GAME_DB_PATH", databasePath);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["SECURITY_HASH_SECRET"] = secret }));
            configure?.Invoke(builder);
        });

        try
        {
            using var scope = factory.Services.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<GameDb>().Database.GetDbConnection();
            if (connection is not SqliteConnection ||
                !Path.GetFullPath(connection.DataSource).Equals(Path.GetFullPath(databasePath), StringComparison.Ordinal))
                throw new InvalidOperationException("The test server must use its own SQLite database.");
            return factory;
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }
}
