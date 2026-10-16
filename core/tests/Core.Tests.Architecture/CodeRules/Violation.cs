namespace Core.Tests.Architecture.CodeRules;

/// <summary>One breach of a code rule, anchored to a file and a line.</summary>
internal sealed record Violation(string File, int Line, string Message)
{
    public override string ToString() => $"{Shown(File)}:{Line} {Message}";

    private static string Shown(string path)
    {
        var relative = Path.IsPathRooted(path) ? Path.GetRelativePath(SourceTree.CoreRoot, path) : path;
        return relative.StartsWith("..", StringComparison.Ordinal) ? path.Replace(Path.DirectorySeparatorChar, '/') : relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
