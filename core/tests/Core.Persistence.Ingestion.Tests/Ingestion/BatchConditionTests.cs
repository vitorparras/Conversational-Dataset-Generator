using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The condition under which the material came in (issue 85): a batch opened with no condition takes the most restrictive one the installation knows (CA-1), the item has the condition of its
/// batch, read beside it (CA-2), and a condition the installation does not know refuses the opening without leaving a batch or an event (CA-5).
/// </summary>
public sealed class BatchConditionTests
{
    private const string HeldInTheTable = "SELECT condition FROM content.ingestion_batch WHERE id = @id";

    [DatabaseFact]
    public async Task BatchOpenedWithNoConditionTakesTheMostRestrictiveOneTheInstallationKnows()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await IngestionWorld.OpenAsync(world);
        var held = await IngestionWorld.BatchesOf(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal((TestConditions.Strictest, TestConditions.Strictest), (held!.Condition, await world.InDomainAsync(IngestionWorld.Domain, HeldInTheTable, ("id", batch)) as string));
    }

    [DatabaseFact]
    public async Task OmissionNeverFreesTheMostRestrictiveIsTheFirstOfTheListWhateverItsNameOrItsPlaceInTheAlphabet()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = new IngestionBatches(world.DataSource, OriginConditions.Of(["z_sigilosa", "a_livre"]));

        var opened = await batches.OpenAsync(new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person), TestContext.Current.CancellationToken);

        Assert.Equal("z_sigilosa", await world.InDomainAsync(IngestionWorld.Domain, HeldInTheTable, ("id", opened.BatchId!.Value)) as string);
    }

    [DatabaseFact]
    public async Task BatchOpenedUnderAConditionKeepsItAndSoDoesTheBatchThatClosed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world, TestConditions.Middle);
        var rejected = await IngestionWorld.OpenAsync(world, TestConditions.Loosest);
        var batches = IngestionWorld.BatchesOf(world);

        _ = await IngestionWorld.AcceptAsync(world, accepted);
        _ = await IngestionWorld.RejectAsync(world, rejected, "invalid_rows");

        Assert.Equal(
            (TestConditions.Middle, TestConditions.Loosest),
            (
                (await batches.ReadAsync(IngestionWorld.Domain, accepted, TestContext.Current.CancellationToken))!.Condition,
                (await batches.ReadAsync(IngestionWorld.Domain, rejected, TestContext.Current.CancellationToken))!.Condition));
    }

    [DatabaseFact]
    public async Task ConditionTheInstallationDoesNotKnowRefusesTheOpeningAndLeavesNoBatchAndNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await IngestionWorld.BatchesOf(world)
            .OpenAsync(new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, "zqx_desconhecida"), TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.ConditionUnknown, null, null, "0"),
            (refused.Outcome, refused.BatchId, await world.ScalarAsync(IngestionWorld.Events), await world.InDomainAsync(IngestionWorld.Domain, "SELECT count(*)::text FROM content.ingestion_batch") as string));
    }

    [Theory]
    [InlineData("zqx_desconhecida")]
    [InlineData("Restrita")]
    [InlineData("")]
    [InlineData(" restrita")]
    public async Task ConditionOutsideTheListIsRefusedBeforeAnyConnectionOpens(string declared)
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource, TestConditions.Known);

        var refused = await batches.OpenAsync(new BatchOpening(1, 1, declared), TestContext.Current.CancellationToken);

        Assert.Equal(BatchOutcome.ConditionUnknown, refused.Outcome);
    }

    [Fact]
    public void OpeningWithNoOpeningIsRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource, TestConditions.Known);

        Assert.Equal("opening", Param(() => batches.OpenAsync(null!, CancellationToken.None)));
    }

    [Fact]
    public void BatchesWithNoListOfConditionsAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");

        Assert.Equal("conditions", Assert.Throws<ArgumentNullException>(() => new IngestionBatches(dataSource, null!)).ParamName);
    }

    [DatabaseFact]
    public async Task ItemHasTheConditionOfTheBatchItCameInWithOnceTheBatchIsAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var strict = await IngestionWorld.OpenAsync(world);
        var loose = await IngestionWorld.OpenAsync(world, TestConditions.Loosest);
        var strictItem = await IngestionWorld.AddItemAsync(world, strict);
        var looseItem = await IngestionWorld.AddItemAsync(world, loose);
        _ = await IngestionWorld.AcceptAsync(world, strict);
        _ = await IngestionWorld.AcceptAsync(world, loose);
        var items = new ItemConditions(world.DataSource);

        Assert.Equal(
            (TestConditions.Strictest, TestConditions.Loosest),
            (await items.OfAsync(IngestionWorld.Domain, strictItem, token), await items.OfAsync(IngestionWorld.Domain, looseItem, token)));
    }

    [DatabaseFact]
    public async Task ConditionIsRegisteredBesideTheItemAndNotCopiedIntoIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world, TestConditions.Middle);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AcceptAsync(world, batch);

        var columns = await world.ScalarAsync("SELECT string_agg(column_name, ',' ORDER BY column_name) FROM information_schema.columns WHERE table_schema = 'content' AND table_name = 'item_text'") as string;
        var viewed = await world.InDomainAsync(IngestionWorld.Domain, "SELECT item_id || ':' || batch_id || ':' || condition FROM public.item_condition WHERE item_id = @item", ("item", item)) as string;

        Assert.DoesNotContain("condition", columns, StringComparison.Ordinal);
        Assert.Equal($"{item}:{batch}:{TestConditions.Middle}", viewed);
    }

    [DatabaseFact]
    public async Task ItemOfABatchNotAcceptedHasNoConditionToReadAndNeitherDoesOneOfAnotherDomainOrOneThatDoesNotExist()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var open = await IngestionWorld.OpenAsync(world, TestConditions.Middle);
        var item = await IngestionWorld.AddItemAsync(world, open);
        var accepted = await IngestionWorld.OpenAsync(world);
        var acceptedItem = await IngestionWorld.AddItemAsync(world, accepted);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var items = new ItemConditions(world.DataSource);

        Assert.Equal(
            (null, null, null, TestConditions.Strictest),
            (
                await items.OfAsync(IngestionWorld.Domain, item, token),
                await items.OfAsync(IngestionWorld.Domain + 1, acceptedItem, token),
                await items.OfAsync(IngestionWorld.Domain, 9_999, token),
                await items.OfAsync(IngestionWorld.Domain, acceptedItem, token)));
    }

    [Fact]
    public void ArgumentsThatAreNotPositiveOrAreMissingAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var items = new ItemConditions(dataSource);

        Assert.Equal(
            ["domainId", "itemId", "dataSource"],
            [
                Param(() => items.OfAsync(0, 1, CancellationToken.None)),
                Param(() => items.OfAsync(1, 0, CancellationToken.None)),
                Assert.Throws<ArgumentNullException>(() => new ItemConditions(null!)).ParamName,
            ]);
    }

    private static string? Param(Func<object?> call) =>
        Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
