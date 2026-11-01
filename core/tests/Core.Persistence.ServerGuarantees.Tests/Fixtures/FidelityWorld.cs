using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Content;
using Npgsql;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A migrated database of its own with two domains and two people, and the paths that write and read content and people as the
/// application's role, for the tests that set the server's log against the chain.
/// </summary>
internal sealed class FidelityWorld : IAsyncDisposable
{
    private readonly ScratchDatabase database;
    private readonly SyntheticCustody custody;

    private FidelityWorld(ScratchDatabase database, NpgsqlDataSource dataSource, SyntheticCustody custody)
    {
        this.database = database;
        DataSource = dataSource;
        this.custody = custody;
        Writer = new ContentWriter(dataSource, custody.Content);
        Reader = new ProtectedContentReader(dataSource, custody.Content, new CustodyChain(dataSource));
        Registry = new PersonRegistry(dataSource);
    }

    /// <summary>The data source of the application's role, which the comparison of the server's log with the chain connects through.</summary>
    public NpgsqlDataSource DataSource { get; }

    public ContentWriter Writer { get; }

    public ProtectedContentReader Reader { get; }

    public PersonRegistry Registry { get; }

    public static async Task<FidelityWorld> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação'), ('zqx-gone', 'Anotação')",
            "UPDATE public.person SET active = false WHERE name = 'zqx-gone'");
        var custody = SyntheticCustody.Holding("texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1");
        return new FidelityWorld(database, NpgsqlDataSource.Create(database.AppConnectionString), custody);
    }

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

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        custody.Dispose();
        await database.DisposeAsync();
    }
}
