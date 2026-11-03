using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the period of the comparison of the server's log with the chain, and refuses one that is not a
/// duration of a minute to 49 days; the status route says how the last comparison ended (issue #60, part 2).
/// </summary>
public sealed class AuditCompareIntervalTests
{
    [Theory]
    [InlineData("00:01:00", true)]
    [InlineData("01:00:00", true)]
    [InlineData("1.00:00:00", true)]
    [InlineData("49.00:00:00", true)]
    [InlineData("49.00:00:01", false)]
    [InlineData("50.00:00:00", false)]
    [InlineData("9999.00:00:00", false)]
    [InlineData("00:00:59", false)]
    [InlineData("00:00:00", false)]
    [InlineData("uma hora", false)]
    [InlineData("1h", false)]
    public void PeriodIsADurationFromAMinuteToFortyNineDays(string declared, bool accepted)
    {
        var values = new Dictionary<string, string> { [AuditConfiguration.CompareIntervalPath] = declared };

        var refusals = AuditConfiguration.Refusals(values);

        Assert.Equal(accepted, refusals.Count == 0);
        if (accepted)
        {
            using var timer = new PeriodicTimer(AuditConfiguration.CompareInterval(values));
        }
    }

    [Theory]
    [InlineData("50.00:00:00")]
    [InlineData("00:00:30")]
    [InlineData("uma hora")]
    public void RefusalNamesTheKeyTheValueAndWhatIsAccepted(string declared)
    {
        var values = new Dictionary<string, string> { [AuditConfiguration.CompareIntervalPath] = declared };

        var refusal = Assert.Single(AuditConfiguration.Refusals(values));

        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, refusal.Kind);
        var text = refusal.Describe();
        Assert.Contains("'Audit:CompareInterval'", text, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", text, StringComparison.Ordinal);
        Assert.Contains("duração de 00:01:00 a 49.00:00:00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWithoutThePeriodEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress());
        installation = installation with { Arguments = [.. installation.Arguments, "--Audit:ServerLogDirectory=/var/log/postgresql"], ProvidesServerLog = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Audit:CompareInterval'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusSaysPendingBeforeAnyComparisonAndThenHowTheLastOneEnded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(new StartupReport([], DateTimeOffset.UtcNow), new ServiceProcessWatch(), ct);

        using var before = await host.StatusAsync(ct);
        _ = host.Comparison.Record(new AuditComparisonStatus(AuditComparisonResult.Different, DateTimeOffset.UtcNow, 2, 1, ["postgresql-a.csv"]));
        using var after = await host.StatusAsync(ct);

        Assert.Equal("Pending", before.RootElement.GetProperty("auditComparison").GetProperty("result").GetString());
        var comparison = after.RootElement.GetProperty("auditComparison");
        Assert.Equal(("Different", 2, 1), (comparison.GetProperty("result").GetString(), comparison.GetProperty("newDifferences").GetInt32(), comparison.GetProperty("knownDifferences").GetInt32()));
        Assert.Equal(["postgresql-a.csv"], comparison.GetProperty("cutOffFiles").EnumerateArray().Select(file => file.GetString()));
        Assert.Empty(before.RootElement.GetProperty("auditComparison").GetProperty("cutOffFiles").EnumerateArray());
    }
}
