using System.Net;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The destinations of a submission over the host (#841, CA-1 of #609): with the service of the language gone, the batch screen says in words why it was rejected,
/// and once the submission recognizes the passages the detail counts them by destination.
/// </summary>
[Trait("Category", "Database")]
public sealed class SubmissionDestinationsRouteTests
{
    private const string AnalysisUnavailable =
        "O serviço de análise de linguagem não respondeu, e sem a análise não há como saber o que remover. " +
        "Nenhum item foi gravado. Envie o mesmo arquivo de novo quando o serviço voltar.";

    [DatabaseFact]
    public async Task RejectedForAnalysisShowsTheReasonOnTheBatchScreen()
    {
        await using var world = await SubmissionScreensRouteTests.World.StartAsync(recognizer: new FakeRecognizer());
        var (status, _, _) = await world.PostAsync(SubmissionScreensRouteTests.Fields());
        var batch = Assert.Single(await world.BatchesAsync());

        var page = await world.PageAsync(ScreenPaths.BatchOf(batch.Id));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, BatchState.Rejected), (status, batch.State));
        Assert.Contains(AnalysisUnavailable, page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchDetailCountsThePassagesByDestination()
    {
        await using var world = await SubmissionScreensRouteTests.World.StartAsync(recognizer: new FakeRecognizer());
        var content = SubmissionWorld.Messages(("ZQXUM", "ZQX-FAKE-PERSON ZQX-FAKE-PERSON"), ("ZQXDOIS", "ZQX-FAKE-PERSON"));
        var (_, location, _) = await world.PostAsync(SubmissionScreensRouteTests.Fields(), content: content);

        var detail = await world.PageAsync(location!);

        Assert.Matches(@"<dt>Pseudonimizados</dt>\s*<dd>3 trechos</dd>", detail);
        Assert.DoesNotContain(BatchDetailTexts.NotCounted, detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchScreenShowsTheEditsAndTheItemsChangedOfEachStepApart()
    {
        // Two credentials in every text: two items carry four edits, and the screen read from the database must not swap one count for the other.
        static Occurrence Credential(int start) => new(start, start + 3, "CREDENTIAL", "PROPN", "nsubj");
        var twice = new Analysis.AnsweringRecognizer(RecognitionOutcome.Analyzed(new AnalysisResponse([Credential(0), Credential(6)], "zqx-model-0027")));
        await using var world = await SubmissionScreensRouteTests.World.StartAsync(recognizer: twice);
        var content = SubmissionWorld.Messages(("ZQXUM", "ZQXSEMENTE01"), ("ZQXDOIS", "ZQXSEMENTE02"));
        var (_, location, _) = await world.PostAsync(SubmissionScreensRouteTests.Fields(), content: content);

        var detail = await world.PageAsync(location!);

        Assert.Matches(@"<th[^>]*>\s*3\. Aplicação dos destinos\s*</th>\s*<td[^>]*>\s*4\s*</td>\s*<td[^>]*>\s*2\s*</td>", detail);
    }
}
