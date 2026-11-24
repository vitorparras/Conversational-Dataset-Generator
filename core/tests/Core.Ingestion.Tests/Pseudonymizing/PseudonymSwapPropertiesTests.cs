using System.Text;
using Core.Boundary.Contracts;
using Core.Boundary.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// Pseudonymizing the same sanitized text again with the map of the item gives the same bytes and the same markers, whatever the draw does the second time (issue 102, CA-2):
/// the markers come from the map, so another process does not change the result.
/// </summary>
public sealed class PseudonymSwapPropertiesTests
{
    private static readonly TextBoundaries Boundaries = new();

    private static readonly Gen<string> Word = Gen.OneOf(Gen.String[Gen.Char['a', 'z'], 1, 6], Gen.Const("😀"), Gen.Const("é"));

    private static readonly Gen<(string Word, int? Group)[]> Segments = Gen.Select(Word, Gen.Int[0, 3], Gen.Bool, (word, group, named) => (word, named ? (int?)group : null)).Array[1, 20];

    private static readonly Gen<int[]> FewNumbers = Gen.Int[0, 12].Array[0, 20];

    [Fact]
    [Trait("Kind", "Property")]
    public void PseudonymizingAgainGivesTheSameBytesAndMarkers() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Segments, FewNumbers, FewNumbers), generated =>
        {
            var (segments, first, second) = generated;
            var (text, occurrences) = Material(segments);
            var swapped = new PseudonymSwap(Boundaries, Draws.Of(first)).Swap(new SwapRequest { Sanitized = text, Occurrences = occurrences }, _ => false);
            var again = new SwapRequest { Sanitized = text, Occurrences = occurrences, ItemId = 7, Map = new ItemMap(7, swapped.Entries) };

            var rerun = new PseudonymSwap(Boundaries, Draws.Of(second)).Swap(again, _ => false);

            Assert.Equal(Encoding.UTF8.GetBytes(swapped.Text), Encoding.UTF8.GetBytes(rerun.Text));
            Assert.Equal(swapped.Entries, rerun.Entries);
        });

    private static (string Text, ReferredOccurrence[] Occurrences) Material((string Word, int? Group)[] segments)
    {
        var text = new StringBuilder();
        var occurrences = new List<ReferredOccurrence>();
        foreach (var (word, group) in segments)
        {
            var start = Boundaries.CodePointCount(text.ToString());
            _ = text.Append(word);
            if (group is { } number)
            {
                var occurrence = new Occurrence(start, start + Boundaries.CodePointCount(word), OccurrenceKinds.Person, "PROPN", "obj");
                occurrences.Add(new ReferredOccurrence(new DecidedOccurrence(occurrence, Destination.Pseudonymize), Referent.Group(number)));
            }

            _ = text.Append(' ');
        }

        return (text.ToString(), [.. occurrences]);
    }
}
