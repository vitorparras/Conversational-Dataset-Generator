namespace Core.Tests.Architecture.CodeRules;

/// <summary>A <c>DiagnosticSuppressor</c> found among the analyzers of a build, with the diagnostic ids it is able to suppress.</summary>
internal sealed record AnalyzerSuppressor(string AnalyzerPath, string TypeName, IReadOnlyList<string> SuppressedIds)
{
    public bool Suppresses(string diagnosticId) =>
        SuppressedIds.Any(id => id.Equals(diagnosticId, StringComparison.OrdinalIgnoreCase));
}
