using Core.Persistence.Custody;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The comparison mode of the executable: the interval is two instants with a zone in order, its refusal is the host's usual one, and the
/// report says each difference with its counts and steps, tells the known one from a new one, and ends with the code that is the verdict.
/// </summary>
public sealed class AuditCompareTests
{
    private const string Database = "--Database:Host=127.0.0.1:5432 --Database:Name=zqx --Database:Username=zqx_app --Database:Password=ZQX-SENHA-7731";

    private static readonly AuditDifference NewOne =
        new("app", "public.person", ["UPDATE"], [CustodyStep.PersonDeactivated, CustodyStep.PersonFunctionChanged], 2, 1, null);

    private static readonly AuditDifference KnownOne =
        new("app", "public.person_credential", ["INSERT", "UPDATE"], [CustodyStep.PersonPasswordSet], 3, 2, AuditMap.RecomputeWithoutEvent);

    [Theory]
    [InlineData("2026-11-01T12:00:00Z", "2026-11-01T13:00:00Z", true)]
    [InlineData("2026-11-01T12:00:00+02:00", "2026-11-01T13:00:00Z", true)]
    [InlineData("2026-11-01T13:00:00Z", "2026-11-01T12:00:00Z", false)]
    [InlineData("2026-11-01T12:00:00Z", "2026-11-01T12:00:00Z", false)]
    [InlineData("2026-11-01T12:00:00", "2026-11-01T13:00:00", false)]
    [InlineData("ontem", "2026-11-01T13:00:00Z", false)]
    public void IntervalIsTwoInstantsWithAZoneInOrder(string from, string to, bool accepted)
    {
        var values = new Dictionary<string, string> { [AuditConfiguration.FromPath] = from, [AuditConfiguration.ToPath] = to };

        var refusals = AuditConfiguration.Refusals(values);

        Assert.Equal(accepted, refusals.Count == 0);
    }

    [Fact]
    public void RefusalOfAnEndNamesTheKeyAndTheValue()
    {
        var values = new Dictionary<string, string> { [AuditConfiguration.FromPath] = "ontem", [AuditConfiguration.ToPath] = "2026-11-01T13:00:00Z" };

        var refusal = Assert.Single(AuditConfiguration.Refusals(values));

        Assert.Equal((AuditConfiguration.FromPath, "ontem"), (refusal.Key, refusal.Found));
    }

    [Fact]
    public void ReportOfNoDifferenceSaysSoAndEndsInSuccess()
    {
        using var output = new StringWriter();

        var exit = AuditCompareReport.Write(output, TextWriter.Null, new AuditCompareOutcome([], Failure: null));

        Assert.Equal(ExitCode.Success, exit);
        Assert.Contains("sem diferença nova", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportOfANewDifferenceNamesRoleTableCountsAndStepsAndEndsInTheDifferentCode()
    {
        using var output = new StringWriter();

        var exit = AuditCompareReport.Write(output, TextWriter.Null, new AuditCompareOutcome([NewOne], Failure: null));

        Assert.Equal(ExitCode.AuditDifferent, exit);
        Assert.Contains("papel=app tabela=public.person comandos=UPDATE visto=2 gravado=1 passos=PersonDeactivated|PersonFunctionChanged", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportOfOnlyTheKnownDifferenceNamesTheReasonAndEndsInSuccess()
    {
        using var output = new StringWriter();

        var exit = AuditCompareReport.Write(output, TextWriter.Null, new AuditCompareOutcome([KnownOne], Failure: null));

        Assert.Equal(ExitCode.Success, exit);
        Assert.Contains($"[conhecida: {AuditMap.RecomputeWithoutEvent}]", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportOfAFailureNamesOnlyTheTypeAndEndsInTheServingFailureCode()
    {
        using var errors = new StringWriter();

        var exit = AuditCompareReport.Write(TextWriter.Null, errors, new AuditCompareOutcome([], "Npgsql.NpgsqlException"));

        Assert.Equal(ExitCode.ServingFailed, exit);
        Assert.Contains("Npgsql.NpgsqlException", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeWithoutTheIntervalEndsWithTheRefusalCodeAndNamesBothKeys()
    {
        var exit = await RunAsync("--Audit:ServerLogDirectory=/var/log/postgresql");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Audit:From'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Audit:To'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeWithAnIntervalInTheWrongOrderEndsWithTheRefusalCodeAndNamesTheEnd()
    {
        var exit = await RunAsync("--Audit:ServerLogDirectory=/var/log/postgresql", "--Audit:From=2026-11-01T13:00:00Z", "--Audit:To=2026-11-01T12:00:00Z");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Audit:To'", exit.Error, StringComparison.Ordinal);
    }

    private static Task<HostExit> RunAsync(params string[] arguments)
    {
        string[] database = [.. Database.Split(' ')];
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with
        {
            ProvidesServerLog = false,
            Arguments = ["audit-compare", .. database, .. arguments],
        };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
