using System.Text;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// Submissions that reach the same link of the chain together (issue 75): the answer of a collision tells the caller to try again and says it by a code of its own,
/// apart from the database being down, and submissions made together are each kept or told to try again, never rejected as a failure of the writing.
/// </summary>
public sealed class SubmissionCollisionTests
{
    private const int Simultaneous = 8;
    private const string ChainInOrder = "SELECT (count(*) = max(sequence) AND min(sequence) = 1)::text FROM public.custody_event";

    [Fact]
    public async Task CollisionIsA503ThatAsksToTryAgainAndSaysItByItsCodeAndNothingElse()
    {
        var (status, retryAfter, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.Collided, Batch = 7 });

        using var json = JsonDocument.Parse(body);
        var names = json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            (StatusCodes.Status503ServiceUnavailable, "1", "concurrent_submission", 7L),
            (status, retryAfter, json.RootElement.GetProperty("code").GetString(), json.RootElement.GetProperty("batch").GetInt64()));
        Assert.DoesNotContain(names, name => name is "file" or "name" or "origin");
        Assert.DoesNotContain(names, name => name is "text" or "material");
    }

    [Fact]
    public async Task CollisionThatMetTheOpeningSaysNoBatchAndKeepsTheCode()
    {
        var (status, _, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.Collided });

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal((StatusCodes.Status503ServiceUnavailable, "concurrent_submission", JsonValueKind.Null), (status, root.GetProperty("code").GetString(), root.GetProperty("batch").ValueKind));
    }

    [Fact]
    public async Task DatabaseThatIsDownIsA503WithNoBodyAndNoRetryAfterAndNoCodeOfCollision()
    {
        var (status, retryAfter, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = BatchOutcome.NotRecorded });

        Assert.Equal((StatusCodes.Status503ServiceUnavailable, string.Empty, string.Empty), (status, retryAfter, body));
    }

    [DatabaseFact]
    public async Task EightSubmissionsMadeTogetherAreEachKeptOrToldToTryAgainAndEveryBatchThatWasNotKeptIsRejectedForTheCollisionAndTheChainIsWhole()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Simultaneous).Select(_ => Task.Run(() => world.SubmitAsync(SubmissionWorld.Request()), TestContext.Current.CancellationToken)));

        var kept = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Done);
        var told = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Collided);
        var entries = await world.Service.ListAsync(new BatchFilter { Domains = [SubmissionWorld.Domain], Take = 100 }, TestContext.Current.CancellationToken);
        var accepted = entries.Count(entry => entry.State == BatchState.Accepted);
        var rejected = entries.Where(entry => entry.State == BatchState.Rejected).Select(entry => entry.Reason).ToArray();
        var chain = string.Join(',', await world.Database.FirstColumnAsOwnerAsync(ChainInOrder));
        Assert.Equal(
            (Simultaneous, kept, 0, kept * 2L, "true", true),
            (kept + told, accepted, entries.Count(entry => entry.State == BatchState.Open), await world.ItemRowsAsync(), chain, rejected.Length <= told && entries.Count == kept + rejected.Length));
        Assert.All(rejected, reason => Assert.Equal(SubmissionStoring.Collision, reason));
    }

    [DatabaseFact]
    public async Task OpeningThatMeetsAnotherEventOfTheChainIsToldToTryAgainWithNoBatch()
    {
        await using var world = await SubmissionWorld.CreateAsync(attempts: 1);

        var outcome = await SubmissionContention.WhilePendingAsync(world, () => world.SubmitAsync(SubmissionWorld.Request()));

        Assert.Equal((SubmissionKind.Collided, null, 0L, 0L), (outcome.Kind, outcome.Batch, await world.BatchCountAsync(), await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task AcceptanceThatMeetsAnotherEventOfTheChainRejectsTheBatchForTheCollisionWithNoItemAndSaysItCollided()
    {
        await using var world = await SubmissionWorld.CreateAsync(attempts: 1);
        var opened = await world.Batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person), TestContext.Current.CancellationToken);
        var instant = new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
        var material = new ReadMaterial([new ReadUnit("ZQXTERCEIRO", "ZQXUM", instant, null, []), new ReadUnit("ZQXQUARTO", "ZQXDOIS", instant, null, [])]);
        var kept = new SubmissionStoring.Kept(SubmissionWorld.Domain, opened.BatchId!.Value, material, MaterialForm.Conversation);
        var log = new SubmissionLog();
        var intake = SubmissionWorld.Intake(1_000_000, OriginConditions.Of([SubmissionWorld.Strictest, SubmissionWorld.Loosest]), world.Content, world.DataSource);

        var keeping = await SubmissionContention.WhilePendingAsync(world, () => new SubmissionStoring.Keeper(world.Batches, intake, log).KeepAsync(kept, TestContext.Current.CancellationToken));

        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], kept.BatchId, TestContext.Current.CancellationToken);
        var items = await world.ItemRowsAsync();
        Assert.Equal((SubmissionStoring.Keeping.Collided, BatchState.Rejected, SubmissionStoring.Collision, 0L, 0), (keeping, entry?.State, entry?.Reason, items, log.Lines.Count));
    }

    private static async Task<(int Status, string RetryAfter, string Body)> AnswerAsync(SubmissionOutcome outcome)
    {
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Response.Body = body;

        await SubmissionAnswer.Of(outcome).ExecuteAsync(context);

        return (context.Response.StatusCode, context.Response.Headers.RetryAfter.ToString(), Encoding.UTF8.GetString(body.ToArray()));
    }
}
