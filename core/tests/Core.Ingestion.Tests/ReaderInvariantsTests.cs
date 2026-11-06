using System.Text;
using Core.Ingestion.Reading;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests;

/// <summary>
/// Whatever messages the file holds, valid JSON is read whole with as many units as messages; whatever is done to the bytes of a file, the reader gives
/// one of the two outcomes and never an exception.
/// </summary>
public sealed class ReaderInvariantsTests
{
    private static readonly ExportedConversationReader Reader = new();

    private static readonly Gen<string> Marked = Gen.String[Gen.Char['A', 'Z'], 1, 12];

    private static readonly Gen<string> Instants = Gen.Int[0, 86_399].Select(second => $"2031-03-04T{second / 3600:00}:{second / 60 % 60:00}:{second % 60:00}Z");

    private static readonly Gen<List<(string Sender, string Text, string Instant)>> Messages =
        Gen.Select(Marked, Marked, Instants, (sender, text, instant) => (sender, text, instant)).List[0, 8];

    [Fact]
    [Trait("Kind", "Property")]
    public void ValidJsonIsAlwaysReadWholeWithAsManyUnitsAsMessages() =>
        PropertyRunner.FromProcess().Check(Messages, messages =>
        {
            var outcome = Reader.Read(Encoding.UTF8.GetBytes(Document(messages)));

            var units = Assert.IsType<ReadMaterial>(outcome.Material).Units;
            Assert.Equal(messages.Count, units.Count);
            Assert.Equal(messages.Select(message => message.Text), units.Select(unit => unit.Text));
            Assert.Null(outcome.Reason);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void BytesChangedOrCutAreAlwaysOneOfTheTwoOutcomes() =>
        PropertyRunner.FromProcess().Check(Gen.Select(Messages, Gen.Int[0, 100_000], Gen.Byte, Gen.Bool), generated =>
        {
            var (messages, position, replacement, cut) = generated;
            var bytes = Encoding.UTF8.GetBytes(Document(messages));
            var at = position % bytes.Length;
            var changed = cut ? bytes[..at] : Replaced(bytes, at, replacement);

            var outcome = Reader.Read(changed);

            Assert.True((outcome.Material is null) != (outcome.Reason is null));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void AnythingAtAllAsBytesIsOneOfTheTwoOutcomes() =>
        PropertyRunner.FromProcess().Check(Gen.Byte.Array[0, 64], bytes =>
        {
            var outcome = Reader.Read(bytes);

            Assert.True((outcome.Material is null) != (outcome.Reason is null));
        });

    private static byte[] Replaced(byte[] bytes, int at, byte replacement)
    {
        var copy = (byte[])bytes.Clone();
        copy[at] = replacement;
        return copy;
    }

    private static string Document(List<(string Sender, string Text, string Instant)> messages) =>
        """{"formato":"cdg-conversa/1","mensagens":[""" + string.Join(
            ",",
            messages.Select((message, at) => $$"""{"id":"{{at}}","remetente":"{{message.Sender}}","instante":"{{message.Instant}}","texto":"{{message.Text}}"}""")) + "]}";
}
