using System.Text.Json.Nodes;
using Core.Persistence.Tests.Fixtures;
using Xunit;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// A database that holds what a restore puts back, made by the flows it has: two domains, two people (one deactivated), an event for each kind of author, a text and its map in one domain
/// and a text emptied in the other. The copy is each table as the array of its rows in the order of the key, as the 005 describes it.
/// </summary>
public sealed class RestoreSource : IAsyncLifetime
{
    private const string Rows = "SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY {0}), '[]') FROM {1} t";

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
        _ = await world.InDomainAsync(1, "INSERT INTO content.item_text (domain_id, raw_text) VALUES (1, '\\x0102')");
        _ = await world.InDomainAsync(1, "INSERT INTO content.reversal_map (domain_id, item_id, entries) VALUES (1, 1, '\\x03')");
        _ = await world.InDomainAsync(2, "INSERT INTO content.item_text (domain_id, raw_text) VALUES (2, '\\x04')");
        await world.RunAsOwnerAsync(
            "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason, reference) VALUES (1, 'item_text', 2, 'item_emptied', 'removal_requested_by_subject', 'zqx-1')");
        _ = await world.InDomainAsync(2, "UPDATE content.item_text SET emptied_at = clock_timestamp(), raw_text = NULL WHERE id = 2");
        Copy = new Dictionary<string, string>
        {
            ["public.domain"] = await OwnerRowsAsync("public.domain", "id"),
            ["public.person"] = await OwnerRowsAsync("public.person", "id"),
            ["public.custody_event"] = await OwnerRowsAsync("public.custody_event", "sequence"),
            ["content.item_text"] = await ContentRowsAsync("content.item_text"),
            ["content.reversal_map"] = await ContentRowsAsync("content.reversal_map"),
            ["content.item_emptied"] = await ContentRowsAsync("content.item_emptied"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (world is not null)
        {
            await world.DisposeAsync();
        }
    }

    private async Task<string> OwnerRowsAsync(string table, string key) =>
        Assert.Single(await World.OwnerColumnAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, Rows, key, table)));

    private async Task<string> ContentRowsAsync(string table)
    {
        var all = new JsonArray();
        foreach (var domain in new[] { 1, 2 })
        {
            var part = (string)(await World.InDomainAsync(domain, string.Format(System.Globalization.CultureInfo.InvariantCulture, Rows, "id", table)))!;
            foreach (var row in JsonNode.Parse(part)!.AsArray())
            {
                all.Add(row!.DeepClone());
            }
        }

        return all.ToJsonString();
    }
}
