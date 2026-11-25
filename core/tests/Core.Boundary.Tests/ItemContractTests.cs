using System.Reflection;
using System.Text.Json;
using Core.Boundary.Contracts;
using Core.Platform.Logging;

namespace Core.Boundary.Tests;

/// <summary>The request and the answer for a whole item are read and written back unchanged, and their description shows no text nor name.</summary>
public sealed class ItemContractTests
{
    private const string Marker = "ZQX-MARKER-0908";

    [Fact]
    public void ItemRequestFixtureIsWrittenBackUnchanged()
    {
        var request = ContractFixture.ReadItemRequest("accepted/item-request.json");

        var written = JsonSerializer.Serialize(request, ContractJson.Options);

        Assert.True(ContractFixture.SameJson(ContractFixture.Text("accepted/item-request.json"), written), written);
    }

    [Fact]
    public void ItemResponseFixtureIsWrittenBackUnchanged()
    {
        var response = ContractFixture.ReadItemResponse("accepted/item-response.json");

        var written = JsonSerializer.Serialize(response, ContractJson.Options);

        Assert.True(ContractFixture.SameJson(ContractFixture.Text("accepted/item-response.json"), written), written);
    }

    [Fact]
    public void ItemRequestFixtureSpeaksTheVersionOfTheCore()
    {
        var request = ContractFixture.ReadItemRequest("accepted/item-request.json");

        Assert.Equal(ContractVersion.Current, request.ContractVersion);
    }

    [Fact]
    public void TextWithoutAuthorIsWrittenWithANullAuthor()
    {
        var written = JsonSerializer.Serialize(new ItemText("texto", null), ContractJson.Options);

        Assert.True(ContractFixture.SameJson(/*lang=json,strict*/ """{"text":"texto","author":null}""", written), written);
    }

    [Fact]
    public void ItemRequestDescriptionLeavesOutTheTextsAndTheAuthors()
    {
        var request = new ItemAnalysisRequest([new ItemText($"falei com {Marker}", $"{Marker}-A")], "pt", "ZQX-REF-0908", ContractVersion.Current);

        var description = $"{request} {request.Texts[0]}";

        Assert.DoesNotContain(Marker, description, StringComparison.Ordinal);
        Assert.Contains("Texts = 1", description, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorGroupDescriptionLeavesOutTheName()
    {
        var description = new AuthorGroup(Marker, 4).ToString();

        Assert.Equal("AuthorGroup { Group = 4 }", description);
    }

    [Fact]
    public void ItemOccurrenceDescriptionLeavesOutTheOffsets()
    {
        var description = new GroupedOccurrence(1, 48213, 48271, "PERSON", "PROPN", "obl", 3).ToString();

        Assert.Equal("GroupedOccurrence { TextIndex = 1, Kind = PERSON, PartOfSpeech = PROPN, SyntacticRole = obl, Group = 3 }", description);
    }

    [Theory]
    [InlineData(nameof(ItemText.Text))]
    [InlineData(nameof(ItemText.Author))]
    public void TextAndAuthorOfAnItemAreClassifiedAsMaterial(string property)
    {
        var classified = typeof(ItemText).GetProperty(property);

        Assert.NotNull(classified?.GetCustomAttribute<MaterialAttribute>());
    }

    [Fact]
    public void NameInAnAuthorGroupIsClassifiedAsMaterial()
    {
        var author = typeof(AuthorGroup).GetProperty(nameof(AuthorGroup.Author));

        Assert.NotNull(author?.GetCustomAttribute<MaterialAttribute>());
    }

    [Fact]
    public void ItemOutcomeIsEitherTheAnalysisOrTheReason()
    {
        var unavailable = ItemRecognitionOutcome.Unavailable(UnavailabilityReason.TimedOut);

        Assert.Equal((null, UnavailabilityReason.TimedOut), (unavailable.Response, unavailable.Reason));
    }

    [Fact]
    public void ItemOutcomeRefusesAReasonOutsideTheEnumeration()
    {
        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => ItemRecognitionOutcome.Unavailable((UnavailabilityReason)99));

        Assert.Equal("reason", defect.ParamName);
        Assert.Contains("The reason is not one of the enumeration.", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemOutcomeWithoutItsAnalysisIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => ItemRecognitionOutcome.Analyzed(null!));

        Assert.Equal("response", defect.ParamName);
    }

    [Theory]
    [InlineData(Marker, "ItemText { TextChars = 5, HasAuthor = True }")]
    [InlineData(null, "ItemText { TextChars = 5, HasAuthor = False }")]
    public void ItemTextDescriptionGivesOnlyItsSizeAndWhetherItHasAnAuthor(string? author, string expected) =>
        Assert.Equal(expected, new ItemText("texto", author).ToString());

    [Fact]
    public void ItemResponseDescriptionGivesTheCountsAndTheModelButNoName()
    {
        var response = new ItemAnalysisResponse([new GroupedOccurrence(0, 0, 5, "PERSON", "PROPN", "obl", 0)], [new AuthorGroup(Marker, 1)], "zqx-model-2");

        Assert.Equal("ItemAnalysisResponse { Occurrences = 1, AuthorGroups = 1, ModelVersion = zqx-model-2 }", response.ToString());
    }
}
