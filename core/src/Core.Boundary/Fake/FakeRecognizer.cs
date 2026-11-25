using System.Globalization;
using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Fake;

/// <summary>
/// A recognizer with no model and no service, chosen by configuration to exercise what depends on recognition. It
/// decides by the markers written in the text and keeps no state, so the same request always has the same outcome:
/// each <c>ZQX-FAKE-PERSON</c> is one occurrence over the marker itself; a text with none but with
/// <c>ZQX-FAKE-NOTHING</c> is analysed with nothing found; any other text, real material included, is left unanalysed
/// as if the service could not be reached, so a text the fake does not know is never passed as clean. In a whole item,
/// the group of an occurrence is the number written right after its marker as <c>ZQX-FAKE-PERSON-3</c>, zero when
/// there is none; an author whose name carries such a marker shares its group, and any other author has a group of
/// its own.
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

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The caller cancelled before asking.</exception>
    public Task<ItemRecognitionOutcome> RecognizeItemAsync(ItemAnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<ItemRecognitionOutcome>(cancellationToken)
            : Task.FromResult(ItemOutcome(request.Texts));
    }

    private static RecognitionOutcome Outcome(string text)
    {
        var occurrences = Occurrences(text);
        return occurrences.Length > 0 || text.Contains(NothingMarker, StringComparison.Ordinal)
            ? RecognitionOutcome.Analyzed(new AnalysisResponse(occurrences, ModelVersion: null))
            : RecognitionOutcome.Unavailable(UnavailabilityReason.ConnectionRefused);
    }

    /// <summary>The item is analysed when one of its texts has a marker; the author names are never searched for occurrences.</summary>
    private static ItemRecognitionOutcome ItemOutcome(IReadOnlyList<ItemText> texts)
    {
        GroupedOccurrence[] occurrences = [.. texts.SelectMany((text, index) => GroupedOccurrences(index, text.Text))];
        return occurrences.Length > 0 || texts.Any(text => text.Text.Contains(NothingMarker, StringComparison.Ordinal))
            ? ItemRecognitionOutcome.Analyzed(new ItemAnalysisResponse(occurrences, AuthorGroups(texts, occurrences), ModelVersion: null))
            : ItemRecognitionOutcome.Unavailable(UnavailabilityReason.ConnectionRefused);
    }

    /// <summary>One occurrence per marker, measured in code points as the contract counts them.</summary>
    private static Occurrence[] Occurrences(string text) =>
        [.. MarkerIndexes(text).Select(index => new Occurrence(
            OffsetConverter.ToCodePointOffset(text, index),
            OffsetConverter.ToCodePointOffset(text, index + OccurrenceMarker.Length),
            Kind,
            PartOfSpeech,
            SyntacticRole))];

    private static IEnumerable<GroupedOccurrence> GroupedOccurrences(int textIndex, string text) =>
        MarkerIndexes(text).Select(index => new GroupedOccurrence(
            textIndex,
            OffsetConverter.ToCodePointOffset(text, index),
            OffsetConverter.ToCodePointOffset(text, index + OccurrenceMarker.Length),
            Kind,
            PartOfSpeech,
            SyntacticRole,
            GroupAfter(text, index)));

    /// <summary>One group for each distinct author, in the order they first write: the group of the marker in the name, or the next one no occurrence nor other name uses.</summary>
    private static List<AuthorGroup> AuthorGroups(IEnumerable<ItemText> texts, IEnumerable<GroupedOccurrence> occurrences)
    {
        var named = texts.Select(text => text.Author).OfType<string>().Distinct(StringComparer.Ordinal)
            .Select(author => (Author: author, Group: MarkerGroup(author))).ToList();
        var next = occurrences.Select(occurrence => occurrence.Group).Concat(named.Select(name => name.Group ?? -1)).DefaultIfEmpty(-1).Max() + 1;
        var groups = new List<AuthorGroup>();
        foreach (var (author, group) in named)
        {
            groups.Add(new AuthorGroup(author, group ?? next++));
        }

        return groups;
    }

    private static int? MarkerGroup(string author)
    {
        var index = author.IndexOf(OccurrenceMarker, StringComparison.Ordinal);
        return index >= 0 ? GroupAfter(author, index) : null;
    }

    /// <summary>The number after a hyphen right past the marker, zero when there is none.</summary>
    private static int GroupAfter(string text, int markerIndex)
    {
        var after = markerIndex + OccurrenceMarker.Length;
        if (after >= text.Length || text[after] != '-')
        {
            return 0;
        }

        var digits = string.Concat(text.Skip(after + 1).TakeWhile(char.IsAsciiDigit));
        return int.TryParse(digits, CultureInfo.InvariantCulture, out var group) ? group : 0;
    }

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
