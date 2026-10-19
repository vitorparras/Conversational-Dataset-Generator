using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>An answer is used whole only when the service speaks the core's contract and every offset falls inside the text sent.</summary>
public sealed class ResponseValidatorTests
{
    private static ResponseValidation Validate(byte[] body) =>
        ResponseValidator.Validate(Answer.RequestFor(Answer.Text), ContractVersion.Current, body);

    [Fact]
    public void AnswerWithEveryOffsetInsideTheTextIsAcceptedWhole()
    {
        var request = ContractFixture.ReadRequest("accepted/request.json");

        var validation = ResponseValidator.Validate(request, ContractVersion.Current, Answer.Fixture("accepted/response-astral.json"));

        Assert.Equal(ContractFixture.ReadResponse("accepted/response-astral.json").Occurrences, validation.Response?.Occurrences);
    }

    [Fact]
    public void AcceptedAnswerHasNoProblem()
    {
        var validation = Validate(Answer.WithOccurrence(3, 11));

        Assert.Null(validation.Problem);
    }

    [Fact]
    public void AnswerWithNoOccurrenceIsAnAnalysis()
    {
        var validation = Validate(Answer.Fixture("accepted/response-empty.json"));

        Assert.Empty(validation.Response!.Occurrences);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, Answer.TextCodePoints)]
    [InlineData(Answer.TextCodePoints, Answer.TextCodePoints)]
    [InlineData(1, 2)]
    public void OccurrenceWithinTheCodePointsOfTheTextIsAccepted(int start, int end)
    {
        var validation = Validate(Answer.WithOccurrence(start, end));

        Assert.Equal(new Occurrence(start, end, "PERSON", "PROPN", "obl"), Assert.Single(validation.Response!.Occurrences));
    }

    [Fact]
    public void EmptyTextRefusesASpanPastItsEnd()
    {
        var validation = ResponseValidator.Validate(Answer.RequestFor(""), ContractVersion.Current, Answer.WithOccurrence(0, 1));

        Assert.Equal(ValidationProblem.OffsetOutsideText, validation.Problem);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("")]
    [InlineData(" 1")]
    [InlineData("1.0")]
    public void UnknownContractVersionIsRefused(string declared)
    {
        var validation = ResponseValidator.Validate(Answer.RequestFor(Answer.Text), declared, Answer.WithOccurrence(3, 11));

        Assert.Equal(ValidationProblem.UnknownContractVersion, validation.Problem);
    }

    [Fact]
    public void NegativeStartIsRefused()
    {
        var validation = Validate(Answer.WithOccurrence(-1, 3));

        Assert.Equal(ValidationProblem.OffsetOutsideText, validation.Problem);
    }

    [Theory]
    [InlineData(3, Answer.TextCodePoints + 1)]
    [InlineData(3, Answer.TextCodePoints + 2)]
    [InlineData(Answer.TextCodePoints + 1, Answer.TextCodePoints + 1)]
    public void OffsetPastTheTextInCodePointsIsRefusedEvenWithinItsUtf16Length(int start, int end)
    {
        var validation = Validate(Answer.WithOccurrence(start, end));

        Assert.Equal(ValidationProblem.OffsetOutsideText, validation.Problem);
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(Answer.TextCodePoints + 5, Answer.TextCodePoints + 2)]
    public void OccurrenceEndingBeforeItStartsIsRefusedAsSuchEvenOutsideTheText(int start, int end)
    {
        var validation = Validate(Answer.WithOccurrence(start, end));

        Assert.Equal(ValidationProblem.EndBeforeStart, validation.Problem);
    }

    [Fact]
    public void OneOccurrenceOutsideTheTextRefusesTheWholeAnswer()
    {
        var validation = Validate(Answer.WithOccurrences(3, 11, 3, 12));

        Assert.Null(validation.Response);
    }

    [Fact]
    public void ContractVersionIsCheckedBeforeTheBody()
    {
        var validation = ResponseValidator.Validate(Answer.RequestFor(Answer.Text), "2", Answer.Body("{"));

        Assert.Equal(ValidationProblem.UnknownContractVersion, validation.Problem);
    }

    [Fact]
    public void MissingRequestIsADefectOfTheCaller()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => ResponseValidator.Validate(null!, ContractVersion.Current, Answer.WithOccurrence(3, 11)));

        Assert.Equal("request", refusal.ParamName);
    }

    [Fact]
    public void MissingDeclaredVersionIsADefectOfTheCaller()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => ResponseValidator.Validate(Answer.RequestFor(Answer.Text), null!, Answer.WithOccurrence(3, 11)));

        Assert.Equal("declaredContractVersion", refusal.ParamName);
    }

    [Fact]
    public void RefusalDescriptionCarriesNeitherTheTextNorTheOffset()
    {
        var request = Answer.RequestFor("falei com ZQX-MARKER-2471");

        var description = ResponseValidator.Validate(request, ContractVersion.Current, Answer.WithOccurrence(4817, 4863)).ToString();

        Assert.Equal("ResponseValidation { Response = , Problem = OffsetOutsideText }", description);
    }

    [Fact]
    public void AcceptanceDescriptionCarriesNeitherTheTextNorTheOffsets()
    {
        var request = Answer.RequestFor("falei com ZQX-MARKER-2471 ontem");

        var description = ResponseValidator.Validate(request, ContractVersion.Current, Answer.WithOccurrence(10, 25)).ToString();

        Assert.Equal("ResponseValidation { Response = AnalysisResponse { Occurrences = 1, ModelVersion = zqx-model-1 }, Problem =  }", description);
    }
}
