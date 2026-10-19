using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>A message outside the contract is refused whole: an unknown field, a field name in another case, a confidence, a missing or null field, a text offset.</summary>
public sealed class ContractRefusalTests
{
    [Fact]
    public void RequestWithAFieldThatLocatesTheMaterialIsRefused()
    {
        var json = ContractFixture.Text("refused/request-with-item-id.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisRequest>(json, ContractJson.Options));

        Assert.Contains("item_id", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldNameInAnotherCaseIsRefused()
    {
        var json = ContractFixture.Text("refused/request-with-uppercase-field.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisRequest>(json, ContractJson.Options));

        Assert.Contains("TEXT", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("refused/response-with-confidence.json", "confidence")]
    [InlineData("refused/response-without-syntactic-role.json", "syntactic_role")]
    [InlineData("refused/response-with-null-kind.json", "kind")]
    [InlineData("refused/response-with-text-offset.json", "start")]
    [InlineData("refused/response-without-model-version.json", "model_version")]
    public void ResponseOutsideTheContractIsRefused(string fixture, string field)
    {
        var json = ContractFixture.Text(fixture);

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisResponse>(json, ContractJson.Options));

        Assert.Contains(field, refusal.Message, StringComparison.Ordinal);
    }
}
