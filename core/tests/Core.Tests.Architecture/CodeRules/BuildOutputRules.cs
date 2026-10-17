using System.Text.RegularExpressions;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What a production build produces for itself, and the suppressors it carries, are only what the SDK and the generators of the core are known to bring.</summary>
internal static partial class BuildOutputRules
{
    /// <summary>The analyzer skips generated code, so only these files may be one. Each is named here with the tool that makes it.</summary>
    private static readonly Regex[] KnownGeneratedFiles =
    [
        GlobalUsings(),
        AssemblyInfo(),
        TargetFramework(),
        WebTopLevelProgram(),
        LoggerMessages(),
    ];

    /// <summary>The suppressors the SDK itself carries: what they suppress is no analyzer of the core.</summary>
    private static readonly (string Type, string Id)[] SdkSuppressors =
    [
        ("Microsoft.Interop.Analyzers.ShapeBreakingDiagnosticSuppressor", "CA1822"),
        ("Microsoft.CodeAnalysis.Razor.Compiler.Analyzers.ComponentParameterNullableWarningSuppressor", "CS8618"),
    ];

    public static IEnumerable<Violation> CheckGeneratedFiles(CompilerInvocation invocation) =>
        invocation.GeneratedFiles
            .Where(file => !Array.Exists(KnownGeneratedFiles, known => known.IsMatch(file.FileName)))
            .Select(file => new Violation(file.Path, 1, "a file the build produced for itself is not one of the known generated files"));

    public static IEnumerable<Violation> CheckSuppressors(CompilerInvocation invocation) =>
        invocation.Suppressors
            .Where(suppressor => !IsSdkSuppressor(suppressor))
            .Select(suppressor => new Violation(
                invocation.ProjectPath, 1, $"{suppressor.TypeName} in {Path.GetFileName(suppressor.AnalyzerPath)} is able to suppress {string.Join(", ", suppressor.SuppressedIds)}"));

    private static bool IsSdkSuppressor(AnalyzerSuppressor suppressor) =>
        suppressor.AnalyzerPath.StartsWith(ToolLocations.DotnetRoot, StringComparison.OrdinalIgnoreCase)
        && suppressor.SuppressedIds.All(id => Array.Exists(SdkSuppressors, known => known.Type == suppressor.TypeName && known.Id == id));

    [GeneratedRegex(@"^[\w.]+\.GlobalUsings\.g\.cs$")]
    private static partial Regex GlobalUsings();

    [GeneratedRegex(@"^[\w.]+\.AssemblyInfo\.cs$")]
    private static partial Regex AssemblyInfo();

    [GeneratedRegex(@"^\.NETCoreApp,Version=v\d+\.\d+\.AssemblyAttributes\.cs$")]
    private static partial Regex TargetFramework();

    [GeneratedRegex(@"^PublicTopLevelProgram\.Generated\.g\.cs$")]
    private static partial Regex WebTopLevelProgram();

    [GeneratedRegex(@"^Logging\.g\.cs$")]
    private static partial Regex LoggerMessages();
}
