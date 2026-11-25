using System.Globalization;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using static Core.Persistence.Tests.Reversal.ReversalAsks;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// The lists of requests (issues 103 and 104, fatia C): the person's own, newest first, with how many there are; the others' that wait for a decision, pending or expired; narrowed by
/// item and by state, a page below the first is the first. And an act stopped behind another is tried again, up to three times in all.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalListingTests
{
    private const string Attempts = "SELECT last_value FROM public.zqx_attempts";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task OwnRequestsAreListedNewestFirstWithHowManyThereAre()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var first = await ReversalRows.RequestAsync(world, 1, 3);
        var last = await ReversalRows.RequestAsync(world, 1, 3);
        _ = await ReversalRows.RequestAsync(world, 1, second);

        var page = await world.ReversalRequestReader.ListAsync(Mine(1), Token);
        var below = await world.ReversalRequestReader.ListAsync(Mine(0), Token);
        var beyond = await world.ReversalRequestReader.ListAsync(Mine(2), Token);

        Assert.Equal([last, first], Ids(page));
        Assert.Equal([last, first], Ids(below));
        Assert.Equal((2, 2), (page.Total, below.Total));
        Assert.Empty(beyond.Items);
    }

    [DatabaseFact]
    public async Task OthersRequestsAreListedOnlyWhilePendingOrExpired()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var pending = await ReversalRows.RequestAsync(world, 1, 3);
        var authorized = await ReversalRows.RequestAsync(world, 1, 3);
        _ = await ReversalRows.AuthorizeAsync(world, (1, authorized), second, "30 days");
        var expired = await ReversalRows.RequestAsync(world, 1, 3);
        await ReversalRows.ExpiredAsync(world, (1, expired), second);
        var declined = await ReversalRows.RequestAsync(world, 1, 3);
        Assert.Equal(ReversalOutcome.Recorded, (await world.ReversalRequests.DeclineAsync(new ReversalAct(1, declined, second), Token)).Outcome);
        _ = await ReversalRows.RequestAsync(world, 1, second);

        var others = await world.ReversalRequestReader.ListAsync(Others(second), Token);

        Assert.Equal([expired, pending], Ids(others));
        Assert.Equal((2, ReversalState.Expired, ReversalState.Pending), (others.Total, others.Items[0].State, others.Items[1].State));
    }

    [DatabaseFact]
    public async Task ListIsNarrowedByItemAndByState()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var pending = await ReversalRows.RequestAsync(world, 1, 3);
        var expired = await ReversalRows.RequestAsync(world, 1, 3);
        await ReversalRows.ExpiredAsync(world, (1, expired), second);

        var ofTheItem = await world.ReversalRequestReader.ListAsync(Others(second) with { ItemId = 118 }, Token);
        var ofAnother = await world.ReversalRequestReader.ListAsync(Others(second) with { ItemId = 119 }, Token);
        var stillPending = await world.ReversalRequestReader.ListAsync(Others(second) with { State = ReversalState.Pending }, Token);

        Assert.Equal([expired, pending], Ids(ofTheItem));
        Assert.Empty(ofAnother.Items);
        Assert.Equal([pending], Ids(stillPending));
        Assert.Equal((2, 0, 1), (ofTheItem.Total, ofAnother.Total, stillPending.Total));
    }

    [DatabaseFact]
    public async Task DeclineAndAuthorizationTellTheRowTheyAreAbout()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var toDecline = await ReversalRows.RequestAsync(world, 1, 3);
        var toAuthorize = await ReversalRows.RequestAsync(world, 1, 3);

        var declined = await world.ReversalRequests.DeclineAsync(new ReversalAct(1, toDecline, second), Token);
        var authorized = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, toAuthorize, second), DateTimeOffset.UtcNow.AddDays(7), Token);

        var authorization = (long)(await world.OwnerInDomainAsync(1, $"SELECT id FROM content.reversal_authorization WHERE request_id = {toAuthorize}"))!;
        Assert.Equal((ReversalOutcome.Recorded, toDecline), (declined.Outcome, declined.Id));
        Assert.Equal((ReversalOutcome.Recorded, authorization), (authorized.Outcome, authorized.Id));
    }

    [DatabaseFact]
    public async Task NullArgumentIsRefusedByItsName()
    {
        await using var world = await ContentWorld.CreateAsync();

        string?[] names =
        [
            Assert.Throws<ArgumentNullException>(() => new ReversalRequestReader(null!, world.Reader, TimeProvider.System)).ParamName,
            Assert.Throws<ArgumentNullException>(() => new ReversalRequestReader(world.DataSource, null!, TimeProvider.System)).ParamName,
            Assert.Throws<ArgumentNullException>(() => new ReversalRequestReader(world.DataSource, world.Reader, null!)).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => world.ReversalRequestReader.ListAsync(null!, Token))).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => world.ReversalRequests.AuthorizeAsync(null!, DateTimeOffset.UtcNow.AddDays(1), Token))).ParamName,
            (await Assert.ThrowsAsync<ArgumentNullException>(() => world.ReversalRequests.DeclineAsync(null!, Token))).ParamName,
        ];

        Assert.Equal(["dataSource", "reader", "time", "listing", "act", "act"], names);
    }

    [DatabaseTheory]
    [InlineData(0, 1, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    public async Task AnActStoppedBehindAnotherIsTriedAgainUpToThreeTimes(int refusedFirst, long attempts, bool written)
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = await SecondCuratorAsync(world);
        var request = await ReversalRows.RequestAsync(world, 1, 3);
        await world.Database.RunAsOwnerAsync(RefusingTrigger(refusedFirst));

        var authorized = await world.ReversalRequests.AuthorizeAsync(new ReversalAct(1, request, second), DateTimeOffset.UtcNow.AddDays(7), Token);
        var tried = Convert.ToInt64(await world.ScalarAsync(Attempts), CultureInfo.InvariantCulture);

        Assert.Equal(
            (written ? ReversalOutcome.Recorded : ReversalOutcome.NotRecorded, written ? null : CustodyOutcome.Conflict, attempts, written ? 1L : 0L),
            (authorized.Outcome, authorized.Refusal?.Outcome, tried, await CountAsync(world, CountAuthorizations)));
    }

    private static ReversalListing Mine(int page) => new() { DomainId = 1, PersonId = 3, Mine = true, Page = page };

    private static ReversalListing Others(long personId) => new() { DomainId = 1, PersonId = personId, Mine = false };

    private static long[] Ids(ReversalPage page) => [.. page.Items.Select(item => item.Id)];

    private static string[] RefusingTrigger(int refusedFirst) =>
    [
        "CREATE SEQUENCE public.zqx_attempts",
        "CREATE FUNCTION public.zqx_refuse() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + $"BEGIN IF pg_catalog.nextval('public.zqx_attempts') <= {refusedFirst} THEN RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END IF; RETURN NEW; END $f$",
        "CREATE TRIGGER aaa_zqx_refuse BEFORE INSERT ON content.reversal_authorization FOR EACH ROW EXECUTE FUNCTION public.zqx_refuse()",
        "SELECT pg_catalog.setval('public.zqx_attempts', 1, false)",
    ];
}
