using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The configs a production project is given are the root one and the SDK's, and none of them lowers a diagnostic, in bulk or by id, or marks a source as generated.</summary>
internal static partial class AnalyzerConfigRules
{
    private const string GeneratedCodeKey = "generated_code";
    private const string GlobalConfigExtension = ".globalconfig";

    /// <summary>Diagnostics the SDK's analysis level switches off, each named with the reason.</summary>
    private static readonly string[] SdkSwitchedOff =
    [
        "CA1516", // the "all" level file of the SDK carries it switched off
    ];

    public static IEnumerable<Violation> CheckConfigFiles(CompilerInvocation invocation, RootConfig root) =>
        invocation.AnalyzerConfigPaths
            .Where(path => !IsRootConfig(path, root) && !IsSdkGlobalConfig(path) && !IsBuildEditorConfig(path, invocation))
            .Select(path => new Violation(path, 1, "a config the compiler is given is neither the root config nor one the SDK brings"));

    public static IEnumerable<Violation> CheckEffectiveSeverities(CompilerInvocation invocation, RootConfig root)
    {
        var exclusions = RootExclusions.Read(root);
        return invocation.Sources
            .SelectMany(source => JudgeSource(invocation, exclusions, source))
            .GroupBy(violation => violation.Message)
            .Select(group => group.First());
    }

    private static IEnumerable<Violation> JudgeSource(CompilerInvocation invocation, RootExclusions exclusions, string source)
    {
        var section = invocation.ConfigSet.GetOptionsForSourcePath(source);
        var global = invocation.ConfigSet.GlobalConfigOptions;
        var lowered = section.TreeOptions.Concat(global.TreeOptions);
        return BulkSeverities(source, section.AnalyzerOptions).Concat(BulkSeverities(source, global.AnalyzerOptions))
            .Concat(GeneratedCodeMarks(source, section.AnalyzerOptions)).Concat(GeneratedCodeMarks(source, global.AnalyzerOptions))
            .Concat(lowered.Where(entry => IsLowering(entry.Value) && !IsAllowedOff(entry.Key.ToUpperInvariant(), exclusions, source))
                .Select(entry => new Violation(source, 1, $"{entry.Key.ToUpperInvariant()} reaches the compiler as {entry.Value}; only a rule the root config switches off with a reason may")));
    }

    private static IEnumerable<Violation> BulkSeverities(string source, IReadOnlyDictionary<string, string> options) =>
        options.Where(pair => BulkKey().IsMatch(pair.Key) && !string.Equals(pair.Value, "error", StringComparison.OrdinalIgnoreCase))
            .Select(pair => new Violation(source, 1, $"{pair.Key} = {pair.Value} sets the severity of a whole set of diagnostics below error"));

    private static IEnumerable<Violation> GeneratedCodeMarks(string source, IReadOnlyDictionary<string, string> options) =>
        options.Where(pair => pair.Key == GeneratedCodeKey)
            .Select(pair => new Violation(source, 1, $"{GeneratedCodeKey} = {pair.Value} decides whether the analyzers treat a source as generated"));

    private static bool IsLowering(ReportDiagnostic severity) =>
        severity is ReportDiagnostic.Suppress or ReportDiagnostic.Hidden or ReportDiagnostic.Info;

    private static bool IsAllowedOff(string id, RootExclusions exclusions, string source) =>
        exclusions.Covers(id, source) || Array.IndexOf(SdkSwitchedOff, id) >= 0;

    private static bool IsRootConfig(string path, RootConfig root) => SamePaths.Are(path, root.Path);

    private static bool IsSdkGlobalConfig(string path) =>
        path.EndsWith(GlobalConfigExtension, StringComparison.OrdinalIgnoreCase)
        && path.StartsWith(ToolLocations.DotnetRoot, StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildEditorConfig(string path, CompilerInvocation invocation) =>
        SamePaths.Are(Path.GetDirectoryName(path) ?? string.Empty, invocation.IntermediateDirectory)
        && Path.GetFileName(path) == Path.GetFileNameWithoutExtension(invocation.ProjectPath) + ".GeneratedMSBuildEditorConfig.editorconfig";


    [GeneratedRegex(@"^dotnet_analyzer_diagnostic\.(category-[^.]+\.)?severity$")]
    private static partial Regex BulkKey();
}
