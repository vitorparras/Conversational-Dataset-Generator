using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Fixtures;

/// <summary>The server's log as a test reads it: whole, once the line the test waits for has reached it.</summary>
internal static class ServerLog
{
    private static readonly TimeSpan WrittenWithin = TimeSpan.FromSeconds(30);

    /// <summary>The log from its first line, as soon as it holds the text; a log that never does is a failure.</summary>
    public static async Task<string> UntilContainsAsync(string text)
    {
        var database = await PostgresFixture.SharedAsync();
        var deadline = DateTimeOffset.UtcNow + WrittenWithin;
        while (true)
        {
            var log = await database.ServerLogAsync();
            if (log.Contains(text, StringComparison.Ordinal))
            {
                return log;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, $"The server log does not hold '{text}' after {WrittenWithin.TotalSeconds:0} seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// The audit lines the server's own csvlog holds from the instant on, read the way the host reads them: the log is copied into a folder of its
    /// own and read by <see cref="ServerAuditLog"/>, so what is judged is the file the image writes and not a line made up for a test.
    /// </summary>
    public static async Task<IReadOnlyList<ServerAuditEntry>> EntriesAsync(DateTimeOffset from)
    {
        var database = await PostgresFixture.SharedAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"cdg-realcsv-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "copy.csv"), await database.ServerLogAsync(), TestContext.Current.CancellationToken);
            return await new ServerAuditLog(directory).ReadAsync(from, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The lines of the log that audit a statement of the class and command that names the table, as the role that ran it.</summary>
    public static IReadOnlyList<string> AuditLines(string log, string role, string auditedClass, string command, string table) =>
        [.. log.Split('\n').Where(line =>
            line.Contains($",\"{role}\",", StringComparison.Ordinal)
            && line.Contains("AUDIT: SESSION,", StringComparison.Ordinal)
            && line.Contains($",{auditedClass},{command},", StringComparison.Ordinal)
            && line.Contains(table, StringComparison.Ordinal))];
}
