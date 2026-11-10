using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What the database keeps about the replicated content (issue 82, criterion 4): the rows are seen once their batch is accepted, nothing is removed or merged by marking,
/// a row is written once whoever asks, and a batch that is rejected takes its digests and its marks with it.
/// </summary>
public sealed class ReplicationTableTests
{
    private const string WithoutEvent = "CE005";
    private const string Immutable = "CE006";
    private const string Counts = "SELECT (SELECT count(*) FROM content.replication_occurrence) || ',' || (SELECT count(*) FROM content.replication_mark)";
    private static readonly DateTimeOffset Said = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Texts = [.. "abcd".Select(text => new string(text, 64))];

    [DatabaseFact]
    public async Task OccurrencesAndMarksAreSeenOnlyOnceTheirBatchIsAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        StoredOccurrence[] occurrences = [Occurrence(item, 0, 'a', 'b'), Occurrence(item, 1, 'a', 'c')];
        StoredMark[] marks = [new(item, 0, item, 0), new(item, 1, item, 0)];

        await store.RecordAsync(new(1, batch), occurrences, marks, TestContext.Current.CancellationToken);
        var before = (await store.OccurrencesAsync(1, Texts, TestContext.Current.CancellationToken)).Count + (await store.MarksAsync(1, Texts, TestContext.Current.CancellationToken)).Count;
        _ = await IngestionWorld.AcceptAsync(world, batch);

