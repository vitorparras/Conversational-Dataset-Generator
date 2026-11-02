using Core.Persistence.ServerGuarantees;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The records of a csvlog are read whole: commas and line breaks inside quotes belong to the field, a doubled quote is one quote, and the text ends where the file does.</summary>
public sealed class CsvRecordReaderTests
{
    [Fact]
    public async Task PlainFieldsAreSplitAtTheCommas()
    {
        var records = await ReadAllAsync("a,b,,d\n");

        Assert.Equal([["a", "b", string.Empty, "d"]], records);
    }

    [Fact]
    public async Task CommasAndLineBreaksInsideQuotesBelongToTheField()
    {
        var records = await ReadAllAsync("a,\"b,c\nd\",e\nf,g\n");

        Assert.Equal([["a", "b,c\nd", "e"], ["f", "g"]], records);
    }

    [Fact]
    public async Task DoubledQuoteInsideQuotesIsOneQuote()
    {
        var records = await ReadAllAsync("\"say \"\"x\"\"\",y\n");

        Assert.Equal([["say \"x\"", "y"]], records);
    }

    [Fact]
    public async Task CarriageReturnBeforeTheLineBreakIsNotPartOfTheLastField()
    {
        var records = await ReadAllAsync("a,b\r\nc,d\r\n");

        Assert.Equal([["a", "b"], ["c", "d"]], records);
    }

    [Fact]
    public async Task LastRecordWithNoLineBreakAndTheEmptyTextAreRead()
    {
        Assert.Equal([["a", "b"]], await ReadAllAsync("a,b"));
        Assert.Empty(await ReadAllAsync(string.Empty));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(64)]
    public async Task RecordsAreTheSameWhereverTheBufferEndsEvenBetweenTheTwoQuotesOfADoubledOne(int bufferSize)
    {
        var records = await ReadAllAsync("a,\"say \"\"x\"\",\nz\",c\r\nd,e\n", bufferSize);

        Assert.Equal([["a", "say \"x\",\nz", "c"], ["d", "e"]], records);
    }

    [Fact]
    public async Task RecordLongerThanTheBufferIsReadWhole()
    {
        var field = new string('x', (CsvRecordReader.DefaultBufferSize * 3) + 7);

        var records = await ReadAllAsync($"{field},y\n{field}\n");

        Assert.Equal([[field, "y"], [field]], records);
    }

    private static Task<List<string[]>> ReadAllAsync(string text) => ReadAllAsync(text, CsvRecordReader.DefaultBufferSize);

    private static async Task<List<string[]>> ReadAllAsync(string text, int bufferSize)
    {
        using var reader = new StringReader(text);
        var records = new CsvRecordReader(reader, bufferSize);
        var all = new List<string[]>();
        while (await records.ReadAsync(TestContext.Current.CancellationToken) is { } record)
        {
            all.Add([.. record]);
        }

        return all;
    }
}
