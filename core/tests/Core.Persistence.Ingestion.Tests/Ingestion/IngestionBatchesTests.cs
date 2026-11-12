using System.Reflection;
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
    private const string TheReview = "batch_sampled:administrative_act:-,batch_reviewed:review_clean:-";
    private const string Unreachable = "0,0";

    [Fact]
    public void OpeningTakesTheDomainThePersonAndTheConditionAndNoContent() =>
        Assert.Equal(
            [typeof(BatchOpening), typeof(CancellationToken)],
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
        var batches = new IngestionBatches(dataSource, TestConditions.Known);
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
            (new IngestionBatch(BatchState.Open, null, IngestionWorld.Person, TestConditions.Strictest), Unreachable),
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

        Assert.Equal($"{OnlyTheOpening},{TheReview},batch_closed:batch_accepted:-,{OnlyTheOpening},batch_closed:batch_rejected:{Reason}", events);
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
            (BatchOutcome.Rejected, new IngestionBatch(BatchState.Rejected, Reason, IngestionWorld.Person, TestConditions.Strictest), Unreachable),
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

        var person = await batches.OpenAsync(new BatchOpening(IngestionWorld.Domain, 9_999), TestContext.Current.CancellationToken);
        var domain = await batches.OpenAsync(new BatchOpening(99, IngestionWorld.Person), TestContext.Current.CancellationToken);

        Assert.Equal((BatchOutcome.AuthorUnknown, BatchOutcome.DomainUnknown, null), (person.Outcome, domain.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
    }

    [DatabaseFact]
    public async Task PersonWhoIsNotActiveCannotOpenABatchAndTheChainSaysSo()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var gone = await world.PersonAsync("zqx-gone");

        var refused = await IngestionWorld.BatchesOf(world).OpenAsync(new BatchOpening(IngestionWorld.Domain, gone), TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, null),
            (refused.Outcome, refused.Refusal?.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
    }

    [Fact]
    public void ArgumentsThatAreNotPositiveOrAreMissingAreRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource, TestConditions.Known);

        Assert.Equal(
            ["opening.DomainId", "opening.AuthorId", "closing", "domainId", "batchId", "dataSource"],
            [
                Param(() => batches.OpenAsync(new BatchOpening(0, 1), CancellationToken.None)),
                Param(() => batches.OpenAsync(new BatchOpening(1, 0), CancellationToken.None)),
                Param(() => batches.AcceptAsync(null!, CancellationToken.None)),
                Param(() => batches.ReadAsync(0, 1, CancellationToken.None)),
                Param(() => batches.ReadAsync(1, 0, CancellationToken.None)),
                Assert.Throws<ArgumentNullException>(() => new IngestionBatches(null!, TestConditions.Known)).ParamName,
            ]);
    }

    [Fact]
    public void ClosingThatIsMissingOrWithoutAuthorOrNumbersIsRefusedByNameForBothTheAcceptanceAndTheRejection()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var batches = new IngestionBatches(dataSource, TestConditions.Known);
        var none = new BatchClosing(1, 1, null!);
        var noDomain = new BatchClosing(0, 1, CustodyAuthor.System);
        var noBatch = new BatchClosing(1, 0, CustodyAuthor.System);

        Assert.Equal(
            ["closing", "closing.Author", "closing.DomainId", "closing.BatchId", "closing", "closing.Author", "closing.DomainId", "closing.BatchId"],
            [
                Param(() => batches.AcceptAsync(null!, CancellationToken.None)),
                Param(() => batches.AcceptAsync(none, CancellationToken.None)),
                Param(() => batches.AcceptAsync(noDomain, CancellationToken.None)),
                Param(() => batches.AcceptAsync(noBatch, CancellationToken.None)),
                Param(() => batches.RejectAsync(null!, Reason, CancellationToken.None)),
                Param(() => batches.RejectAsync(none, Reason, CancellationToken.None)),
                Param(() => batches.RejectAsync(noDomain, Reason, CancellationToken.None)),
                Param(() => batches.RejectAsync(noBatch, Reason, CancellationToken.None)),
            ]);
    }

    [DatabaseFact]
    public async Task AcceptanceAndRejectionAnswerWithTheirOutcomeTheBatchAndTheSequenceOfTheirEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        var rejected = await IngestionWorld.OpenAsync(world);

        var acceptance = await IngestionWorld.AcceptAsync(world, accepted);
        var rejection = await IngestionWorld.RejectAsync(world, rejected, Reason);

        var closings = await world.ScalarAsync("SELECT string_agg(sequence::text, ',' ORDER BY sequence) FROM public.custody_event WHERE step = 'batch_closed'") as string;
        Assert.Equal(
            (BatchOutcome.Accepted, accepted, BatchOutcome.Rejected, rejected, $"{acceptance.Sequence},{rejection.Sequence}"),
            (acceptance.Outcome, acceptance.BatchId, rejection.Outcome, rejection.BatchId, closings));
    }

    [DatabaseFact]
    public async Task ABatchAcceptedOrRejectedIsReadWithItsStateItsMotiveAndItsAuthor()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        var rejected = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        _ = await IngestionWorld.RejectAsync(world, rejected, Reason);
        var batches = IngestionWorld.BatchesOf(world);

        Assert.Equal(
            (
                new IngestionBatch(BatchState.Accepted, null, IngestionWorld.Person, TestConditions.Strictest),
                new IngestionBatch(BatchState.Rejected, Reason, IngestionWorld.Person, TestConditions.Strictest)),
            (
                await batches.ReadAsync(IngestionWorld.Domain, accepted, TestContext.Current.CancellationToken),
                await batches.ReadAsync(IngestionWorld.Domain, rejected, TestContext.Current.CancellationToken)));
    }

    [DatabaseFact]
    public async Task ClosingByAPersonWhoIsNotActiveIsToldSoByTheChainAndLeavesTheBatchOpenWithNoNewEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var closing = new BatchClosing(IngestionWorld.Domain, batch, CustodyAuthor.Person(await world.PersonAsync("zqx-gone")));
        var batches = IngestionWorld.BatchesOf(world);
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, batch, IngestionWorld.Person);

        var accepted = await batches.AcceptAsync(closing, TestContext.Current.CancellationToken);
        var rejected = await batches.RejectAsync(closing, Reason, TestContext.Current.CancellationToken);
        var held = await batches.ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, BatchOutcome.NotRecorded, CustodyOutcome.ActorDeactivated, BatchState.Open, $"{OnlyTheOpening},{TheReview}"),
            (accepted.Outcome, accepted.Refusal?.Outcome, rejected.Outcome, rejected.Refusal?.Outcome, held?.State, await world.ScalarAsync(IngestionWorld.Events) as string));
    }

    private static string? Param(Func<object?> call) =>
        Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
