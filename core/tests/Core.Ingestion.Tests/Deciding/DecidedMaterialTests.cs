using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Tests.Deciding;

/// <summary>Material reaches the writing only with a destination for every occurrence found, and a service that gave no analysis is not an empty list (issue 84, CA-1).</summary>
public sealed class DecidedMaterialTests
{
    private static readonly Occurrence Santos = Found.Occurrence("PERSON", "PROPN", "obl", 0, 6);

    private static readonly Occurrence Place = Found.Occurrence("LOCATION", "PROPN", "obl", 10, 16);

    private static readonly Occurrence Key = Found.Occurrence("SECRET", "NOUN", "obj", 20, 28);

    [Fact]
    public void EveryOccurrenceFoundHasItsDestinationAndTheOffsetsComeThroughUntouched()
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("Santos fui Santos senha12", "ana", null));

        var decided = Assert.IsType<DecidedMaterial>(DecidedMaterial.Decide(material, [Found.Analysis(Santos, Place, Key)]).Decided);

        Assert.Equal(
            [
                new DecidedOccurrence(Santos, Destination.Pseudonymize),
                new DecidedOccurrence(Place, Destination.Preserve),
                new DecidedOccurrence(Key, Destination.Remove),
            ],
            decided.Of(0));
    }

    [Fact]
    public void ATextAnalysedWithNothingFoundIsDecidedWithNoOccurrences()
    {
        var material = Found.Accepted(MaterialForm.Document, Units.Plain("limpo"), Units.Plain("também limpo"));

        var result = DecidedMaterial.Decide(material, [Found.Analysis(), Found.Analysis(Santos)]);

        Assert.Equal((0, 1, null), (result.Decided!.Of(0).Count, result.Decided.Of(1).Count, result.Reason));
    }

    [Theory]
    [InlineData(UnavailabilityReason.TimedOut)]
    [InlineData(UnavailabilityReason.ConnectionRefused)]
    [InlineData(UnavailabilityReason.MalformedResponse)]
    [InlineData(UnavailabilityReason.UnknownContractVersion)]
    public void AServiceThatGaveNoAnalysisIsTheRefusalOfTheFoundationAndNeverAnItemDecidedAsClean(UnavailabilityReason reason)
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("texto", "ana", null));

        var result = DecidedMaterial.Decide(material, [RecognitionOutcome.Unavailable(reason)]);

        Assert.Equal((null, reason), (result.Decided, result.Reason));
    }

    [Fact]
    public void OneTextWithNoAnalysisRefusesTheWholeItemWhateverTheOthersGave()
    {
        var material = Found.Accepted(MaterialForm.Document, Units.Plain("um"), Units.Plain("dois"), Units.Plain("três"));
        RecognitionOutcome[] analyses = [Found.Analysis(Santos), RecognitionOutcome.Unavailable(UnavailabilityReason.TimedOut), Found.Analysis()];

        var result = DecidedMaterial.Decide(material, analyses);

        Assert.Equal((null, UnavailabilityReason.TimedOut), (result.Decided, result.Reason));
    }

    [Fact]
    public void ThereIsOneTextForEachUnitOfEachFormOfTheItem()
    {
        var conversation = Found.Accepted(MaterialForm.Conversation, Units.Message("a", "ana", 1), Units.Message("b", "bia", 2));
        var sequence = Found.Accepted(MaterialForm.LabeledSequence, Units.Labeled("abc", new Passage(0, 1)), Units.Labeled("def", new Passage(1, 2)), Units.Labeled("ghi", new Passage(0, 3)));

        Assert.Equal((2, 3), (MaterialTexts.Of(conversation).Count, MaterialTexts.Of(sequence).Count));
        Assert.Equal(["a", "b"], MaterialTexts.Of(conversation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void AnalysesThatAreNotOneForEachTextAreADefectAndNotADecision(int analyses)
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("texto", "ana", null));
        var given = Enumerable.Range(0, analyses).Select(_ => Found.Analysis()).ToList();

        var defect = Assert.Throws<ArgumentException>(() => DecidedMaterial.Decide(material, given));

        Assert.Equal("analyses", defect.ParamName);
    }

    [Fact]
    public void ATextOutsideTheItemIsADefect()
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("texto", "ana", null));
        var decided = DecidedMaterial.Decide(material, [Found.Analysis()]).Decided!;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => decided.Of(1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => decided.Of(-1));
    }

    [Fact]
    public void NoItemAndNoAnalysesAreDefects()
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("texto", "ana", null));

        Assert.Equal("material", Assert.Throws<ArgumentNullException>(() => DecidedMaterial.Decide(null!, [Found.Analysis()])).ParamName);
        Assert.Equal("analyses", Assert.Throws<ArgumentNullException>(() => DecidedMaterial.Decide(material, null!)).ParamName);
    }

    [Fact]
    public void TheDescriptionsTellCountsAndKindsAndNeverAnOffsetOrAWord()
    {
        var material = Found.Accepted(MaterialForm.Comment, Units.Message("ZQXMARCADOR", "ana", null));
        var decided = DecidedMaterial.Decide(material, [Found.Analysis(Santos)]).Decided!;

        Assert.Equal("DecidedMaterial { Texts = 1, Occurrences = 1 }", decided.ToString());
        Assert.Equal("DecidedOccurrence { Kind = PERSON, Destination = Pseudonymize }", decided.Of(0)[0].ToString());
    }
}
