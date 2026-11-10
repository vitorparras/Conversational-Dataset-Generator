using System.Diagnostics.CodeAnalysis;
using Core.Host.Settings;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The applier of the approved changes (#634, part 4; decision 0053, CA-4 and CA-6), with no database: a change the chain and the registry still stand behind is written into the file with the
/// version before kept; one whose chain does not close, whose author was deactivated or lost the function, whose authors are the same person, whose key only the server changes or whose result the
/// start would refuse is not applied and the file stays as it was, the log telling the reason and the two sequences and nothing of a person.
/// </summary>
public sealed class ConfigurationApplierTests : IDisposable
{
    private const string Key = "Session:IdleTimeout";
    [StringSyntax(StringSyntaxAttribute.Json)]
    private const string File0 = """{ "Session": { "IdleTimeout": "00:30:00" } }""";
    private const long SubmissionSequence = 41;
    private const long ApprovalSequence = 57;

    private readonly TemporaryDirectory folder = new();
    private readonly RecordedLog log = new();
    private readonly ILoggerFactory loggers;

    public ConfigurationApplierTests()
    {
        loggers = LoggerFactory.Create(builder => builder.AddProvider(log));
    }

    [Fact]
    public void AChangeTheChainAndTheRegistryStandBehindIsWrittenWithTheVersionBeforeKept()
    {
        var path = Written();

        var refusal = Applier(path).Apply(Change(Key, "00:20:00"), Standing(), InForce());

        Assert.Equal(ApplicationRefusal.None, refusal);
        Assert.Contains("\"00:20:00\"", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal(File0, File.ReadAllText(Assert.Single(Directory.GetFiles(folder.Path, "*.previous"))));
        Assert.Contains(log.Lines, line => line.StartsWith("Information", StringComparison.Ordinal) && line.Contains($"{SubmissionSequence}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, true, true, nameof(ApplicationRefusal.ChainBroken))]
    [InlineData(true, false, true, nameof(ApplicationRefusal.SubmitterNotAllowed))]
    [InlineData(true, true, false, nameof(ApplicationRefusal.ApproverNotAllowed))]
    public void Ca6AChainThatDoesNotCloseOrAnAuthorWhoLostTheFunctionIsNotAppliedAndTheFileStaysAsItWas(bool closed, bool submitter, bool approver, string expected)
    {
        var path = Written();

        var refusal = Applier(path).Apply(Change(Key, "00:20:00"), new ApplicationFacts(submitter, approver, closed), InForce());

        Assert.Equal(Enum.Parse<ApplicationRefusal>(expected), refusal);
        Assert.Equal((File0, 1), (File.ReadAllText(path), Directory.GetFiles(folder.Path).Length));
    }

    [Fact]
    public void Ca6TheSamePersonOnBothEventsIsNotApplied()
    {
        var path = Written();

        var refusal = Applier(path).Apply(Change(Key, "00:20:00") with { ApproverId = 7 }, Standing(), InForce());

        Assert.Equal(ApplicationRefusal.SameAuthor, refusal);
        Assert.Equal(File0, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("Host:Urls", "https://127.0.0.1:6000", nameof(ApplicationRefusal.KeyOnlyOnServer))]
    [InlineData("Database:Password", "x", nameof(ApplicationRefusal.KeyOnlyOnServer))]
    [InlineData(Key, "zqx-nao-e-tempo", nameof(ApplicationRefusal.ValueRefused))]
    [InlineData("LanguageService:StartupTimeoutSeconds", "0", nameof(ApplicationRefusal.ValueRefused))]
    public void AKeyOnlyTheServerChangesOrAValueTheStartWouldRefuseIsNotApplied(string key, string value, string expected)
    {
        var path = Written();

        var refusal = Applier(path).Apply(Change(key, value), Standing(), InForce());

        Assert.Equal(Enum.Parse<ApplicationRefusal>(expected), refusal);
        Assert.Equal(File0, File.ReadAllText(path));
    }

    [Fact]
    public void AFileTheApplierCannotReadIsNotApplied()
    {
        var path = Written();
        File.WriteAllText(path, "{ isto não é JSON");

        var refusal = Applier(path).Apply(Change(Key, "00:20:00"), Standing(), InForce());

        Assert.Equal(ApplicationRefusal.FileUnreadable, refusal);
    }

    [Fact]
    public void Ca6TheLogTellsTheReasonAndTheTwoSequencesAndNothingOfThePeopleNorTheValue()
    {
        var path = Written();

        _ = Applier(path).Apply(Change(Key, "00:20:00"), new ApplicationFacts(true, true, ChainClosed: false), InForce());

        var line = Assert.Single(log.Lines);
        Assert.Contains("ChainBroken", line, StringComparison.Ordinal);
        Assert.Contains($"{SubmissionSequence}", line, StringComparison.Ordinal);
        Assert.Contains($"{ApprovalSequence}", line, StringComparison.Ordinal);
        Assert.DoesNotContain("00:20:00", line, StringComparison.Ordinal);
        Assert.DoesNotContain("pessoa 7", line, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        loggers.Dispose();
        folder.Dispose();
        log.Dispose();
    }

    private static ApplicationFacts Standing() => new(SubmitterAllowed: true, ApproverAllowed: true, ChainClosed: true);

    private static ApprovedChange Change(string key, string value) => new()
    {
        Id = 3,
        Key = key,
        Value = value,
        SubmitterId = 7,
        SubmissionSequence = SubmissionSequence,
        ApproverId = 8,
        ApprovalSequence = ApprovalSequence,
    };

    private static IReadOnlyDictionary<string, string> InForce()
    {
        var arguments = new HostInstallation(string.Empty, string.Empty, ["--Recognizer:Kind=fake"]) { ProvidesLanguageService = false }.LaunchArguments
            .Concat(["--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6340"]);
        var values = arguments.Select(argument => argument[2..].Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        return ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new TrialSource(values, Key, "00:30:00")).Values;
    }

    private ConfigurationApplier Applier(string path) => new(path, new ManualClock(), loggers);

    private string Written()
    {
        var path = Path.Combine(folder.Path, "appsettings.json");
        File.WriteAllText(path, File0);
        return path;
    }
}
