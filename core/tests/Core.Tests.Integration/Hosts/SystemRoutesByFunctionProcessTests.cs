using System.Net;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The routes of JSON of the configuration, of the versions and of the analysis answer by the function of the person who entered, as their screens do (policy 006, #827): whoever lacks it
/// meets a route that does not exist, and the audit reads the configuration and the versions but not the content of a text.
/// </summary>
[Trait("Category", "Database")]
public sealed class SystemRoutesByFunctionProcessTests
{
    private const string Analysis = "/analysis";
    private const string AnalysisBody = /*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt"}""";

    [DatabaseFact]
    public async Task AnAnnotatorMeetsNoneOfTheThreeRoutes()
    {
        var statuses = await StatusesAsync(PersonFunctions.Accepted[0]);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound), statuses);
    }

    [DatabaseFact]
    public async Task TheAuditReadsTheConfigurationAndTheVersionsButNotTheContentOfAText()
    {
        var statuses = await StatusesAsync(PersonFunctions.Audit);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound), statuses);
    }

    [DatabaseFact]
    public async Task TheTechnicalAdministrationReachesTheThreeRoutes()
    {
        var statuses = await StatusesAsync(PersonFunctions.Administration);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), statuses);
    }

    private static async Task<(HttpStatusCode Configuration, HttpStatusCode Versions, HttpStatusCode Analysis)> StatusesAsync(string function)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = function }, cancellationToken);

        using var configuration = await host.GetAsync("/configuration", cancellationToken);
        using var versions = await host.GetAsync("/versions", cancellationToken);
        using var analysis = await host.PostAsync(Analysis, AnalysisBody, cancellationToken);

        return (configuration.StatusCode, versions.StatusCode, analysis.StatusCode);
    }
}
