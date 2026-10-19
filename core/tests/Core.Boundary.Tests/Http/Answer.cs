using System.Text;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests.Http;

/// <summary>Requests and answer bodies for the validator, written as the language service would send them.</summary>
internal static class Answer
{
    /// <summary>Two emoji before the name: 11 code points, 13 UTF-16 units.</summary>
    public const string Text = "\U0001F642\U0001F642 PESSOA_1";

    public const int TextCodePoints = 11;

    public static AnalysisRequest RequestFor(string text) => new(text, "pt", "ZQX-REF-0024", ContractVersion.Current);

    public static byte[] Body(string json) => Encoding.UTF8.GetBytes(json);

    public static byte[] Fixture(string relativePath) => Body(ContractFixture.Text(relativePath));

    public static byte[] WithOccurrence(int start, int end) =>
        Body($$"""{"occurrences":[{{OccurrenceJson(start, end)}}],"model_version":"zqx-model-1"}""");

    public static byte[] WithOccurrences(int firstStart, int firstEnd, int secondStart, int secondEnd) =>
        Body($$"""{"occurrences":[{{OccurrenceJson(firstStart, firstEnd)}},{{OccurrenceJson(secondStart, secondEnd)}}],"model_version":null}""");

    private static string OccurrenceJson(int start, int end) =>
        $$"""{"start":{{start}},"end":{{end}},"kind":"PERSON","part_of_speech":"PROPN","syntactic_role":"obl"}""";
}
