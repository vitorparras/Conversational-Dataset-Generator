using System.Net;
using System.Text.Json;
using Core.Host.Tests;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host, entered, analyses a text and writes no line that holds it (issue #32, CA-2 and CA-5; requirement 021): what the route answers is read in
/// Core.Host.Tests over a host in process, and what the whole process writes to its output is read here, where a person can enter.
/// </summary>
[Trait("Category", "Database")]
public sealed class AnalysisProcessTests
{
    private const string Route = "/analysis";
    private const string Marker = "ZQX-MARKER-0027-ROUTE";

    [Trait("Category", "RealService")]
    [DatabaseFact]
    public async Task HostAskingTheRealServiceAnalysesTheTextAndFindsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { RealService = true }, cancellationToken);

        using var response = await host.PostAsync(Route, Body($"falei com PESSOA_1 ontem {Marker}"), cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.RootElement.GetProperty("occurrences").GetInt32());
        Assert.Empty(body.RootElement.GetProperty("kinds").EnumerateObject());
    }

    [DatabaseFact]
    public async Task RetryNamingTheInterruptedAnalysisIsLoggedWithBothReferences()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);
        var interrupted = await ReferenceOfAsync(host, Body(Marker), cancellationToken);

        var resumed = await ReferenceOfAsync(host, Body("ZQX-FAKE-NOTHING", interrupted), cancellationToken);
        var output = (await host.StopAsync()).Stdout;

        Assert.Contains($"Análise {interrupted} interrompida: ConnectionRefused; nada foi gravado", output, StringComparison.Ordinal);
        Assert.Contains($"Análise {resumed} pedida para retomar a análise interrompida {interrupted}", output, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task NoLineTheHostWritesHoldsTheTextItAnalysedRefusedOrRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);
        var interrupted = await ReferenceOfAsync(host, Body($"PESSOA_1 {Marker}"), cancellationToken);
        _ = await ReferenceOfAsync(host, Body($"ZQX-FAKE-PERSON PESSOA_1 {Marker}", interrupted), cancellationToken);
        using var rejected = await host.PostAsync(Route, $$"""{"text":"PESSOA_1 {{Marker}}","language":"pt","zqx":1}""", cancellationToken);

        var output = (await host.StopAsync()).Stdout;

        Assert.Contains("interrompida", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", output, StringComparison.Ordinal);
    }

    [Trait("Category", "RealService")]
    [DatabaseFact]
    public async Task NoLineTheHostAskingTheRealServiceWritesHoldsTheText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { RealService = true }, cancellationToken);
        _ = await ReferenceOfAsync(host, Body($"falei com PESSOA_1 ontem {Marker}"), cancellationToken);

        var output = (await host.StopAsync()).Stdout;

        Assert.Contains("Pedido analisado pelo serviço de linguagem", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", output, StringComparison.Ordinal);
    }

    private static string Body(string text, string? resumes = null) =>
        JsonSerializer.Serialize(new Dictionary<string, string?> { ["text"] = text, ["language"] = "pt", ["resumes"] = resumes });

    private static async Task<string> ReferenceOfAsync(AuthenticatedHost host, string json, CancellationToken cancellationToken)
    {
        using var response = await host.PostAsync(Route, json, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("reference").GetString()!;
    }
}
