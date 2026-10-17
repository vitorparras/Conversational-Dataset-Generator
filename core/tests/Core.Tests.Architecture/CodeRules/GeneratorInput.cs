using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the generators of a build are handed: the compilation, the additional files, the merged configs and the parse options.</summary>
internal sealed record GeneratorInput(
    CSharpCompilation Compilation,
    IReadOnlyList<string> AdditionalFiles,
    AnalyzerConfigSet Configs,
    CSharpParseOptions ParseOptions);
