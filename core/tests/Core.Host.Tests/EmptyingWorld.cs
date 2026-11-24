using System.Diagnostics.CodeAnalysis;
using Core.Host.Endpoints;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>A migrated database of its own with two domains, a curator, an annotator, an auditor and one item with content in the first domain, put in by the owner.</summary>
internal sealed class EmptyingWorld : IAsyncDisposable
{
    internal const long Item = 1;

    private static readonly string WithContent =
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped, sanitization_version, sanitization_trail, "
        + $"sanitization_counts) VALUES (1, 1, '\x01', '\x02', '\x05', 1, '\x03', '{ItemTexts.Version}', '\x04', '{{0}}')";

    // An item holds a pseudonymized text only with a map of its own written in the same transaction, which the batch of statements is.
    private const string MapOfTheItem =
        "INSERT INTO content.reversal_map (domain_id, batch_id, item_id, entries, dek_epoch, dek_wrapped) SELECT domain_id, batch_id, id, '\x06', 1, '\x07' FROM content.item_text";

    private const string PersonOf = "SELECT id FROM public.person WHERE name = @name";

    private readonly ScratchDatabase database;

    private EmptyingWorld(ScratchDatabase database, NpgsqlDataSource source, long[] people)
    {
        this.database = database;
        Source = source;
        Desk = ItemEmptyingDesk.On(source);
        Curator = people[0];
        Annotator = people[1];
        Auditor = people[2];
    }

    internal NpgsqlDataSource Source { get; }

    internal ItemEmptyingDesk Desk { get; }

    internal long Curator { get; }

    internal long Annotator { get; }

    internal long Auditor { get; }

    internal static async Task<EmptyingWorld> CreateAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-annotator', 'Anotação'), ('zqx-curator', 'Curadoria do acervo'), ('zqx-auditor', 'Auditoria')");
        await database.RunAsOwnerAsync(
        [
            .. SeededBatches.Statements(1, 2), "SELECT set_config('app.domain', '1', false)", ItemTexts.VersionRow(1), WithContent, MapOfTheItem,
            "SELECT set_config('app.domain', '', false)",
        ]);
        long[] people =
        [
            await PersonIdAsync(database, "zqx-curator"),
            await PersonIdAsync(database, "zqx-annotator"),
            await PersonIdAsync(database, "zqx-auditor"),
        ];
        return new EmptyingWorld(database, NpgsqlDataSource.Create(database.AppConnectionString), people);
    }

    public async ValueTask DisposeAsync()
    {
        await Source.DisposeAsync();
        await database.DisposeAsync();
    }

    internal Task<EmptyingResult> EmptyAsync(long person, EmptyingMotive motive, string protocol, int domain = 1) =>
        Desk.EmptyAsync(person, new ItemEmptyingDesk.EmptyingRequest(domain, Item, motive, protocol), TestContext.Current.CancellationToken);

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal, never input.")]
    private static async Task<long> PersonIdAsync(ScratchDatabase database, string name)
    {
        await using var connection = new NpgsqlConnection(database.OwnerConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(PersonOf, connection);
        _ = command.Parameters.AddWithValue("name", name);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
