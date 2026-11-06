using System.Reflection;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The batch of ingestion as the system uses it: it opens with no content, closes once, and a rejection leaves nothing of the material reachable (issue 74, criteria 1 to 5).</summary>
public sealed class IngestionBatchesTests
{
    private const string Reason = "invalid_rows";
    private const string OnlyTheOpening = "batch_opened:administrative_act:-";
    private const string Unreachable = "0,0";

    [Fact]
    public void OpeningTakesTheDomainAndThePersonAndNoContent() =>
        Assert.Equal(
            [typeof(int), typeof(long), typeof(CancellationToken)],
            typeof(IngestionBatches).GetMethod(nameof(IngestionBatches.OpenAsync))!.GetParameters().Select(parameter => parameter.ParameterType));

    [Fact]
    public void NoMethodOfTheBatchesTakesBytesAndOnlyTheRejectionTakesText()
    {
        var methods = typeof(IngestionBatches).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var texts = methods.SelectMany(method => method.GetParameters().Select(parameter => (method.Name, parameter.ParameterType)))
            .Where(taken => taken.ParameterType == typeof(string) || taken.ParameterType == typeof(byte[]) || taken.ParameterType == typeof(ReadOnlyMemory<byte>));

        Assert.Equal([(nameof(IngestionBatches.RejectAsync), typeof(string))], texts);
    }

    [Fact]
    public async Task MotiveThatIsNotAWordOfTheClosedFormIsRefusedBeforeAnyConnectionOpens()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource);
        var closing = new BatchClosing(1, 1, CustodyAuthor.System);

        var refused = await Task.WhenAll(
            new[] { string.Empty, "Upper", "1starts_with_digit", "has space", "with-dash", new string('a', 64), "texto livre zqx" }
                .Select(motive => batches.RejectAsync(closing, motive, TestContext.Current.CancellationToken)));

        Assert.All(refused, result => Assert.Equal(BatchOutcome.MotiveRefused, result.Outcome));
    }

    [DatabaseFact]
    public async Task OpenedBatchIsOpenWithItsAuthorAndHoldsNothing()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await IngestionWorld.OpenAsync(world);
        var held = await IngestionWorld.BatchesOf(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(
            (new IngestionBatch(BatchState.Open, null, IngestionWorld.Person), Unreachable),
            (held, await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string));
    }

    [DatabaseFact]
    public async Task OpeningAndClosingEachLeaveAnEventAndTheChainStillVerifies()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var rejected = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, rejected, Reason);

        var events = await world.ScalarAsync(IngestionWorld.Events) as string;
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"{OnlyTheOpening},batch_closed:batch_accepted:-,{OnlyTheOpening},batch_closed:batch_rejected:{Reason}", events);
        Assert.True(verified.Passed);
    }

    [DatabaseFact]
    public async Task ItemsOfAnOpenBatchAreNotInTheStockUntilTheBatchIsAccepted()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddMapAsync(world, batch, item);
        var whileOpen = await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable);

        var accepted = await IngestionWorld.AcceptAsync(world, batch);

        Assert.Equal(
            (Unreachable, BatchOutcome.Accepted, "1,1"),
            (whileOpen as string, accepted.Outcome, await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string));
    }

    [DatabaseFact]
    public async Task RejectedBatchLeavesNoItemReachableAndStaysConsultableWithItsMotive()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddMapAsync(world, batch, item);

        var rejected = await IngestionWorld.RejectAsync(world, batch, Reason);
        var held = await IngestionWorld.BatchesOf(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.Rejected, new IngestionBatch(BatchState.Rejected, Reason, IngestionWorld.Person), Unreachable),
            (rejected.Outcome, held, await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string));
    }

    [DatabaseFact]
    public async Task FailureInTheMiddleOfTheBatchEndsWithNoItemReachableOnceTheBatchIsRejected()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await Assert.ThrowsAsync<PostgresException>(() => IngestionWorld.AddMapAsync(world, batch, 9_999));

        var rejected = await IngestionWorld.RejectAsync(world, batch, "write_failed");

        Assert.Equal(
            (BatchOutcome.Rejected, Unreachable),
            (rejected.Outcome, await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string));
    }

    [DatabaseFact]
    public async Task ContentIsNotAddedToABatchThatClosed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, accepted);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var rejected = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, rejected, Reason);

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, accepted));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, rejected));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddMapAsync(world, accepted, item));
    }

    [DatabaseFact]
    public async Task ContentIsNotAddedToABatchOfAnotherDomainOrOneThatDoesNotExist()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = new NewItemText("texto bruto sintetico zqx");

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => world.RealWriter.AddItemTextAsync(2, batch, item, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => world.RealWriter.AddItemTextAsync(IngestionWorld.Domain, 9_999, item, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task BatchThatClosedClosesNoMoreAndTheSecondAttemptLeavesNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        var events = await world.ScalarAsync(IngestionWorld.Events) as string;

        var again = await IngestionWorld.AcceptAsync(world, batch);
        var rejection = await IngestionWorld.RejectAsync(world, batch, Reason);

        Assert.Equal(
            (BatchOutcome.AlreadyClosed, BatchOutcome.AlreadyClosed, events),
            (again.Outcome, rejection.Outcome, await world.ScalarAsync(IngestionWorld.Events) as string));
    }

    [DatabaseFact]
    public async Task BatchOfAnotherDomainOrOneThatDoesNotExistIsNotFoundAndLeavesNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var batches = IngestionWorld.BatchesOf(world);

        var elsewhere = await batches.AcceptAsync(new BatchClosing(2, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);
        var missing = await IngestionWorld.AcceptAsync(world, 9_999);

        Assert.Equal(
            (BatchOutcome.NotFound, BatchOutcome.NotFound, OnlyTheOpening),
            (elsewhere.Outcome, missing.Outcome, await world.ScalarAsync(IngestionWorld.Events) as string));
        Assert.Null(await batches.ReadAsync(2, batch, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task PersonOrDomainThatDoesNotExistCannotOpenABatchAndLeavesNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);

        var person = await batches.OpenAsync(IngestionWorld.Domain, 9_999, TestContext.Current.CancellationToken);
        var domain = await batches.OpenAsync(99, IngestionWorld.Person, TestContext.Current.CancellationToken);

        Assert.Equal((BatchOutcome.AuthorUnknown, BatchOutcome.DomainUnknown, null), (person.Outcome, domain.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
    }

    [DatabaseFact]
    public async Task PersonWhoIsNotActiveCannotOpenABatchAndTheChainSaysSo()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var gone = await world.PersonAsync("zqx-gone");

        var refused = await IngestionWorld.BatchesOf(world).OpenAsync(IngestionWorld.Domain, gone, TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, null),
            (refused.Outcome, refused.Refusal?.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
    }

    [Fact]
    public void ArgumentsThatAreNotPositiveOrAreMissingAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource);

        Assert.Equal(
            ["domainId", "authorId", "closing", "domainId", "batchId", "dataSource"],
            [
                Param(() => batches.OpenAsync(0, 1, CancellationToken.None)),
                Param(() => batches.OpenAsync(1, 0, CancellationToken.None)),
                Param(() => batches.AcceptAsync(null!, CancellationToken.None)),
                Param(() => batches.ReadAsync(0, 1, CancellationToken.None)),
                Param(() => batches.ReadAsync(1, 0, CancellationToken.None)),
                Assert.Throws<ArgumentNullException>(() => new IngestionBatches(null!)).ParamName,
            ]);
    }

    private static string? Param(Func<object?> call) =>
        Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
