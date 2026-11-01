using Core.Persistence.Tests.Fixtures;
using System.Net;

namespace Core.Tests.Integration.Privacy;

/// <summary>
/// The error path writes no more content than the normal one: a language service killed by its process number, the
/// analysis refused, the retry that names it and a body the route rejects all leave the marker in no destination, and
/// each of them is shown to have run by the line it wrote (issue #251, CA-5; requirement 021).
/// </summary>
[Trait("Category", "RealService")]
public sealed class KilledServiceLogTests
{
    [DatabaseFact]
    public async Task AnalysisAfterTheServiceWasKilledIsRefused()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, outcome.Refused);
    }

    [DatabaseFact]
    public async Task RefusedAnalysisIsLoggedAsInterruptedWithItsReference()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Contains($"[204] Análise {outcome.Interrupted} interrompida: ", outcome.LogText, StringComparison.Ordinal);
        Assert.Contains($"[204] Análise {outcome.Interrupted} interrompida: ", outcome.Stdout, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RetryNamingTheInterruptedAnalysisIsLoggedWithBothReferences()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, outcome.Retried);
        Assert.Contains($"[205] Análise ", outcome.LogText, StringComparison.Ordinal);
        Assert.Contains($"pedida para retomar a análise interrompida {outcome.Interrupted}", outcome.LogText, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BodyOutsideTheShapeOfTheRouteIsRejected()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Equal(HttpStatusCode.BadRequest, outcome.Rejected);
    }

    [DatabaseFact]
    public async Task ServiceKilledFromOutsideIsLoggedAsLost()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Contains("[102] Serviço de linguagem encerrou", outcome.LogText, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MarkerSeededInTheErrorPathReachesNoDestination()
    {
        var outcome = await KilledServiceRun.OutcomeAsync();

        Assert.Empty(outcome.Leaks);
    }
}
