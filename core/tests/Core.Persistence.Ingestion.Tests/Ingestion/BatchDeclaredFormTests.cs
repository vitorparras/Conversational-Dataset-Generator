using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The form a submission declares, kept with its batch (issue 821, migration 0055): written at the opening, one of the four forms or none for a batch that declared none, and never changed after,
/// whoever writes.
/// </summary>
public sealed class BatchDeclaredFormTests
{
    private const string Form = "SELECT declared_form FROM content.ingestion_batch WHERE id = @id";

    [DatabaseFact]
    public async Task DeclaredFormIsKeptOnOpening()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var declared = await OpenAsync(world, "comment");
        var none = await IngestionWorld.OpenAsync(world);

        Assert.Equal(("comment", null), (await world.AsSuperuserAsync(Form, ("id", declared)), await world.AsSuperuserAsync(Form, ("id", none))));
    }

    [DatabaseFact]
    public async Task DeclaredFormCannotChange()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, "conversation");

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.AsSuperuserAsync("UPDATE content.ingestion_batch SET declared_form = 'document' WHERE id = @id", ("id", batch)));

        Assert.Equal(("CE006", "conversation"), (refused.SqlState, await world.AsSuperuserAsync(Form, ("id", batch))));
    }

    [DatabaseFact]
    public async Task DeclaredFormOutsideTheFourIsRefusedAtTheOpening()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => OpenAsync(world, "planilha"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static async Task<long> OpenAsync(BatchesWorld world, string form)
    {
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null) { DeclaredForm = form }, TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }
}
