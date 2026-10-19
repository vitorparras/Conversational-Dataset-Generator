using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>The JSON files of core/tests/fixtures/contract, which the language service tests read as well.</summary>
internal static class ContractFixture
{
    private static readonly Lazy<string> Directory = new(Find);

    public static string Text(string relativePath) =>
        File.ReadAllText(Path.Combine(Directory.Value, relativePath));

    public static AnalysisRequest ReadRequest(string relativePath) =>
        JsonSerializer.Deserialize<AnalysisRequest>(Text(relativePath), ContractJson.Options)
            ?? throw new InvalidOperationException($"{relativePath} holds a JSON null.");

    public static AnalysisResponse ReadResponse(string relativePath) =>
        JsonSerializer.Deserialize<AnalysisResponse>(Text(relativePath), ContractJson.Options)
            ?? throw new InvalidOperationException($"{relativePath} holds a JSON null.");

    /// <summary>Whether two JSON texts carry the same values, whatever their spacing, key order or escaping.</summary>
    public static bool SameJson(string expected, string actual) =>
        JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new DirectoryNotFoundException("Core.slnx was not found above the test output."),
            "tests",
            "fixtures",
            "contract");
    }
}
