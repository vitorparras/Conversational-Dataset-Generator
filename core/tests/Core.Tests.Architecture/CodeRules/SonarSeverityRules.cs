using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>For every source the compiler is given, each Sonar rule reaches it as an error; a rule is switched off only where the root config says why.</summary>
internal static class SonarSeverityRules
{
    private const string AnalyzerFile = "SonarAnalyzer.CSharp.dll";

    private static readonly string[] PlatformAnalyzers =
    [
        "Microsoft.CodeAnalysis.NetAnalyzers.dll",
        "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
        "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll",
    ];

    public static IEnumerable<Violation> Check(IEnumerable<SonarRule> rules, CompilerInvocation invocation, RootConfig root)
    {
        var exclusions = RootExclusions.Read(root);
        var ruleList = rules.ToList();
        return invocation.Sources
            .SelectMany(source => Judge(ruleList, invocation, exclusions, source))
            .GroupBy(violation => violation.Message)
            .Select(group => group.First());
    }

    /// <summary>The analyzer the rules were read from is the one the compiler loads.</summary>
    public static IEnumerable<Violation> CheckAnalyzerLoaded(CompilerInvocation invocation, string analyzerPath) =>
        invocation.Analyzers.Any(path => SamePaths.Are(path, analyzerPath))
            ? []
            : [new Violation(invocation.ProjectPath, 1, $"{AnalyzerFile} is not given to the compiler from {analyzerPath}")];

    /// <summary>The analyzers of the platform come from the SDK, so that its rules are never silently missing.</summary>
    public static IEnumerable<Violation> CheckPlatformAnalyzersLoaded(CompilerInvocation invocation) =>
        PlatformAnalyzers
            .Where(name => !invocation.Analyzers.Any(path => IsFromSdk(path, name)))
            .Select(name => new Violation(invocation.ProjectPath, 1, $"{name} is not given to the compiler from the SDK"));

    /// <summary>The rule set a build points the compiler at turns rules off outside the config files.</summary>
    public static IEnumerable<Violation> CheckNoRuleSet(CompilerInvocation invocation) =>
        invocation.RuleSetPath is { } ruleSet
            ? [new Violation(invocation.ProjectPath, 1, $"the compiler is given a rule set: {ruleSet}")]
            : [];

    private static IEnumerable<Violation> Judge(IEnumerable<SonarRule> rules, CompilerInvocation invocation, RootExclusions exclusions, string source)
    {
        var severities = new SeveritiesAt(invocation, source);
        return rules
            .Select(rule => (Rule: rule, Severity: severities.Of(rule)))
            .Where(entry => !IsAccepted(entry.Rule, entry.Severity, exclusions, source))
            .Select(entry => new Violation(
                source, 1, $"{entry.Rule.Id} reaches the compiler as {entry.Severity}; it must be error, or none with a reason in the root config"));
    }

    private static bool IsFromSdk(string path, string name) =>
        Path.GetFileName(path) == name && path.StartsWith(ToolLocations.DotnetRoot, StringComparison.OrdinalIgnoreCase);

    private static bool IsAccepted(SonarRule rule, ReportDiagnostic severity, RootExclusions exclusions, string source) =>
        severity == ReportDiagnostic.Error
        || (severity == ReportDiagnostic.Suppress && exclusions.Covers(rule.Id, source))
        || IsOnByDefault(rule, severity);

    private static bool IsOnByDefault(SonarRule rule, ReportDiagnostic severity) =>
        !rule.NeedsExplicitSeverity && severity is ReportDiagnostic.Default or ReportDiagnostic.Warn;


    /// <summary>What the section for a source says, over what the global configs say, over the command line, over the analyzer's own default.</summary>
    private sealed class SeveritiesAt(CompilerInvocation invocation, string source)
    {
        private readonly ImmutableDictionary<string, ReportDiagnostic> section = invocation.ConfigSet.GetOptionsForSourcePath(source).TreeOptions;
        private readonly ImmutableDictionary<string, ReportDiagnostic> global = invocation.ConfigSet.GlobalConfigOptions.TreeOptions;

        public ReportDiagnostic Of(SonarRule rule)
        {
            if (section.TryGetValue(rule.Id, out var fromSection))
            {
                return fromSection;
            }

            return global.TryGetValue(rule.Id, out var fromGlobal)
                ? fromGlobal
                : invocation.SpecificDiagnosticOptions.GetValueOrDefault(rule.Id, ReportDiagnostic.Default);
        }
    }
}
