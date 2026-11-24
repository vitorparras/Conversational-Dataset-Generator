using System.Text;
using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// An answer for a whole item is used only when every occurrence names a text of the item and falls inside that text,
/// and every author named in the request has exactly one group; one problem refuses the whole answer.
/// </summary>
public sealed class ItemResponseValidatorTests
{
    /// <summary>The three texts of the request fixture measure 21, 28 and 15 code points.</summary>
    private static readonly ItemAnalysisRequest Request = ContractFixture.ReadItemRequest("accepted/item-request.json");

    private const string BothAuthors = /*lang=json,strict*/ """[{"author":"ZQX-AUTOR-A","group":7},{"author":"ZQX-AUTOR-B","group":8}]""";

    [Fact]
    public void EveryOccurrenceInsideItsTextIsAcceptedWhole()
    {
        var validation = Validate(Encoding.UTF8.GetBytes(ContractFixture.Text("accepted/item-response.json")));

        Assert.Equal(ContractFixture.ReadItemResponse("accepted/item-response.json").Occurrences, validation.Response?.Occurrences);
    }

    [Theory]
    [InlineData(0, 0, 21)]
    [InlineData(1, 28, 28)]
    [InlineData(2, 0, 15)]
    public void OccurrenceUpToTheEndOfItsOwnTextIsAccepted(int textIndex, int start, int end)
    {
        var validation = Validate(WithOccurrence(textIndex, start, end));

        Assert.Equal(new GroupedOccurrence(textIndex, start, end, "PERSON", "PROPN", "obl", 7), Assert.Single(validation.Response!.Occurrences));
    }

    [Theory]
    [InlineData(-1, 0, 1, nameof(ValidationProblem.TextOutsideItem))]
    [InlineData(3, 0, 1, nameof(ValidationProblem.TextOutsideItem))]
    [InlineData(0, 2, 28, nameof(ValidationProblem.OffsetOutsideText))]
    [InlineData(2, 0, 16, nameof(ValidationProblem.OffsetOutsideText))]
    [InlineData(1, -1, 3, nameof(ValidationProblem.OffsetOutsideText))]
    [InlineData(1, 5, 4, nameof(ValidationProblem.EndBeforeStart))]
    public void OccurrenceOutsideItsTextIsRefused(int textIndex, int start, int end, string expected)
    {
        var validation = Validate(WithOccurrence(textIndex, start, end));

        Assert.Equal((null, expected), (validation.Response, validation.Problem?.ToString()));
    }

    [Fact]
    public void OccurrenceWithoutGroupIsRefusedByTheNewContract()
    {
        var body = $$"""
            {"occurrences":[{"text_index":0,"start":2,"end":10,"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"nsubj"}],
             "author_groups":{{BothAuthors}},"model_version":null}
            """;

        var validation = Validate(Encoding.UTF8.GetBytes(body));

        Assert.Equal((null, ValidationProblem.MissingField), (validation.Response, validation.Problem));
    }

    [Theory]
    [InlineData("", nameof(ValidationProblem.MissingField))]
    [InlineData(""","author_groups":[{"author":"ZQX-AUTOR-A"},{"author":"ZQX-AUTOR-B","group":1}]""", nameof(ValidationProblem.MissingField))]
    [InlineData(""","author_groups":[{"author":"ZQX-AUTOR-A","group":null},{"author":"ZQX-AUTOR-B","group":1}]""", nameof(ValidationProblem.WrongValue))]
    [InlineData(""","author_groups":[{"author":"ZQX-AUTOR-A","group":0}]""", nameof(ValidationProblem.AuthorWithoutOneGroup))]
    [InlineData(""","author_groups":[]""", nameof(ValidationProblem.AuthorWithoutOneGroup))]
    public void ResponseWithoutGroupsIsRefusedByTheNewContract(string authorGroups, string expected)
    {
        var validation = Validate(Encoding.UTF8.GetBytes($$"""{"occurrences":[]{{authorGroups}},"model_version":null}"""));

        Assert.Equal((null, expected), (validation.Response, validation.Problem?.ToString()));
    }

