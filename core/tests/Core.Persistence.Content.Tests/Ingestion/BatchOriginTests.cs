using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The origin of a submission is sealed in a row of its own with a key of its own (issue 75, part 2b, decision 0025): it is born with the batch,
/// never changes, is read only with an event of the chain, and its key is wrapped again by the rotation.
/// </summary>
public sealed class BatchOriginTests
{
    private const string Origin = "origem sintetica zqx";
    private const string Hex32 = "0123456789abcdef0123456789abcdef";
    private const string Hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OriginEvents = "SELECT count(*) FROM public.custody_event WHERE step = 'origin_read' AND item_kind = 'ingestion_batch' AND reason = 'content_read' AND item_id = @id";
    private const string Rows = "SELECT count(*) FROM content.batch_origin";

    [DatabaseFact]
    public async Task OriginDeclaredIsSealedInARowOfItsOwnAndTheBatchKeepsOnlyTheStateInClear()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await OpenAsync(world, OriginDeclaration.Declared(world.Content, IngestionWorld.Domain, Origin), Hex32);
        var state = await world.OwnerInDomainAsync(IngestionWorld.Domain, "SELECT origin_state FROM content.ingestion_batch WHERE id = @id", ("id", batch));

        Assert.Equal(("declared", 1L), (state, await world.OwnerInDomainAsync(IngestionWorld.Domain, Rows)));
        Assert.DoesNotContain(Origin, await world.Database.DumpAsync(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OriginUnknownKeepsOnlyTheStateAndSealsNothing()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await OpenAsync(world, OriginDeclaration.Unknown, Hex32);
        var state = await world.OwnerInDomainAsync(IngestionWorld.Domain, "SELECT origin_state FROM content.ingestion_batch WHERE id = @id", ("id", batch));

        Assert.Equal(("unknown", 0L), (state, await world.OwnerInDomainAsync(IngestionWorld.Domain, Rows)));
    }

    [DatabaseFact]
    public async Task ReadingTheOriginRecordsTheEventOfTheBatchAndGivesTheTextBack()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Declared(world.Content, IngestionWorld.Domain, Origin), Hex32);
        var before = await world.ScalarAsync(OriginEvents, ("id", batch));

        var read = await world.Reader.ReadAsync(
            new ContentRead(CustodyAuthor.Person(IngestionWorld.Person), ContentColumns.Origin, IngestionWorld.Domain, batch), TestContext.Current.CancellationToken);

        Assert.Equal((0L, ContentReadOutcome.Read, Origin, 1L), (before, read.Outcome, read.Text, await world.ScalarAsync(OriginEvents, ("id", batch))));
    }

    [DatabaseFact]
    public async Task ReadingTheOriginOfABatchThatDeclaredNoneFindsNothingAndRecordsNothing()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Unknown, Hex32);

        var read = await world.Reader.ReadAsync(
            new ContentRead(CustodyAuthor.Person(IngestionWorld.Person), ContentColumns.Origin, IngestionWorld.Domain, batch), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.NotFound, 0L), (read.Outcome, await world.ScalarAsync(OriginEvents, ("id", batch))));
    }

    [DatabaseFact]
    public async Task ABatchOfASubmissionHasItsOriginAndItsReceiptTogetherAndABatchOfNoneHasNeither()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);
        var receipt = new SubmissionReceipt(Hex32, 1, Hex64);

        var opening = new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null, receipt);
        var withoutReceipt = opening with { Receipt = null, Origin = OriginDeclaration.Unknown };

        _ = Assert.Throws<ArgumentException>(() => batches.OpenAsync(opening, TestContext.Current.CancellationToken));
        _ = Assert.Throws<ArgumentException>(() => batches.OpenAsync(withoutReceipt, TestContext.Current.CancellationToken));
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.batch_origin SET origin = '\\x00'::bytea WHERE batch_id = @id")]
    [InlineData("DELETE FROM content.batch_origin WHERE batch_id = @id")]
    public async Task OriginNeverChangesNorIsRemovedNotEvenForTheOwner(string statement)
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Declared(world.Content, IngestionWorld.Domain, Origin), Hex32);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(IngestionWorld.Domain, statement, ("id", batch)));

        Assert.Equal("CE006", refused.SqlState);
    }

    [DatabaseFact]
    public async Task TheStateOfTheOriginNeverChangesAfterTheOpeningNotEvenForTheOwner()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Unknown, Hex32);
        await world.Database.RunAsOwnerAsync("ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_change_needs_event");

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(IngestionWorld.Domain, "UPDATE content.ingestion_batch SET origin_state = 'declared' WHERE id = @id", ("id", batch)));

        Assert.Equal("CE006", refused.SqlState);
    }

    [DatabaseFact]
    public async Task TheKeyOfTheOriginIsWrappedAgainOnlyWithTheEventOfTheRotation()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Declared(world.Content, IngestionWorld.Domain, Origin), Hex32);
        const string Rewrap = "UPDATE content.batch_origin SET dek_epoch = 2 WHERE batch_id = @id";

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(IngestionWorld.Domain, Rewrap, ("id", batch)));

        Assert.Equal("CE005", refused.SqlState);
    }

    [DatabaseFact]
    public async Task RotationWrapsTheKeyOfTheOriginAgainAndTheOriginReadsAsBefore()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world, OriginDeclaration.Declared(world.Content, IngestionWorld.Domain, Origin), Hex32);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);

        var rewrapped = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(TestContext.Current.CancellationToken);
        var read = await world.ReaderWith(epochs).ReadAsync(
            new ContentRead(CustodyAuthor.System, ContentColumns.Origin, IngestionWorld.Domain, batch), TestContext.Current.CancellationToken);
        var epoch = await world.OwnerInDomainAsync(IngestionWorld.Domain, "SELECT dek_epoch FROM content.batch_origin WHERE batch_id = @id", ("id", batch));

        Assert.Equal((1, 2, Origin), (rewrapped, epoch, read.Text));
    }

    private static async Task<long> OpenAsync(ContentWorld world, OriginDeclaration origin, string submission)
    {
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null, new SubmissionReceipt(submission, 1, Hex64), origin), TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }
}
