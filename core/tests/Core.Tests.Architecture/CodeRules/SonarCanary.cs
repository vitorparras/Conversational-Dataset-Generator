using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A copy of a source with one S1227 finding added to every type and at the end of the file, which only a skipped file or a skipped type can hide.</summary>
internal static class SonarCanary
{
    public const string Rule = "S1227";
    private const string Marker = "// canary-s1227";
    private const string IdMark = "__ID__";
    private const string FileMark = "__FILE__";

    private const string Extra = """

        internal static class SonarCanaryExtra__FILE__
        {__ID__}

        """;

    private const string Method = """

        private static int CanaryCount__ID__(int[] values)
        {
            var count = 0;
            foreach (var value in values)
            {
                if (value < 0)
                {
                    break; // canary-s1227
                }

                count++;
            }

            return count;
        }

        """;

    public static string Rewrite(string text, int file)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
        var injected = (CompilationUnitSyntax)new Injector().Visit(root);
        return WithExtraClass(injected, file).ToFullString();
    }

    /// <summary>The line of every planted finding, in the order they appear.</summary>
    public static IReadOnlyList<int> Lines(string rewritten) =>
        [.. rewritten.Split('\n').Select((line, index) => (line, index)).Where(entry => entry.line.Contains(Marker, StringComparison.Ordinal)).Select(entry => entry.index + 1)];

    private static CompilationUnitSyntax WithExtraClass(CompilationUnitSyntax root, int file)
    {
        var id = file.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var method = Method.Replace(IdMark, "Extra", StringComparison.Ordinal);
        var extra = SyntaxFactory.ParseMemberDeclaration(Extra.Replace(FileMark, id, StringComparison.Ordinal).Replace(IdMark, method, StringComparison.Ordinal))!;
        var scoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return scoped is null ? root.AddMembers(extra) : root.ReplaceNode(scoped, scoped.AddMembers(extra));
    }

    private sealed class Injector : CSharpSyntaxRewriter
    {
        private int ordinal;

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => Inject((ClassDeclarationSyntax)base.VisitClassDeclaration(node)!);

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) => Inject((StructDeclarationSyntax)base.VisitStructDeclaration(node)!);

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) => Inject((RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!);

        private T Inject<T>(T type)
            where T : TypeDeclarationSyntax
        {
            var member = SyntaxFactory.ParseMemberDeclaration(Method.Replace(IdMark, ordinal++.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))!;
            var braced = type.SemicolonToken.IsKind(SyntaxKind.SemicolonToken)
                ? type.WithSemicolonToken(default).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)).WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken))
                : type;
            return (T)braced.AddMembers(member);
        }
    }
}
