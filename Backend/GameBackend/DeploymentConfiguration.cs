using Npgsql;

public static class DeploymentConfiguration
{
    public static string? PostgresConnectionString(IConfiguration configuration, bool production)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return new NpgsqlConnectionStringBuilder(connectionString).ConnectionString;

        string[] keys = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE"];
        if (keys.Any(key => !string.IsNullOrWhiteSpace(configuration[key])))
        {
            var missing = keys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Missing PostgreSQL variables: " + string.Join(", ", missing));
            if (!int.TryParse(configuration["PGPORT"], out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("PGPORT must be an integer between 1 and 65535.");
            return new NpgsqlConnectionStringBuilder
            {
                Host = configuration["PGHOST"], Port = port,
                Username = configuration["PGUSER"], Password = configuration["PGPASSWORD"],
                Database = configuration["PGDATABASE"]
            }.ConnectionString;
        }

        if (production)
            throw new InvalidOperationException("Production requires PostgreSQL. Configure PGHOST, PGPORT, PGUSER, PGPASSWORD and PGDATABASE.");
        return null;
    }

    public static string? ListenUrl(IConfiguration configuration)
    {
        var value = configuration["PORT"];
        if (value == null) return null;
        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
        return $"http://0.0.0.0:{port}";
    }
}
