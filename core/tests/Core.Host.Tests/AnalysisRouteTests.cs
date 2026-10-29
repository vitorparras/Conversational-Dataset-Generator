using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// The built host analyses a text through the recognizer the configuration chose: a concluded analysis answers with the
/// count of each kind; a refusal answers 503 with the reason, the cause and no analysis; a body the route does not know
/// is refused; no line the host writes holds the text (issue #32, CA-2 and CA-5; requirement 021).
/// </summary>
public sealed class AnalysisRouteTests
{
    private const string Route = "/analysis";
    private const string Marker = "ZQX-MARKER-0027-ROUTE";

    [Trait("Category", "RealService")]
    [Fact]
    public async Task HostAskingTheRealServiceAnalysesTheTextAndFindsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        using var host = await HostProcess.StartWithTheRealServiceAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body($"falei com PESSOA_1 ontem {Marker}"), cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.RootElement.GetProperty("occurrences").GetInt32());
        Assert.Empty(body.RootElement.GetProperty("kinds").EnumerateObject());
    }

    [Fact]
    public async Task HostWithTheFakeAnswersTheCountOfEachKind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body("ZQX-FAKE-PERSON falou com ZQX-FAKE-PERSON"), cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.RootElement.GetProperty("occurrences").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("kinds").GetProperty("PERSON").GetInt32());
        Assert.True(Guid.TryParse(body.RootElement.GetProperty("reference").GetString(), out _));
    }

    [Fact]
    public async Task RefusalAnswersServiceUnavailableWithTheReasonTheCauseAndNoAnalysis()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body(Marker), cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("ConnectionRefused", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(
            "Material não analisado e nada gravado: o serviço de linguagem não pôde ser alcançado.",
            body.RootElement.GetProperty("cause").GetString());
        Assert.False(body.RootElement.TryGetProperty("occurrences", out _));
        Assert.False(body.RootElement.TryGetProperty("kinds", out _));
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt","resume":"00000000-0000-0000-0000-000000000027"}""")]
    [InlineData(/*lang=json,strict*/ """{"language":"pt"}""")]
    [InlineData(/*lang=json,strict*/ """{"text":null,"language":"pt"}""")]
    [InlineData(/*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt","resumes":"ZQX-NOT-A-REFERENCE"}""")]
    public async Task BodyOutsideTheShapeOfTheRouteIsRefusedWithoutAnalysis(string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.PostAsync(Route, json, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RetryNamingTheInterruptedAnalysisIsLoggedWithBothReferences()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        var interrupted = await ReferenceOfAsync(host, Body(Marker), cancellationToken);

        var resumed = await ReferenceOfAsync(host, Body("ZQX-FAKE-NOTHING", interrupted), cancellationToken);
        var output = await host.StopAsync();

        Assert.Contains($"Análise {interrupted} interrompida: ConnectionRefused; nada foi gravado", output, StringComparison.Ordinal);
        Assert.Contains($"Análise {resumed} pedida para retomar a análise interrompida {interrupted}", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoLineTheHostWritesHoldsTheTextItAnalysedRefusedOrRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        var interrupted = await ReferenceOfAsync(host, Body($"PESSOA_1 {Marker}"), cancellationToken);
        _ = await ReferenceOfAsync(host, Body($"ZQX-FAKE-PERSON PESSOA_1 {Marker}", interrupted), cancellationToken);
        using var rejected = await host.PostAsync(Route, $$"""{"text":"PESSOA_1 {{Marker}}","language":"pt","zqx":1}""", cancellationToken);

        var output = await host.StopAsync();

        Assert.Contains("interrompida", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", output, StringComparison.Ordinal);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task NoLineTheHostAskingTheRealServiceWritesHoldsTheText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        using var host = await HostProcess.StartWithTheRealServiceAsync(cancellationToken);
        _ = await ReferenceOfAsync(host, Body($"falei com PESSOA_1 ontem {Marker}"), cancellationToken);

        var output = await host.StopAsync();

        Assert.Contains("Pedido analisado pelo serviço de linguagem", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, output, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", output, StringComparison.Ordinal);
    }

    private static string Body(string text, string? resumes = null) =>
        JsonSerializer.Serialize(new Dictionary<string, string?> { ["text"] = text, ["language"] = "pt", ["resumes"] = resumes });

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

    private static async Task<string> ReferenceOfAsync(HostProcess host, string json, CancellationToken cancellationToken)
    {
        using var response = await host.PostAsync(Route, json, cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);
        return body.RootElement.GetProperty("reference").GetString()!;
    }
}
