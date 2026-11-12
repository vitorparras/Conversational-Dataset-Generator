using System.Text;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Submissions;
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

    /// <summary>A file of its own for each submission, so that none of them is a repeat of another.</summary>
    private static SubmissionRequest Distinct(int at) => SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", $"ZQXum {at}"), ("ZQXDOIS", $"ZQXdois {at}")));

    [DatabaseFact]
    public async Task EightSubmissionsMadeTogetherAreEachKeptOrToldToTryAgainAndEveryBatchThatWasNotKeptIsRejectedForTheCollisionAndTheChainIsWhole()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Simultaneous).Select(at => Task.Run(() => world.SubmitAsync(Distinct(at)), TestContext.Current.CancellationToken)));

        var kept = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Done);
        var told = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Collided);
        var entries = await world.Service.ListAsync(new BatchFilter { Domains = [SubmissionWorld.Domain], Take = 100 }, TestContext.Current.CancellationToken);
        var open = entries.Count(entry => entry.State == BatchState.Open);
        var rejected = entries.Where(entry => entry.State == BatchState.Rejected).Select(entry => entry.Reason).ToArray();
        var chain = string.Join(',', await world.Database.FirstColumnAsOwnerAsync(ChainInOrder));
        Assert.Equal(
            (Simultaneous, kept, 0, kept * 2L, "true", true),
            (kept + told, open, entries.Count(entry => entry.State == BatchState.Accepted), await world.ItemRowsAsync(), chain, rejected.Length <= told && entries.Count == kept + rejected.Length));
        Assert.All(rejected, reason => Assert.Equal(SubmissionStoring.Collision, reason));
    }

    [DatabaseFact]
    public async Task OpeningThatMeetsAnotherEventOfTheChainIsToldToTryAgainWithNoBatch()
    {
        await using var world = await SubmissionWorld.CreateAsync(attempts: 1);

        var outcome = await SubmissionContention.WhilePendingAsync(world, () => world.SubmitAsync(SubmissionWorld.Request()));

        Assert.Equal((SubmissionKind.Collided, null, 0L, 0L), (outcome.Kind, outcome.Batch, await world.BatchCountAsync(), await world.ItemRowsAsync()));
    }

    private static async Task<(int Status, string RetryAfter, string Body)> AnswerAsync(SubmissionOutcome outcome)
    {
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Response.Body = body;

        await SubmissionAnswer.Of(outcome).ExecuteAsync(context);

        return (context.Response.StatusCode, context.Response.Headers.RetryAfter.ToString(), Encoding.UTF8.GetString(body.ToArray()));
    }
}
