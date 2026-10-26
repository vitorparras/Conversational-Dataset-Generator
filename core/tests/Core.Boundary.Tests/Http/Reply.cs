namespace Core.Boundary.Tests.Http;

/// <summary>What the fake service answers on one route: a status and a body, after a delay, with a redirect when given one.</summary>
internal sealed record Reply(int Status, string Body)
{
    public const string Secret = "ZQX-MARKER-SECRET-0025";

    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    public string? Location { get; init; }

    public static Reply Version(string contractVersion) =>
        new(200, $$"""{"service_version":"0.1.0","contract_version":"{{contractVersion}}","model_version":null}""");

    /// <summary>A version answer whose model version is the text given, written as JSON null when it is null.</summary>
    public static Reply VersionOfModel(string? modelVersion)
    {
        var model = modelVersion is null ? "null" : $"\"{modelVersion}\"";
        return new(200, $$"""{"service_version":"0.1.0","contract_version":"1","model_version":{{model}}}""");
    }

    /// <summary>A version answer with the three fields given, each written as JSON null when it is null.</summary>
    public static Reply Versions(string? service, string? contract, string? model) =>
        new(200, $$"""{"service_version":{{Json(service)}},"contract_version":{{Json(contract)}},"model_version":{{Json(model)}}}""");

    public static Reply Ok(string body) => new(200, body);

    public static Reply Fixture(string relativePath) => new(200, ContractFixture.Text(relativePath));

    public static Reply Unauthorized => new(401, /*lang=json,strict*/ """{"error":"unauthorized"}""");

    private static string Json(string? text) => text is null ? "null" : $"\"{text}\"";
}
