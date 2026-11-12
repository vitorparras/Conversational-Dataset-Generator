using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What the rejection of a batch with a motive removes (issue 80) and how the register names the states: each kind of row of the batch is gone, each state has its own name.
/// </summary>
public sealed class BatchRejectionRemovalTests
{
    private const string Counted =
        "SELECT (SELECT count(*) FROM content.replication_mark) || ',' || (SELECT count(*) FROM content.replication_occurrence) || ',' || "
        + "(SELECT count(*) FROM content.reversal_map) || ',' || (SELECT count(*) FROM content.item_text)";

    private static readonly DateTimeOffset Said = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [DatabaseFact]
    public async Task RejectingABatchWithAMotiveRemovesItsMarksOccurrencesMapsAndItemsAndNothingOfAnotherBatch()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var kept = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        var keptItem = await IngestionWorld.AddItemAsync(world, kept);
        _ = await IngestionWorld.AddMapAsync(world, kept, keptItem);
        _ = await IngestionWorld.AcceptAsync(world, kept);
        var batch = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'b');
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddMapAsync(world, batch, item);
        var occurrence = new StoredOccurrence(item, 0, new string('a', 64), new string('b', 64), 40, Said);
        await new ReplicationStore(world.DataSource).RecordAsync(new ReplicationTarget(IngestionWorld.Domain, batch), [occurrence], [new StoredMark(item, 0, item, 0)], token);
        var before = await CountedAsync(world);

        var rejected = await IngestionWorld.RejectAsync(world, batch, "invalid_rows");

        Assert.Equal((BatchOutcome.Rejected, "1,1,2,2", "0,0,1,1"), (rejected.Outcome, before, await CountedAsync(world)));
    }

    [DatabaseFact]
    public async Task EachStateOfABatchIsFoundByItsOwnNameAndNoOther()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var open = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        var accepted = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'b');
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var rejected = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'c');
        _ = await IngestionWorld.RejectAsync(world, rejected, "too_large");
        var register = new BatchRegister(world.DataSource);
        var domains = new[] { IngestionWorld.Domain };

        Assert.Equal(
            [open, accepted, rejected],
            [
                Assert.Single(await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Open }, token)).Id,
                Assert.Single(await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Accepted }, token)).Id,
                Assert.Single(await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Rejected }, token)).Id,
            ]);
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of the test, never input.")]
    private static async Task<object?> CountedAsync(BatchesWorld world)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var command = new NpgsqlCommand(Counted, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
