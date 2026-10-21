using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// Every way the service can fail to analyse ends as an unavailability with its reason, never as an empty list; only an
/// answer the validator accepts whole is an analysis. The service is a stand-in on a free loopback port.
/// </summary>
public sealed class HttpRecognizerTests : IDisposable
{
    private const int Hang = 60_000;

    private readonly Recognition recognition = new();

    [Fact]
    public async Task AnswerTheValidatorAcceptsIsAnAnalysisWithItsOccurrences()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-astral.json"));
        var request = ContractFixture.ReadRequest("accepted/request.json");

        var outcome = await recognition.Of(service.Port, Recognition.Generous).RecognizeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(ContractFixture.ReadResponse("accepted/response-astral.json").Occurrences, outcome.Response?.Occurrences);
    }

    [Fact]
    public async Task EmptyAnswerIsAnAnalysisAndNotAnUnavailability()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Null(outcome.Reason);
        Assert.Empty(outcome.Response!.Occurrences);
    }

    [Fact(Timeout = Hang)]
    public async Task ServiceThatNeverAnswersWithinTheLimitIsTimedOut()
    {
        using var service = FakeService.NeverAnalyzing();

        var outcome = await recognition.Of(service.Port, TimeSpan.FromMilliseconds(200)).RecognizeAsync(Recognition.Request, TestContext.Current.CancellationToken);

        Assert.Equal(UnavailabilityReason.TimedOut, outcome.Reason);
    }

    [Fact(Timeout = Hang)]
    public async Task AnswerThatTakesLongerThanASecondButStaysWithinTheLimitIsAnAnalysis()
    {
        var slow = Reply.Fixture("accepted/response-empty.json") with { Delay = TimeSpan.FromMilliseconds(1500) };
        using var service = FakeService.Answering(slow);

        var outcome = await recognition.Of(service.Port, TimeSpan.FromSeconds(30)).RecognizeAsync(Recognition.Request, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Reason);
    }

    [Fact(Timeout = Hang)]
    public async Task CallerCancellationIsThrownAndNotTurnedIntoAnOutcome()
    {
        using var service = FakeService.NeverAnalyzing();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var recognizing = recognition.Of(service.Port, Recognition.Generous).RecognizeAsync(Recognition.Request, caller.Token);

        await caller.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognizing);
    }

    [Fact]
    public async Task PortWithNoListenerIsConnectionRefused()
    {
        var outcome = await recognition.AskAsync(FakeService.FreePort());

        Assert.Equal(UnavailabilityReason.ConnectionRefused, outcome.Reason);
    }

    [Theory]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 500\r\n\r\n{\"service_")]
    [InlineData("SSH-2.0-ZQX\r\n")]
    public async Task ConnectionCutMidAnswerOrAnswerThatIsNotHttpIsMalformedResponse(string raw)
    {
        using var service = new RawService(raw);

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Fact]
    public async Task BodyThatIsNotJsonIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Ok("""{"occurrences":["""));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Fact]
    public async Task OccurrenceOutsideTheTextIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Ok(
            /*lang=json,strict*/ """{"occurrences":[{"start":0,"end":999,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}],"model_version":null}"""));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Fact]
    public async Task ServiceDeclaringAnotherContractVersionIsUnknownContractVersion()
    {
        using var service = FakeService.Answering(Reply.Version("2"), Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.UnknownContractVersion, outcome.Reason);
    }

    [Fact]
    public async Task RequestOfAVersionTheCoreDoesNotSpeakIsUnknownContractVersion()
    {
        using var service = FakeService.Answering(Reply.Version("2"), Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.Of(service.Port, Recognition.Generous)
            .RecognizeAsync(Recognition.RequestOfVersion("2"), TestContext.Current.CancellationToken);

        Assert.Equal(UnavailabilityReason.UnknownContractVersion, outcome.Reason);
    }

    [Fact]
    public async Task RequestOfAVersionTheServiceDoesNotSpeakIsNotSent()
    {
        using var service = FakeService.Answering(Reply.Version("1"), Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.Of(service.Port, Recognition.Generous)
            .RecognizeAsync(Recognition.RequestOfVersion("2"), TestContext.Current.CancellationToken);

        Assert.Equal(UnavailabilityReason.UnknownContractVersion, outcome.Reason);
        Assert.DoesNotContain(service.Seen, seen => seen.Path == "/analyze");
    }

    [Fact]
    public async Task ContractVersionIsReadAgainOnEveryCall()
    {
        using var service = FakeService.WithVersions([Reply.Version("1"), Reply.Version("2")], Reply.Fixture("accepted/response-empty.json"));
        _ = await recognition.AskAsync(service.Port);

        var second = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.UnknownContractVersion, second.Reason);
    }

    [Fact]
    public async Task AnalysisRefusedByTheServiceIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Unauthorized);

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    public async Task RefusalStatusWithAValidBodyIsNotAnAnalysis(int status)
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json") with { Status = status });

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Fact]
    public async Task VersionRefusedByTheServiceIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Unauthorized, Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Fact]
    public async Task VersionAnswerOutsideItsShapeIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Ok(/*lang=json,strict*/ """{"contract_version":"1"}"""), Reply.Fixture("accepted/response-empty.json"));

        var outcome = await recognition.AskAsync(service.Port);

        Assert.Equal(UnavailabilityReason.MalformedResponse, outcome.Reason);
    }

    [Theory]
    [InlineData(401, /*lang=json,strict*/ """{"error":"unauthorized"}""")]
    [InlineData(200, /*lang=json,strict*/ """{"contract_version":"1"}""")]
    [InlineData(200, "null")]
    [InlineData(200, /*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"2","model_version":null}""")]
    public async Task TextIsNotSentWhenTheVersionIsNotRead(int status, string body)
    {
        using var service = FakeService.Answering(new Reply(status, body), Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        Assert.DoesNotContain(service.Seen, seen => seen.Path == "/analyze");
    }

    [Fact]
    public async Task EveryRequestCarriesTheSecretInItsHeader()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        Assert.All(service.Seen, seen => Assert.Equal(Reply.Secret, seen.Secret));
    }

    [Fact]
    public async Task AnalysisIsPostedAsTheContractJsonAfterTheVersionIsRead()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        Assert.Equal(["GET /version", "POST /analyze"], service.Seen.Select(seen => $"{seen.Method} {seen.Path}"));
        Assert.True(ContractFixture.SameJson(JsonSerializer.Serialize(Recognition.Request, ContractJson.Options), service.Seen[1].Body));
    }

    [Fact]
    public async Task MissingRequestIsADefect()
    {
        var recognizer = recognition.Of(FakeService.FreePort(), Recognition.Generous);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => recognizer.RecognizeAsync(null!, TestContext.Current.CancellationToken));
    }

    public void Dispose() => recognition.Dispose();
}
