using System.Text.Json;
using Core.Persistence.Custody;
using Core.Persistence.ServerGuarantees;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The host compares the server's audit log with the chain by itself, every interval, from where the last comparison ended: a new difference
/// is a warning in the log and a known one is only counted, a comparison that cannot run is said and retried over the same interval, and what
/// the last one found is in the state that /status reports (issue #60, part 2).
/// </summary>
public sealed class AuditComparisonServiceTests
{
    private static readonly TimeSpan Lag = TimeSpan.FromMinutes(1);

    private static readonly AuditDifference NewOne = new("app", "public.person", ["UPDATE"], [CustodyStep.PersonDeactivated], 2, 1, null);

    private static readonly AuditDifference KnownOne =
        new("app", "public.person_credential", ["UPDATE"], [CustodyStep.PersonPasswordSet], 3, 2, AuditMap.RecomputeWithoutEvent);

    [Fact]
    public async Task BeforeTheFirstIntervalNothingHasBeenComparedAndTheStateSaysSo()
    {
        await using var run = await AuditComparisonRun.StartAsync(_ => []);

        Assert.Equal(AuditComparisonResult.Pending, run.State.Current.Result);
        Assert.Null(run.State.Current.At);
        Assert.Empty(run.Calls);
    }

    [Fact]
    public async Task AgreeingRoundIsAgreedAndFollowsTheLastOneWithoutGapOrOverlapStartingAMinuteBeforeTheHost()
    {
        await using var run = await AuditComparisonRun.StartAsync(_ => []);
        var started = run.Clock.GetUtcNow();

        var first = await run.NextRoundAsync();
        _ = await run.NextRoundAsync();

        Assert.Equal((AuditComparisonResult.Agreed, 0, 0), (first.Result, first.NewDifferences, first.KnownDifferences));
        Assert.Equal(started + AuditComparisonRun.Interval, first.At);
        var calls = run.Calls;
        Assert.Equal((started - Lag, started + AuditComparisonRun.Interval - Lag), calls[0]);
        Assert.Equal(calls[0].To, calls[1].From);
        Assert.Equal(calls[0].To + AuditComparisonRun.Interval, calls[1].To);
    }

