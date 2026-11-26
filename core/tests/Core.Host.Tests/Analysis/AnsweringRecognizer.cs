using System.Collections.Concurrent;
using Core.Boundary.Contracts;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// A recognizer at the boundary that answers every request with the outcome given and keeps the requests it was asked. Asked for a whole item, it finds the occurrences of the outcome
/// in each text of the item, each in a group of its own, and gives each author a group of its own.
/// </summary>
internal sealed class AnsweringRecognizer(RecognitionOutcome outcome) : IRecognizer
{
    private readonly ConcurrentQueue<AnalysisRequest> asked = new();
    private readonly ConcurrentQueue<ItemAnalysisRequest> askedItems = new();

    public IReadOnlyList<AnalysisRequest> Asked => [.. asked];

    public IReadOnlyList<ItemAnalysisRequest> AskedItems => [.. askedItems];

    public static AnsweringRecognizer Finding(params string[] kinds) =>
        new(RecognitionOutcome.Analyzed(new AnalysisResponse([.. kinds.Select(kind => new Occurrence(0, 1, kind, "PROPN", "nsubj"))], "zqx-model-0027")));

    public static AnsweringRecognizer Unavailable(UnavailabilityReason reason) => new(RecognitionOutcome.Unavailable(reason));

    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        asked.Enqueue(request);
        return Task.FromResult(outcome);
    }

    public Task<ItemRecognitionOutcome> RecognizeItemAsync(ItemAnalysisRequest request, CancellationToken cancellationToken)
    {
        askedItems.Enqueue(request);
        return Task.FromResult(outcome.Response is { } response ? ItemRecognitionOutcome.Analyzed(Grouped(request, response)) : ItemRecognitionOutcome.Unavailable(outcome.Reason!.Value));
    }

    private static ItemAnalysisResponse Grouped(ItemAnalysisRequest request, AnalysisResponse response)
    {
        List<GroupedOccurrence> occurrences = [.. request.Texts
            .SelectMany((_, text) => response.Occurrences.Select(found => (Text: text, Found: found)))
            .Select((pair, group) => new GroupedOccurrence(pair.Text, pair.Found.Start, pair.Found.End, pair.Found.Kind, pair.Found.PartOfSpeech, pair.Found.SyntacticRole, group))];
        List<AuthorGroup> authors = [.. request.Texts.Select(text => text.Author).OfType<string>().Distinct(StringComparer.Ordinal)
            .Select((author, index) => new AuthorGroup(author, occurrences.Count + index))];
        return new ItemAnalysisResponse(occurrences, authors, response.ModelVersion);
    }
}
