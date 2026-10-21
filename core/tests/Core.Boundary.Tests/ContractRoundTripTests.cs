using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>Every accepted fixture is read and written back to the same JSON, field names included.</summary>
public sealed class ContractRoundTripTests
{
    [Fact]
    public void RequestFixtureIsWrittenBackUnchanged()
    {
        var request = ContractFixture.ReadRequest("accepted/request.json");

        var written = JsonSerializer.Serialize(request, ContractJson.Options);

        Assert.True(ContractFixture.SameJson(ContractFixture.Text("accepted/request.json"), written), written);
    }

    [Theory]
    [InlineData("accepted/response-empty.json")]
    [InlineData("accepted/response-astral.json")]
    public void ResponseFixtureIsWrittenBackUnchanged(string fixture)
    {
        var response = ContractFixture.ReadResponse(fixture);

        var written = JsonSerializer.Serialize(response, ContractJson.Options);

        Assert.True(ContractFixture.SameJson(ContractFixture.Text(fixture), written), written);
    }

    [Fact]
    public void RequestFixtureSpeaksTheVersionOfTheCore()
    {
        var request = ContractFixture.ReadRequest("accepted/request.json");

        Assert.Equal(ContractVersion.Current, request.ContractVersion);
    }

    [Fact]
    public void EmptyResponseIsAnAnalysisWithNothingFoundAndNoModel()
    {
        var response = ContractFixture.ReadResponse("accepted/response-empty.json");

        Assert.Empty(response.Occurrences);
        Assert.Null(response.ModelVersion);
    }

    [Fact]
    public void OccurrenceCarriesItsFiveFieldsAndTheResponseItsModel()
    {
        var response = ContractFixture.ReadResponse("accepted/response-astral.json");

        Assert.Equal(new Occurrence(12, 20, "PERSON", "PROPN", "obl"), response.Occurrences[0]);
        Assert.Equal("zqx-model-1", response.ModelVersion);
    }
}
