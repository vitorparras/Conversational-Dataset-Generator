using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>The record of an emptied item is read by the identifier, from the table of the items emptied and the chain, and never from a table of texts (#617).</summary>
[Trait("Category", "Database")]
public sealed class EmptiedItemReaderTests
{
    private const string Protocol = "PROT-2026/0042";
    private const string Events = "SELECT count(*) FROM public.custody_event WHERE step IN ('raw_text_read', 'sanitized_text_read', 'reversal_entries_read')";

    [DatabaseFact]
    public async Task EmptiedItemAnswersWhoEmptiedWhenWhyAndForWhichProtocol()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await EmptiedAsync(world, EmptyingMotive.ContractRequirement);

        var record = await new EmptiedItemReader(world.DataSource).ReadAsync(1, item, TestContext.Current.CancellationToken);

        Assert.Equal(("zqx-curator", EmptyingMotive.ContractRequirement, Protocol, true), (record?.Executor, record?.Motive, record?.Reference, record?.At > DateTimeOffset.UtcNow.AddMinutes(-5)));
    }

    [DatabaseFact]
    public async Task EachMotiveComesBackAsTheOneTheEmptyingGave()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, removal) = await EmptiedAsync(world, EmptyingMotive.RemovalRequestedBySubject);

        var record = await new EmptiedItemReader(world.DataSource).ReadAsync(1, removal, TestContext.Current.CancellationToken);

        Assert.Equal(EmptyingMotive.RemovalRequestedBySubject, record?.Motive);
    }

    [DatabaseFact]
    public async Task ItemNotEmptiedAndItemThatDoesNotExistAnswerNone()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto zqx", "sanitizado zqx", "pseudonimizado zqx"), TestContext.Current.CancellationToken);
        var reader = new EmptiedItemReader(world.DataSource);

        var present = await reader.ReadAsync(1, item, TestContext.Current.CancellationToken);
        var absent = await reader.ReadAsync(1, item + 1000, TestContext.Current.CancellationToken);

        Assert.Equal((null, null), (present, absent));
    }

    [DatabaseFact]
    public async Task ItemEmptiedInAnotherDomainIsNotFoundInThisOne()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await EmptiedAsync(world, EmptyingMotive.ContractRequirement);

        var record = await new EmptiedItemReader(world.DataSource).ReadAsync(2, item, TestContext.Current.CancellationToken);

        Assert.Null(record);
    }

    [DatabaseFact]
    public async Task RefusedEmptyingLeavesNoRecord()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto zqx", "sanitizado zqx", "pseudonimizado zqx"), TestContext.Current.CancellationToken);
        var annotator = await world.PersonAsync("zqx-active");
        var refused = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(annotator), 1, item, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);

        var record = await new EmptiedItemReader(world.DataSource).ReadAsync(1, item, TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.AuthorWithoutFunction, null), (refused.Outcome, record));
    }

    [DatabaseFact]
    public async Task ReadingTheRecordReadsNoTextAndLeavesNoReadingEventInTheChain()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await EmptiedAsync(world, EmptyingMotive.ContractRequirement);

        _ = await new EmptiedItemReader(world.DataSource).ReadAsync(1, item, TestContext.Current.CancellationToken);

        Assert.Equal(0L, await world.ScalarAsync(Events));
    }

    [DatabaseFact]
    public async Task IdentifiersThatAreNotPositiveAreRefusedBeforeAnyConnection()
    {
        await using var world = await ContentWorld.CreateAsync();
        var reader = new EmptiedItemReader(world.DataSource);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(0, 1, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(1, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReaderWithoutADataSourceIsRefused() =>
        Assert.Throws<ArgumentNullException>(() => new EmptiedItemReader(null!));

    private static async Task<(long Curator, long Item)> EmptiedAsync(ContentWorld world, EmptyingMotive motive)
    {
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto zqx", "sanitizado zqx", "pseudonimizado zqx"), TestContext.Current.CancellationToken);
        var curator = await world.PersonAsync("zqx-curator");
        var result = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(curator), 1, item, motive, Protocol), TestContext.Current.CancellationToken);
        Assert.Equal(EmptyingOutcome.Emptied, result.Outcome);
        return (curator, item);
    }
}
