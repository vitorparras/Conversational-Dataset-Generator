using System.Net;
using System.Text.Json;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The status route by who asks (#632): with no session, alive and ready alone (CA-1); with a session of the technical administration or the audit, the detail (CA-2);
/// with a session of another function, the same answer as a route that does not exist (CA-3).
/// </summary>
[Trait("Category", "Database")]
public sealed class StatusDetailHostTests
{
    [DatabaseFact]
    public async Task WithNoSessionTheStatusSaysAliveAndReadyAndNothingOfTheChecksTheInstallationOrTheComparison()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);

        using var response = await host.SendAsAsync(HttpMethod.Get, "/status", json: null, session: null, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["alive", "ready"], body.RootElement.EnumerateObject().Select(field => field.Name));
    }

    [DatabaseTheory]
    [InlineData("Administração técnica")]
    [InlineData("Auditoria")]
    public async Task ASessionOfTheAdministrationOrTheAuditReadsTheDetail(string function)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = function }, cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("alive").GetBoolean());
        Assert.Contains(body.RootElement.GetProperty("checks").EnumerateArray(), check => check.GetProperty("name").GetString() == "MasterKey");
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("installation").ValueKind);
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("auditComparison").ValueKind);
    }

    [DatabaseFact]
    public async Task ASessionOfAnotherFunctionGetsTheAnswerOfARouteThatDoesNotExist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = "Anotação" }, cancellationToken);

        using var status = await host.GetAsync("/status", cancellationToken);
        using var missing = await host.GetAsync("/zqx-nao-existe-6320", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(
            (missing.StatusCode, await missing.Content.ReadAsStringAsync(cancellationToken)),
            (status.StatusCode, await status.Content.ReadAsStringAsync(cancellationToken)));
    }
}
