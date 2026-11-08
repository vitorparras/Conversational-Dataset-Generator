using Core.Host.Settings;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The desk of the changes of configuration (#634, part 2b; decision 0053), with no database: the data source points nowhere, so a refusal that reached it would fail the test.
/// A change the start would refuse is refused with the key and the value named and nothing written (CA-1); a key outside the list changes only on the server (CA-5); a motive
/// outside the closed list is refused; a value the environment or an argument overrides is refused, since the file would not change it; and the trial judges by the rule of the start.
/// </summary>
public sealed class ConfigurationChangeDeskTests : IDisposable
{
    private const string Key = "Session:IdleTimeout";
    private const string Motive = "operational_adjustment";

    private readonly NpgsqlDataSource nowhere = NpgsqlDataSource.Create("Host=zqx-nenhum-6340.invalid;Database=zqx;Username=zqx;Timeout=1");

    public static TheoryData<string> ServerOnlyKeys() =>
        ["Database:Host", "Database:Password", "Tls:CertificateFile", "MasterKey:File", "Host:Urls", "Installation:Kind", "Ingestion:Conditions", "Zqx:Desconhecida"];

    [Fact]
    public void TheConfigurationTheTestsStartFromIsOneTheStartTakes() =>
        Assert.Empty(HostRefusals.OfService(InForce()));

    [Fact]
    public void AValueTheStartTakesIsNotRefused() =>
        Assert.Null(Desk(InForce()).Judge(new ChangeRequest(Key, "00:20:00", Motive, 1)));

    [Theory]
    [InlineData(Key, "zqx-nao-e-tempo")]
    [InlineData("Passwords:WorkFactor", "0")]
    [InlineData("Session:AbsoluteTimeout", "-00:10:00")]
    public async Task Ca1AChangeTheStartWouldRefuseIsRefusedNamingTheKeyAndTheValueAndNothingIsWritten(string key, string value)
    {
        var answer = await Desk(InForce()).SubmitAsync(new ChangeRequest(key, value, Motive, 1), TestContext.Current.CancellationToken);

        Assert.Equal(ChangeOutcome.ValueRefused, answer.Outcome);
        Assert.Contains(answer.Problems!, problem => problem.Key == key && problem.Found == value);
        Assert.Null(answer.ChangeId);
    }

    [Fact]
    public void AValueOutsidePrintableAsciiIsRefusedNamingTheKeyAndTheValue()
    {
        var answer = Desk(InForce()).Judge(new ChangeRequest(Key, "00:20:00\n", Motive, 1));

        var problem = Assert.Single(answer!.Problems!);
        Assert.Equal((ChangeOutcome.ValueRefused, ConfigurationProblemKind.ValueNotAccepted, Key, "00:20:00\n"), (answer.Outcome, problem.Kind, problem.Key, problem.Found));
    }

    [Theory]
    [MemberData(nameof(ServerOnlyKeys))]
    public async Task Ca5AKeyOutsideTheListChangesOnlyOnTheServer(string key)
    {
        var answer = await Desk(InForce()).SubmitAsync(new ChangeRequest(key, "zqx", Motive, 1), TestContext.Current.CancellationToken);

        Assert.Equal(ChangeOutcome.OnlyOnServer, answer.Outcome);
    }

    [Theory]
    [InlineData("Ajuste")]
    [InlineData("zqx_motivo")]
    [InlineData("")]
    public void AMotiveOutsideTheClosedListIsRefused(string motive) =>
        Assert.Equal(ChangeOutcome.MotiveUnknown, Desk(InForce()).Judge(new ChangeRequest(Key, "00:20:00", motive, 1))!.Outcome);

    [Fact]
    public void AValueAnArgumentOverridesIsRefusedSinceTheFileWouldNotChangeIt()
    {
        var overridden = ClosedConfiguration.Load(Schema(), new TrialSource(Values(), Key, "00:30:00"), new StrictArgumentsSource([$"--{Key}=00:30:00"]));

        Assert.Equal(ChangeOutcome.OverriddenOnServer, Desk(overridden).Judge(new ChangeRequest(Key, "00:20:00", Motive, 1))!.Outcome);
    }

    [Fact]
    public void EveryKeyOfTheListIsOneTheStartDeclaresAndNoneIsSensitive()
    {
        var shown = InForce().View.Entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        Assert.All(ChangeableKeys.All, key => Assert.NotEqual(EffectiveConfiguration.Redacted, Assert.Contains(key, (IReadOnlyDictionary<string, string?>)shown)));
    }

    public void Dispose() => nowhere.Dispose();

    private static ConfigurationSchema Schema() => HostConfiguration.Schema(migrating: false, comparing: false);

    private static ConfigurationLoadResult InForce() => ClosedConfiguration.Load(Schema(), new TrialSource(Values(), Key, "00:30:00"));

    /// <summary>The values of an installation the start takes: the arguments the tests of the host start it with, and a database that is never reached.</summary>
    private static Dictionary<string, string> Values()
    {
        var arguments = new HostInstallation(string.Empty, string.Empty, ["--Recognizer:Kind=fake"]) { ProvidesLanguageService = false }.LaunchArguments
            .Concat(["--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6340"]);
        return arguments.Select(argument => argument[2..].Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
    }

    private ConfigurationChangeDesk Desk(ConfigurationLoadResult inForce) => new(inForce, new ConfigurationChanges(nowhere), new PersonRegistry(nowhere));
}
