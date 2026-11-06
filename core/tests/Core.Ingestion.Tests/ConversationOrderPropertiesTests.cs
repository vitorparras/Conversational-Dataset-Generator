using System.Text.Json;
using Core.Ingestion.Reading;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// The order of a conversation never comes from the place in the file (issue 77, criteria 2 and 3): the same units in any order of the file give the same conversation or the same refusal,
/// and reading the same material twice gives the same outcome.
/// </summary>
public sealed class ConversationOrderPropertiesTests
{
    private static readonly FormConference Conference = FormConference.Minimal();

    private static readonly Gen<string> Marked = Gen.String[Gen.Char['A', 'Z'], 1, 6];

    /// <summary>Few moments, so that ties are common; answers that mostly name a unit, sometimes none and now and then a unit that does not exist.</summary>
    private static readonly Gen<ReadUnit[]> Units = Gen.Int[0, 6].SelectMany(count => Gen.Select(
        Marked,
        Gen.Frequency((9, Marked.Select(author => (string?)author)), (1, Gen.Const((string?)null))),
        Gen.Frequency((9, Gen.Int[0, 2].Select(second => (DateTimeOffset?)new DateTimeOffset(2031, 1, 1, 0, 0, second, TimeSpan.Zero))), (1, Gen.Const((DateTimeOffset?)null))),
        Gen.Int[-1, 8],
        (text, author, instant, raw) => new ReadUnit(text, author, instant, Answer(raw, count), [])).Array[count]);

    private static readonly Gen<(ReadUnit[] Units, int[] Permutation)> Permuted = Units.SelectMany(units =>
        Gen.Int.Array[units.Length].Select(keys => (units, Enumerable.Range(0, units.Length).OrderBy(at => keys[at]).ThenBy(at => at).ToArray())));

    [Fact]
    [Trait("Kind", "Property")]
    public void TheSameUnitsInAnyOrderOfTheFileGiveTheSameConversationOrTheSameRefusal() =>
        PropertyRunner.FromProcess().Check(Permuted, generated =>
        {
            var (units, permutation) = generated;

            var original = Conference.Confer(MaterialForm.Conversation, Read(units));
            var shuffled = Conference.Confer(MaterialForm.Conversation, Read(Shuffled(units, permutation)));

            Assert.Equal(Outcome(original, null), Outcome(shuffled, permutation));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void AChainOfAnswersAtOneMomentIsAlwaysOrderedFromTheFirstAnsweredWhateverThePlaceInTheFile() =>
        PropertyRunner.FromProcess().Check(Gen.Int[1, 6].SelectMany(length => Gen.Int.Array[length].Select(keys => (length, keys))), generated =>
        {
            var (length, keys) = generated;
            var chain = Enumerable.Range(0, length).Select(link => new ReadUnit($"T{link}", "ana", Units0, link == 0 ? null : link - 1, [])).ToArray();
            var permutation = Enumerable.Range(0, length).OrderBy(at => keys[at]).ThenBy(at => at).ToArray();

            var result = Conference.Confer(MaterialForm.Conversation, Read(Shuffled(chain, permutation)));

            var conversation = Assert.IsType<Conversation>(result.Accepted);
            Assert.Equal(Enumerable.Range(0, length).Select(link => $"T{link}"), conversation.Messages.Select(message => message.Text));
            Assert.Equal(Enumerable.Range(0, length), conversation.Messages.Select(message => message.Rank));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void ConferringTheSameMaterialTwiceGivesTheSameOutcome() =>
        PropertyRunner.FromProcess().Check(Units, units =>
        {
            var first = Conference.Confer(MaterialForm.Conversation, Read(units));
            var again = Conference.Confer(MaterialForm.Conversation, Read([.. units.Select(unit => unit with { })]));

            Assert.Equal(Outcome(first, null), Outcome(again, null));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void ReadingTheSameFileTwiceGivesTheSameOutcome() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Marked, Marked, Gen.Int[0, 2]).List[0, 6], messages =>
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                formato = "cdg-conversa/1",
                mensagens = messages.Select((message, at) => new { id = $"m{at}", remetente = message.Item1, instante = $"2031-03-04T08:00:0{message.Item3}Z", texto = message.Item2 }),
            });
            var reader = new ExportedConversationReader();

            var first = Conference.Confer(MaterialForm.Conversation, reader.Read(bytes));
            var again = Conference.Confer(MaterialForm.Conversation, new ExportedConversationReader().Read(bytes.ToArray()));

            Assert.Equal(Outcome(first, null), Outcome(again, null));
        });

    private static DateTimeOffset Units0 => new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReadOutcome Read(ReadUnit[] units) => ReadOutcome.Read(new ReadMaterial(units));

    private static int? Answer(int raw, int count) => raw switch
    {
        < 0 => null,
        < 7 when count > 0 => raw % count,
        _ => count,
    };

    /// <summary>The units as a file that listed them in the order of the permutation: the answers keep naming the same units, wherever those now stand.</summary>
    private static ReadUnit[] Shuffled(ReadUnit[] units, int[] permutation)
    {
        var where = new int[units.Length];
        for (var at = 0; at < permutation.Length; at++)
        {
            where[permutation[at]] = at;
        }

        return [.. permutation.Select(old => units[old] with { RepliesTo = units[old].RepliesTo is { } to && to >= 0 && to < units.Length ? where[to] : units[old].RepliesTo })];
    }

    /// <summary>The outcome told apart from the place in the file: the messages in order, or the code with the units it names put back where they stood in the original.</summary>
    private static string Outcome(ConferenceResult result, int[]? permutation)
    {
        if (result.Accepted is Conversation conversation)
        {
            return "accepted: " + string.Join("; ", conversation.Messages.Select(message => $"{message.Rank}|{message.Author}|{message.Instant:O}|{message.RepliesTo}|{message.Text}"));
        }

        var refusal = result.Refusal!;
        var findings = refusal.Findings.Select(finding => $"{(finding.Unit is { } at && permutation is not null ? permutation[at] : finding.Unit)}|{finding.Field}").Order(StringComparer.Ordinal);
        return $"refused: {refusal.Code}: {string.Join("; ", findings)}";
    }
}
