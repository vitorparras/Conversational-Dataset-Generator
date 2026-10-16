namespace Core.Tests.Architecture.CodeRules;

/// <summary>A canary planted for one production source: that source, where the canary sits as the build prints it, and the line of each banned route.</summary>
internal sealed record CanarySite(string Source, string CanaryPath, IReadOnlyList<int> RouteLines);
