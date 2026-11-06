using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The database every host started by a test of this project runs against, since the host does not start without one: a database of its own on the
/// shared server, migrated once, read by the application's role. The hosts only check it, so they share it; it is dropped with the server when the run ends.
/// </summary>
internal static class MigratedDatabase
{
    private static readonly Lazy<ScratchDatabase> Shared = new(() => Task.Run(CreateAsync).GetAwaiter().GetResult());

    /// <summary>The variables that give a host the four keys of the database; without Docker the test is skipped, as every database test is.</summary>
    public static IReadOnlyDictionary<string, string> Variables
    {
        get
        {
            if (!DockerAvailability.ShouldRun)
            {
                Assert.Skip(DockerAvailability.SkipReason);
            }

            var connection = new NpgsqlConnectionStringBuilder(Shared.Value.AppConnectionString);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CDG_Database__Host"] = $"{connection.Host}:{connection.Port}",
                ["CDG_Database__Name"] = connection.Database!,
                ["CDG_Database__Username"] = connection.Username!,
                ["CDG_Database__Password"] = connection.Password!,
            };
        }
    }

    private static async Task<ScratchDatabase> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }
}
