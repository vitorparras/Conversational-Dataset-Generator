using Core.Persistence.ServerGuarantees;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>Sets the real server's audit log against the chain of the database a test worked in, once the log has caught up with what the test did.</summary>
internal static class AuditFidelity
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The differences between the layers from <paramref name="from"/> on, for the application's role of the data source. The log is copied from
    /// the server and compared again while the chain holds more than the log shows, which is the log being late and not a difference.
    /// </summary>
    internal static async Task<IReadOnlyList<AuditDifference>> DifferencesAsync(NpgsqlDataSource application, DateTimeOffset from)
    {
        var deadline = DateTimeOffset.UtcNow + Wait;
        while (true)
        {
            var differences = await CompareOnceAsync(application, from);
            if (!differences.Any(difference => difference.Seen < difference.Recorded) || DateTimeOffset.UtcNow > deadline)
            {
                return differences;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<IReadOnlyList<AuditDifference>> CompareOnceAsync(NpgsqlDataSource application, DateTimeOffset from)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cdg-fidelity-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var shared = await PostgresFixture.SharedAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "copy.csv"), await shared.ServerLogAsync(), TestContext.Current.CancellationToken);
            return await new AuditComparer(application, directory).CompareAsync(from, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
