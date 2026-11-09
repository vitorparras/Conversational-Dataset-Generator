using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The register of the submissions as it is read (issue 75, lists and detail): only the domains given, any state, a batch rejected with its motive,
/// filtered and cut in pages by the server.
/// </summary>
public sealed class BatchRegisterTests
{
    private const string Reason = "too_large";
    private const int Other = IngestionWorld.Domain + 1;

    [DatabaseFact]
    public async Task RejectedBatchStaysInTheListWithItsMotiveAndWhatTheSubmissionBrought()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenInAsync(world, IngestionWorld.Domain, 'a', 10);
        _ = await IngestionWorld.RejectAsync(world, batch, Reason);

        var listed = await Register(world).ListAsync(new BatchFilter { Domains = [IngestionWorld.Domain] }, TestContext.Current.CancellationToken);

        var entry = Assert.Single(listed);
        Assert.Equal((batch, BatchState.Rejected, Reason, IngestionWorld.Person, 10L), (entry.Id, entry.State, entry.Reason, entry.SubmittedBy, entry.Receipt!.Bytes));
    }

    [DatabaseFact]
    public async Task ListShowsOnlyTheDomainsGivenAndNoneWhenNoneIsGiven()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var mine = await OpenInAsync(world, IngestionWorld.Domain, 'a', 1);
        var theirs = await OpenInAsync(world, Other, 'b', 1);
        var register = Register(world);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(
            $"{mine}|{theirs}|0|{theirs},{mine}",
            string.Join(
                '|',
                Seen(await register.ListAsync(new BatchFilter { Domains = [IngestionWorld.Domain] }, token)),
                Seen(await register.ListAsync(new BatchFilter { Domains = [Other] }, token)),
                (await register.ListAsync(new BatchFilter { Domains = [] }, token)).Count,
                Seen(await register.ListAsync(new BatchFilter { Domains = [IngestionWorld.Domain, Other] }, token))));
    }

    [DatabaseFact]
    public async Task ListFiltersByStateAndByThePeriodOfTheSubmission()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var open = await OpenInAsync(world, IngestionWorld.Domain, 'a', 1);
        var accepted = await OpenInAsync(world, IngestionWorld.Domain, 'b', 1);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var register = Register(world);
        var token = TestContext.Current.CancellationToken;
        var domains = new[] { IngestionWorld.Domain };
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(
            $"{accepted}|{open}|0|0|0|2",
            string.Join(
                '|',
                Seen(await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Accepted }, token)),
                Seen(await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Open }, token)),
                (await register.ListAsync(new BatchFilter { Domains = domains, State = BatchState.Rejected }, token)).Count,
                (await register.ListAsync(new BatchFilter { Domains = domains, From = now.AddDays(1) }, token)).Count,
                (await register.ListAsync(new BatchFilter { Domains = domains, Until = now.AddDays(-1) }, token)).Count,
                (await register.ListAsync(new BatchFilter { Domains = domains, From = now.AddDays(-1), Until = now.AddDays(1) }, token)).Count));
    }

    [DatabaseFact]
    public async Task ListIsNewestFirstAndTheServerCutsThePageAcrossDomains()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var first = await OpenInAsync(world, IngestionWorld.Domain, 'a', 1);
        var second = await OpenInAsync(world, Other, 'b', 1);
        var third = await OpenInAsync(world, IngestionWorld.Domain, 'c', 1);
        var register = Register(world);
        var domains = new[] { IngestionWorld.Domain, Other };
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(
            $"{third},{second},{first}|{second}|{first}|0",
            string.Join(
                '|',
                Seen(await register.ListAsync(new BatchFilter { Domains = domains }, token)),
                Seen(await register.ListAsync(new BatchFilter { Domains = domains, Skip = 1, Take = 1 }, token)),
                Seen(await register.ListAsync(new BatchFilter { Domains = domains, Skip = 2, Take = 5 }, token)),
                (await register.ListAsync(new BatchFilter { Domains = domains, Skip = 3 }, token)).Count));
    }

    [DatabaseFact]
    public async Task DetailIsFoundOnlyInADomainGivenAndNeverInAnotherOrWhenItDoesNotExist()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenInAsync(world, IngestionWorld.Domain, 'a', 7);
        var register = Register(world);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(
            $"{batch}|none|none|none",
            string.Join(
                '|',
                (await register.ReadAsync([Other, IngestionWorld.Domain], batch, token))!.Id,
                Name(await register.ReadAsync([Other], batch, token)),
                Name(await register.ReadAsync([], batch, token)),
                Name(await register.ReadAsync([IngestionWorld.Domain], 9_999, token))));
    }

    [Fact]
    public void ArgumentsThatAreMissingOrOutOfRangeAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var register = new BatchRegister(dataSource);
        var domains = new[] { 1 };

        Assert.Equal(
            ["dataSource", "filter", "filter.Skip", "filter.Skip", "filter.Take", "filter.Take", "batchId", "domains"],
            [
                Assert.Throws<ArgumentNullException>(() => new BatchRegister(null!)).ParamName,
                Param(() => register.ListAsync(null!, CancellationToken.None)),
                Param(() => register.ListAsync(new BatchFilter { Domains = domains, Skip = -1 }, CancellationToken.None)),
                Param(() => register.ListAsync(new BatchFilter { Domains = domains, Skip = BatchRegister.MostSkipped + 1 }, CancellationToken.None)),
                Param(() => register.ListAsync(new BatchFilter { Domains = domains, Take = 0 }, CancellationToken.None)),
                Param(() => register.ListAsync(new BatchFilter { Domains = domains, Take = BatchRegister.MostTaken + 1 }, CancellationToken.None)),
                Param(() => register.ReadAsync(domains, 0, CancellationToken.None)),
                Param(() => register.ReadAsync(null!, 1, CancellationToken.None)),
            ]);
    }

    private static BatchRegister Register(BatchesWorld world) => new(world.DataSource);

    private static string Seen(IEnumerable<BatchEntry> entries) => string.Join(',', entries.Select(entry => entry.Id));

    private static string Name(BatchEntry? entry) => entry is null ? "none" : entry.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<long> OpenInAsync(BatchesWorld world, int domain, char digit, long bytes)
    {
        var receipt = new SubmissionReceipt(new string(digit, 32), bytes, new string(digit, 64));
        var opening = new BatchOpening(domain, IngestionWorld.Person, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown));
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(opening, TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    private static string? Param(Func<object?> call) => Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
