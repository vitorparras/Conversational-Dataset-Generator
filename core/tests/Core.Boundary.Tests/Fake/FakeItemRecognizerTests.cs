using System.Text.Json;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Fake;

/// <summary>
/// For a whole item the fake gives each occurrence the group written after its marker, gives each author one group, and
/// answers in the shape the validator of the real recognizer accepts.
/// </summary>
public sealed class FakeItemRecognizerTests
{
    private const string Person = "ZQX-FAKE-PERSON";
    private const string Nothing = "ZQX-FAKE-NOTHING";

    private readonly FakeRecognizer recognizer = new();

    [Fact]
    public async Task FakeServiceGivesGroups()
    {
        var outcome = await AskAsync(new ItemText($"{Person}-1 chegou", "ZQX-AUTOR-A"), new ItemText($"ela viu {Person}-1 e {Person}-2", "ZQX-AUTOR-B"));

        Assert.Equal([Found(0, 0, 15, 1), Found(1, 8, 23, 1), Found(1, 28, 43, 2)], outcome.Response!.Occurrences);
        Assert.Equal([new AuthorGroup("ZQX-AUTOR-A", 3), new AuthorGroup("ZQX-AUTOR-B", 4)], outcome.Response.AuthorGroups);
    }

    [Fact]
    public async Task MarkerWithoutANumberIsGroupZero()
    {
        var outcome = await AskAsync(new ItemText($"\U0001F600 {Person} e {Person}-x", null));

        Assert.Equal([Found(0, 2, 17, 0), Found(0, 20, 35, 0)], outcome.Response!.Occurrences);
    }

    [Fact]
    public async Task AuthorNamedWithAMarkerSharesItsGroupAndIsNeverAnOccurrence()
    {
        var outcome = await AskAsync(new ItemText($"oi {Person}-6", $"{Person}-6"), new ItemText(Nothing, $"{Person}-9"));

        Assert.Equal([Found(0, 3, 18, 6)], outcome.Response!.Occurrences);
        Assert.Equal([new AuthorGroup($"{Person}-6", 6), new AuthorGroup($"{Person}-9", 9)], outcome.Response.AuthorGroups);
    }

    [Fact]
    public async Task AuthorWithoutAMarkerGetsAGroupNoOtherNameUses()
    {
        var outcome = await AskAsync(new ItemText(Nothing, "ZQX-AUTOR-A"), new ItemText("ola", $"{Person}-4"), new ItemText("ola", "ZQX-AUTOR-A"));

        Assert.Empty(outcome.Response!.Occurrences);
        Assert.Equal([new AuthorGroup("ZQX-AUTOR-A", 5), new AuthorGroup($"{Person}-4", 4)], outcome.Response.AuthorGroups);
    }

    [Fact]
    public async Task ItemWithoutAnyMarkerIsNotAnalysed()
    {
        var outcome = await AskAsync(new ItemText("falei com ZQX-MARKER-TEXT-0908", "ZQX-AUTOR-A"));

        Assert.Equal((null, UnavailabilityReason.ConnectionRefused), (outcome.Response, outcome.Reason));
    }

    [Fact]
    public async Task ItemAnalysisIsAcceptedWholeByTheValidatorOfTheRealRecognizer()
    {
        var request = Request(new ItemText($"{Person}-1 chegou", "ZQX-AUTOR-A"), new ItemText($"\U0001F600 {Person}", "ZQX-AUTOR-B"));
        var outcome = await recognizer.RecognizeItemAsync(request, TestContext.Current.CancellationToken);

        var validation = ResponseValidator.ValidateItem(request, ContractVersion.Current, JsonSerializer.SerializeToUtf8Bytes(outcome.Response, ContractJson.Options));

        Assert.Null(validation.Problem);
        Assert.Equal(outcome.Response!.Occurrences, validation.Response!.Occurrences);
    }

    [Fact]
    public async Task CallerCancellationIsThrownForAnItem()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognizer.RecognizeItemAsync(Request(new ItemText(Person, null)), cancelled.Token));
    }

    [Fact]
    public async Task MissingItemIsADefect() =>
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => recognizer.RecognizeItemAsync(null!, TestContext.Current.CancellationToken));

    private static ItemAnalysisRequest Request(params ItemText[] texts) => new(texts, "pt", "ZQX-REF-0908", ContractVersion.Current);

    private static GroupedOccurrence Found(int textIndex, int start, int end, int group) => new(textIndex, start, end, "PERSON", "PROPN", "nsubj", group);

    private Task<ItemRecognitionOutcome> AskAsync(params ItemText[] texts) => recognizer.RecognizeItemAsync(Request(texts), TestContext.Current.CancellationToken);
}
