namespace Core.Tests.Architecture.CodeRules;

/// <summary>One breach of a code rule, anchored to a file and a line.</summary>
internal sealed record Violation(string File, int Line, string Message)
{
    public override string ToString() => $"{File}:{Line} {Message}";
}
