using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Content;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A migrated database of its own with two domains and two people, and the paths that write and read content and people as the
/// application's role, for the tests that set the server's log against the chain.
/// </summary>
internal sealed class FidelityWorld : IAsyncDisposable
{
    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    private readonly ScratchDatabase database;
    private readonly SyntheticCustody custody;

    private FidelityWorld(ScratchDatabase database, NpgsqlDataSource dataSource, SyntheticCustody custody)
    {
        this.database = database;
        DataSource = dataSource;
        this.custody = custody;
        RealWriter = new ContentWriter(dataSource, custody.Content);
        Writer = new SeededWriter(RealWriter);
        Reader = new ProtectedContentReader(dataSource, custody.Content, new CustodyChain(dataSource));
        Registry = new PersonRegistry(dataSource);
    }

    /// <summary>The connection string of the owner of the database, which the copy of the data is read with.</summary>
    public string OwnerConnectionString => database.OwnerConnectionString;

    /// <summary>The data source of the application's role, which the comparison of the server's log with the chain connects through.</summary>
    public NpgsqlDataSource DataSource { get; }

    /// <summary>The writer as the system has it, which adds content only to a batch that is open; <see cref="Writer" /> adds into a batch accepted at creation.</summary>
    public ContentWriter RealWriter { get; }

    public SeededWriter Writer { get; }

    public ProtectedContentReader Reader { get; }

    public PersonRegistry Registry { get; }

    /// <param name="seeded">Whether the two domains and the two people are put in; a database that never held anything is the one a restore loads into.</param>
    public static async Task<FidelityWorld> CreateAsync(bool seeded = true)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        if (seeded)
        {
            await database.RunAsOwnerAsync(
                "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação'), ('zqx-gone', 'Anotação')",
                "UPDATE public.person SET active = false WHERE name = 'zqx-gone'");
            await SeededBatches.SeedAsync(database, 1, 2);
        }

        var custody = SyntheticCustody.Holding(Labels);
        return new FidelityWorld(database, NpgsqlDataSource.Create(database.AppConnectionString), custody);
    }

    /// <summary>The conference of the guarantees over the key the content is sealed with.</summary>
    public GuaranteeVerifier Verifier() => VerifierOver(new SyntheticKeySource(Labels));

    /// <summary>The conference of the guarantees over the source of the key given, wired as the composition root wires it.</summary>
    public GuaranteeVerifier VerifierOver(IMasterKeySource source) =>
        new(DataSource, source.ReadAsync, reading => new ProtectedContentReader(
            DataSource, new ProtectedContent(reading.Labels, new ItemKeys(new KeyDerivation(reading.Key!, reading.Labels), reading.Epochs), new HashSet<string>()), new CustodyChain(DataSource)));

    /// <summary>Runs the migration over the database with the owner credential, as the migration mode does.</summary>
    public Task<MigrationResult> MigrateAsync(IReadOnlyList<MigrationStep> steps) => database.MigrateAsync(steps);

    /// <summary>Runs the statements as the owner of the database.</summary>
    public Task RunAsOwnerAsync(params string[] statements) => database.RunAsOwnerAsync(statements);

    /// <summary>Runs the statement as the superuser of the server, which no row policy holds.</summary>
    public Task RunAsSuperuserAsync(string statement) => database.ExecuteAsSuperuserAsync(statement);

    /// <summary>The first column of the query, read as the owner.</summary>
    public Task<IReadOnlyList<string>> OwnerColumnAsync(string query) => database.FirstColumnAsOwnerAsync(query);

    /// <summary>Makes the first person the curator of the collection, as the owner would; the owner is not the role the comparison looks at.</summary>
    public Task MakeCuratorAsync() => database.RunAsOwnerAsync("UPDATE public.person SET function_name = '" + PersonFunctions.Curation + "' WHERE name = 'zqx-active'");

    /// <summary>The store of passwords, writing as the application's role and deriving with the work factor given.</summary>
    public PersonCredentials Credentials(int workFactor) => new(DataSource, new PasswordDerivation(workFactor));

    /// <summary>The first value of the first row of the statement, as the application and under the domain.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    public async Task<object?> InDomainAsync(int domainId, string statement, params (string Name, object Value)[] parameters)
    {
        await using var transaction = await ContentTransaction.BeginAsync(DataSource, domainId, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }

    /// <summary>Opens a batch in the first domain, by the first person, and puts two items in it and the map of the first, as the system does; the batch stays open. The batch is the third.</summary>
    public async Task LeaveABatchOpenAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await new IngestionBatches(DataSource, TestConditions.Known).OpenAsync(new BatchOpening(1, 1), token);
        Assert.Equal((BatchOutcome.Opened, 3L), (opened.Outcome, opened.BatchId));
        var first = await RealWriter.AddItemTextAsync(1, 3, new NewItemText("texto do lote aberto zqx"), token);
        _ = await RealWriter.AddItemTextAsync(1, 3, new NewItemText("outro texto do lote aberto zqx"), token);
        _ = await RealWriter.AddReversalMapAsync(1, new NewReversalMap(3, first, "mapa do lote aberto zqx"), token);
    }

    /// <summary>The first value of the first row of the statement, as the application under the domain and in the scope of the system, which sees every batch.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    public static async Task<object?> InSystemScopeAsync(NpgsqlDataSource source, int domainId, string statement)
    {
        await using var transaction = await ContentTransaction.BeginSystemAsync(source, domainId, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        custody.Dispose();
        await database.DisposeAsync();
    }
}
