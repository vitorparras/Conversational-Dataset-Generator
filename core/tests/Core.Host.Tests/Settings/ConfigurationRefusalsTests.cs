using Core.Host.Settings;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The record of the changes that will never be applied (issue 748; decision 0053), with no database: a refusal that lasts has a code for the chain and one that may pass has none, the codes are in
/// lower case and underscores as the chain takes them, a host with no database records nothing, and a database that cannot be reached writes nothing and is told by the type of the failure alone.
/// </summary>
public sealed class ConfigurationRefusalsTests : IDisposable
{
    private readonly RecordedLog log = new();
    private readonly NpgsqlDataSource nowhere = NpgsqlDataSource.Create("Host=zqx-nenhum-6340.invalid;Database=zqx;Username=zqx;Password=ZQX-SENHA-6340;Timeout=1");
    private readonly ILoggerFactory loggers;

    public ConfigurationRefusalsTests()
    {
        loggers = LoggerFactory.Create(builder => builder.AddProvider(log));
    }

    public static TheoryData<string, bool> Kinds() => new()
    {
        { nameof(ApplicationRefusal.SameAuthor), true },
        { nameof(ApplicationRefusal.SubmitterNotAllowed), true },
        { nameof(ApplicationRefusal.ApproverNotAllowed), true },
        { nameof(ApplicationRefusal.KeyOnlyOnServer), true },
        { nameof(ApplicationRefusal.ValueRefused), true },
        { nameof(ApplicationRefusal.None), false },
        { nameof(ApplicationRefusal.ChainBroken), false },
        { nameof(ApplicationRefusal.FileUnreadable), false },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ARefusalThatLastsIsTheOnlyOneThatIsRecorded(string refusal, bool definitive) =>
        Assert.Equal(definitive, Enum.Parse<ApplicationRefusal>(refusal).IsDefinitive());

    public static TheoryData<string, string> Codes() => new()
    {
        { nameof(ApplicationRefusal.SameAuthor), "same_author" },
        { nameof(ApplicationRefusal.SubmitterNotAllowed), "submitter_not_allowed" },
        { nameof(ApplicationRefusal.ApproverNotAllowed), "approver_not_allowed" },
        { nameof(ApplicationRefusal.KeyOnlyOnServer), "key_only_on_server" },
        { nameof(ApplicationRefusal.ValueRefused), "value_refused" },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void TheCodeTheChainCarriesIsFixedAndNotDerivedFromTheNameOfTheMember(string refusal, string code) =>
        Assert.Equal(code, Enum.Parse<ApplicationRefusal>(refusal).CodeOf());

    [Fact]
    public void EveryDefinitiveRefusalHasACodeAndOnlyThoseHaveOne()
    {
        var definitive = Enum.GetValues<ApplicationRefusal>().Where(refusal => refusal.IsDefinitive()).ToList();

        Assert.Equal(5, definitive.Select(refusal => refusal.CodeOf()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, Codes().Count);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationRefusal.ChainBroken.CodeOf());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationRefusal.None.CodeOf());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationRefusal.FileUnreadable.CodeOf());
    }

    [Fact]
    public async Task AHostWithNoDatabaseRecordsNothingAndWritesNothingInTheLog()
    {
        using var service = Service(null);
        var recorded = await service.RecordAsync(TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (recorded, log.Lines.Count));
    }

    [Fact]
    public async Task ADatabaseThatCannotBeReachedRecordsNothingAndTheLogTellsOnlyTheTypeOfTheFailure()
    {
        using var service = Service(new ApprovedChanges(nowhere));
        var recorded = await service.RecordAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(0, recorded);
        Assert.StartsWith("Error: ", line, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx", line, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        loggers.Dispose();
        nowhere.Dispose();
        log.Dispose();
    }

    private ConfigurationRefusals Service(ApprovedChanges? changes)
    {
        var entries = new[] { new EffectiveEntry("Session:IdleTimeout", "00:20:00", ConfigurationOrigin.File) };
        return new ConfigurationRefusals(changes, null, entries, loggers);
    }
}
