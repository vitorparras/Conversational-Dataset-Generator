namespace Core.Tests.Architecture.CodeRules;

/// <summary>A production file with the canary appended: its path below the source root, its new text and the line of each banned route.</summary>
internal sealed record CanarySite(string RelativePath, string Text, IReadOnlyList<int> RouteLines);
