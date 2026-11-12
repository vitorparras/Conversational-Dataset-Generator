using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>Where an item came from (issue 80, CA-1): the batch that kept it, when the submission came and who sent it, found in the first domain given that holds it and nowhere else.</summary>
public sealed class BatchProvenanceTests
{
    private const int Other = IngestionWorld.Domain + 1;

    [DatabaseFact]
    public async Task AnItemIsTracedToItsBatchTheMomentOfTheSubmissionAndWhoSentIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var batch = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        var bare = await IngestionWorld.OpenAsync(world);
        var bareItem = await IngestionWorld.AddItemAsync(world, bare);
        _ = await IngestionWorld.AcceptAsync(world, bare);
        var register = new BatchRegister(world.DataSource);

        var traced = await register.ProvenanceAsync([IngestionWorld.Domain], item, token);
        var untraced = await register.ProvenanceAsync([IngestionWorld.Domain], bareItem, token);

        Assert.Equal((batch, IngestionWorld.Person, new string('a', 32), bare, null), (traced!.BatchId, traced.SubmittedBy, traced.Submission, untraced!.BatchId, untraced.Submission));
        Assert.InRange(traced.SubmittedAt, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
    }

    [DatabaseFact]
    public async Task AnItemIsFoundOnlyInADomainThatHoldsItEvenWhenTheDomainsAreRepeated()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var batch = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        var register = new BatchRegister(world.DataSource);

        Assert.Equal(
            [null, null, null, batch, batch],
            [
                (await register.ProvenanceAsync([Other], item, token))?.BatchId,
                (await register.ProvenanceAsync([], item, token))?.BatchId,
                (await register.ProvenanceAsync([IngestionWorld.Domain], item + 100, token))?.BatchId,
                (await register.ProvenanceAsync([Other, IngestionWorld.Domain, IngestionWorld.Domain], item, token))?.BatchId,
                (await register.ProvenanceAsync([IngestionWorld.Domain, Other], item, token))?.BatchId,
            ]);
    }

    [DatabaseFact]
    public async Task TheItemOfAnotherDomainIsTracedInThatDomainAlone()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var batch = await IngestionWorld.OpenFileAsync(world, Other, 'b');
        var item = await IngestionWorld.AddItemAsync(world, Other, batch);
        _ = await IngestionWorld.AcceptInAsync(world, Other, batch);
        var register = new BatchRegister(world.DataSource);

        Assert.Equal([null, batch], [(await register.ProvenanceAsync([IngestionWorld.Domain], item, token))?.BatchId, (await register.ProvenanceAsync([Other], item, token))?.BatchId]);
    }

    [Fact]
    public void DomainsThatAreMissingAndAnItemThatIsNotAnItemAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var register = new BatchRegister(dataSource);

        Assert.Equal(
            ["domains", "itemId", "itemId"],
            [
                Param(() => register.ProvenanceAsync(null!, 1, CancellationToken.None)),
                Param(() => register.ProvenanceAsync([1], 0, CancellationToken.None)),
                Param(() => register.ProvenanceAsync([1], -1, CancellationToken.None)),
            ]);
    }

    private static string? Param(Func<object?> call) => Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
