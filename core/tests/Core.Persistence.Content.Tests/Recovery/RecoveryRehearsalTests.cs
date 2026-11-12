using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Recovery;

/// <summary>
/// The rehearsal of the recovery over a copy (issue 67, CA-1): it opens in memory a sample of the raw text and of the reversal map of every domain, counts what opened and what did not, tells the top
/// of the chain of the copy, writes no reading to the chain and shows no content.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecoveryRehearsalTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Entries = "mapa de reversao sintetico zqx";
    private const string Tip = "SELECT sequence || ',' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string Proof = "0123456789abcdef";

    [DatabaseFact]
    public async Task ASampleOfTheRawTextAndOfTheMapOfEveryDomainOpensAndTheReceiptTellsTheTopOfTheCopyAndNoContent()
    {
        await using var world = await WorldWithAChainAsync();
        await AddAsync(world, 1, 1);
        await AddAsync(world, 2, 1);
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        var receipt = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal((new SampleCounts(2, 2, 0), true, Proof), (receipt.Counts, receipt.Counts.Holds, receipt.KeyProof));
        Assert.Equal(await world.ScalarAsync(Tip), $"{receipt.TopSequence},{receipt.TopDigest}");
        Assert.DoesNotContain(Raw, receipt.ToText(), StringComparison.Ordinal);
        Assert.DoesNotContain(Entries, receipt.ToText(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheRehearsalWritesNoReadingAndNoEventToTheChainOfTheCopy()
    {
        await using var world = await WorldWithAChainAsync();
        await AddAsync(world, 1, 1);
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);
        var before = await world.ScalarAsync("SELECT count(*) FROM public.custody_event");

        _ = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal(before, await world.ScalarAsync("SELECT count(*) FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task OnlyASampleOfThreeOfEachPurposeAndDomainIsOpened()
    {
        await using var world = await WorldWithAChainAsync();
        await AddAsync(world, 1, 5);
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        var receipt = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal(new SampleCounts(3, 3, 0), receipt.Counts);
    }

    [DatabaseFact]
    public async Task ADomainWhoseLabelsTheCustodyDoesNotHoldCountsItsSamplesAsFailedAndTheRehearsalDoesNotHold()
    {
        await using var world = await WorldWithAChainAsync();
        await AddAsync(world, 1, 1);
        await AddAsync(world, 2, 1);
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels.Where(label => !label.Contains("/2/", StringComparison.Ordinal)));

        var receipt = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal((new SampleCounts(1, 1, 2), false), (receipt.Counts, receipt.Counts.Holds));
    }

    [DatabaseFact]
    public async Task ACopyWithNoContentGivesAReceiptThatDoesNotHold()
    {
        await using var world = await WorldWithAChainAsync();
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        var receipt = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal((new SampleCounts(0, 0, 0), false), (receipt.Counts, receipt.Counts.Holds));
    }

    [DatabaseFact]
    public async Task ACopyWithNoChainIsRefusedBecauseItIsNotACopyOfTheProduction()
    {
        await using var world = await ContentWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task TheMomentOfTheReceiptIsInWholeSecondsAndNotInTheFuture()
    {
        await using var world = await WorldWithAChainAsync();
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        var receipt = await Rehearsal(world, custody).RehearseAsync(Proof, TestContext.Current.CancellationToken);

        Assert.Equal((0L, true), (receipt.At.Ticks % TimeSpan.TicksPerSecond, receipt.At <= DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task NothingIsOpenedWithoutItsPartsAndAnEmptyProofIsADefectOfTheCaller()
    {
        await using var source = Npgsql.NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding(ContentWorld.Labels);

        Assert.Equal(
            ["dataSource", "content", "time", "keyProof"],
            [
                Assert.Throws<ArgumentNullException>(() => new RecoveryRehearsal(null!, custody.Content, TimeProvider.System)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new RecoveryRehearsal(source, null!, TimeProvider.System)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new RecoveryRehearsal(source, custody.Content, null!)).ParamName,
                (await Assert.ThrowsAsync<ArgumentException>(
                    () => new RecoveryRehearsal(source, custody.Content, TimeProvider.System).RehearseAsync(string.Empty, TestContext.Current.CancellationToken))).ParamName,
            ]);
    }

    private static RecoveryRehearsal Rehearsal(ContentWorld world, SyntheticCustody custody) => new(world.DataSource, custody.Content, TimeProvider.System);

    private static async Task<ContentWorld> WorldWithAChainAsync()
    {
        var world = await ContentWorld.CreateAsync();
        var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(world.DataSource).AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        return world;
    }

    private static async Task AddAsync(ContentWorld world, int domain, int items)
    {
        for (var index = 0; index < items; index++)
        {
            var item = await world.Writer.AddItemTextAsync(domain, ItemTexts.Of(Raw), TestContext.Current.CancellationToken);
            _ = await world.Writer.AddReversalMapAsync(domain, item, Entries, TestContext.Current.CancellationToken);
        }
    }
}
