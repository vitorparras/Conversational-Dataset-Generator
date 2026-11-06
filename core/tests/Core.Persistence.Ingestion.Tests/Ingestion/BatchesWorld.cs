using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>A migrated database of its own with two domains and three people (one active, one who is not and a curator), and the application's credential over it.</summary>
internal sealed class BatchesWorld : IAsyncDisposable
{
    private BatchesWorld(ScratchDatabase database, NpgsqlDataSource dataSource)
    {
        Database = database;
        DataSource = dataSource;
    }

    public ScratchDatabase Database { get; }

    public NpgsqlDataSource DataSource { get; }

    public static async Task<BatchesWorld> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação'), ('zqx-gone', 'Anotação'), ('zqx-curator', 'Curadoria do acervo')",
            "UPDATE public.person SET active = false WHERE name = 'zqx-gone'");
        return new BatchesWorld(database, NpgsqlDataSource.Create(database.AppConnectionString));
    }

    public async Task<long> PersonAsync(string name) => (long)(await ScalarAsync("SELECT id FROM public.person WHERE name = @name", ("name", name)))!;

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

    /// <summary>The first value of the first row of the statement, as the application and under the domain.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    public async Task<object?> InDomainAsync(int domainId, string statement)
    {
        await using var transaction = await ContentTransaction.BeginAsync(DataSource, domainId, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(statement);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return result is DBNull ? null : result;
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await Database.DisposeAsync();
    }
}
