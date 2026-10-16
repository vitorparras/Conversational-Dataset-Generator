using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One rule of the analyzer as it ships: its id, the severity it reports with and whether it runs by default.</summary>
internal sealed record SonarRule(string Id, DiagnosticSeverity DefaultSeverity, bool EnabledByDefault)
{
    /// <summary>A rule the default profile leaves off, or reports below a warning, never fails a build unless a config says so.</summary>
    public bool NeedsExplicitSeverity => !EnabledByDefault || DefaultSeverity < DiagnosticSeverity.Warning;
}
