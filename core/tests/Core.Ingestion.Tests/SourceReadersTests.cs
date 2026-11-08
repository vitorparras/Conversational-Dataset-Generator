using System.Text;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests;

/// <summary>A format with no reader is unreadable by its name, a format with one is read by it alone, and a new format is a new reader and nothing else (CA-2, CA-3).</summary>
public sealed class SourceReadersTests
{
    private static readonly byte[] Content = [1, 2, 3];

    [Fact]
    public void TheReaderOfTheFormatReadsAndNoOtherIsAsked()
    {
        var first = new FakeReader("primeiro/1", ReadOutcome.Read(new ReadMaterial([Units.Plain("um")])));
        var second = new FakeReader("segundo/1", ReadOutcome.Read(new ReadMaterial([Units.Plain("dois")])));
        var readers = new SourceReaders([first, second]);

        var outcome = readers.Read("segundo/1", Content);

        Assert.Equal("dois", Assert.IsType<ReadMaterial>(outcome.Material).Units[0].Text);
        Assert.Equal((0, 1), (first.Calls, second.Calls));
        Assert.Equal(Content, second.Received);
    }

    [Theory]
    [InlineData("pdf/1")]
    [InlineData("")]
    [InlineData("PRIMEIRO/1")]
    [InlineData("primeiro/1 ")]
    public void AFormatWithNoReaderIsUnreadableAsUnsupportedAndNoReaderIsTried(string format)
    {
        var only = new FakeReader("primeiro/1", ReadOutcome.Read(new ReadMaterial([])));
        var readers = new SourceReaders([only]);

        var outcome = readers.Read(format, Content);

        Assert.Equal((null, UnreadableReason.UnsupportedFormat, 0), (outcome.Material, outcome.Reason, only.Calls));
    }

    [Fact]
    public void ARegistryWithNoReaderReadsNothing()
    {
        var outcome = new SourceReaders([]).Read("cdg-conversa/1", Content);

        Assert.Equal(UnreadableReason.UnsupportedFormat, outcome.Reason);
    }

    [Fact]
    public void ANewFormatIsOneReaderMoreAndTheOldOnesAreUntouched()
    {
        var added = new FakeReader("novo/1", ReadOutcome.Unreadable(UnreadableReason.Truncated));
        var readers = new SourceReaders([new ExportedConversationReader(), added]);

        var conversation = readers.Read(ExportedConversationReader.FormatName, Empty("cdg-conversa/1"));
        var novel = readers.Read("novo/1", Content);

        Assert.Equal((true, UnreadableReason.Truncated), (conversation.Material is not null, novel.Reason!.Value));
    }

    [Fact]
    public void TheRegistryOfTheFirstVersionReadsTheExportedConversationAndNothingElse()
    {
        var readers = SourceReaders.Default();

        var conversation = readers.Read("cdg-conversa/1", Empty("cdg-conversa/1"));
        var other = readers.Read("cdg-conversa/2", Empty("cdg-conversa/2"));

        Assert.Equal((true, UnreadableReason.UnsupportedFormat), (conversation.Material is not null, other.Reason!.Value));
    }

    [Fact]
    public void TwoReadersForOneFormatAreADefectAndNullIsNone()
    {
        var one = new FakeReader("igual/1", ReadOutcome.Unreadable(UnreadableReason.Corrupted));
        var twice = Assert.Throws<ArgumentException>(() => new SourceReaders([one, new FakeReader("igual/1", ReadOutcome.Unreadable(UnreadableReason.Corrupted))]));
        var nothing = Assert.Throws<ArgumentNullException>(() => new SourceReaders(null!));
        var empty = Assert.Throws<ArgumentNullException>(() => new SourceReaders([null!]));
        var format = Assert.Throws<ArgumentNullException>(() => new SourceReaders([one]).Read(null!, Content));

        Assert.Equal(["readers", "readers", "reader", "format"], [twice.ParamName, nothing.ParamName, empty.ParamName, format.ParamName]);
    }

    private static byte[] Empty(string format) => Encoding.UTF8.GetBytes($$"""{"formato":"{{format}}","mensagens":[]}""");

    private sealed class FakeReader(string format, ReadOutcome outcome) : ISourceReader
    {
        public int Calls { get; private set; }

        public byte[]? Received { get; private set; }

        public string Format { get; } = format;

        public ReadLimits Limits { get; } = new(4, 10, 100, 64);

        public ReadOutcome Read(ReadOnlyMemory<byte> content)
        {
            Calls++;
            Received = content.ToArray();
            return outcome;
        }

        public bool Recognizes(ReadOnlySpan<byte> head) => head.Length > 0;
    }
}
