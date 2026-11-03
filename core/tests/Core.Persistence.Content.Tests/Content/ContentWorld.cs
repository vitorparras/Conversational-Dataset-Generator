using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>A migrated database of its own with two domains, a person who is active and one who is not, and the path of protected content over a synthetic key.</summary>
internal sealed class ContentWorld : IAsyncDisposable
{
    private readonly SyntheticCustody custody;

    private ContentWorld(ScratchDatabase database, NpgsqlDataSource dataSource, SyntheticCustody custody)
    {
        Database = database;
        DataSource = dataSource;
        this.custody = custody;
        Writer = new ContentWriter(dataSource, custody.Content);
        Reader = new ProtectedContentReader(dataSource, custody.Content, new CustodyChain(dataSource));
    }

    public ScratchDatabase Database { get; }

    public NpgsqlDataSource DataSource { get; }

    public ContentWriter Writer { get; }

    public ProtectedContentReader Reader { get; }

    public ContentEmptier Emptier => new(DataSource);

    /// <summary>The labels the world holds, which a custody of other epochs shares.</summary>
    public static IReadOnlyList<string> Labels { get; } = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    /// <summary>The keys of the rows, to unwrap what the world stored.</summary>
    public ItemKeys Keys => custody.Keys;

    /// <summary>A reader of the world's database that opens content with the custody given instead of the world's own.</summary>
    public ProtectedContentReader ReaderWith(SyntheticCustody other) => new(DataSource, other.Content, new CustodyChain(DataSource));

    public static async Task<ContentWorld> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação'), ('zqx-gone', 'Anotação'), ('zqx-curator', 'Curadoria do acervo')",
            "UPDATE public.person SET active = false WHERE name = 'zqx-gone'");
        var custody = SyntheticCustody.Holding(Labels);
        return new ContentWorld(database, NpgsqlDataSource.Create(database.AppConnectionString), custody);
    }

    /// <summary>Two events in the chain, one of them changed outside the system, and a verification that failed and was recorded: from then on the chain is broken.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal, never input.")]
    public async Task BreakTheChainAsync()
    {
        var chain = new CustodyChain(DataSource);
        foreach (var item in new long[] { 1, 2 })
        {
            var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
            Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        }

        var name = new NpgsqlConnectionStringBuilder(Database.AppConnectionString).Database;
        var connectionString = new NpgsqlConnectionStringBuilder((await PostgresFixture.SharedAsync()).SuperuserConnectionString) { Database = name, Pooling = false }.ConnectionString;
        await using (var connection = await Sql.OpenAsync(connectionString))
        {
            await using var command = new NpgsqlCommand(
                "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 1; "
                + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed",
                connection);
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var failed = await new CustodyVerifier(DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, failed.FirstInconsistent?.Sequence);
    }

    /// <summary>The identifier of the person with the name given, as the owner.</summary>
    public async Task<long> PersonAsync(string name) =>
        (long)(await ScalarAsync("SELECT id FROM public.person WHERE name = @name", ("name", name)))!;

    /// <summary>The sealed bytes the column holds for the row, read as the application under the domain, without opening them.</summary>
    public async Task<byte[]> StoredAsync(int domainId, string statement, long id)
    {
        await using var transaction = await ContentTransaction.BeginAsync(DataSource, domainId, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        _ = command.Parameters.AddWithValue("id", id);
        return (byte[])(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The first value of the first row of the statement, as the application and under the domain.</summary>
    public Task<object?> InDomainAsync(int domainId, string statement, params (string Name, object Value)[] parameters) =>
        InDomainAsync(Database.AppConnectionString, domainId, statement, parameters);

    /// <summary>The first value of the first row of the statement, as the owner of the database and under the domain; the owner is held to the row policy too.</summary>
    public Task<object?> OwnerInDomainAsync(int domainId, string statement, params (string Name, object Value)[] parameters) =>
        InDomainAsync(Database.OwnerConnectionString, domainId, statement, parameters);

    /// <summary>The first value of the first row of the statement, as the owner.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    public async Task<object?> ScalarAsync(string statement, params (string Name, object Value)[] parameters)
    {
        await using var connection = await Sql.OpenAsync(Database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    private static async Task<object?> InDomainAsync(string connectionString, int domainId, string statement, (string Name, object Value)[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        custody.Dispose();
        await Database.DisposeAsync();
    }
}
