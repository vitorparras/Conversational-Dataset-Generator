using Core.Host.Settings;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The desk of the decisions on changes of configuration (#634, part 3; decision 0053), with no database: who submitted never decides (CA-2), a submission decided already or past the
/// deadline is decided no more, and the deadline is 72 h unless the installation declares another from 1 h to 168 h, a key that changes only on the server.
/// </summary>
public sealed class ChangeDecisionDeskTests : IDisposable
{
    private const string Path = "ConfigurationChange:PendingDeadline";
    private const long Submitter = 41;
    private const long Another = 42;

    private static readonly DateTimeOffset SubmittedAt = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly NpgsqlDataSource nowhere = NpgsqlDataSource.Create("Host=zqx-nenhum-6342.invalid;Database=zqx;Username=zqx;Timeout=1");

    [Fact]
    public void Ca2WhoSubmittedNeverDecides() =>
        Assert.Equal(DecisionOutcome.OwnSubmission, ChangeDecisionDesk.Refusal(Change(decided: false), Submitter, SubmittedAt.AddMinutes(1), TimeSpan.FromHours(72)));

    [Fact]
    public void AnotherPersonDecidesAPendingSubmissionWithinTheDeadline() =>
        Assert.Null(ChangeDecisionDesk.Refusal(Change(decided: false), Another, SubmittedAt.AddHours(72), TimeSpan.FromHours(72)));

    [Fact]
    public void ASubmissionPastTheDeadlineIsDecidedNoMore() =>
        Assert.Equal(DecisionOutcome.Expired, ChangeDecisionDesk.Refusal(Change(decided: false), Another, SubmittedAt.AddHours(72).AddSeconds(1), TimeSpan.FromHours(72)));

    [Fact]
    public void ASubmissionDecidedAlreadyIsDecidedNoMoreNotEvenByWhoSubmitted() =>
        Assert.Equal(DecisionOutcome.AlreadyDecided, ChangeDecisionDesk.Refusal(Change(decided: true), Submitter, SubmittedAt, TimeSpan.FromHours(72)));

    [Fact]
    public void ASubmissionThatDoesNotExistIsNotFound() =>
        Assert.Equal(DecisionOutcome.NotFound, ChangeDecisionDesk.Refusal(null, Another, SubmittedAt, TimeSpan.FromHours(72)));

    [Fact]
    public void TheDeadlineIsSeventyTwoHoursUnlessTheInstallationDeclaresAnother()
    {
        Assert.Equal(TimeSpan.FromHours(72), Desk(Load()).Deadline);
        Assert.Equal(TimeSpan.FromHours(12), Desk(Load($"--{Path}=12:00:00")).Deadline);
    }

    [Theory]
    [InlineData("00:59:59")]
    [InlineData("7.00:00:01")]
    [InlineData("zqx")]
    public void ADeadlineOutsideOneToOneHundredSixtyEightHoursIsRefusedAtTheStart(string declared) =>
        Assert.Contains(HostRefusals.OfService(Load($"--{Path}={declared}")), problem => problem.Key == Path && problem.Found == declared);

    [Theory]
    [InlineData("01:00:00")]
    [InlineData("7.00:00:00")]
    public void TheEndsOfTheRangeAreTaken(string declared) =>
        Assert.Empty(HostRefusals.OfService(Load($"--{Path}={declared}")));

    [Fact]
    public void TheDeadlineIsNotAKeyTheScreenChanges() =>
        Assert.False(ChangeableKeys.Has(Path));

    public void Dispose() => nowhere.Dispose();

    private static SubmittedChange Change(bool decided) => new()
    {
        Id = 7,
        Key = "Session:IdleTimeout",
        Value = "00:20:00",
        Motive = "operational_adjustment",
        AuthorId = Submitter,
        SubmittedAt = SubmittedAt,
        Sequence = 70,
        Decided = decided,
    };

    /// <summary>The configuration of an installation the start takes, with the arguments given on top of it.</summary>
    private static ConfigurationLoadResult Load(params string[] extra)
    {
        var arguments = new HostInstallation(string.Empty, string.Empty, ["--Recognizer:Kind=fake"]) { ProvidesLanguageService = false }.LaunchArguments
            .Concat(["--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6342"])
            .Concat(extra)
            .ToArray();
        return ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new StrictArgumentsSource(arguments));
    }

    private ChangeDecisionDesk Desk(ConfigurationLoadResult inForce) =>
        new(inForce, new ConfigurationChanges(nowhere), new PersonRegistry(nowhere), new FakeTimeProvider(SubmittedAt));
}
