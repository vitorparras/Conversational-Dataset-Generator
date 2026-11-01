using Core.Persistence.ServerGuarantees;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The audit lines of the server's csvlog are read for an interval with the role, the class, the command and the object,
/// never the statement; what cannot be read is an error and not a line left out.
/// </summary>
public sealed class ServerAuditLogTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ServerLogFolder folder = new();

    [Fact]
    public async Task LineInTheIntervalIsReadWithTheRoleTheClassTheCommandAndTheObject()
    {
        _ = folder.Write("postgresql-a.csv", Noon, ServerLogFolder.AuditLine(Noon.AddMinutes(1), "app", "INSERT", "public.person"));

        var entries = await ReadAsync(Noon, Noon.AddHours(1));

        var entry = Assert.Single(entries);
        Assert.Equal(new ServerAuditEntry(Noon.AddMinutes(1), "app", "cdg", "WRITE", "INSERT", "TABLE", "public.person"), entry);
    }

    [Fact]
    public async Task IntervalIncludesTheStartAndExcludesTheEnd()
    {
        _ = folder.Write(
            "postgresql-a.csv",
            Noon.AddHours(2),
            ServerLogFolder.AuditLine(Noon.AddSeconds(-1), "app", "INSERT", "public.person"),
            ServerLogFolder.AuditLine(Noon, "app", "UPDATE", "public.person"),
            ServerLogFolder.AuditLine(Noon.AddHours(1), "app", "DELETE", "public.person"));

        var entries = await ReadAsync(Noon, Noon.AddHours(1));

        Assert.Equal(["UPDATE"], entries.Select(entry => entry.Command));
    }

    [Fact]
    public async Task StatementWithCommasQuotesAndLineBreaksIsNeitherKeptNorBreaksTheLine()
    {
        var statement = "/* zqx-tag */ INSERT INTO public.person (name) VALUES ('a,\"b\"\nc')";
        _ = folder.Write("postgresql-a.csv", Noon, ServerLogFolder.AuditLine(Noon, "app", "INSERT", "public.person", statement));

        var entry = Assert.Single(await ReadAsync(Noon.AddSeconds(-1), Noon.AddSeconds(1)));

        Assert.Equal("INSERT", entry.Command);
        Assert.DoesNotContain("zqx-tag", entry.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LineThatNamesNoObjectHasNoneAndLinesThatAreNotAuditAreLeftOut()
    {
        var other = ServerLogFolder.AuditLine(Noon, "app", "INSERT", null).Replace("AUDIT: SESSION,1,1,WRITE,INSERT,,,SELECT 1,<not logged>", "connection authorized", StringComparison.Ordinal);
        _ = folder.Write("postgresql-a.csv", Noon, other, ServerLogFolder.AuditLine(Noon, "app", "TRUNCATE TABLE", null));

        var entry = Assert.Single(await ReadAsync(Noon, Noon.AddHours(1)));

        Assert.Equal(("TRUNCATE TABLE", null, null), (entry.Command, entry.ObjectType, entry.ObjectName));
    }

    [Fact]
    public async Task FilesAreReadOldestNameFirstAndAFileThatStoppedBeforeTheIntervalIsNotOpened()
    {
        _ = folder.Write("postgresql-2.csv", Noon.AddHours(3), ServerLogFolder.AuditLine(Noon.AddMinutes(30), "app", "UPDATE", "public.person"));
        _ = folder.Write("postgresql-1.csv", Noon.AddHours(3), ServerLogFolder.AuditLine(Noon.AddMinutes(10), "app", "INSERT", "public.person"));
        _ = folder.Write("postgresql-0.csv", Noon.AddHours(-3), "this file ended before the interval and is not even a csvlog,,,,,,,,,,,,,,,,");

        var entries = await ReadAsync(Noon, Noon.AddHours(1));

        Assert.Equal(["INSERT", "UPDATE"], entries.Select(entry => entry.Command));
    }

    [Fact]
    public async Task LineWhoseTimeIsNotInUtcIsAnErrorAndNotALineLeftOut()
    {
        var line = ServerLogFolder.AuditLine(Noon, "app", "INSERT", "public.person").Replace(" UTC,", " -03,", StringComparison.Ordinal);
        _ = folder.Write("postgresql-a.csv", Noon, line);

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Noon, Noon.AddHours(1)));
    }

    [Fact]
    public async Task AuditLineWithFewerPartsThanPgauditWritesIsAnError()
    {
        var whole = "AUDIT: SESSION,1,1,WRITE,INSERT,TABLE,public.person,SELECT 1,<not logged>";
        var line = ServerLogFolder.AuditLine(Noon, "app", "INSERT", "public.person").Replace(whole, "AUDIT: SESSION,1,1", StringComparison.Ordinal);
        _ = folder.Write("postgresql-a.csv", Noon, line);

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Noon, Noon.AddHours(1)));
    }

    [Fact]
    public async Task ReadingStopsAtTheLimitAndKeepsTheOldestLines()
    {
        _ = folder.Write(
            "postgresql-a.csv",
            Noon.AddHours(2),
            ServerLogFolder.AuditLine(Noon.AddSeconds(1), "app", "INSERT", "public.person"),
            ServerLogFolder.AuditLine(Noon.AddSeconds(2), "app", "UPDATE", "public.person"),
            ServerLogFolder.AuditLine(Noon.AddSeconds(3), "app", "DELETE", "public.person"));

        var entries = await new ServerAuditLog(folder.Path).ReadAsync(Noon, Noon.AddHours(1), 2, TestContext.Current.CancellationToken);

        Assert.Equal(["INSERT", "UPDATE"], entries.Select(entry => entry.Command));
    }

    [Fact]
    public async Task FileAfterTheIntervalIsNotOpenedOnceALineLaterThanTheIntervalHasBeenSeen()
    {
        var unreadable = ServerLogFolder.AuditLine(Noon.AddHours(5), "app", "INSERT", null).Replace("WRITE,INSERT", "WRITE", StringComparison.Ordinal);
        _ = folder.Write(
            "postgresql-a.csv",
            Noon.AddHours(3),
            ServerLogFolder.AuditLine(Noon.AddMinutes(1), "app", "INSERT", "public.person"),
            ServerLogFolder.AuditLine(Noon.AddHours(2), "app", "UPDATE", "public.person"));
        _ = folder.Write("postgresql-b.csv", Noon.AddHours(6), unreadable);

        var entries = await ReadAsync(Noon, Noon.AddHours(1));

        Assert.Equal(["INSERT"], entries.Select(entry => entry.Command));
    }

    public void Dispose() => folder.Dispose();

    private Task<IReadOnlyList<ServerAuditEntry>> ReadAsync(DateTimeOffset from, DateTimeOffset to) =>
        new ServerAuditLog(folder.Path).ReadAsync(from, to, TestContext.Current.CancellationToken);
}
