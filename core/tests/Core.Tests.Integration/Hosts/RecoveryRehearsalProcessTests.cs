using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Tests.Support.Custody;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to rehearse the recovery (issue 67, CA-1), as its own process over a copy of its own and the key of the custody files: it opens a sample of the raw text and of the map of
/// every domain, prints a receipt that reads back and carries no content, and writes nothing to the chain; with a key that opens nothing it prints no receipt and exits with the refusal.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecoveryRehearsalProcessTests
{
    private const string Raw = "texto bruto sintetico zqx-9931";
    private const string Entries = "mapa de reversao sintetico zqx-9931";
    private const string Events = "SELECT count(*) FROM public.custody_event";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);
    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    [DatabaseFact]
    public async Task Ca1TheCommandOpensTheSamplesPrintsAReceiptThatReadsBackWithTheTopOfTheCopyAndNoContentAndWritesNothing()
    {
        await using var database = await CopyWithContentAsync();
        using var files = new CustodyFiles();
        var before = await ScalarAsync(database, Events);

        var (exit, words) = await RunAsync(database, Arguments(files, CustodyFiles.SyntheticKey()));

        var receipt = RecoveryReceipt.Read(words.Split('\n').Single(line => line.StartsWith("CDG-ENSAIO-1|", StringComparison.Ordinal))).Receipt!;
        Assert.Equal((0, new SampleCounts(2, 2, 0), before), (exit, receipt.Counts, receipt.TopSequence));
        Assert.DoesNotContain("zqx-9931", words, StringComparison.Ordinal);
        Assert.Equal(before, await ScalarAsync(database, Events));
    }

    [DatabaseFact]
    public async Task Ca1TheCommandWithAKeyThatOpensNothingPrintsNoReceiptAndExitsWithTheRefusalCode()
    {
        await using var database = await CopyWithContentAsync();
        using var files = new CustodyFiles();
        byte[] wrong = [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(index + 1))];

        var (exit, words) = await RunAsync(database, Arguments(files, wrong));

        Assert.Equal((29, false, true), (exit, words.Contains("CDG-ENSAIO-1", StringComparison.Ordinal), words.Contains("4 não abriram", StringComparison.Ordinal)));
    }

    private static string[] Arguments(CustodyFiles files, byte[] key) =>
        [
            "rehearse-recovery",
            $"--MasterKey:File={files.Write("key", Encoding.ASCII.GetBytes(Convert.ToBase64String(key) + "\n"))}",
            $"--MasterKey:LabelsFile={files.WriteText("labels", string.Concat(Labels.Select(label => label + "\n")))}",
            $"--MasterKey:EpochsFile={files.WriteText("epochs", CustodyFiles.EpochLine)}",
        ];

    /// <summary>A database with two domains, each with a raw text and a map sealed under the key of the custody files, and so a chain with events.</summary>
    private static async Task<ScratchDatabase> CopyWithContentAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using (var owner = NpgsqlDataSource.Create(database.OwnerConnectionString))
        {
            var creation = new DomainCreation(owner);
            _ = await creation.CreateAsync("zqx-domain-a", DerivationLabels.Of(Labels), TestContext.Current.CancellationToken);
            _ = await creation.CreateAsync("zqx-domain-b", DerivationLabels.Of(Labels), TestContext.Current.CancellationToken);
        }

        var labels = DerivationLabels.Of(Labels);
        using var reading = MasterKeyReading.FromBytes(
            CustodyFiles.SyntheticKey(), labels, CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = CustodyFiles.SyntheticEpoch(1) }), "teste");
        await using var app = NpgsqlDataSource.Create(database.AppConnectionString);
        await SeededBatches.SeedAsync(database, 1, 2);
        var keys = new ItemKeys(new KeyDerivation(reading.Key!, labels), reading.Epochs);
        var writer = new SeededWriter(new ContentWriter(app, new ProtectedContent(labels, keys, new HashSet<string>(Labels, StringComparer.Ordinal))));
        foreach (var domain in (int[])[1, 2])
        {
            var item = await writer.AddItemTextAsync(domain, ItemTexts.Of(Raw), TestContext.Current.CancellationToken);
            _ = await writer.AddReversalMapAsync(domain, item, Entries, TestContext.Current.CancellationToken);
        }

        return database;
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
