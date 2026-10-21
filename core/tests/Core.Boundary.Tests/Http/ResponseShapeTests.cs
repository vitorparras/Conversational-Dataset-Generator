using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>An answer outside the shape of the contract is refused whole, and the refusal names what was wrong with it.</summary>
public sealed class ResponseShapeTests
{
    private static ResponseValidation Validate(byte[] body) =>
        ResponseValidator.Validate(Answer.RequestFor(Answer.Text), ContractVersion.Current, body);

    [Fact]
    public void TruncatedAnswerIsRefusedAsMalformed()
    {
        var whole = ContractFixture.Text("accepted/response-astral.json");

        var validation = Validate(Answer.Body(whole[..(whole.Length / 2)]));

        Assert.Equal(ValidationProblem.MalformedJson, validation.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("occurrences")]
    [InlineData("""{"occurrences":[],"model_version":null} {}""")]
    [InlineData(/*lang=json*/ """{"occurrences":[],"model_version":null,}""")]
    [InlineData(/*lang=json*/ """{"occurrences":[],/* none */"model_version":null}""")]
    public void BodyThatIsNotOneJsonValueIsRefusedAsMalformed(string body)
    {
        var validation = Validate(Answer.Body(body));

        Assert.Equal(ValidationProblem.MalformedJson, validation.Problem);
    }

    [Fact]
    public void BodyThatIsNotUtf8IsRefusedAsMalformed()
    {
        var body = Answer.Body(/*lang=json,strict*/ """{"occurrences":[],"model_version":"zqx"}""");
        body[^4] = 0xFF;

        var validation = Validate(body);

        Assert.Equal(ValidationProblem.MalformedJson, validation.Problem);
    }

    [Fact]
    public void RepeatedFieldIsRefusedAsMalformedInsteadOfKeepingTheLastOne()
    {
        var body = Answer.Body(/*lang=json,strict*/ """{"occurrences":[{"start":3,"end":11,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}],"occurrences":[],"model_version":null}""");

        var validation = Validate(body);

        Assert.Equal(ValidationProblem.MalformedJson, validation.Problem);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[],"model_version":null,"confidence":0.9}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[],"model_version":null,"contract_version":"1"}""")]
    [InlineData(/*lang=json,strict*/ """{"Occurrences":[],"model_version":null}""")]
    public void FieldTheContractDoesNotDeclareIsRefused(string body)
    {
        var validation = Validate(Answer.Body(body));

        Assert.Equal(ValidationProblem.UnknownField, validation.Problem);
    }

    [Fact]
    public void ConfidenceInAnOccurrenceIsRefusedAsUnknownField()
    {
        var validation = Validate(Answer.Fixture("refused/response-with-confidence.json"));

        Assert.Equal(ValidationProblem.UnknownField, validation.Problem);
    }

    [Theory]
    [InlineData("refused/response-without-syntactic-role.json")]
    [InlineData("refused/response-without-model-version.json")]
    public void AnswerLackingARequiredFieldIsRefused(string fixture)
    {
        var validation = Validate(Answer.Fixture(fixture));

        Assert.Equal(ValidationProblem.MissingField, validation.Problem);
    }

    [Theory]
    [InlineData("refused/response-with-text-offset.json")]
    [InlineData("refused/response-with-null-kind.json")]
    public void FixtureWithAValueOfTheWrongTypeIsRefused(string fixture)
    {
        var validation = Validate(Answer.Fixture(fixture));

        Assert.Equal(ValidationProblem.WrongValue, validation.Problem);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"occurrences\"")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":null,"model_version":null}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":{},"model_version":null}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[null],"model_version":null}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[[]],"model_version":null}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[],"model_version":7}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[{"start":3.0,"end":11,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}],"model_version":null}""")]
    [InlineData(/*lang=json,strict*/ """{"occurrences":[{"start":3,"end":99999999999,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}],"model_version":null}""")]
    public void ValueOfTheWrongTypeIsRefused(string body)
    {
        var validation = Validate(Answer.Body(body));

        Assert.Equal(ValidationProblem.WrongValue, validation.Problem);
    }

    [Fact]
    public void RefusedAnswerKeepsNoPartOfIt()
    {
        var validation = Validate(Answer.Fixture("refused/response-with-confidence.json"));

        Assert.Null(validation.Response);
    }
}
