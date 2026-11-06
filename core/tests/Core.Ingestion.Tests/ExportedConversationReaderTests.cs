using System.Text;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests;

/// <summary>The reader of cdg-conversa/1 reads a conversation whole or says why not, and a reason never holds a word of the content.</summary>
public sealed class ExportedConversationReaderTests
{
    private const string Marker = "ZQXMARCADOR";
    private const string Head = """{"formato":"cdg-conversa/1","mensagens":""";

    private static readonly ExportedConversationReader Reader = new();

    public static TheoryData<string, UnreadableReason> Defects() => new()
    {
        { """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR" """, UnreadableReason.Truncated },
        { """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"An""", UnreadableReason.Truncated },
        { """{"formato":"cdg-conversa/1","mensagens":""", UnreadableReason.Truncated },
        { /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[],"formato":"cdg-conversa/1"}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","id":"b","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR","cor":"azul"}]}""", UnreadableReason.Corrupted },
        { Head + """[],"extra":1}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":42}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":7,"remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":null,"instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """{"id":"a"}}""", UnreadableReason.Corrupted },
        { Head + """["ZQXMARCADOR"]}""", UnreadableReason.Corrupted },
        { """["cdg-conversa/1"]""", UnreadableReason.Corrupted },
        { /*lang=json*/ """{"mensagens":[]}""", UnreadableReason.Corrupted },
        { /*lang=json*/ """{"formato":7,"mensagens":[]}""", UnreadableReason.Corrupted },
        { /*lang=json*/ """{"formato":"cdg-conversa/1"}""", UnreadableReason.Corrupted },
        { /*lang=json*/ """{"formato":"cdg-conversa/2","mensagens":[]}""", UnreadableReason.UnsupportedFormat },
        { /*lang=json*/ """{"formato":"CDG-CONVERSA/1","mensagens":[]}""", UnreadableReason.UnsupportedFormat },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04 08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"ontem","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":1,"texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"},"""
            + """{"id":"a","remetente":"Bia","instante":"2031-03-04T08:01:00Z","texto":"x"}]}""",
            UnreadableReason.Corrupted
        },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR","responde_a":"zzz"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR","responde_a":3}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR","responde_a":"a"}]}""", UnreadableReason.Corrupted },
        { Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"ZQXMARCADOR"},]}""", UnreadableReason.Corrupted },
        { Head + """[/* ZQXMARCADOR */]}""", UnreadableReason.Corrupted },
        { Head + """[] // ZQXMARCADOR""", UnreadableReason.Corrupted },
        { Head + """[{"id":{"a":{"b":"ZQXMARCADOR"}}}]}""", UnreadableReason.Corrupted },
        { Head + """[]} ZQXMARCADOR""", UnreadableReason.Corrupted },
        { Head + """[]}{"formato":"cdg-conversa/1","mensagens":[]}""", UnreadableReason.Corrupted },
        { "", UnreadableReason.Truncated },
        { "   ", UnreadableReason.Truncated },
        { "tru", UnreadableReason.Truncated },
        { "\"ZQXMARCADOR\"", UnreadableReason.Corrupted },
        { "ZQXMARCADOR", UnreadableReason.Corrupted },
        { "null", UnreadableReason.Corrupted },
    };

    [Fact]
    public void AConversationIsReadWholeWithWhatTheFileBroughtAndInTheOrderOfTheArray()
    {
        var content = Head + """
            [{"id":"b","remetente":"Bia","instante":"2031-03-04T05:01:00-03:00","texto":"segunda"},
             {"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"primeira","responde_a":"b"}]}
            """;

        var outcome = Reader.Read(Bytes(content));

        var units = Assert.IsType<ReadMaterial>(outcome.Material).Units;
        Assert.Equal(["segunda", "primeira"], [.. units.Select(unit => unit.Text)]);
        Assert.Equal(["Bia", "Ana"], [.. units.Select(unit => unit.Author)]);
        Assert.Equal([null, 0], [.. units.Select(unit => unit.RepliesTo)]);
        Assert.Equal(new DateTimeOffset(2031, 3, 4, 5, 1, 0, TimeSpan.FromHours(-3)), units[0].Instant);
        Assert.Equal(TimeSpan.FromHours(-3), units[0].Instant!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, units[1].Instant!.Value.Offset);
        Assert.All(units, unit => Assert.Empty(unit.Passages));
        Assert.Null(outcome.Reason);
    }

    [Fact]
    public void ASecondWithAFractionIsAcceptedAndTheOffsetIsKeptAsZOrAsHours()
    {
        var instants = new[] { "2031-03-04T08:00:00.5Z", "2031-03-04T08:00:00.1234567+02:00", "2031-03-04T08:00:00+00:00" };
        var content = Head + "[" + string.Join(",", instants.Select((instant, at) => $$"""{"id":"{{at}}","remetente":"Ana","instante":"{{instant}}","texto":"t"}""")) + "]}";

        var units = Assert.IsType<ReadMaterial>(Reader.Read(Bytes(content)).Material).Units;

        Assert.Equal(
            [
                new DateTimeOffset(2031, 3, 4, 8, 0, 0, 500, TimeSpan.Zero),
                new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.FromHours(2)).AddTicks(1_234_567),
                new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero),
            ],
            [.. units.Select(unit => unit.Instant!.Value)]);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromHours(2), TimeSpan.Zero], [.. units.Select(unit => unit.Instant!.Value.Offset)]);
    }

    [Fact]
    public void AConversationWithNoMessageIsReadAsNoneAndLeftForTheConferenceToRefuse()
    {
        var outcome = Reader.Read(Bytes(Head + "[]}"));

        Assert.Empty(Assert.IsType<ReadMaterial>(outcome.Material).Units);
    }

    [Fact]
    public void AnAnswerNamesTheMessageByItsIdWhereverInTheFileThatMessageIs()
    {
        var content = Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"t","responde_a":"b"},"""
            + """{"id":"b","remetente":"Bia","instante":"2031-03-04T08:01:00Z","texto":"u"}]}""";

        var units = Assert.IsType<ReadMaterial>(Reader.Read(Bytes(content)).Material).Units;

        Assert.Equal([1, null], [.. units.Select(unit => unit.RepliesTo)]);
    }

    [Fact]
    public void TwoMessagesOfTheSameSenderAnswerDifferentMessagesAndTheReaderKeepsWhich()
    {
        var content = Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"t"},"""
            + """{"id":"b","remetente":"Ana","instante":"2031-03-04T08:01:00Z","texto":"u"},"""
            + """{"id":"c","remetente":"Bia","instante":"2031-03-04T08:02:00Z","texto":"v","responde_a":"b"},"""
            + """{"id":"d","remetente":"Bia","instante":"2031-03-04T08:03:00Z","texto":"w","responde_a":"a"}]}""";

        var units = Assert.IsType<ReadMaterial>(Reader.Read(Bytes(content)).Material).Units;

        Assert.Equal([null, null, 1, 0], [.. units.Select(unit => unit.RepliesTo)]);
    }

    [Theory]
    [MemberData(nameof(Defects))]
    public void EachDefectOfTheFileIsUnreadableForItsReasonAndHandsOverNothing(string content, UnreadableReason reason)
    {
        var outcome = Reader.Read(Bytes(content));

        Assert.Equal((null, reason), (outcome.Material, outcome.Reason));
        Assert.DoesNotContain(Marker, outcome.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMessageThatIsWrongTakesTheWholeFileEvenWhenTheOnesBeforeItAreRight()
    {
        var content = Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"t"},{"id":"b","remetente":"Bia","instante":"sem fuso","texto":"u"}]}""";

        var outcome = Reader.Read(Bytes(content));

        Assert.Equal((null, UnreadableReason.Corrupted), (outcome.Material, outcome.Reason));
    }

    [Fact]
    public void ABytePastTheLimitIsUnreadableAndTheLimitItselfIsStillRead()
    {
        var padding = new string(' ', ExportedConversationReader.MaxBytes - (Head + "[]}").Length);
        var atTheLimit = Bytes(Head + "[]" + padding + "}");

        var past = Reader.Read(new byte[ExportedConversationReader.MaxBytes + 1]);
        var at = Reader.Read(atTheLimit);

        Assert.Equal(ExportedConversationReader.MaxBytes, atTheLimit.Length);
        Assert.Equal((UnreadableReason.Corrupted, true), (past.Reason!.Value, at.Material is not null));
    }

    [Fact]
    public void BytesThatAreNotUtf8AreUnreadableAndNotAnException()
    {
        var bytes = Encoding.UTF8.GetBytes(Head + """[{"id":"a","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"X"}]}""");
        var at = Array.IndexOf(bytes, (byte)'X');
        bytes[at] = 0xFF;
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Head + "[]}")).ToArray();

        Assert.Equal(
            [UnreadableReason.Corrupted, UnreadableReason.Corrupted],
            [Reader.Read(bytes).Reason!.Value, Reader.Read(withBom).Reason!.Value]);
    }

    [Fact]
    public void TheReaderNamesItsFormat()
    {
        Assert.Equal("cdg-conversa/1", Reader.Format);
        Assert.Equal("cdg-conversa/1", ExportedConversationReader.FormatName);
    }

    private static byte[] Bytes(string content) => Encoding.UTF8.GetBytes(content);
}
