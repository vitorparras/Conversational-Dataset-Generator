using Core.Boundary.Http;
using Core.Host.Submissions;
using Core.Host.Tests.Analysis;
using Core.Host.Tests.Supervision;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Submissions;

/// <summary>An answer of the language service with an offset outside the text sent is refused at the boundary, and the batch is rejected before any item exists.</summary>
public sealed class SubmissionOffsetTests : IDisposable
{
    private const string OutsideTheText =
        /*lang=json,strict*/ """{"occurrences":[{"start":0,"end":99999,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"nsubj"}],"model_version":null}""";

    private const string InsideTheText =
        /*lang=json,strict*/ """{"occurrences":[{"start":0,"end":1,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"nsubj"}],"model_version":null}""";

    private readonly HttpClient client = LoopbackHttpClient.Create();

    [DatabaseFact]
    public async Task OffsetOutsideTheTextRejectsTheBatchWithNoItem()
    {
        using var service = new AnsweringService(OutsideTheText);
        await using var world = await SubmissionWorld.CreateAsync(recognizer: RecognizerOf(service));

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());

        var detail = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);
        Assert.Equal(
            (SubmissionKind.Unavailable, BatchState.Rejected, "analysis_unavailable", 0L),
            (outcome.Kind, detail!.State, detail.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task OffsetInsideTheTextFromTheSameServiceKeepsTheBatch()
    {
        using var service = new AnsweringService(InsideTheText);
        await using var world = await SubmissionWorld.CreateAsync(recognizer: RecognizerOf(service));

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());

        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        Assert.NotEqual(0L, await world.ItemRowsAsync());
    }

    public void Dispose() => client.Dispose();

    private HttpRecognizer RecognizerOf(AnsweringService service) =>
        new(client, new HttpRecognizerSettings(service.Port, service.Secret, SupervisionHarness.Timeout), NullLogger<HttpRecognizer>.Instance);
}
