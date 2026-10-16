using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>For every source the compiler is given, each Sonar rule reaches it as an error; a rule is lowered only where the root config says why.</summary>
internal static partial class SonarSeverityRules
{
    private const string ReasonMark = "#";

    public static IEnumerable<Violation> Check(IEnumerable<SonarRule> rules, CompilerInvocation invocation, string rootConfigText)
    {
        var excluded = Excluded(rootConfigText);
        var ruleList = rules.ToList();
        return invocation.Sources
            .SelectMany(source => Judge(ruleList, invocation, excluded, source))
            .GroupBy(violation => violation.Message)
            .Select(group => group.First());
    }

    /// <summary>The rules switched off in the root config with a reason: one comment line of real words directly above, and nothing else.</summary>
    private static HashSet<string> Excluded(string rootConfigText)
    {
        var lines = new[] { string.Empty, string.Empty }.Concat(rootConfigText.ReplaceLineEndings("\n").Split('\n')).ToList();
        return [.. lines
            .Zip(lines.Skip(1), lines.Skip(2))
            .Select(window => (Above: window.First, Reason: window.Second, Match: Exclusion().Match(window.Third)))
            .Where(window => window.Match.Success && IsReason(window.Above, window.Reason))
            .Select(window => window.Match.Groups["id"].Value)];
    }

    /// <summary>A single comment line of real words: a comment block above it is a header, not a reason.</summary>
    private static bool IsReason(string above, string comment) =>
        Reason().IsMatch(comment) && !above.TrimStart().StartsWith(ReasonMark, StringComparison.Ordinal);

    private static IEnumerable<Violation> Judge(
        IEnumerable<SonarRule> rules, CompilerInvocation invocation, ICollection<string> excluded, string source)
    {
        var severities = new SeveritiesAt(invocation.ConfigSet, source);
        return rules
            .Select(rule => (Rule: rule, Severity: severities.Of(rule)))
            .Where(entry => !IsAccepted(entry.Rule, entry.Severity, excluded))
            .Select(entry => new Violation(
                source, 1, $"{entry.Rule.Id} reaches the compiler as {entry.Severity}; it must be error, or none with a reason in the root config"));
    }

    private static bool IsAccepted(SonarRule rule, ReportDiagnostic severity, ICollection<string> excluded) =>
        severity == ReportDiagnostic.Error
        || (severity == ReportDiagnostic.Suppress && excluded.Contains(rule.Id))
        || IsOnByDefault(rule, severity);

    private static bool IsOnByDefault(SonarRule rule, ReportDiagnostic severity) =>
        !rule.NeedsExplicitSeverity && severity is ReportDiagnostic.Default or ReportDiagnostic.Warn;

    /// <summary>What the section for a source says, over what the global configs say, over the analyzer's own default.</summary>
    private sealed class SeveritiesAt(AnalyzerConfigSet set, string source)
    {
        private readonly ImmutableDictionary<string, ReportDiagnostic> section = set.GetOptionsForSourcePath(source).TreeOptions;
        private readonly ImmutableDictionary<string, ReportDiagnostic> global = set.GlobalConfigOptions.TreeOptions;

        public ReportDiagnostic Of(SonarRule rule) =>
            section.TryGetValue(rule.Id, out var fromSection) ? fromSection : global.GetValueOrDefault(rule.Id, ReportDiagnostic.Default);
    }

    [GeneratedRegex(@"^dotnet_diagnostic\.(?<id>S\d+)\.severity = none$")]
    private static partial Regex Exclusion();

    [GeneratedRegex(@"^#(\s+\S+){3,}\s*$")]
    private static partial Regex Reason();
}
