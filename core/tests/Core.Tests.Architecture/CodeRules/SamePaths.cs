namespace Core.Tests.Architecture.CodeRules;

/// <summary>Two paths name the same place when they match apart from case, a trailing separator and the kind of separator.</summary>
internal static class SamePaths
{
    public static bool Are(string? left, string? right) =>
        !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right)
        && string.Equals(Trimmed(left), Trimmed(right), StringComparison.OrdinalIgnoreCase);

    private static string Trimmed(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