    [Fact]
    public async Task NewDifferenceIsAWarningNamingRoleTableCountsAndStepsAndTheKnownOneIsOnlyCounted()
    {
        await using var run = await AuditComparisonRun.StartAsync(_ => [NewOne, KnownOne]);

        var status = await run.NextRoundAsync();

        Assert.Equal((AuditComparisonResult.Different, 1, 1), (status.Result, status.NewDifferences, status.KnownDifferences));
        var warning = run.Log.Lines.Single(entry => entry.Level == LogLevel.Warning).Line;
        Assert.Contains("papel app, tabela public.person, comandos UPDATE, visto 2, gravado 1, passos PersonDeactivated", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(run.Log.Lines, entry => entry.Line.Contains("public.person_credential", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CutFileIsAWarningThatNamesItAndTheStatusSaysItWithoutChangingTheResult()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(_ => new AuditCompared([], ["postgresql-a.csv"]));

        var status = await run.NextRoundAsync();

        Assert.Equal((AuditComparisonResult.Agreed, 0, 0), (status.Result, status.NewDifferences, status.KnownDifferences));
        Assert.Equal(["postgresql-a.csv"], status.CutOffFiles);
        var warning = Assert.Single(run.Log.Lines, entry => entry.Level == LogLevel.Warning).Line;
        Assert.Contains("postgresql-a.csv", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CutFileWithANewDifferenceStillCountsTheDifferenceAndKeepsBothWarnings()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(_ => new AuditCompared([NewOne], ["postgresql-a.csv"]));

        var status = await run.NextRoundAsync();

        Assert.Equal((AuditComparisonResult.Different, 1), (status.Result, status.NewDifferences));
        Assert.Equal(2, run.Log.Lines.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task CutFileSeenInARoundStaysListedInTheRoundsThatNoLongerReadIt()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(call => call == 0 ? new AuditCompared([], ["postgresql-a.csv"]) : new AuditCompared([], []));

        var first = await run.NextRoundAsync();
        var second = await run.NextRoundAsync();
        var third = await run.NextRoundAsync();

        Assert.Equal(["postgresql-a.csv"], first.CutOffFiles);
        Assert.Equal(["postgresql-a.csv"], second.CutOffFiles);
        Assert.Equal(["postgresql-a.csv"], third.CutOffFiles);
        Assert.Equal((AuditComparisonResult.Agreed, 0, 0), (third.Result, third.NewDifferences, third.KnownDifferences));
    }

    [Fact]
    public async Task TwoCutFilesSeenInDifferentRoundsAreBothListedOnceEachInTheOrderSeen()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(call => call switch
        {
            0 => new AuditCompared([], ["postgresql-a.csv"]),
            1 => new AuditCompared([], ["postgresql-b.csv", "postgresql-a.csv"]),
            _ => new AuditCompared([], ["postgresql-b.csv"]),
        });

        _ = await run.NextRoundAsync();
        var second = await run.NextRoundAsync();
        var third = await run.NextRoundAsync();

        Assert.Equal(["postgresql-a.csv", "postgresql-b.csv"], second.CutOffFiles);
        Assert.Equal(["postgresql-a.csv", "postgresql-b.csv"], third.CutOffFiles);
    }

    [Fact]
    public async Task FailedRoundKeepsTheCutFilesAlreadyListed()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(call => call switch
        {
            0 => new AuditCompared([], ["postgresql-a.csv"]),
            1 => throw new IOException("zqx"),
            _ => new AuditCompared([], []),
        });

        _ = await run.NextRoundAsync();
        var failed = await run.NextRoundAsync();
        var retried = await run.NextRoundAsync();

        Assert.Equal(AuditComparisonResult.Failed, failed.Result);
        Assert.Equal(["postgresql-a.csv"], failed.CutOffFiles);
        Assert.Equal(["postgresql-a.csv"], retried.CutOffFiles);
    }

    [Fact]
    public async Task HostStartedAnewStartsWithNoCutFileListed()
    {
        await using (var before = await AuditComparisonRun.StartWithAsync(_ => new AuditCompared([], ["postgresql-a.csv"])))
        {
            Assert.Equal(["postgresql-a.csv"], (await before.NextRoundAsync()).CutOffFiles);
        }

        await using var restarted = await AuditComparisonRun.StartWithAsync(_ => new AuditCompared([], []));

        Assert.Empty(restarted.State.Current.CutOffFiles);
        Assert.Empty((await restarted.NextRoundAsync()).CutOffFiles);
    }

    [Fact]
    public async Task CutFileIsWarnedOnceInTheRoundThatReadsItAndNotAgainWhenAnotherRoundReadsItToo()
    {
        await using var run = await AuditComparisonRun.StartWithAsync(call => call < 2 ? new AuditCompared([], ["postgresql-a.csv"]) : new AuditCompared([], []));

        _ = await run.NextRoundAsync();
        _ = await run.NextRoundAsync();
        _ = await run.NextRoundAsync();

        _ = Assert.Single(run.Log.Lines, entry => entry.Level == LogLevel.Warning && entry.Line.Contains("postgresql-a.csv", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedRoundIsFailedAndTheNextOneStartsWhereTheFailedOneStartedWithNothingButTheTypeSaid()
    {
        const string marker = "zqx-marcador-do-comando";
        await using var run = await AuditComparisonRun.StartAsync(call => call == 0 ? throw new IOException(marker) : []);

        var failed = await run.NextRoundAsync();
        var failedLog = run.Log.Lines.ToArray();
        var statusText = JsonSerializer.Serialize(failed);
        var retried = await run.NextRoundAsync();

        Assert.Equal((AuditComparisonResult.Failed, 0, 0), (failed.Result, failed.NewDifferences, failed.KnownDifferences));
        Assert.Equal(AuditComparisonResult.Agreed, retried.Result);
        var calls = run.Calls;
        Assert.Equal(calls[0].From, calls[1].From);
        Assert.Equal(calls[0].To + AuditComparisonRun.Interval, calls[1].To);
        var line = Assert.Single(failedLog, entry => entry.Level == LogLevel.Error).Line;
        Assert.Contains("System.IO.IOException", line, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, line + statusText, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("database")]
    [InlineData("unreadable-line")]
    [InlineData("file-denied")]
    public async Task EveryFailureOfReadingTheLogOrAskingTheDatabaseIsAFailedRound(string kind)
    {
        var failure = Failure(kind);
        await using var run = await AuditComparisonRun.StartAsync(call => call == 0 ? throw failure : []);

        var status = await run.NextRoundAsync();

        Assert.Equal(AuditComparisonResult.Failed, status.Result);
        Assert.Contains(failure.GetType().FullName!, Assert.Single(run.Log.Lines, entry => entry.Level == LogLevel.Error).Line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("disposed")]
    [InlineData("argument")]
    public async Task AnyOtherExceptionIsADefectThatEndsTheServiceAndRecordsNoRound(string kind)
    {
        var defect = Failure(kind);
        await using var run = await AuditComparisonRun.StartAsync(_ => throw defect);

        var thrown = await run.DefectAfterNextIntervalAsync();

        Assert.Same(defect, thrown);
        Assert.Equal(AuditComparisonResult.Pending, run.State.Current.Result);
    }

    private static Exception Failure(string kind) => kind switch
    {
        "database" => new NpgsqlException("zqx-detalhe"),
        "unreadable-line" => new InvalidDataException("zqx-detalhe"),
        "file-denied" => new UnauthorizedAccessException("zqx-detalhe"),
        "invalid-operation" => new InvalidOperationException("zqx-detalhe"),
        "disposed" => new ObjectDisposedException("zqx-detalhe"),
        _ => new ArgumentException("zqx-detalhe"),
    };
}
