using System.Text;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests;

/// <summary>
/// The limits the reader declares and the format it recognises by the first bytes (issue 75, criteria 2 and 4):
/// the excess is refused with a reason, and nothing outside the file is followed.
/// </summary>
public sealed class ReaderLimitsTests
{
    private const string Head = """{"formato":"cdg-conversa/1","mensagens":""";

    private static readonly ExportedConversationReader Reader = new();

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Message(string id, string sender, string text) => $$"""{"id":"{{id}}","remetente":"{{sender}}","instante":"2031-03-04T08:00:00Z","texto":"{{text}}"}""";

    [Fact]
    public void TheReaderDeclaresItsLimits() =>
        Assert.Equal(new ReadLimits(4, 100_000, 65_536, 4096), Reader.Limits);

    [Fact]
    public void AStructureDeeperThanTheLimitIsRefusedAsOverTheLimitsWithoutBeingFollowed()
    {
        var deep = Head + new string('[', 5000) + new string(']', 5000) + "}";

        var outcome = Reader.Read(Bytes(deep));

        Assert.Equal((null, UnreadableReason.OverLimits), (outcome.Material, outcome.Reason));
    }

    [Fact]
    public void BracketsInsideAStringAreNotStructure()
    {
        var outcome = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", "[[[[[[[[ \\\" {{{{{{{{ ]]]]]]]]") + "]}"));

        Assert.NotNull(outcome.Material);
    }

    [Fact]
    public void ATextPastTheLimitIsRefusedAndOneAtTheLimitIsRead()
    {
        var atTheLimit = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", new string('x', 65_536)) + "]}"));
        var past = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", new string('x', 65_537)) + "]}"));

        Assert.Equal((true, UnreadableReason.OverLimits), (atTheLimit.Material is not null, past.Reason));
    }

    [Fact]
    public void TheLimitOfTheTextCountsBytesAndNotCharacters()
    {
        var past = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", new string('é', 32_769)) + "]}"));

        Assert.Equal(UnreadableReason.OverLimits, past.Reason);
    }

    [Fact]
    public void MoreUnitsThanTheLimitAreRefusedAndTheLimitItselfIsRead()
    {
        var one = Message("m", "Ana", "t");
        var atTheLimit = Reader.Read(Bytes(Head + "[" + string.Join(',', Enumerable.Repeat(one, 100_000).Select((text, at) => text.Replace("\"m\"", $"\"m{at}\"", StringComparison.Ordinal))) + "]}"));
        var past = Reader.Read(Bytes(Head + "[" + string.Join(',', Enumerable.Repeat(one, 100_001).Select((text, at) => text.Replace("\"m\"", $"\"m{at}\"", StringComparison.Ordinal))) + "]}"));

        Assert.Equal((100_000, UnreadableReason.OverLimits), (atTheLimit.Material?.Units.Count, past.Reason));
    }

    [Theory]
    [InlineData("http://exemplo.test/a")]
    [InlineData("file:///etc/passwd")]
    [InlineData("../segredo")]
    [InlineData("a/../b")]
    [InlineData("..\\\\segredo")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\\\\\servidor\\\\pasta")]
    [InlineData("~/chave")]
    [InlineData("C:\\\\dados\\\\x")]
    [InlineData("c:/dados/x")]
    public void AnIdentifierOrASenderThatReadsAsAnAddressOrAPathIsRefusedAndNeverFollowed(string reference)
    {
        var asId = Reader.Read(Bytes(Head + "[" + Message(reference, "Ana", "t") + "]}"));
        var asSender = Reader.Read(Bytes(Head + "[" + Message("a", reference, "t") + "]}"));

        Assert.Equal((UnreadableReason.ExternalReference, UnreadableReason.ExternalReference), (asId.Reason, asSender.Reason));
    }

    [Fact]
    public void AnAnsweredMessageThatReadsAsAnAddressIsRefused()
    {
        var answers = /*lang=json*/ """{"id":"b","remetente":"Ana","instante":"2031-03-04T08:00:00Z","texto":"t","responde_a":"http://x.test/a"}""";

        var outcome = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", "t") + "," + answers + "]}"));

        Assert.Equal(UnreadableReason.ExternalReference, outcome.Reason);
    }

    [Fact]
    public void TheTextOfAMessageMayHoldAnAddressOrAPathBecauseItIsContent()
    {
        var outcome = Reader.Read(Bytes(Head + "[" + Message("a", "Ana", "veja http://x.test/a e ../b e C:\\\\z") + "]}"));

        Assert.NotNull(outcome.Material);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a.b")]
    [InlineData("a:b")]
    [InlineData("c:x")]
    public void NamesThatOnlyLookLikeOneAreNotReferences(string name)
    {
        var outcome = Reader.Read(Bytes(Head + "[" + Message(name, name, "t") + "]}"));

        Assert.NotNull(outcome.Material);
    }

    [Fact]
    public void TheReaderRecognisesItsFormatByTheFirstBytes()
    {
        Assert.True(Reader.Recognizes(Bytes(Head + "[]}")));
        Assert.True(Reader.Recognizes(Bytes("  \n" + Head + "[")));
        Assert.True(Reader.Recognizes(Bytes("""{"x":{"a":[1,2,{"b":3}]},"formato":"cdg-conversa/1","mensagens":[""")));
        Assert.True(Reader.Recognizes(Bytes(/*lang=json*/ """{"mensagens":[],"formato":"cdg-conversa/1"}""")));
    }

    [Theory]
    [InlineData(/*lang=json*/ """{"formato":"cdg-conversa/2","mensagens":[]}""")]
    [InlineData(/*lang=json*/ """{"formato":"CDG-CONVERSA/1","mensagens":[]}""")]
    [InlineData(/*lang=json*/ """{"formato":7,"mensagens":[]}""")]
    [InlineData("""["cdg-conversa/1"]""")]
    [InlineData("""{"formato":""")]
    [InlineData("""{"formato""")]
    [InlineData("")]
    [InlineData("texto qualquer")]
    [InlineData(/*lang=json*/ """{"x":{"formato":"cdg-conversa/1"}}""")]
    public void AFormatTheContentDoesNotConfirmIsNotRecognised(string content) =>
        Assert.False(Reader.Recognizes(Bytes(content)));

    [Fact]
    public void AByteOrderMarkOrAnotherEncodingIsNotRecognised()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(Head + "[]}")).ToArray();
        var utf16 = Encoding.Unicode.GetBytes(Head + "[]}");
        var utf16Marked = Encoding.Unicode.GetPreamble().Concat(utf16).ToArray();

        Assert.Equal([false, false, false], [Reader.Recognizes(bom), Reader.Recognizes(utf16), Reader.Recognizes(utf16Marked)]);
    }

    [Fact]
    public void TheFormatMemberMustComeInsideTheHeadTheReaderDeclares()
    {
        var late = /*lang=json*/ $$"""{"x":"{{new string('a', 5000)}}","formato":"cdg-conversa/1","mensagens":[]}""";
        var content = Bytes(late);

        Assert.Equal((false, true), (Reader.Recognizes(content.AsSpan(0, Reader.Limits.HeadBytes)), Reader.Recognizes(content)));
    }
}
