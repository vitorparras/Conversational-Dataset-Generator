using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>The offsets of the shared fixture count code points, so they differ from UTF-16 indexes after an emoji or an astral character.</summary>
public sealed class CodePointOffsetTests
{
    private static readonly AnalysisRequest Request = ContractFixture.ReadRequest("accepted/request.json");
    private static readonly AnalysisResponse Response = ContractFixture.ReadResponse("accepted/response-astral.json");

    [Theory]
    [InlineData(0, "PESSOA_1")]
    [InlineData(1, "LUGAR_1")]
    public void OffsetsCountCodePoints(int index, string expected)
    {
        var occurrence = Response.Occurrences[index];

        var span = string.Concat(Request.Text.EnumerateRunes().Skip(occurrence.Start).Take(occurrence.End - occurrence.Start));

        Assert.Equal(expected, span);
    }

    [Theory]
    [InlineData(0, "PESSOA_1")]
    [InlineData(1, "LUGAR_1")]
    public void OffsetsAreNotUtf16Indexes(int index, string marker)
    {
        var occurrence = Response.Occurrences[index];

        var utf16Index = Request.Text.IndexOf(marker, StringComparison.Ordinal);

        Assert.NotEqual(utf16Index, occurrence.Start);
    }
}
