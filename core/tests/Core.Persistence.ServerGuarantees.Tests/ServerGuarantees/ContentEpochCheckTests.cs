using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// A row of content wrapped by an epoch that the epochs file lacks refuses the start and names the epoch, so a copy of the database restored from before a rotation fails closed
/// (decision 0049) instead of leaving the content under it to look unreadable.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentEpochCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new ContentEpochCheck(server.DataSource, EpochsSource.Providing(1));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("ContentEpochs", result, server);
    }

    [Fact]
    public async Task KeyNotReadRefusesWithoutAskingTheServer()
    {
        await using var server = new UnreachableServer();
        var check = new ContentEpochCheck(server.DataSource, EpochsSource.WithoutKey());

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("a chave-mestra e os arquivos que a acompanham não foram lidos", result.Message, StringComparison.Ordinal);
        Assert.Equal("fonte de teste", result.ExpectedSource);
    }

    [Fact]
    public void EpochsInUseAllProvidedPassAndCountThem()
    {
        using var provided = CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = Secret(1), [2] = Secret(2) });

        var finding = ContentEpochCheck.Judge(provided, [1, 2]);

        Assert.Equal((CheckStatus.Passed, true), (finding.Status, finding.Message.StartsWith("2 época(s)", StringComparison.Ordinal)));
    }

    [Fact]
    public void EpochInUseThatWasNotProvidedFailsNamingEveryOneMissing()
    {
        using var provided = CustodyEpochs.Of(new Dictionary<int, byte[]> { [2] = Secret(2) });

        var finding = ContentEpochCheck.Judge(provided, [3, 1, 2]);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ausente(s) do arquivo de épocas: 1, 3;", finding.Message, StringComparison.Ordinal);
        Assert.Contains("restaurado de uma cópia anterior a uma rotação", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckWithoutItsPartsIsADefect()
    {
        await using var server = new UnreachableServer();

        Assert.Equal(
            ["dataSource", "source"],
            [
                Assert.Throws<ArgumentNullException>(() => new ContentEpochCheck(null!, EpochsSource.WithoutKey())).ParamName,
                Assert.Throws<ArgumentNullException>(() => new ContentEpochCheck(server.DataSource, null!)).ParamName,
            ]);
    }

    [DatabaseFact]
    public async Task DatabaseWithNoContentPassesWhateverEpochsAreProvided()
    {
        await using var database = await MigratedAsync();

        var result = await CheckAsync(database, EpochsSource.Providing(5));

        Assert.Equal((CheckStatus.Passed, true), (result.Status, result.Message.StartsWith("0 época(s)", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task ContentOfEveryDomainWrappedByProvidedEpochsPasses()
    {
        await using var database = await MigratedAsync();
        await SeedAsync(database, (1, 1), (2, 2));

        var result = await CheckAsync(database, EpochsSource.Providing(1, 2));

        Assert.Equal((CheckStatus.Passed, true), (result.Status, result.Message.StartsWith("2 época(s)", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task CopyRestoredFromBeforeARotationFailsClosedNamingTheEpochTheFileLacks()
    {
        await using var database = await MigratedAsync();
        await SeedAsync(database, (1, 1), (2, 1));

        var result = await CheckAsync(database, EpochsSource.Providing(2));

        Assert.Equal((CheckStatus.Failed, "ContentEpochs"), (result.Status, result.Name));
        Assert.Contains("ausente(s) do arquivo de épocas: 1;", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task EmptiedRowsDoNotCountBecauseTheyHoldNoKey()
    {
        await using var database = await MigratedAsync();
        await SeedAsync(database, (1, 2));
        await database.RunAsOwnerAsync(
            "SELECT set_config('app.domain', '1', true)",
            "INSERT INTO content.item_text (domain_id, batch_id, emptied_at) VALUES (1, 1, now())");

        var result = await CheckAsync(database, EpochsSource.Providing(2));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    private static byte[] Secret(int number) => [.. Enumerable.Repeat((byte)number, MasterKey.Length)];

    private static async Task SeedAsync(ScratchDatabase database, params (int Domain, int Epoch)[] rows)
    {
        await database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-a'), ('zqx-b')");
        await SeededBatches.SeedAsync(database, 1, 2);
        foreach (var (domain, epoch) in rows)
        {
            await database.RunAsOwnerAsync(
                $"SELECT set_config('app.domain', '{domain}', true)",
                $"INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped) VALUES ({domain}, {domain}, '\\x01', {epoch}, '\\x01')");
        }
    }

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database, IMasterKeySource source) =>
        AppStartup.CheckAsync(database, dataSource => new ContentEpochCheck(dataSource, source));
}
