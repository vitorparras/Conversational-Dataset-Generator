using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Content lives in the content schema, and the domain a session declares is what the database judges it by. A statement over that schema
/// is a public constant of the top-level <c>ContentQueries</c>, the list the test with no domain declared runs, so one written anywhere else, or built at run
/// time, would reach content outside that test. And no statement may move the search path: the schema is named in every statement, and
/// naming it is what the rule above sees. The member that sets it on a connection string builder is refused by name.
/// </summary>
internal static partial class ContentSchemaAccessRules
{
    private const string ListName = "ContentQueries";
    private const string QueriesFile = ListName + ".cs";
    private const string AllowedFolder = "/Core.Persistence/Content/";

    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken,
    ];

    /// <summary>Every string in the files that names a table of the content schema outside a public constant of the list of queries, or that moves the search path.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) => files.SelectMany(Findings);

    private static IEnumerable<Violation> Findings(SourceFile file) =>
        file.Root.DescendantTokens()
            .Where(token => TextKinds.Contains(token.Kind()) || token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => (token, Reason: ReasonOf(file, token)))
            .Where(found => found.Reason is not null)
            .Select(found => new Violation(file.Path, found.token.GetLocation().GetLineSpan().StartLinePosition.Line + 1, found.Reason!));

    private static string? ReasonOf(SourceFile file, SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.IdentifierToken) ? SearchPathMember().IsMatch(token.ValueText) : SearchPath().IsMatch(token.ValueText))
        {
            return "a statement moves the search path, so a table of the content schema could be reached without naming the schema";
        }

        return !token.IsKind(SyntaxKind.IdentifierToken) && ContentTable().IsMatch(token.ValueText) && !IsQueryConstant(file, token)
            ? "a statement reaches the content schema outside a public constant of ContentQueries"
            : null;
    }

    private static bool IsQueryConstant(SourceFile file, SyntaxToken token) =>
        IsQueriesFile(file)
        && token.Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is { Parent: ClassDeclarationSyntax { Identifier.Text: ListName, Parent: not TypeDeclarationSyntax } } field
        && field.Modifiers.Any(SyntaxKind.ConstKeyword) && field.Modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool IsQueriesFile(SourceFile file) =>
        file.Path.Replace(Path.DirectorySeparatorChar, '/').EndsWith(AllowedFolder + QueriesFile, StringComparison.Ordinal);

    // SQL lets blanks surround the dot; a blank only after it must lead into a lowercase word, so the sentence "the content. Then" is not a table.
    [GeneratedRegex(@"(?<![\w.])""?content""?(?:\s*\.(?:[A-Za-z_""]|\s*(?-i:[a-z_""]))|\s+\.\s*[A-Za-z_""])", RegexOptions.IgnoreCase)]
    private static partial Regex ContentTable();

    [GeneratedRegex(@"^search_?path$", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPathMember();

    [GeneratedRegex(@"search[_ ]?path", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPath();
}
