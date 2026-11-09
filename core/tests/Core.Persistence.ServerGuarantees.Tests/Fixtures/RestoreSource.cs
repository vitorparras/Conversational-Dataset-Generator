using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A database that holds what a restore puts back, made by the flows it has: two domains, two people (one deactivated), an event for each kind of author, a text and its map in one domain
/// and a text emptied in the other. The copy is each table as the array of its rows in the order of the key, as the 005 describes it.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class RestoreSource : IAsyncLifetime
{
    private FidelityWorld? world;

    /// <summary>Each table of the copy, by name, as the array of its rows.</summary>
    public IReadOnlyDictionary<string, string> Copy { get; private set; } = new Dictionary<string, string>();

    internal FidelityWorld World => world ?? throw new InvalidOperationException("The source was not started.");

    public async ValueTask InitializeAsync()
    {
        world = await FidelityWorld.CreateAsync();
        await world.RunAsOwnerAsync(
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES (1, 'person', 1, 'person_created', 'administrative_act')",
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES (1, 'person', 2, 'person_created', 'administrative_act')",
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES (1, 'person', 2, 'person_deactivated', 'administrative_act')",
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 3, 'verify_chain', 'verification_passed')");
        var token = TestContext.Current.CancellationToken;
        var first = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, first, "mapa sintetico zqx", token);
        var second = await world.Writer.AddItemTextAsync(2, new NewItemText("texto bruto sintetico zqx"), token);
        await world.RunAsOwnerAsync(
            "ALTER TABLE content.replication_occurrence DISABLE TRIGGER content_needs_open_batch",
            "ALTER TABLE content.replication_mark DISABLE TRIGGER content_needs_open_batch",
            "SELECT set_config('app.domain', '1', false)",
            "INSERT INTO content.replication_occurrence (domain_id, batch_id, item_id, message_position, text_digest, sender_digest, text_length, said_at) "
            + "VALUES (1, 1, 1, 0, repeat('a', 64), repeat('b', 64), 40, now()), (1, 1, 1, 1, repeat('a', 64), repeat('c', 64), 40, now())",
            "INSERT INTO content.replication_mark (domain_id, batch_id, item_id, message_position, first_item_id, first_message_position) VALUES (1, 1, 1, 0, 1, 0), (1, 1, 1, 1, 1, 0)",
            "SELECT set_config('app.domain', '', false)",
            "ALTER TABLE content.replication_occurrence ENABLE ALWAYS TRIGGER content_needs_open_batch",
            "ALTER TABLE content.replication_mark ENABLE ALWAYS TRIGGER content_needs_open_batch");
        Assert.Equal(PersonOutcome.Done, (await world.Registry.ChangeFunctionAsync(1, PersonFunctions.Curation, CustodyAuthor.System, token)).Outcome);
        var emptying = new ItemEmptying(CustodyAuthor.Person(1), 2, second, EmptyingMotive.RemovalRequestedBySubject, "PROT-2026/0042");
        Assert.Equal(EmptyingOutcome.Emptied, (await new ContentEmptier(world.DataSource).EmptyAsync(emptying, token)).Outcome);
        Assert.Equal(PersonOutcome.Done, (await world.Registry.ChangeFunctionAsync(1, "Anotação", CustodyAuthor.System, token)).Outcome);
        await world.RunAsOwnerAsync(
            "INSERT INTO public.derivation_label (label) VALUES ('zqx-label/1/v1')",
            "INSERT INTO public.derivation_label_revocation (label) VALUES ('zqx-label/1/v1')",
            "INSERT INTO public.person_credential (person_id, algorithm, work_factor, salt, derived) VALUES (1, 'zqx-algorithm', 1, decode('00112233445566778899aabbccddeeff', 'hex'), '\x0102')",
            "INSERT INTO public.person_change (person_id, kind, new_function, actor_id) VALUES (1, 'created', 'Anotação', NULL)",
            "INSERT INTO public.person_change (person_id, kind, actor_id) VALUES (2, 'deactivated', 1)");
        await world.RunAsOwnerAsync(
            "INSERT INTO public.sign_in_attempt (network) VALUES ('203.0.113.0/24')",
            "INSERT INTO public.sign_in_attempt (network) VALUES (NULL)");
        Copy = new Dictionary<string, string>
        {
            ["public.domain"] = await OwnerRowsAsync("public.domain", "id"),
            ["public.person"] = await OwnerRowsAsync("public.person", "id"),
            ["public.derivation_label"] = await OwnerRowsAsync("public.derivation_label", "label"),
            ["public.derivation_label_revocation"] = await OwnerRowsAsync("public.derivation_label_revocation", "label"),
            ["public.person_credential"] = await OwnerRowsAsync("public.person_credential", "person_id"),
            ["public.person_change"] = await OwnerRowsAsync("public.person_change", "id"),
            ["public.sign_in_attempt"] = await OwnerRowsAsync("public.sign_in_attempt", "id"),
            ["public.configuration_change"] = await OwnerRowsAsync("public.configuration_change", "id"),
            ["public.custody_event"] = await OwnerRowsAsync("public.custody_event", "sequence"),
            ["content.ingestion_batch"] = await ContentRowsAsync("content.ingestion_batch"),
            ["content.item_text"] = await ContentRowsAsync("content.item_text"),
            ["content.reversal_map"] = await ContentRowsAsync("content.reversal_map"),
            ["content.item_emptied"] = await ContentRowsAsync("content.item_emptied"),
            ["content.replication_occurrence"] = await ContentRowsAsync("content.replication_occurrence"),
            ["content.replication_mark"] = await ContentRowsAsync("content.replication_mark"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (world is not null)
        {
            await world.DisposeAsync();
        }
    }

    // nosemgrep: sql-concatenation-csharp interpolates only the key and the table of the copy, literals of this fixture, never a value.
    private static string AllRows(string key, string table) => $"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY {key}), '[]') FROM {table} t";

    private async Task<string> OwnerRowsAsync(string table, string key) =>
        Assert.Single(await World.OwnerColumnAsync(AllRows(key, table)));

    private async Task<string> ContentRowsAsync(string table)
    {
        var all = new JsonArray();
        foreach (var domain in new[] { 1, 2 })
        {
            var part = (string)(await World.InDomainAsync(domain, AllRows("id", table)))!;
            foreach (var row in JsonNode.Parse(part)!.AsArray())
            {
                all.Add(row!.DeepClone());
            }
        }

        return all.ToJsonString();
    }
}