        Assert.Equal(0, before);
        Assert.Equal(occurrences, await store.OccurrencesAsync(1, Texts, TestContext.Current.CancellationToken));
        Assert.Equal(marks, await store.MarksAsync(1, Texts, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task TheDomainSeesNothingOfAnotherDomain()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        await store.RecordAsync(new(1, batch), [Occurrence(item, 0, 'a', 'b')], [], TestContext.Current.CancellationToken);
        _ = await IngestionWorld.AcceptAsync(world, batch);

        Assert.Empty(await store.OccurrencesAsync(2, Texts, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task AMarkedMessageStaysInTheArchiveWithItsSenderItsMomentAndItsConversationAndNothingIsRemovedOrMerged()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var token = TestContext.Current.CancellationToken;
        var firstBatch = await IngestionWorld.OpenAsync(world);
        var first = await IngestionWorld.AddItemAsync(world, firstBatch);
        var original = Occurrence(first, 0, 'a', 'b');
        await store.RecordAsync(new(1, firstBatch), [original], [], token);
        _ = await IngestionWorld.AcceptAsync(world, firstBatch);
        var secondBatch = await IngestionWorld.OpenAsync(world);
        var second = await IngestionWorld.AddItemAsync(world, secondBatch);
        var forwarded = Occurrence(second, 0, 'a', 'c');

        await store.RecordAsync(new(1, secondBatch), [forwarded], [new(first, 0, first, 0), new(second, 0, first, 0)], token);
        _ = await IngestionWorld.AcceptAsync(world, secondBatch);

        Assert.Equal([original, forwarded], await store.OccurrencesAsync(1, Texts, token));
        Assert.Equal([new StoredMark(first, 0, first, 0), new StoredMark(second, 0, first, 0)], await store.MarksAsync(1, Texts, token));
        Assert.Equal("2,0", await world.InDomainAsync(1, IngestionWorld.Reachable) as string);
        Assert.Equal("2,2", await world.OwnerInDomainAsync(1, Counts) as string);
    }

    [DatabaseFact]
    public async Task AMarkAlreadyWrittenIsNeverUndoneOrPointedAnywhereElse()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var token = TestContext.Current.CancellationToken;
        var firstBatch = await IngestionWorld.OpenAsync(world);
        var first = await IngestionWorld.AddItemAsync(world, firstBatch);
        await store.RecordAsync(new(1, firstBatch), [Occurrence(first, 0, 'a', 'b'), Occurrence(first, 1, 'a', 'c')], [new(first, 1, first, 0)], token);
        _ = await IngestionWorld.AcceptAsync(world, firstBatch);
        var secondBatch = await IngestionWorld.OpenAsync(world);
        var second = await IngestionWorld.AddItemAsync(world, secondBatch);

        await store.RecordAsync(new(1, secondBatch), [Occurrence(second, 0, 'a', 'd')], [new(first, 1, second, 0)], token);
        _ = await IngestionWorld.AcceptAsync(world, secondBatch);

        Assert.Equal([new StoredMark(first, 1, first, 0)], await store.MarksAsync(1, Texts, token));
    }

    [DatabaseFact]
    public async Task ARowIsWrittenOnceAndNeverChangesNorIsRemovedNotEvenByTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        StoredOccurrence[] written = [Occurrence(item, 0, 'a', 'b'), Occurrence(item, 1, 'a', 'c')];
        await store.RecordAsync(new(1, batch), written, [new(item, 1, item, 0)], TestContext.Current.CancellationToken);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        const string ChangeOccurrence = "UPDATE content.replication_occurrence SET text_digest = repeat('f', 64) WHERE item_id = @id";
        const string ChangeMark = "UPDATE content.replication_mark SET first_message_position = 1 WHERE item_id = @id";

        var changes = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, ChangeOccurrence, ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, ChangeMark, ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => AsSuperuserAsync(world, ChangeOccurrence, true, ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => AsSuperuserAsync(world, ChangeMark, true, ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => AsSuperuserAsync(world, "TRUNCATE content.replication_mark", false)),
            await Assert.ThrowsAsync<PostgresException>(() => AsSuperuserAsync(world, "TRUNCATE content.replication_occurrence CASCADE", true)),
        };
        var removals = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "DELETE FROM content.replication_mark WHERE item_id = @id", ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "DELETE FROM content.replication_mark WHERE item_id = @id", ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "DELETE FROM content.replication_occurrence WHERE item_id = @id", ("id", item))),
            await Assert.ThrowsAsync<PostgresException>(() => AsSuperuserAsync(world, "DELETE FROM content.replication_occurrence WHERE item_id = @id", true, ("id", item))),
        };

        Assert.All(changes, one => Assert.Equal(Immutable, one.SqlState));
        Assert.All(removals, one => Assert.Equal(WithoutEvent, one.SqlState));
        Assert.Equal("2,1", await world.OwnerInDomainAsync(1, Counts) as string);
    }

    [DatabaseFact]
    public async Task ABatchThatIsRejectedTakesItsOccurrencesAndItsMarksWithIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var token = TestContext.Current.CancellationToken;
        var firstBatch = await IngestionWorld.OpenAsync(world);
        var first = await IngestionWorld.AddItemAsync(world, firstBatch);
        await store.RecordAsync(new(1, firstBatch), [Occurrence(first, 0, 'a', 'b')], [], token);
        _ = await IngestionWorld.AcceptAsync(world, firstBatch);
        var secondBatch = await IngestionWorld.OpenAsync(world);
        var second = await IngestionWorld.AddItemAsync(world, secondBatch);
        await store.RecordAsync(new(1, secondBatch), [Occurrence(second, 0, 'a', 'c')], [new(first, 0, first, 0), new(second, 0, first, 0)], token);

        _ = await IngestionWorld.RejectAsync(world, secondBatch, "invalid_rows");

        Assert.Equal("1,0", await world.OwnerInDomainAsync(1, Counts) as string);
        Assert.Equal([Occurrence(first, 0, 'a', 'b')], await store.OccurrencesAsync(1, Texts, token));
    }

    [DatabaseFact]
    public async Task TheReadingFollowsTheDigestsOfTheBatchAndLeavesTheOtherDigestsOfTheDomainOut()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var token = TestContext.Current.CancellationToken;
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        var wanted = Occurrence(item, 0, 'a', 'b');
        await store.RecordAsync(new(1, batch), [wanted, Occurrence(item, 1, 'c', 'b'), Occurrence(item, 2, 'd', 'b')], [new(item, 0, item, 0), new(item, 1, item, 1)], token);
        _ = await IngestionWorld.AcceptAsync(world, batch);

        Assert.Equal([wanted], await store.OccurrencesAsync(1, [wanted.TextDigest], token));
        Assert.Equal([new StoredMark(item, 0, item, 0)], await store.MarksAsync(1, [wanted.TextDigest], token));
        Assert.Empty(await store.OccurrencesAsync(1, [new string('e', 64)], token));
        Assert.Empty(await store.MarksAsync(1, [], token));
    }

    [DatabaseFact]
    public async Task NothingIsWrittenToABatchThatIsNotOpen()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AcceptAsync(world, batch);

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(
            () => store.RecordAsync(new(1, batch), [Occurrence(item, 0, 'a', 'b')], [], TestContext.Current.CancellationToken));

        Assert.Equal("0,0", await world.OwnerInDomainAsync(1, Counts) as string);
    }

    [DatabaseFact]
    public async Task ThePlaceOfAnOccurrenceIsWrittenOnceAndAMarkNeverPointsToAnOccurrenceThatIsNotThere()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        await store.RecordAsync(new(1, batch), [Occurrence(item, 0, 'a', 'b')], [], TestContext.Current.CancellationToken);

        var twice = await Assert.ThrowsAsync<PostgresException>(
            () => store.RecordAsync(new(1, batch), [Occurrence(item, 0, 'c', 'd')], [], TestContext.Current.CancellationToken));
        var nowhere = await Assert.ThrowsAsync<PostgresException>(
            () => store.RecordAsync(new(1, batch), [], [new(item, 0, item, 7)], TestContext.Current.CancellationToken));

        Assert.Equal((PostgresErrorCodes.UniqueViolation, PostgresErrorCodes.ForeignKeyViolation), (twice.SqlState, nowhere.SqlState));
    }

    [DatabaseFact]
    public async Task AMarkOfOneDomainNeverPointsToTheOccurrenceOfAnotherDomain()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        await store.RecordAsync(new(1, batch), [Occurrence(item, 0, 'a', 'b')], [], TestContext.Current.CancellationToken);
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(new BatchOpening(2, IngestionWorld.Person), TestContext.Current.CancellationToken);
        var otherBatch = opened.BatchId!.Value;
        var otherItem = await world.RealWriter.AddItemTextAsync(2, otherBatch, new NewItemText("texto bruto sintetico zqx"), TestContext.Current.CancellationToken);
        await store.RecordAsync(new(2, otherBatch), [Occurrence(otherItem, 0, 'c', 'd')], [], TestContext.Current.CancellationToken);

        var across = await Assert.ThrowsAsync<PostgresException>(
            () => store.RecordAsync(new(2, otherBatch), [], [new(otherItem, 0, item, 0)], TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, across.SqlState);
    }

    [DatabaseFact]
    public async Task ATextOrASenderThatIsNotADigestIsRefusedByTheTable()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        string[] notDigests = ["ZQX-texto-em-claro", new string('A', 64), new string('a', 63), new string('a', 65), string.Empty];

        foreach (var notDigest in notDigests)
        {
            var asText = Occurrence(item, 0, 'a', 'b') with { TextDigest = notDigest };
            var asSender = Occurrence(item, 0, 'a', 'b') with { SenderDigest = notDigest };

            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => RecordAsync(store, batch, asText))).SqlState);
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => RecordAsync(store, batch, asSender))).SqlState);
        }

        Assert.Equal("0,0", await world.OwnerInDomainAsync(1, Counts) as string);
    }

    private static Task RecordAsync(ReplicationStore store, long batch, StoredOccurrence occurrence) =>
        store.RecordAsync(new(1, batch), [occurrence], [], TestContext.Current.CancellationToken);

    private static StoredOccurrence Occurrence(long item, int position, char text, char sender) =>
        new(item, position, new string(text, 64), new string(sender, 64), 40, Said.AddMinutes(position));

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> AsSuperuserAsync(ContentWorld world, string statement, bool replica, params (string Name, object Value)[] parameters)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        if (replica)
        {
            await using var skip = new NpgsqlCommand("SET session_replication_role = replica", connection);
            _ = await skip.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var command = new NpgsqlCommand(statement, connection);
        foreach (var (name, value) in parameters.Where(parameter => statement.Contains("@" + parameter.Name, StringComparison.Ordinal)))
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
