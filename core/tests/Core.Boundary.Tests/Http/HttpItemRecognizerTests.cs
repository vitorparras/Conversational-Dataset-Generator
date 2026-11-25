using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// A whole item goes to the service in one request with every text and its author, and comes back with its groups only
/// when the validator accepts the answer whole (part F2 of the coreference slice).
/// </summary>
public sealed class HttpItemRecognizerTests : IDisposable
{
    private static readonly ItemAnalysisRequest Item = ContractFixture.ReadItemRequest("accepted/item-request.json");

    private readonly Recognition recognition = new();

    [Fact]
    public async Task OneRequestCarriesEveryTextOfTheItemWithItsAuthor()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/item-response.json"));

        _ = await AskAsync(service.Port, Item);

        Assert.Equal(["GET /version", "POST /analyze-item"], service.Seen.Select(seen => $"{seen.Method} {seen.Path}"));
        Assert.True(ContractFixture.SameJson(ContractFixture.Text("accepted/item-request.json"), service.Seen[1].Body), service.Seen[1].Body);
    }

    [Fact]
    public async Task AnswerTheValidatorAcceptsIsAnAnalysisWithItsGroups()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/item-response.json"));

        var outcome = await AskAsync(service.Port, Item);

        var expected = ContractFixture.ReadItemResponse("accepted/item-response.json");
        Assert.Equal(expected.Occurrences, outcome.Response!.Occurrences);
        Assert.Equal(expected.AuthorGroups, outcome.Response.AuthorGroups);
    }

    [Fact]
    public async Task AnswerWithoutGroupsIsMalformedResponse()
    {
        using var service = FakeService.Answering(Reply.Fixture("refused/item-response-without-groups.json"));

        var outcome = await AskAsync(service.Port, Item);

        Assert.Equal((null, UnavailabilityReason.MalformedResponse), (outcome.Response, outcome.Reason));
    }

    [Fact]
    public async Task ServiceDeclaringAnotherContractVersionIsNotSentTheItem()
    {
        using var service = FakeService.Answering(Reply.Version("1"), Reply.Fixture("accepted/item-response.json"));

        var outcome = await AskAsync(service.Port, Item);

        Assert.Equal(UnavailabilityReason.UnknownContractVersion, outcome.Reason);
        Assert.DoesNotContain(service.Seen, seen => seen.Path == "/analyze-item");
    }

    [Fact]
    public async Task AnalysisOfTheItemIsWrittenOnceWithTheReferenceAndTheCount()
    {
        using var service = FakeService.Answering(Reply.Fixture("accepted/item-response.json"));

        _ = await AskAsync(service.Port, Item);

        var line = Assert.Single(recognition.Log.Lines);
        Assert.Equal(("ZQX-REF-0908", "3"), (line.Fields["Reference"], line.Fields["Occurrences"]));
        Assert.DoesNotContain(recognition.Log.Lines, logged => logged.Message.Contains("ZQX-AUTOR", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingItemIsADefect()
    {
        var refusal = await Assert.ThrowsAsync<ArgumentNullException>(() => recognition.Of(1, Recognition.Generous).RecognizeItemAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", refusal.ParamName);
    }

    [Fact]
    public void ItemRequestIsWrittenWithEveryTextInOrder()
    {
        var written = JsonSerializer.SerializeToElement(Item, ContractJson.Options);

        Assert.Equal(["ZQX-AUTOR-A", "ZQX-AUTOR-B", "ZQX-AUTOR-A"], written.GetProperty("texts").EnumerateArray().Select(text => text.GetProperty("author").GetString()));
    }

    public void Dispose() => recognition.Dispose();

    private Task<ItemRecognitionOutcome> AskAsync(int port, ItemAnalysisRequest request) =>
        recognition.Of(port, Recognition.Generous).RecognizeItemAsync(request, TestContext.Current.CancellationToken);
}
