using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// A copy of a source with two Sonar findings added to every type and at the end of the file, which only a skipped file or a skipped type can hide:
/// one of a rule the root config turns on, one of a rule the analyzer has on by default.
/// </summary>
internal static class SonarCanary
{
    public const string Rule = "S1227";
    public const string DefaultOnRule = "S3358";
    private const string Marker = "// canary-s1227";
    private const string DefaultOnMarker = "// canary-s3358";
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

        private static int CanarySign__ID__(int value)
        {
            return value < 0 ? -1 : value > 0 ? 1 : 0; // canary-s3358
        }

        """;

    public static string Rewrite(string text, int file)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
        var injected = (CompilationUnitSyntax)new Injector(file).Visit(root);
        return WithExtraClass(injected, file).ToFullString();
    }

    /// <summary>The line of every planted finding of the rule the root config turns on, in the order they appear.</summary>
    public static IReadOnlyList<int> Lines(string rewritten) => LinesOf(rewritten, Marker);

    /// <summary>The line of every planted finding of the rule that is on by default.</summary>
    public static IReadOnlyList<int> DefaultOnLines(string rewritten) => LinesOf(rewritten, DefaultOnMarker);

    private static List<int> LinesOf(string rewritten, string marker) =>
        [.. rewritten.Split('\n').Select((line, index) => (line, index)).Where(entry => entry.line.Contains(marker, StringComparison.Ordinal)).Select(entry => entry.index + 1)];

    private static string Named(string id) => Method.Replace(IdMark, id, StringComparison.Ordinal);

    private static CompilationUnitSyntax WithExtraClass(CompilationUnitSyntax root, int file)
    {
        var id = file.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var extra = SyntaxFactory.ParseMemberDeclaration(Extra.Replace(FileMark, id, StringComparison.Ordinal).Replace(IdMark, Named("Extra"), StringComparison.Ordinal))!;
        var scoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return scoped is null ? root.AddMembers(extra) : root.ReplaceNode(scoped, scoped.AddMembers(extra));
    }

    /// <summary>Each method is named by its file and its type, so that a class split over several files, each with a copy, never meets itself.</summary>
    private sealed class Injector(int file) : CSharpSyntaxRewriter
    {
        private int ordinal;

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => Inject((ClassDeclarationSyntax)base.VisitClassDeclaration(node)!);

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) => Inject((StructDeclarationSyntax)base.VisitStructDeclaration(node)!);

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) => Inject((RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!);

        private T Inject<T>(T type)
            where T : TypeDeclarationSyntax
        {
            var id = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"F{file}T{ordinal++}");
            var members = SyntaxFactory.ParseCompilationUnit("class C {" + Named(id) + "}").Members.OfType<ClassDeclarationSyntax>().Single().Members;
            var braced = type.SemicolonToken.IsKind(SyntaxKind.SemicolonToken)
                ? type.WithSemicolonToken(default).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)).WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken))
                : type;
            return (T)braced.AddMembers([.. members]);
        }
    }
}
