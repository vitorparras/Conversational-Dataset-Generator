using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What a submission brought is kept with the batch it opened (issue 75, CA-5): the identifier, the bytes, the SHA-256 and the warning,
/// born with the batch, all or none, and never changed.
/// </summary>
public sealed class BatchReceiptTests
{
    private const string Hex32 = "0123456789abcdef0123456789abcdef";
    private const string Hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Insert =
        "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition, submission, received_bytes, received_digest, warning, origin_state) "
        + "VALUES (1, 1, 'restrita', @s, @b, @d, @w, CASE WHEN @s::text IS NULL THEN NULL ELSE 'unknown' END)";

    [DatabaseFact]
    public async Task ReceiptIsKeptWithTheBatchTheSubmissionOpened()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var receipt = new SubmissionReceipt(Hex32, 1234, Hex64, "name_with_path");

        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(new BatchOpening(1, 1, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown)), TestContext.Current.CancellationToken);
        var entry = await new BatchRegister(world.DataSource).ReadAsync([1], opened.BatchId!.Value, TestContext.Current.CancellationToken);

        Assert.Equal(receipt, entry!.Receipt);
    }

    [DatabaseFact]
    public async Task BatchOpenedWithNoSubmissionHasNoReceipt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var entry = await new BatchRegister(world.DataSource).ReadAsync([1], batch, TestContext.Current.CancellationToken);

        Assert.Null(entry!.Receipt);
    }

    [DatabaseTheory]
    [InlineData("0123", 1L, Hex64, null)]
    [InlineData(Hex32, -1L, Hex64, null)]
    [InlineData(Hex32, 1L, "abc", null)]
    [InlineData(Hex32, 1L, Hex64, "Name With Path")]
    [InlineData(null, 1L, Hex64, null)]
    [InlineData(null, null, null, "name_with_path")]
    [InlineData(Hex32, null, Hex64, null)]
    public async Task ReceiptThatIsNotWholeOrNotInShapeIsRefusedByTheTable(string? submission, long? bytes, string? digest, string? warning)
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(
                1,
                Insert,
                ("s", (object?)submission ?? DBNull.Value),
                ("b", (object?)bytes ?? DBNull.Value),
                ("d", (object?)digest ?? DBNull.Value),
                ("w", (object?)warning ?? DBNull.Value)));

        Assert.Equal(("ingestion_batch_receipt", PostgresErrorCodes.CheckViolation), (refused.ConstraintName, refused.SqlState));
    }

    [DatabaseFact]
    public async Task TheSameSubmissionNeverOpensTwoBatches()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);
        var receipt = new SubmissionReceipt(Hex32, 1, Hex64);
        _ = await batches.OpenAsync(new BatchOpening(1, 1, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown)), TestContext.Current.CancellationToken);

        var second = await Record.ExceptionAsync(() => batches.OpenAsync(new BatchOpening(1, 1, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown)), TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(second).SqlState);
    }

    [DatabaseFact]
    public async Task ReceiptNeverChangesAfterTheOpeningNotEvenForTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(1, 1, null, new SubmissionOpening(new SubmissionReceipt(Hex32, 5, Hex64), OriginDeclaration.Unknown)), TestContext.Current.CancellationToken);
        await world.Database.RunAsOwnerAsync("ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_change_needs_event");
        const string Change = "UPDATE content.ingestion_batch SET received_bytes = 6 WHERE id = @id";

        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Change, ("id", opened.BatchId!.Value)));
        var asReplica = await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Change, opened.BatchId!.Value));

        Assert.Equal(("CE006", "CE006"), (byOwner.SqlState, asReplica.SqlState));
    }

    [DatabaseFact]
    public async Task TheTriggerThatKeepsTheReceiptFiresEvenWhereOrdinaryTriggersAreSkipped()
    {
        await using var world = await IngestionWorld.CreateAsync();

        Assert.Equal("A", await world.ScalarAsync("SELECT tgenabled::text FROM pg_trigger WHERE tgname = 'batch_receipt_is_never_changed'") as string);
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> AsReplicaAsync(ContentWorld world, string statement, long id)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var replica = new NpgsqlCommand("SET session_replication_role = replica", connection);
        _ = await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(statement, connection);
        _ = command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
