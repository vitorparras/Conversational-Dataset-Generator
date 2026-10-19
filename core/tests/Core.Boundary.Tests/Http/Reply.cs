namespace Core.Boundary.Tests.Http;

/// <summary>What the fake service answers on one route: a status and a body, after a delay, with a redirect when given one.</summary>
internal sealed record Reply(int Status, string Body)
{
    public const string Secret = "ZQX-MARKER-SECRET-0025";

    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    public string? Location { get; init; }

    public static Reply Version(string contractVersion) =>
        new(200, $$"""{"service_version":"0.1.0","contract_version":"{{contractVersion}}","model_version":null}""");

    public static Reply Ok(string body) => new(200, body);

    public static Reply Fixture(string relativePath) => new(200, ContractFixture.Text(relativePath));

    public static Reply Unauthorized => new(401, /*lang=json,strict*/ """{"error":"unauthorized"}""");
}
