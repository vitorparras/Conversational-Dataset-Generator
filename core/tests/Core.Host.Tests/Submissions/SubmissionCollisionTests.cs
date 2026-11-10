using System.Text;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Submissions;
using Core.Persistence.Ingestion;
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

    [Fact]
    public async Task CollisionIsA503ThatAsksToTryAgainAndSaysItByItsCodeAndNothingElse()
    {
        var (status, retryAfter, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.Collided, Batch = 7 });

        using var json = JsonDocument.Parse(body);
        var names = json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            (StatusCodes.Status503ServiceUnavailable, "1", "concurrent_submission", 7L),
            (status, retryAfter, json.RootElement.GetProperty("code").GetString(), json.RootElement.GetProperty("batch").GetInt64()));
        Assert.DoesNotContain(names, name => name is "file" or "name" or "origin" or "text" or "material");
    }

    [Fact]
    public async Task CollisionThatMetTheOpeningSaysNoBatchAndKeepsTheCode()
    {
        var (status, _, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.Collided });

        using var json = JsonDocument.Parse(body);
        Assert.Equal((StatusCodes.Status503ServiceUnavailable, "concurrent_submission", JsonValueKind.Null), (status, json.RootElement.GetProperty("code").GetString(), json.RootElement.GetProperty("batch").ValueKind));
    }

    [Fact]
    public async Task DatabaseThatIsDownIsA503WithNoBodyAndNoRetryAfterAndNoCodeOfCollision()
    {
        var (status, retryAfter, body) = await AnswerAsync(new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = BatchOutcome.NotRecorded });

        Assert.Equal((StatusCodes.Status503ServiceUnavailable, string.Empty, string.Empty), (status, retryAfter, body));
    }

    [DatabaseFact]
    public async Task EightSubmissionsMadeTogetherAreEachKeptOrToldToTryAgainAndNoneIsRejectedAsAFailureOfTheWriting()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Simultaneous).Select(_ => Task.Run(() => world.SubmitAsync(SubmissionWorld.Request()), TestContext.Current.CancellationToken)));

        var kept = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Done);
        var told = outcomes.Count(outcome => outcome.Kind == SubmissionKind.Collided);
        var opened = outcomes.Count(outcome => outcome.Batch is not null);
        Assert.Equal((Simultaneous, opened, false), (kept + told, await world.BatchCountAsync(), outcomes.Any(outcome => outcome.Code == "write_failed")));
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
