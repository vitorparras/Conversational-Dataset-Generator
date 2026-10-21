using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Fake;

/// <summary>
/// A recognizer with no model and no service, chosen by configuration to exercise what depends on recognition. It
/// decides by the markers written in the text and keeps no state, so the same request always has the same outcome:
/// each <c>ZQX-FAKE-PERSON</c> is one occurrence over the marker itself; a text with none but with
/// <c>ZQX-FAKE-NOTHING</c> is analysed with nothing found; any other text, real material included, is left unanalysed
/// as if the service could not be reached, so a text the fake does not know is never passed as clean.
/// </summary>
public sealed class FakeRecognizer : IRecognizer
{
    private const string OccurrenceMarker = "ZQX-FAKE-PERSON";
    private const string NothingMarker = "ZQX-FAKE-NOTHING";
    private const string Kind = "PERSON";
    private const string PartOfSpeech = "PROPN";
    private const string SyntacticRole = "nsubj";

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The caller cancelled before asking.</exception>
    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<RecognitionOutcome>(cancellationToken)
            : Task.FromResult(Outcome(request.Text));
    }

    private static RecognitionOutcome Outcome(string text)
    {
        var occurrences = Occurrences(text);
        return occurrences.Length > 0 || text.Contains(NothingMarker, StringComparison.Ordinal)
            ? RecognitionOutcome.Analyzed(new AnalysisResponse(occurrences, ModelVersion: null))
            : RecognitionOutcome.Unavailable(UnavailabilityReason.ConnectionRefused);
    }

    /// <summary>One occurrence per marker, measured in code points as the contract counts them.</summary>
    private static Occurrence[] Occurrences(string text) =>
        [.. MarkerIndexes(text).Select(index => new Occurrence(
            OffsetConverter.ToCodePointOffset(text, index),
            OffsetConverter.ToCodePointOffset(text, index + OccurrenceMarker.Length),
            Kind,
            PartOfSpeech,
            SyntacticRole))];

    private static IEnumerable<int> MarkerIndexes(string text)
    {
        var index = text.IndexOf(OccurrenceMarker, StringComparison.Ordinal);
        while (index >= 0)
        {
            yield return index;
            index = text.IndexOf(OccurrenceMarker, index + OccurrenceMarker.Length, StringComparison.Ordinal);
        }
    }
}
