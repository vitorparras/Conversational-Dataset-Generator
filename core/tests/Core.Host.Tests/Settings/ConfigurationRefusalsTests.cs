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

    public static TheoryData<ApplicationRefusal, bool> Kinds() => new()
    {
        { ApplicationRefusal.SameAuthor, true },
        { ApplicationRefusal.SubmitterNotAllowed, true },
        { ApplicationRefusal.ApproverNotAllowed, true },
        { ApplicationRefusal.KeyOnlyOnServer, true },
        { ApplicationRefusal.ValueRefused, true },
        { ApplicationRefusal.None, false },
        { ApplicationRefusal.ChainBroken, false },
        { ApplicationRefusal.FileUnreadable, false },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ARefusalThatLastsIsTheOnlyOneThatIsRecorded(ApplicationRefusal refusal, bool definitive) =>
        Assert.Equal(definitive, refusal.IsDefinitive());

    [Fact]
    public void EveryDefinitiveRefusalHasACodeTheChainTakes()
    {
        var codes = Enum.GetValues<ApplicationRefusal>().Where(refusal => refusal.IsDefinitive()).Select(refusal => refusal.CodeOf()).ToList();

        Assert.Equal(5, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[a-z_]{1,40}$", code));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationRefusal.ChainBroken.CodeOf());
    }

    [Fact]
    public async Task AHostWithNoDatabaseRecordsNothingAndWritesNothingInTheLog()
    {
        var recorded = await Service(null).RecordAsync(TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (recorded, log.Lines.Count));
    }

    [Fact]
    public async Task ADatabaseThatCannotBeReachedRecordsNothingAndTheLogTellsOnlyTheTypeOfTheFailure()
    {
        var recorded = await Service(new ApprovedChanges(nowhere)).RecordAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(0, recorded);
        Assert.StartsWith("Error: ", line, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx", line, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        nowhere.Dispose();
        log.Dispose();
    }

    private ConfigurationRefusals Service(ApprovedChanges? changes)
    {
        var entries = new[] { new EffectiveEntry("Session:IdleTimeout", "00:20:00", ConfigurationOrigin.File) };
        return new ConfigurationRefusals(changes, null, entries, LoggerFactory.Create(builder => builder.AddProvider(log)));
    }
}
