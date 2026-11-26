using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// No path that writes an item puts in the column of the pseudonymized text a text that did not go through the pseudonymization (issue 102, CA-4): the submission of the host writes every
/// item through the swap, with its map, and the writer, the rerun and the emptying each refuse the sanitized text put where the pseudonymized one goes, with no swap and no map.
/// </summary>
[Trait("Category", "Database")]
public sealed class PseudonymizedWritingPathsTests
{
    private const string Pseudonymized =
        "SELECT count(*) FILTER (WHERE pseudonymized_text IS NOT NULL) || ',' || (SELECT count(*) FROM content.reversal_map) || ',' || count(*) FROM content.item_text";
    private const string Sealed = "SELECT string_agg(encode(pseudonymized_text, 'base64'), ',' ORDER BY id) FROM content.item_text";
    private const string Sanitized = "SELECT (sanitized_text IS NOT NULL)::text FROM content.item_text ORDER BY id LIMIT 1";
    private const string SanitizedAsPseudonymized = "UPDATE content.item_text SET pseudonymized_text = sanitized_text WHERE id = @item";
    private const string Resanitized = "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (true, 'item_text', @item, 'item_resanitized', 'sanitization_replaced', repeat('0a', 32))";
    private const string Emptied = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (@person, false, 'item_text', @item, 'item_emptied', 'contract_requirement', 'zqx-1')";

    private const string ContentKept = "CE004";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task EveryWritingPathRefusesTextThatWasNotPseudonymized()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var batch = (await world.SubmitAsync(SubmissionWorld.Request())).Batch!.Value;
        var submitted = Assert.Single(await world.StoredAsync(Pseudonymized));
        var swapped = Assert.Single(await world.StoredAsync(Sealed));
        var item = long.Parse(Assert.Single(await world.StoredAsync("SELECT min(id)::text FROM content.item_text")), CultureInfo.InvariantCulture);
        var notSwapped = new NewItemText("texto bruto zqx", "texto sanitizado zqx", []) { Sanitization = ItemTexts.Sanitization("texto sanitizado zqx") };

        _ = await Assert.ThrowsAsync<PseudonymizedRefusedException>(() => new ContentWriter(world.DataSource, world.Content).AddItemTextAsync(SubmissionWorld.Domain, batch, notSwapped, Token));
        // The rerun and the emptying reach only items of an accepted batch: the row policy hides the items of an open one, and an update that meets no row refuses nothing.
        await world.AcceptReviewedAsync(batch);
        var rerun = await Assert.ThrowsAsync<PostgresException>(() => InOneTransactionAsync(world, item, Resanitized, SanitizedAsPseudonymized));
        var emptying = await Assert.ThrowsAsync<PostgresException>(() => InOneTransactionAsync(world, item, Emptied, SanitizedAsPseudonymized));

        // The item updated, the first one, holds its sanitized and its pseudonymized texts each sealed on its own, so the update changes the column: the rerun with no conclusion in the queue
        // and the emptying both meet the guard that lets the texts of a row change only with a rerun; after both, every item holds the pseudonymized text the swap gave it.
        Assert.Equal(("2,2,2", "true"), (submitted, Assert.Single(await world.StoredAsync(Sanitized))));
        Assert.Equal((ContentKept, ContentKept), (rerun.SqlState, emptying.SqlState));
        Assert.All([rerun, emptying], refused => Assert.Contains("only with the event of its rerun and its conclusion in the queue", refused.MessageText, StringComparison.Ordinal));
        Assert.Equal(swapped, Assert.Single(await world.StoredAsync(Sealed)));
    }

    /// <summary>Runs the statements in one transaction of the owner, in the domain of the tests, and commits, so that a check put off to the commit runs too.</summary>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The statements are constants of the tests, never input.")]
    private static async Task InOneTransactionAsync(SubmissionWorld world, long item, params string[] statements)
    {
        await using var connection = new NpgsqlConnection(world.Database.OwnerConnectionString);
        await connection.OpenAsync(Token);
        await using var transaction = await connection.BeginTransactionAsync(Token);
        await using var command = new NpgsqlCommand("SELECT set_config('app.domain', @domain, true)", connection, transaction);
        _ = command.Parameters.AddWithValue("domain", SubmissionWorld.Domain.ToString(CultureInfo.InvariantCulture));
        _ = await command.ExecuteScalarAsync(Token);
        _ = command.Parameters.AddWithValue("item", item);
        _ = command.Parameters.AddWithValue("person", SubmissionWorld.Person);
        foreach (var statement in statements)
        {
            command.CommandText = statement;
            _ = await command.ExecuteNonQueryAsync(Token);
        }

        await transaction.CommitAsync(Token);
    }
}
