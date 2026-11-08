using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The copy is faithful to the batch that is still open: it carries the items and the maps, the restore puts them back in the same batch,
/// and an acceptance afterwards makes them part of the stock.
/// </summary>
[Trait("Category", "Database")]
public sealed class OpenBatchCopyTests
{
    private const string OfTheOpenBatch =
        "SELECT (SELECT count(*) FROM content.item_text WHERE batch_id = 3) || ':' || (SELECT count(*) FROM content.reversal_map WHERE batch_id = 3)"
        + " || ':' || (SELECT string_agg(id::text || '/' || encode(raw_text, 'hex') || '/' || encode(dek_wrapped, 'hex'), ',' ORDER BY id) FROM content.item_text WHERE batch_id = 3)";

    private const string InTheStock = "SELECT (SELECT count(*) FROM content.item_text) || ':' || (SELECT count(*) FROM content.reversal_map)";

    [DatabaseFact]
    public async Task OpenBatchComesBackFromTheCopyOpenWithTheSameItemsAndAnAcceptanceMakesThemVisible()
    {
        await using var copied = await CopiedWorld.CreateAsync(openBatch: true);
        var before = await FidelityWorld.InSystemScopeAsync(copied.World.DataSource, 1, OfTheOpenBatch) as string;
        var visibleBefore = await copied.World.InDomainAsync(1, InTheStock) as string;

        await using var restored = await RestoredCopy.LoadAsync(copied);
        var batches = new IngestionBatches(restored.App, TestConditions.Known);
        var held = await batches.ReadAsync(1, 3, TestContext.Current.CancellationToken);
        var after = await FidelityWorld.InSystemScopeAsync(restored.App, 1, OfTheOpenBatch) as string;
        var hiddenWhileOpen = await InTheStockOfAsync(restored, 1);
        var accepted = await batches.AcceptAsync(new BatchClosing(1, 3, CustodyAuthor.System), TestContext.Current.CancellationToken);

        Assert.StartsWith("2:1:", before, StringComparison.Ordinal);
        Assert.Equal((before, new IngestionBatch(BatchState.Open, null, 1, TestConditions.Strictest)), (after, held));
        Assert.Equal((visibleBefore, BatchOutcome.Accepted), (hiddenWhileOpen, accepted.Outcome));
        Assert.Equal("3:2", await InTheStockOfAsync(restored, 1));
    }

    private static async Task<string?> InTheStockOfAsync(RestoredCopy restored, int domain)
    {
        await using var transaction = await ContentTransaction.BeginAsync(restored.App, domain, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(InTheStock);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }
}
