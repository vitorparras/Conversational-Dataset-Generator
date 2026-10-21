using System.Text.Json;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Fake;

/// <summary>
/// The fake recognizer gives each of the three outcomes the contract tells apart, decided by markers in the text alone:
/// one occurrence per occurrence marker, nothing found with the nothing marker, and no analysis for any other text.
/// </summary>
public sealed class FakeRecognizerTests
{
    private const string Person = "ZQX-FAKE-PERSON";

    private readonly FakeRecognizer recognizer = new();

    [Fact]
    public async Task EachOccurrenceMarkerIsOneOccurrenceOverTheMarker()
    {
        var outcome = await AskAsync($"{Person} falou com {Person}");

        Assert.Equal([Found(0, 15), Found(26, 41)], outcome.Response!.Occurrences);
    }

    [Fact]
    public async Task OffsetsAreCountedInCodePoints()
    {
        var outcome = await AskAsync($"\U0001F600 {Person}");

        Assert.Equal([Found(2, 17)], outcome.Response!.Occurrences);
    }

    [Fact]
    public async Task NothingMarkerIsAnAnalysisWithNothingFound()
    {
        var outcome = await AskAsync("falei com ZQX-FAKE-NOTHING ontem");

        Assert.Null(outcome.Reason);
        Assert.Empty(outcome.Response!.Occurrences);
    }

    [Fact]
    public async Task OccurrenceMarkerIsFoundEvenBesideTheNothingMarker()
    {
        var outcome = await AskAsync($"ZQX-FAKE-NOTHING {Person}");

        Assert.Equal([Found(17, 32)], outcome.Response!.Occurrences);
    }

    [Theory]
    [InlineData("falei com PESSOA_1 ontem")]
    [InlineData("")]
    [InlineData("zqx-fake-person zqx-fake-nothing")]
    [InlineData("ZQX-FAKE-PERSO N")]
    public async Task TextWithoutAMarkerIsLeftUnanalysedAsAServiceOutOfReach(string text)
    {
        var outcome = await AskAsync(text);

        Assert.Null(outcome.Response);
        Assert.Equal(UnavailabilityReason.ConnectionRefused, outcome.Reason);
    }

    [Fact]
    public async Task AnalysisHasNoModelVersionSinceThereIsNoModel()
    {
        var outcome = await AskAsync(Person);

        Assert.Null(outcome.Response!.ModelVersion);
    }

    [Theory]
    [InlineData("\U0001F600 ZQX-FAKE-PERSON e ZQX-FAKE-PERSON")]
    [InlineData("ZQX-FAKE-NOTHING")]
    [InlineData("falei com PESSOA_1 ontem")]
    public async Task SameRequestAskedOfTwoRecognizersHasTheSameOutcome(string text)
    {
        var first = await AskAsync(text);
        var second = await new FakeRecognizer().RecognizeAsync(Request(text), TestContext.Current.CancellationToken);

        Assert.Equal(Json(first), Json(second));
    }

    [Fact]
    public async Task AnalysisIsAcceptedWholeByTheValidatorOfTheRealRecognizer()
    {
        var request = Request($"\U0001F600 {Person} na rua {Person}");
        var outcome = await recognizer.RecognizeAsync(request, TestContext.Current.CancellationToken);

        var validation = ResponseValidator.Validate(request, ContractVersion.Current, JsonSerializer.SerializeToUtf8Bytes(outcome.Response, ContractJson.Options));

        Assert.Null(validation.Problem);
        Assert.Equal(outcome.Response!.Occurrences, validation.Response!.Occurrences);
    }

    [Fact]
    public async Task AnalysisCarriesNoTextOfTheRequest()
    {
        var outcome = await AskAsync($"falei com {Person} ontem ZQX-MARKER-TEXT-0026");

        var written = JsonSerializer.Serialize(outcome.Response, ContractJson.Options);

        Assert.DoesNotContain(Person, written, StringComparison.Ordinal);
        Assert.DoesNotContain("falei", written, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-MARKER-TEXT-0026", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsThrownAndNotTurnedIntoAnOutcome()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognizer.RecognizeAsync(Request(Person), cancelled.Token));
    }

    [Fact]
    public async Task MissingRequestIsADefect() =>
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => recognizer.RecognizeAsync(null!, TestContext.Current.CancellationToken));

    private static AnalysisRequest Request(string text) => new(text, "pt", "ZQX-REF-0026", ContractVersion.Current);

    private static Occurrence Found(int start, int end) => new(start, end, "PERSON", "PROPN", "nsubj");

    private static string Json(RecognitionOutcome outcome) =>
        JsonSerializer.Serialize(new { outcome.Reason, outcome.Response }, ContractJson.Options);

    private Task<RecognitionOutcome> AskAsync(string text) => recognizer.RecognizeAsync(Request(text), TestContext.Current.CancellationToken);
}
