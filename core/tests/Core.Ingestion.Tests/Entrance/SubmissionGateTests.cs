using System.Security.Cryptography;
using System.Text;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests.Entrance;

/// <summary>The gate of a submission (issue 75, criteria 1 to 4 and the base of 5): the limit of bytes, the format by the content, the name that never crosses, the digest.</summary>
public sealed class SubmissionGateTests
{
    private const string Format = "cdg-conversa/1";
    private const string Valid = /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"ZQXAUTOR","instante":"2031-03-04T08:00:00Z","texto":"ZQXTEXTO"}]}""";
    private static readonly Guid Generated = new("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static SubmissionGate Gate(long limit = 1_000_000) => new(SourceReaders.Default(), limit, () => Generated);

    private static Task<SubmissionResult> ReceiveAsync(SubmissionGate gate, byte[] content, string format = Format, string? name = "x.json") =>
        gate.ReceiveAsync(new Submission(new CountingStream(content), format, name), TestContext.Current.CancellationToken);

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public async Task AValidSubmissionIsReadWholeWithTheGeneratedIdentifierTheDigestAndTheCount()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid);

        var result = await ReceiveAsync(Gate(), bytes);

        Assert.Equal(
            (true, null, null, new SubmissionId(Generated), Sha(bytes), bytes.LongLength),
            (result.Material is { Units.Count: 1 }, result.Refusal, result.Reason, result.Id, result.Digest, result.BytesRead));
    }

    [Fact]
    public async Task AContentPastTheLimitIsRefusedAtTheFirstByteExceedingAndNothingMoreIsRead()
    {
        var bytes = new byte[100_000];
        var stream = new CountingStream(bytes);

        var result = await Gate(10).ReceiveAsync(new Submission(stream, Format, "a.json"), TestContext.Current.CancellationToken);

        Assert.Equal(
            (SubmissionRefusal.TooLarge, 11L, 11L, Sha(bytes[..11]), "o envio passa do limite de tamanho, e a leitura parou no primeiro byte excedente", true),
            (result.Refusal, result.BytesRead, stream.Delivered, result.Digest, result.Reason, stream.LargestRequest <= 11));
    }

    [Fact]
    public async Task AContentExactlyAtTheLimitIsNotPastIt()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid);

        var atTheLimit = await ReceiveAsync(Gate(bytes.Length), bytes);
        var oneByteBelow = await ReceiveAsync(Gate(bytes.Length - 1), bytes);

        Assert.Equal((null, SubmissionRefusal.TooLarge), (atTheLimit.Refusal, oneByteBelow.Refusal));
    }

    [Fact]
    public async Task ThePiecesTheStreamGivesDoNotChangeWhatIsRead()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid);

        var result = await Gate().ReceiveAsync(new Submission(new CountingStream(bytes, piece: 3), Format, null), TestContext.Current.CancellationToken);

        Assert.Equal((true, Sha(bytes)), (result.Material is not null, result.Digest));
    }

    [Fact]
    public async Task AFormatTheVersionHasNoReaderForIsRefusedAndTheDigestStillComes()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid);

        var result = await ReceiveAsync(Gate(), bytes, "csv/1");

        Assert.Equal((SubmissionRefusal.UnknownFormat, "esta versão não tem leitor para o formato declarado", Sha(bytes)), (result.Refusal, result.Reason, result.Digest));
    }

    [Theory]
    [InlineData("texto qualquer, e não JSON")]
    [InlineData(/*lang=json*/ """{"formato":"cdg-conversa/2","mensagens":[]}""")]
    [InlineData(/*lang=json*/ """{"mensagens":[]}""")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task AFormatTheContentDoesNotConfirmIsRefusedWhateverTheNameSays(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        var result = await ReceiveAsync(Gate(), bytes, name: "conversa.cdg-conversa-1.json");

        Assert.Equal((SubmissionRefusal.FormatNotConfirmed, "o conteúdo não confirma o formato declarado"), (result.Refusal, result.Reason));
    }

    [Fact]
    public async Task AByteOrderMarkAndAnotherEncodingAreNotConfirmed()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Valid)).ToArray();
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Valid)).ToArray();

        var refused = new[] { await ReceiveAsync(Gate(), bom), await ReceiveAsync(Gate(), utf16), await ReceiveAsync(Gate(), Encoding.Unicode.GetBytes(Valid)) };

        Assert.All(refused, result => Assert.Equal(SubmissionRefusal.FormatNotConfirmed, result.Refusal));
    }

    [Theory]
    [InlineData("a.json")]
    [InlineData("a.exe")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("nome\u0000com\u0007controle\n.json")]
    [InlineData("")]
    [InlineData(null)]
    public async Task TheNameNeverDecidesNorCrossesAndTheResultIsTheSameForAnyName(string? name)
    {
        var bytes = Encoding.UTF8.GetBytes(Valid);
        var reference = await ReceiveAsync(Gate(), bytes, name: "referencia.json");

        var result = await ReceiveAsync(Gate(), bytes, name: name);

        Assert.Equal((reference.Id, reference.Digest, reference.BytesRead, reference.Refusal), (result.Id, result.Digest, result.BytesRead, result.Refusal));
        Assert.DoesNotContain("passwd", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("system32", result.ToString(), StringComparison.Ordinal);
        Assert.Equal(new SubmissionId(Generated).ToString(), result.Id.ToString());
    }

    [Fact]
    public async Task EachSubmissionGetsAnIdentifierOfItsOwnFromTheSystem()
    {
        var gate = new SubmissionGate(SourceReaders.Default(), 1_000_000);
        var bytes = Encoding.UTF8.GetBytes(Valid);

        var first = await ReceiveAsync(gate, bytes, name: "mesmo.json");
        var second = await ReceiveAsync(gate, bytes, name: "mesmo.json");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(32, first.Id.ToString().Length);
    }

    [Fact]
    public async Task AContentThatPassesTheHeadButIsCutIsRefusedAsUnreadableWithItsCause()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid[..^10]);

        var result = await ReceiveAsync(Gate(), bytes);

        Assert.Equal((SubmissionRefusal.Unreadable, UnreadableReason.Truncated, Sha(bytes)), (result.Refusal, result.Cause, result.Digest));
        Assert.StartsWith("a leitura não foi completa (", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStructureTooDeepAndAReferenceOutsideTheFileAreRefusedByTheReaderWithTheirReason()
    {
        var deep = Encoding.UTF8.GetBytes(Valid[..^2] + string.Concat(Enumerable.Repeat("[", 300)) + "]}");
        var reference = Encoding.UTF8.GetBytes(Valid.Replace("\"a\"", "\"http://x.test/a\"", StringComparison.Ordinal));

        var results = new[] { await ReceiveAsync(Gate(), deep), await ReceiveAsync(Gate(), reference) };

        Assert.Equal([UnreadableReason.OverLimits, UnreadableReason.ExternalReference], [.. results.Select(result => result.Cause!.Value)]);
    }

    [Fact]
    public async Task NoReasonCarriesAWordOfTheContentOrOfTheName()
    {
        var bytes = Encoding.UTF8.GetBytes(Valid.Replace("\"a\"", "\"http://ZQXREF.test/a\"", StringComparison.Ordinal));

        var results = new[] { await ReceiveAsync(Gate(), bytes, name: "ZQXNOME.json"), await ReceiveAsync(Gate(5), bytes, name: "ZQXNOME.json"), await ReceiveAsync(Gate(), bytes, "ZQXFORMATO") };

        Assert.All(results, result => Assert.DoesNotContain("ZQX", result.Reason ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheArgumentsAreCheckedByName()
    {
        var gate = Gate();
        var none = await Assert.ThrowsAsync<ArgumentNullException>(() => gate.ReceiveAsync(null!, TestContext.Current.CancellationToken));
        var stream = await Assert.ThrowsAsync<ArgumentNullException>(() => gate.ReceiveAsync(new Submission(null!, Format, null), TestContext.Current.CancellationToken));
        var format = await Assert.ThrowsAsync<ArgumentNullException>(() => gate.ReceiveAsync(new Submission(new MemoryStream(), null!, null), TestContext.Current.CancellationToken));
        var readers = Assert.Throws<ArgumentNullException>(() => new SubmissionGate(null!, 1));
        var limit = Assert.Throws<ArgumentOutOfRangeException>(() => new SubmissionGate(SourceReaders.Default(), 0));
        var generator = Assert.Throws<ArgumentNullException>(() => new SubmissionGate(SourceReaders.Default(), 1, null!));

        Assert.Equal(
            ["submission", "submission.Content", "submission.Format", "readers", "maxBytes", "newId"],
            [none.ParamName, stream.ParamName, format.ParamName, readers.ParamName, limit.ParamName, generator.ParamName]);
    }
}
