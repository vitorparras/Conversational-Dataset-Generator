using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Tests.Support.Custody;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to rotate the label of a domain (issue 56, part 1), as its own process over a database of its own and with the owner credential: with the labels of the
/// next generation in the custody file it passes the domain to them and writes the event; with one missing it names it and exits with the code of the missing label.
/// Only the exit code, the words and the rows are looked at, never a key.
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainLabelProcessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);
    private static readonly string[] Everything =
    [
        "texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1", "texto-bruto/1/v2", "mapa-reversao/1/v2",
    ];

    [DatabaseFact]
    public async Task Ca1TheCommandPassesTheDomainToTheNextGenerationAndLeavesTheOtherDomainAlone()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rotate-domain-label", "--domain=1", Everything));

        Assert.Equal((0, true), (exit, words.Contains("domínio 1 cifra sob a geração v2", StringComparison.Ordinal)));
        Assert.Equal(
            (6L, 2L, 1L, 0L),
            (await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"),
             await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label WHERE label LIKE '%/2/v%'"),
             await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_rotated' AND item_kind = 'domain' AND item_id = 1 AND reference = 'v2'"),
             await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_rotated' AND item_id = 2")));
    }

    [DatabaseFact]
    public async Task Ca3TheCommandWithALabelOfTheNextGenerationMissingNamesItExitsWithItsCodeAndWritesNothing()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rotate-domain-label", "--domain=1", [.. Everything.Where(label => label != "mapa-reversao/1/v2")]));

        Assert.Equal((17, true, false), (exit, words.Contains("mapa-reversao/1/v2", StringComparison.Ordinal), words.Contains("texto-bruto/1/v2", StringComparison.Ordinal)));
        Assert.Equal((4L, 2L), (await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"), await ScalarAsync(database, "SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseFact]
    public async Task TheCommandForADomainThatDoesNotExistExitsWithTheRefusalCodeAndWritesNothing()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rotate-domain-label", "--domain=9", Everything));

        Assert.Equal((24, true), (exit, words.Contains("não há domínio", StringComparison.Ordinal)));
        Assert.Equal(4L, await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"));
    }

    [DatabaseFact]
    public async Task Ca4TheCommandRevokesAnOlderGenerationAndRefusesTheGenerationInUseWithItsOwnCode()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();
        var (rotation, _) = await RunAsync(database, Arguments(files, "rotate-domain-label", "--domain=1", Everything));
        Assert.Equal(0, rotation);

        var (revoked, saidRevoked) = await RunAsync(database, Arguments(files, "revoke-domain-label", "--label=texto-bruto/1/v1", Everything));
        var (inUse, saidInUse) = await RunAsync(database, Arguments(files, "revoke-domain-label", "--label=texto-bruto/1/v2", Everything));
        var (again, saidAgain) = await RunAsync(database, Arguments(files, "revoke-domain-label", "--label=texto-bruto/1/v1", Everything));

        Assert.Equal(
            (0, true, 37, true, 37, true),
            (revoked, saidRevoked.Contains("texto-bruto/1/v1 revogado no domínio 1", StringComparison.Ordinal),
             inUse, saidInUse.Contains("rotacione antes", StringComparison.Ordinal),
             again, saidAgain.Contains("já foi revogado", StringComparison.Ordinal)));
        Assert.Equal(
            (1L, 1L),
            (await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label_revocation"),
             await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'domain_label_revoked' AND item_id = 1 AND reference = 'texto-bruto/1/v1'")));
    }

    [DatabaseFact]
    public async Task TheCommandToRevokeALabelThatIsNotRegisteredExitsWithTheRefusalCodeAndWritesNothing()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "revoke-domain-label", "--label=texto-bruto/9/v1", Everything));

        Assert.Equal((37, true), (exit, words.Contains("nenhum rótulo registrado", StringComparison.Ordinal)));
        Assert.Equal((0L, 2L), (await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label_revocation"), await ScalarAsync(database, "SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseFact]
    public async Task TheCommandToRevokeWithoutItsOptionRefusesWithTheConfigurationCode()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "revoke-domain-label", null, Everything));

        Assert.Equal((78, true), (exit, words.Contains("--label", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task TheRewrapOfADomainWithNoRowsSaysSoAndItsStatusCountsNothing()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (rewrap, saidRewrap) = await RunAsync(database, Arguments(files, "rewrap-domain", "--domain=1", Everything));
        var (status, saidStatus) = await RunAsync(database, Arguments(files, "rewrap-domain", "--domain=1", Everything, "--status"));

        Assert.Equal(
            (0, true, 0, true),
            (rewrap, saidRewrap.Contains("0 chave(s) do domínio 1", StringComparison.Ordinal),
             status, saidStatus.Contains("0 chave(s) na geração mais nova não revogada, 0 por reembrulhar", StringComparison.Ordinal)));
        Assert.Equal(0L, await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'domain_rewrapped'"));
    }

    [DatabaseFact]
    public async Task TheRewrapOfADomainThatIsNotAPositiveIntegerExitsWithTheRefusalCode()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rewrap-domain", "--domain=0", Everything));

        Assert.Equal((39, true), (exit, words.Contains("inteiro positivo", StringComparison.Ordinal)));
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheRewrapOrTheStatusOfADomainThatDoesNotExistExitsWithTheRefusalCodeNamesItAndWritesNothing(bool status)
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();
        var chainBefore = await ScalarAsync(database, "SELECT count(*) FROM public.custody_event");

        var (exit, words) = await RunAsync(database, Arguments(files, "rewrap-domain", "--domain=99", Everything, status ? "--status" : null));

        Assert.Equal((39, true, chainBefore), (exit, words.Contains("99", StringComparison.Ordinal), await ScalarAsync(database, "SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseFact]
    public async Task TheRewrapWithTheNewLabelRegisteredAndMissingFromTheFileExitsWithTheRefusalCodeNamesItAndWritesNothing()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();
        await AddRowUnderTheFirstGenerationAsync(database);
        var (rotation, _) = await RunAsync(database, Arguments(files, "rotate-domain-label", "--domain=1", Everything));
        Assert.Equal(0, rotation);
        var chainBefore = await ScalarAsync(database, "SELECT count(*) FROM public.custody_event");

        var (exit, words) = await RunAsync(database, Arguments(files, "rewrap-domain", "--domain=1", [.. Everything.Where(label => label != "texto-bruto/1/v2")]));

        Assert.Equal((39, true, chainBefore), (exit, words.Contains("texto-bruto/1/v2", StringComparison.Ordinal), await ScalarAsync(database, "SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseFact]
    public async Task TheRewrapWithoutItsOptionRefusesWithTheConfigurationCode()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rewrap-domain", null, Everything, "--status"));

        Assert.Equal((78, true), (exit, words.Contains("--domain", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task TheCommandWithoutItsOptionRefusesWithTheConfigurationCodeBeforeAnythingElse()
    {
        await using var database = await WithTwoDomainsAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "rotate-domain-label", null, Everything));

        Assert.Equal((78, true), (exit, words.Contains("--domain", StringComparison.Ordinal)));
        Assert.Equal(4L, await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"));
    }

    private static async Task<ScratchDatabase> WithTwoDomainsAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var owner = NpgsqlDataSource.Create(database.OwnerConnectionString);
        var creation = new DomainCreation(owner);
        var both = DerivationLabels.Of(Everything);
        _ = await creation.CreateAsync("zqx-domain-a", both, TestContext.Current.CancellationToken);
        _ = await creation.CreateAsync("zqx-domain-b", both, TestContext.Current.CancellationToken);
        return database;
    }

    private static async Task AddRowUnderTheFirstGenerationAsync(ScratchDatabase database)
    {
        var first = new[] { "texto-bruto/1/v1", "mapa-reversao/1/v1" };
        using var reading = MasterKeyReading.FromBytes(
            CustodyFiles.SyntheticKey(), DerivationLabels.Of(first), CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = CustodyFiles.SyntheticEpoch(1) }), "test");
        var keys = new ItemKeys(new KeyDerivation(reading.Key!, reading.Labels), reading.Epochs);
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        await SeededBatches.SeedAsync(database, 1);
        var writer = new SeededWriter(new ContentWriter(app, new ProtectedContent(reading.Labels, keys, new HashSet<string>(first, StringComparer.Ordinal))));
        _ = await writer.AddItemTextAsync(1, new NewItemText("zqx-texto"), TestContext.Current.CancellationToken);
    }

    private static string[] Arguments(CustodyFiles files, string mode, string? option, string[] labels, string? flag = null)
    {
        var withKey = new[]
        {
            mode,
            $"--MasterKey:File={files.Write("key", System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(CustodyFiles.SyntheticKey()) + "\n"))}",
            $"--MasterKey:LabelsFile={files.WriteText("labels", string.Concat(labels.Select(label => label + "\n")))}",
            $"--MasterKey:EpochsFile={files.WriteText("epochs", $"1:{Convert.ToBase64String(CustodyFiles.SyntheticEpoch(1))}\n")}",
        };
        var given = new[] { option, flag }.OfType<string>();
        return [.. withKey, .. given];
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<long> ScalarAsync(ScratchDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ScratchDatabase database, string[] arguments)
    {
        var owner = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString);
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host"))
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        _ = info.Environment.Remove("STRYKER_COVERAGE_FILE");
        _ = info.Environment.Remove("STRYKER_COVERAGE_EPOCH_FILE");
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["CDG_Database__Host"] = $"{owner.Host}:{owner.Port}";
        info.Environment["CDG_Database__Name"] = owner.Database;
        info.Environment["CDG_Database__Username"] = owner.Username;
        info.Environment["CDG_Database__Password"] = owner.Password;
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The host process did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Limit);
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }
}
