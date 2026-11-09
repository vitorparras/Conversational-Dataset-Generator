using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The marking of the approved changes as in force at the start (#634, part 4; decision 0053), with no database: a host with no database marks nothing and is not an error; a database that cannot
/// be reached writes nothing and is told in the log by the type of the failure alone, with no value of a key and no address, and the start goes on.
/// </summary>
public sealed class ConfigurationInForceTests : IDisposable
{
    private readonly RecordedLog log = new();
    private readonly NpgsqlDataSource nowhere = NpgsqlDataSource.Create("Host=zqx-nenhum-6340.invalid;Database=zqx;Username=zqx;Password=ZQX-SENHA-6340;Timeout=1");

    [Fact]
    public async Task AHostWithNoDatabaseMarksNothingAndWritesNothingInTheLog()
    {
        var marked = await Service(changes: null).RecordAsync(TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (marked, log.Lines.Count));
    }

    [Fact]
    public async Task ADatabaseThatCannotBeReachedMarksNothingAndTheLogTellsOnlyTheTypeOfTheFailure()
    {
        var marked = await Service(new ApprovedChanges(nowhere)).RecordAsync(TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(0, marked);
        Assert.StartsWith("Error: ", line, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx", line, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        nowhere.Dispose();
        log.Dispose();
    }

    private ConfigurationInForce Service(ApprovedChanges? changes)
    {
        var entries = new[] { new EffectiveEntry("Session:IdleTimeout", "00:20:00", ConfigurationOrigin.File) };
        return new ConfigurationInForce(changes, entries, LoggerFactory.Create(builder => builder.AddProvider(log)));
    }
}
