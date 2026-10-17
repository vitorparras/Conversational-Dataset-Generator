using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One C# file as the rules see it: its path, its text and its syntax tree.</summary>
internal sealed class SourceFile
{
    private SourceFile(string path, string text)
    {
        Path = path;
        Text = text;
        Tree = CSharpSyntaxTree.ParseText(text, path: path);
    }

    public string Path { get; }

    public string Text { get; }

    public SyntaxTree Tree { get; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public SyntaxNode Root => Tree.GetRoot();

    /// <summary>Lines as an editor counts them: a final line break does not open an empty line.</summary>
    public int LineCount => Text.Count(character => character == '\n') + (Text.EndsWith('\n') ? 0 : 1);

    public static SourceFile Read(string path) => new(path, File.ReadAllText(path));

    public static SourceFile FromText(string path, string text) => new(path, text);

    public static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    public static int LinesSpannedBy(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }
}
