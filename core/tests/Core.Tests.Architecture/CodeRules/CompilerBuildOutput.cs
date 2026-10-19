namespace Core.Tests.Architecture.CodeRules;

/// <summary>What a reading build left behind: the compiler's arguments, where its own outputs went and the folders analyzers may come from.</summary>
internal sealed record CompilerBuildOutput(
    string ProjectPath,
    IReadOnlyList<string> Arguments,
    string ArtifactsDirectory,
    IReadOnlyList<string> AnalyzerRoots,
    string IntermediateDirectory = "");
