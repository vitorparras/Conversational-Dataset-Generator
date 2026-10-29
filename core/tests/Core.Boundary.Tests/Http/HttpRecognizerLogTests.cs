using System.Globalization;
using Core.Tests.Support.Net;
using Microsoft.Extensions.Logging;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// Each recognition writes one line with the opaque reference, the outcome and the time; no line carries the text, a
/// body or the secret (requirement 021, decision 0011).
/// </summary>
public sealed class HttpRecognizerLogTests : IDisposable
{
    private const string Reference = "ZQX-REF-0025";
    private const string OutsideTheText =
        /*lang=json,strict*/ """{"occurrences":[{"start":0,"end":999,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}],"model_version":null}""";

    private readonly Recognition recognition = new();

    [Fact]
    public async Task AnalysisIsWrittenOnceWithTheReferenceAndTheCount()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal((LogLevel.Information, Reference, "0"), (line.Level, line.Fields["Reference"], line.Fields["Occurrences"]));
    }

    [Fact]
    public async Task AnalysisIsWrittenWithTheModelVersionAndTheTotalCount()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-astral.json"));
        var request = ContractFixture.ReadRequest("accepted/request.json");

        _ = await recognition.Of(service.Port, Recognition.Generous).RecognizeAsync(request, TestContext.Current.CancellationToken);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal(("zqx-model-1", "2"), (line.Fields["ModelVersion"], line.Fields["Occurrences"]));
    }

    [Fact]
    public async Task RefusedAnswerIsWrittenOnceWithTheProblem()
    {
        using var service = FakeService.Answering(Reply.Ok(OutsideTheText));

        _ = await recognition.AskAsync(service.Port);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal((LogLevel.Error, "OffsetOutsideText"), (line.Level, line.Fields["Problem"]));
    }

    [Fact]
    public async Task UnknownContractVersionIsWrittenAsTheReason()
    {
        using var service = FakeService.Answering(Reply.Version("2"), Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal("UnknownContractVersion", line.Fields["Reason"]);
    }

    [Fact]
    public async Task ServiceRefusalIsWrittenOnceWithTheStatusCode()
    {
        using var service = FakeService.Answering(Reply.Unauthorized);

        _ = await recognition.AskAsync(service.Port);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal((LogLevel.Error, "401"), (line.Level, line.Fields["StatusCode"]));
    }

    [Fact]
    public async Task UnreadableVersionIsWrittenOnceAsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Ok("null"), Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal((LogLevel.Error, "MalformedResponse"), (line.Level, line.Fields["Reason"]));
    }

    [Fact]
    public async Task UnreachableServiceIsWrittenOnceWithTheReferenceAndTheReason()
    {
        using var closed = new ClosedPort();

        _ = await recognition.AskAsync(closed.Number);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal((Reference, "ConnectionRefused"), (line.Fields["Reference"], line.Fields["Reason"]));
    }

    [Fact(Timeout = 60_000)]
    public async Task TimeOutIsWrittenOnceWithTheReason()
    {
        using var service = FakeService.NeverAnalyzing();

        _ = await recognition.Of(service.Port, TimeSpan.FromMilliseconds(200)).RecognizeAsync(Recognition.Request, TestContext.Current.CancellationToken);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal("TimedOut", line.Fields["Reason"]);
    }

    [Fact(Timeout = 60_000)]
    public async Task TimeIsWrittenInMilliseconds()
    {
        using var service = FakeService.NeverAnalyzing();

        _ = await recognition.Of(service.Port, TimeSpan.FromMilliseconds(200)).RecognizeAsync(Recognition.Request, TestContext.Current.CancellationToken);

        Assert.True(long.Parse(Assert.Single(recognition.Log.Lines).Fields["ElapsedMs"]!, CultureInfo.InvariantCulture) >= 100);
    }

    [Fact]
    public async Task NoLineCarriesTheTextOrTheSecretWhenTheServiceIsUnreachable()
    {
        using var closed = new ClosedPort();

        _ = await recognition.AskAsync(closed.Number);

        Assert.DoesNotContain("ZQX-MARKER", recognition.Log.Everything, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task NoLineCarriesTheTextOrTheSecretWhenTheTimeLimitPasses()
    {
        using var service = FakeService.NeverAnalyzing();

        _ = await recognition.Of(service.Port, TimeSpan.FromMilliseconds(200)).RecognizeAsync(Recognition.Request, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ZQX-MARKER", recognition.Log.Everything, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(200, /*lang=json,strict*/ """{"occurrences":[],"model_version":null}""")]
    [InlineData(200, """{"occurrences":[""")]
    [InlineData(200, OutsideTheText)]
    [InlineData(401, /*lang=json,strict*/ """{"error":"unauthorized"}""")]
    public async Task NoLineCarriesTheTextOrTheSecret(int status, string body)
    {
        using var service = FakeService.Answering(new Reply(status, body));

        _ = await recognition.AskAsync(service.Port);

        Assert.DoesNotContain("ZQX-MARKER", recognition.Log.Everything, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", recognition.Log.Everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoLineCarriesTheBodyTheServiceSent()
    {
        using var service = FakeService.Answering(Reply.Ok(/*lang=json,strict*/ """{"occurrences":[],"model_version":null,"zqx_marker_body":1}"""));

        _ = await recognition.AskAsync(service.Port);

        Assert.DoesNotContain("zqx_marker_body", recognition.Log.Everything, StringComparison.Ordinal);
    }

    public void Dispose() => recognition.Dispose();
}
