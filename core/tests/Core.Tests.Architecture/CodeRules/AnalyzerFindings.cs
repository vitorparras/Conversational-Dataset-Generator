namespace Core.Tests.Architecture.CodeRules;

/// <summary>What loading and running the analyzers of a build showed: the suppressors among them, the sources their generators emit, and everything that stopped one from being inspected.</summary>
internal sealed record AnalyzerFindings(
    IReadOnlyList<AnalyzerSuppressor> Suppressors,
    IReadOnlyList<SourceFile> GeneratedSources,
    IReadOnlyList<string> Failures);
