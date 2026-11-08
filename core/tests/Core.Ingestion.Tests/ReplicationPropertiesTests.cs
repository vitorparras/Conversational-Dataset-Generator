using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// What holds for any batch (issue 82): the marks do not depend on the order the conversations came in, none of the three signals marks alone, marking never removes or merges a message, and the
/// marks are exactly what the criterion says, checked against a plain reading of it.
/// </summary>
public sealed class ReplicationPropertiesTests
{
    private static readonly ReplicationRule Rule = new(minLength: 10, minSenders: 2);

    private static readonly DateTimeOffset Start = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Few texts, senders and moments, so that equal texts, repeated senders and ties are the common case; the length is a function of the text, as it is of a real one.</summary>
    private static readonly Gen<ReplicationOccurrence[]> Messages = Gen.Int[0, 14].SelectMany(count => Gen.Select(Gen.Int[0, 3], Gen.Int[0, 3], Gen.Int[0, 2], (text, sender, minute) => (text, sender, minute))
        .Array[count].Select(drawn => drawn.Select((one, at) => Occurrence(at, one.text, one.sender, one.minute)).ToArray()));

    private static readonly Gen<(ReplicationOccurrence[] Messages, int[] Permutation)> Permuted = Messages.SelectMany(messages =>
        Gen.Int.Array[messages.Length].Select(keys => (messages, Enumerable.Range(0, messages.Length).OrderBy(at => keys[at]).ThenBy(at => at).ToArray())));

    [Fact]
    [Trait("Kind", "Property")]
    public void TheMarksDoNotDependOnTheOrderTheConversationsCameIn() =>
        PropertyRunner.FromProcess().Check(Permuted, generated =>
        {
            var (messages, permutation) = generated;

            var original = Replication.Marks(Rule, [], messages);
            var shuffled = Replication.Marks(Rule, [], permutation.Select(at => messages[at]).ToList());

            Assert.Equal(original, shuffled);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheMarksOfALaterBatchDoNotDependOnTheOrderEitherWhenThereIsAKnownOne() =>
        PropertyRunner.FromProcess().Check(Permuted, generated =>
        {
            var (messages, permutation) = generated;
            var kept = messages.Take(messages.Length / 2).ToList();
            var arriving = messages.Skip(messages.Length / 2).ToList();

            var original = Replication.Marks(Rule, kept, arriving);
            var shuffled = Replication.Marks(Rule, kept, permutation.Where(at => at >= kept.Count).Select(at => messages[at]).ToList());

            Assert.Equal(original, shuffled);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void OneSenderAloneNeverMarksWhateverTheTextAndTheLength() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
            Assert.Empty(Replication.Marks(Rule, [], messages.Select(message => message with { Sender = "ZQX-um-so" }).ToList())));

    [Fact]
    [Trait("Kind", "Property")]
    public void ShortTextsNeverMarkWhateverTheSendersAndHowManyTimesTheyRepeat() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
            Assert.Empty(Replication.Marks(Rule, [], messages.Select(message => message with { Length = 9 }).ToList())));

    [Fact]
    [Trait("Kind", "Property")]
    public void DistinctTextsNeverMarkWhateverTheSendersAndTheLength() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
            Assert.Empty(Replication.Marks(Rule, [], messages.Select((message, at) => message with { Text = "ZQX-texto-" + at, Length = 99 }).ToList())));

    [Fact]
    [Trait("Kind", "Property")]
    public void MarkingNeverRemovesOrMergesAMessageAndEveryPointerEndsInAMarkedFirstOccurrence() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
        {
            var marks = Replication.Marks(Rule, [], messages);
            var places = messages.Select(message => message.Place).ToHashSet();

            Assert.Equal(marks.Count, marks.Select(mark => mark.Of).Distinct().Count());
            Assert.All(marks, mark => Assert.Contains(mark.Of, places));
            Assert.All(marks, mark => Assert.Contains(new ReplicationMark(mark.First, mark.First), marks));
            Assert.Equal(places.Count, messages.Select(message => message.Place).Distinct().Count());
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheMarkedAreExactlyTheMessagesWhoseTextIsLongEnoughAndSaidByEnoughSenders() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
        {
            var expected = messages
                .Where(message => message.Length >= Rule.MinLength)
                .Where(message => messages.Where(other => other.Text == message.Text).Select(other => other.Sender).Distinct().Count() >= Rule.MinSenders)
                .Select(message => message.Place)
                .Order()
                .ToList();

            var marked = Replication.Marks(Rule, [], messages).Select(mark => mark.Of).Order().ToList();

            Assert.Equal(expected, marked);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheFirstOccurrenceIsTheEarliestOneOfTheSameText() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
            Assert.All(Replication.Marks(Rule, [], messages), mark =>
            {
                var text = messages.Single(message => message.Place == mark.Of).Text;
                var earliest = messages.Where(message => message.Text == text).MinBy(message => (message.Instant, message.Place))!;

                Assert.Equal(earliest.Place, mark.First);
            }));

    private static ReplicationOccurrence Occurrence(int at, int text, int sender, int minute) =>
        new(new ReplicationPlace(at / 3, at % 3), "ZQX-texto-" + text, "ZQX-remetente-" + sender, 5 + (text * 5), Start.AddMinutes(minute));
}
