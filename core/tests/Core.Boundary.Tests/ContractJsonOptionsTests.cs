using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>The options of the contract refuse what is outside it, each one by itself, both as published and as built anew.</summary>
public sealed class ContractJsonOptionsTests
{
    public static TheoryData<string> Builds => ["published", "rebuilt"];

    [Theory]
    [MemberData(nameof(Builds))]
    public void UnknownFieldIsRefusedNamingIt(string build)
    {
        var json = ContractFixture.Text("refused/request-with-item-id.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisRequest>(json, Options(build)));

        Assert.Contains("item_id", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void MissingFieldIsRefusedNamingIt(string build)
    {
        var json = ContractFixture.Text("refused/response-without-syntactic-role.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisResponse>(json, Options(build)));

        Assert.Contains("syntactic_role", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void NullWhereTheTypeAllowsNoneIsRefusedNamingTheField(string build)
    {
        var json = ContractFixture.Text("refused/response-with-null-kind.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisResponse>(json, Options(build)));

        Assert.Contains("kind", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void RepeatedFieldIsRefusedNamingIt(string build)
    {
        var json = ContractFixture.Text("refused/response-with-duplicate-field.json");

        var refusal = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AnalysisResponse>(json, Options(build)));

        Assert.Contains("start", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void FieldsAreWrittenAndReadInSnakeCase(string build)
    {
        var request = new AnalysisRequest("texto", "pt", "ZQX-REF-0002", "1");

        var written = JsonSerializer.Serialize(request, Options(build));

        Assert.True(ContractFixture.SameJson("""{"text":"texto","language":"pt","reference":"ZQX-REF-0002","contract_version":"1"}""", written), written);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void OptionsAreSealedAgainstChangeAndStillKnowHowToSerializeTheContract(string build)
    {
        var options = Options(build);

        Assert.True(options.IsReadOnly);
        Assert.Equal(
            """{"occurrences":[],"model_version":null}""",
            JsonSerializer.Serialize(new AnalysisResponse([], null), options));
    }

    private static JsonSerializerOptions Options(string build) =>
        build == "published" ? ContractJson.Options : RebuiltContractJson.Options();
}
