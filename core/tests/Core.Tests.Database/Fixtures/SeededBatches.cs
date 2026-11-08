using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// The batch behind the tests that are not about batches: a world that adds items and maps writes them into a batch accepted at creation, one for each domain and numbered as the domain,
/// seeded by the owner outside the system (no event of the chain, so the chain of the world starts as it always did). The rule that content enters only an open batch is off in such a world,
/// because the batch is accepted; the rule and the batch itself are proven in a database of their own, by the tests of the ingestion.
/// </summary>
internal static class SeededBatches
{
    /// <summary>Seeds, as the owner, one batch accepted for each domain, with the id of the domain, opened by the first person of the database (one is put in when there is none).</summary>
    /// <param name="database">The migrated database, with the domains already in it.</param>
    /// <param name="domains">The domains; they must be seeded in ascending order from 1, so that each batch takes the number of its domain.</param>
    public static Task SeedAsync(ScratchDatabase database, params int[] domains)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.RunAsOwnerAsync(Statements(domains));
    }

    /// <summary>The statements that <see cref="SeedAsync" /> runs as the owner, for a world that runs its own.</summary>
    /// <param name="domains">The domains, as in <see cref="SeedAsync" />.</param>
    public static string[] Statements(params int[] domains)
    {
        string[] before =
        [
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event",
            "ALTER TABLE content.item_text DISABLE TRIGGER content_needs_open_batch",
            "ALTER TABLE content.reversal_map DISABLE TRIGGER content_needs_open_batch",
            "INSERT INTO public.person (name, function_name) SELECT 'zqx-seed', 'Anotação' WHERE NOT EXISTS (SELECT 1 FROM public.person)",
        ];
        string[] after =
        [
            "SELECT set_config('app.domain', '', false)",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_open_needs_event",
        ];
        var seeds = domains.SelectMany(domain => new[]
        {
            $"SELECT set_config('app.domain', '{domain}', false)",
            $"INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT {domain}, 'accepted', min(id), 'restrita', now() FROM public.person",
        });
        return [.. before, .. seeds, .. after];
    }
}

/// <summary>The writer of a world seeded by <see cref="SeededBatches" />: it adds items and maps with the old shape of the call, into the batch of the domain.</summary>
internal sealed class SeededWriter
{
    private readonly ContentWriter inner;

    /// <summary>The writer is the real one, over the same source and the same custody.</summary>
    internal SeededWriter(ContentWriter inner)
    {
        this.inner = inner;
    }

    /// <summary>Adds the item to the batch of the domain and answers its identifier.</summary>
    public Task<long> AddItemTextAsync(int domainId, NewItemText item, CancellationToken cancellationToken) =>
        inner.AddItemTextAsync(domainId, domainId, item, cancellationToken);

    /// <summary>Adds the map of the item to the batch of the domain and answers its identifier.</summary>
    public Task<long> AddReversalMapAsync(int domainId, long itemId, string entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return inner.AddReversalMapAsync(domainId, new NewReversalMap(domainId, itemId, entries), cancellationToken);
    }
}
