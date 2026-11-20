using Core.Ingestion.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>The trail is sealed as text and read back as the same trail: the same edits, step by step, translating the same positions (issue 92).</summary>
public sealed class TrailJsonTests
{
    [Fact]
    public void TrailReadBackTranslatesAsTheOneWritten()
    {
        const string raw = "a [dado] b QXZ7 c";
        var first = new FixedStep("primeiro", _ => [new SanitizationEdit(2, 8, SanitizationMarker.Data)]);
        var second = new FixedStep("segundo", _ => [new SanitizationEdit(11, 15, SanitizationMarker.Credential)]);
        var written = Sanitized(Sanitizer(first, second), raw).Trail;

        var read = TrailJson.Read(TrailJson.Write(written));

        Assert.Equal(written.Steps, read.Steps);
        Assert.Equal((written.RawLength, written.SanitizedLength), (read.RawLength, read.SanitizedLength));
        Assert.Equal((true, 24, 25), (read.Forward(16, 17).Exists, read.Forward(16, 17).Start, read.Forward(16, 17).End));
        Assert.False(read.Forward(11, 15).Exists);
    }

    [Fact]
    public void TrailOfWrittenTextReadBackTranslatesAsTheOneWritten()
    {
        const string raw = "x\u00C3\u00A9y\u000Cpg\u000Cw";
        var rewriting = new FixedStep("reescreve", _ => [SanitizationEdit.Rewriting(1, 3, "\u00E9", Boundaries), SanitizationEdit.Rewriting(4, 7, string.Empty, Boundaries)]);
        var sanitized = Sanitized(Sanitizer(rewriting), raw);

        var read = TrailJson.Read(TrailJson.Write(sanitized.Trail));

        Assert.Equal("x\u00E9y\u000Cw", sanitized.Text);
        Assert.Equal(sanitized.Trail.Steps, read.Steps);
        Assert.Equal((9, 5), (read.RawLength, read.SanitizedLength));
        Assert.Equal((true, 4, 5), (read.Forward(8, 9).Exists, read.Forward(8, 9).Start, read.Forward(8, 9).End));
        Assert.False(read.Forward(5, 6).Exists);
    }

    [Fact]
    public void TrailOfAStepThatEditedNothingIsWrittenAsTheStorageExpects()
    {
        var trail = Sanitized("texto sem nada zqx").Trail;

        Assert.Equal(/*lang=json,strict*/ """{"raw":18,"steps":[[]]}""", TrailJson.Write(trail));
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":1,"end":9,"marker":"Data"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":0,"end":2,"marker":"Outro"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[]],"extra":1}""")]
    [InlineData(/*lang=json,strict*/ """{"steps":[[]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":2,"end":2,"marker":"Data"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":6,"steps":[[{"start":0,"end":3,"marker":"Data"},{"start":2,"end":4,"marker":"Data"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":3,"steps":[null]}""")]
    [InlineData("null")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":0,"end":2,"marker":null}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":0,"end":2,"marker":"Data","replacement":"x"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":0,"end":2,"marker":0}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":4,"steps":[[{"start":-1,"end":2,"marker":"Data"}]]}""")]
    [InlineData(/*lang=json,strict*/ """{"raw":-1,"steps":[[]]}""")]
    [InlineData(/*lang=json*/ """{"raw":4,"raw":4,"steps":[[]]}""")]
    [InlineData(/*lang=json*/ """{"raw":4,"steps":[[{"start":0,"start":1,"end":2,"marker":"Data"}]]}""")]
    public void TrailThatDoesNotFitItsTextIsADefect(string json) =>
        Assert.Throws<InvalidDataException>(() => TrailJson.Read(json));

    [Fact]
    public void NoTrailIsWritten() => Assert.Throws<ArgumentNullException>(() => TrailJson.Write(null!));
}