    [Fact]
    public void AnswerInTheShapeOfASingleTextIsRefused()
    {
        var validation = Validate(Encoding.UTF8.GetBytes(ContractFixture.Text("refused/item-response-without-groups.json")));

        Assert.Equal(ValidationProblem.MissingField, validation.Problem);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """[{"author":"ZQX-AUTOR-A","group":0},{"author":"ZQX-AUTOR-A","group":0},{"author":"ZQX-AUTOR-B","group":1}]""")]
    [InlineData(/*lang=json,strict*/ """[{"author":"ZQX-AUTOR-A","group":0},{"author":"ZQX-AUTOR-B","group":1},{"author":"ZQX-AUTOR-C","group":2}]""")]
    [InlineData(/*lang=json,strict*/ """[{"author":"ZQX-AUTOR-A","group":0},{"author":"zqx-autor-b","group":1}]""")]
    public void GroupForANameTheRequestDidNotCarryOnceIsRefused(string authorGroups)
    {
        var validation = Validate(Encoding.UTF8.GetBytes($$"""{"occurrences":[],"author_groups":{{authorGroups}},"model_version":null}"""));

        Assert.Equal(ValidationProblem.AuthorWithoutOneGroup, validation.Problem);
    }

    [Fact]
    public void ItemWithoutAuthorsNeedsNoAuthorGroup()
    {
        var request = new ItemAnalysisRequest([new ItemText("texto", null)], "pt", "ZQX-REF-0908", ContractVersion.Current);

        var validation = ResponseValidator.ValidateItem(request, ContractVersion.Current, /*lang=json,strict*/ """{"occurrences":[],"author_groups":[],"model_version":null}"""u8.ToArray());

        Assert.Empty(validation.Response!.AuthorGroups);
    }

    [Fact]
    public void KindOutsideTheListIsRefusedInAnItem()
    {
        var body = $$"""
            {"occurrences":[{"text_index":0,"start":0,"end":1,"kind":"person","part_of_speech":"PROPN","syntactic_role":"obl","group":0}],
             "author_groups":{{BothAuthors}},"model_version":null}
            """;

        var validation = Validate(Encoding.UTF8.GetBytes(body));

        Assert.Equal(ValidationProblem.KindOutsideVocabulary, validation.Problem);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("")]
    public void UnknownContractVersionIsRefusedForAnItem(string declared)
    {
        var validation = ResponseValidator.ValidateItem(Request, declared, WithOccurrence(0, 0, 1));

        Assert.Equal(ValidationProblem.UnknownContractVersion, validation.Problem);
    }

    [Fact]
    public void BodyThatIsNotJsonIsRefusedForAnItem()
    {
        var validation = Validate("{"u8.ToArray());

        Assert.Equal(ValidationProblem.MalformedJson, validation.Problem);
    }

    [Fact]
    public void NullRequestIsADefect()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => ResponseValidator.ValidateItem(null!, ContractVersion.Current, WithOccurrence(0, 0, 1)));

        Assert.Equal("request", refusal.ParamName);
    }

    [Fact]
    public void NullDeclaredVersionIsADefect()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => ResponseValidator.ValidateItem(Request, null!, WithOccurrence(0, 0, 1)));

        Assert.Equal("declaredContractVersion", refusal.ParamName);
    }

    private static ResponseValidation<ItemAnalysisResponse> Validate(byte[] body) => ResponseValidator.ValidateItem(Request, ContractVersion.Current, body);

    private static byte[] WithOccurrence(int textIndex, int start, int end) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {"occurrences":[{"text_index":{{textIndex}},"start":{{start}},"end":{{end}},"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl","group":7}],
             "author_groups":{{BothAuthors}},"model_version":"zqx-model-2"}
            """);
}
