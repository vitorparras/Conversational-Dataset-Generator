using System.Collections.Concurrent;
using Core.Boundary.Contracts;

namespace Core.Host.Tests.Analysis;

/// <summary>A recognizer at the boundary that answers every request with the outcome given and keeps the requests it was asked.</summary>
internal sealed class AnsweringRecognizer(RecognitionOutcome outcome) : IRecognizer
{
    private readonly ConcurrentQueue<AnalysisRequest> asked = new();

    public IReadOnlyList<AnalysisRequest> Asked => [.. asked];

    public static AnsweringRecognizer Finding(params string[] kinds) =>
        new(RecognitionOutcome.Analyzed(new AnalysisResponse([.. kinds.Select(kind => new Occurrence(0, 1, kind, "PROPN", "nsubj"))], "zqx-model-0027")));

    public static AnsweringRecognizer Unavailable(UnavailabilityReason reason) => new(RecognitionOutcome.Unavailable(reason));

    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        asked.Enqueue(request);
        return Task.FromResult(outcome);
    }
}
