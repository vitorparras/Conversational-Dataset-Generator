using Core.Ingestion.Pseudonymizing;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>
/// The marker is stable for its referent within the conversation and never repeats outside it, two referents never share one whatever their name, and nothing of the
/// original nor of the order of arrival can be read from its number (issue 100, CA-1, CA-2 and CA-4).
/// </summary>
public sealed class MarkerPropertiesTests
{
    /// <summary>
    /// The limit of the test of independence: six standard deviations of the correlation of two independent samples, whose correlation times the square root of the count less
    /// one is close to a standard normal. The real draw passes it but once in some hundreds of millions of cases, and a number that follows the original does not pass it.
    /// </summary>
    private const double Limit = 6.0;

    private static readonly Gen<int[]> FewNumbers = Gen.Int[0, 12].Array[0, 40];

    private static readonly Gen<string> Name = Gen.Select(Gen.Char['A', 'Z'], Gen.String[Gen.Char['a', 'z'], 0, 20], (initial, rest) => initial + rest);

    private static readonly Gen<string[]> Names = Name.Array[400].Select(names => names.Distinct(StringComparer.Ordinal).ToArray());

    [Fact]
    [Trait("Kind", "Property")]
    public void SameParticipantGetsTheSameMarkerWithinTheConversation() =>
        PropertyRunner.FromProcess().Check(Gen.Select(FewNumbers, Gen.Int[0, 5].Array[1, 40]), generated =>
        {
            var (numbers, authors) = generated;
            var conversation = Draws.Conversation(Draws.Of(numbers), new HashSet<Marker>());

            var markers = authors.Select(author => (Author: author, Marker: conversation.Of(Referent.Author($"ZQX-AUTOR-{author}"), MarkerKind.Person))).ToList();

            Assert.All(markers.GroupBy(entry => entry.Author), group => Assert.Single(group.Select(entry => entry.Marker).Distinct()));
            Assert.Equal(authors.Distinct().Count(), markers.Select(entry => entry.Marker).Distinct().Count());
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void NoMarkerAppearsInTwoConversations() =>
        PropertyRunner.FromProcess().Check(Gen.Select(FewNumbers, Gen.Int[1, 6], Gen.Int[1, 6]), generated =>
        {
            var (numbers, first, second) = generated;
            var draw = Draws.Of(numbers);
            var inUse = new HashSet<Marker>();
            var one = Draws.Conversation(draw, inUse);
            var two = Draws.Conversation(draw, inUse);

            var ofOne = Enumerable.Range(0, first).Select(author => one.Of(Referent.Author($"ZQX-AUTOR-{author}"), MarkerKind.Person)).ToList();
            inUse.UnionWith(ofOne);
            var ofTwo = Enumerable.Range(0, second).Select(author => two.Of(Referent.Author($"ZQX-AUTOR-{author}"), MarkerKind.Person)).ToList();

            Assert.Empty(ofOne.Intersect(ofTwo));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void DistinctAuthorsWithTheSameNameGetDistinctMarkers() =>
        PropertyRunner.FromProcess().Check(Gen.Select(FewNumbers, Name, Gen.Int[1, 4]), generated =>
        {
            var (numbers, name, groups) = generated;
            var draw = Draws.Of(numbers);
            var inUse = new HashSet<Marker>();
            var here = Draws.Conversation(draw, inUse);

            var author = here.Of(Referent.Author(name), MarkerKind.Person);
            var mentions = Enumerable.Range(1, groups).Select(group => here.Of(Referent.Group(group), MarkerKind.Person)).ToList();
            inUse.UnionWith([author, .. mentions]);
            var elsewhere = Draws.Conversation(draw, inUse).Of(Referent.Author(name), MarkerKind.Person);

            Assert.Equal(groups + 2, new HashSet<Marker>([author, elsewhere, .. mentions]).Count);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void MarkerCarriesNoInitialLengthNorAlphabeticalPosition() =>
        PropertyRunner.FromProcess().Check(Names, names =>
        {
            var numbers = NumbersOf(names);
            var alphabetical = names.Order(StringComparer.Ordinal).Select((name, rank) => (name, rank)).ToDictionary(entry => entry.name, entry => (double)entry.rank, StringComparer.Ordinal);

            var initial = Independence(numbers, [.. names.Select(name => (double)name[0])]);
            var length = Independence(numbers, [.. names.Select(name => (double)name.Length)]);
            var position = Independence(numbers, [.. names.Select(name => alphabetical[name])]);

            Assert.All([initial, length, position], statistic => Assert.InRange(statistic, -Limit, Limit));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void MarkerNumberIsExactlyTheDrawnOneWhateverTheName() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Gen.Int[0, 99_999_999].Array[1, 30], Name.Array[30]), generated =>
        {
            var (numbers, candidates) = generated;
            var names = candidates.Distinct(StringComparer.Ordinal).Take(numbers.Distinct().Count()).ToArray();
            var drawn = numbers.Distinct().Take(names.Length).ToArray();
            var conversation = Draws.Conversation(Draws.Of(drawn), new HashSet<Marker>());

            var given = names.Select(name => conversation.Of(Referent.Author(name), MarkerKind.Person).Number).ToArray();

            Assert.Equal(drawn, given);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void MarkerDoesNotRevealTheOrderOfAppearance() =>
        PropertyRunner.FromProcess().Check(Names, names =>
        {
            var numbers = NumbersOf(names);

            var order = Independence(numbers, [.. names.Select((_, arrival) => (double)arrival)]);

            Assert.InRange(order, -Limit, Limit);
        });

    /// <summary>The numbers the real draw gives the names, each the author of its turn in one conversation, in the order the names arrive.</summary>
    private static double[] NumbersOf(string[] names)
    {
        var conversation = new ConversationMarkers(new MarkerDraw(), _ => false);
        return [.. names.Select(name => (double)conversation.Of(Referent.Author(name), MarkerKind.Person).Number)];
    }

    /// <summary>The correlation of the two samples times the square root of their count less one: close to a standard normal when they are independent.</summary>
    private static double Independence(double[] drawn, double[] original)
    {
        var (meanDrawn, meanOriginal) = (drawn.Average(), original.Average());
        var covariance = drawn.Zip(original, (x, y) => (x - meanDrawn) * (y - meanOriginal)).Sum();
        var spread = Math.Sqrt(drawn.Sum(x => (x - meanDrawn) * (x - meanDrawn)) * original.Sum(y => (y - meanOriginal) * (y - meanOriginal)));
        return spread == 0 ? 0 : covariance / spread * Math.Sqrt(drawn.Length - 1);
    }
}
