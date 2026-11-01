using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Content lives in the content schema, and the domain a session declares is what the database judges it by. A statement over that schema
/// is a public or internal constant of the top-level <c>ContentQueries</c>, the list the test with no domain declared runs, so one written anywhere else, or built at run
/// time, would reach content outside that test. And no statement may move the search path: the schema is named in every statement, and
/// naming it is what the rule above sees. The member that sets it on a connection string builder is refused by name. The server guarantees alone
/// may hold the whole name of a table of the schema, as the server's log gives it, and nothing longer.
/// </summary>
internal static partial class ContentSchemaAccessRules
{
    private const string ListName = "ContentQueries";
    private const string QueriesFile = ListName + ".cs";
    private const string AllowedFolder = "/Core.Persistence/Content/";
    private const string GuaranteesFolder = "/Core.Persistence/ServerGuarantees/";

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
        if (token.IsKind(SyntaxKind.IdentifierToken) ? SearchPathMember().IsMatch(token.ValueText) : MovesSearchPath(file, token.ValueText))
        {
            return "a statement moves the search path, so a table of the content schema could be reached without naming the schema";
        }

        return !token.IsKind(SyntaxKind.IdentifierToken) && ReachesContent(file, token)
            ? "a statement reaches the content schema outside a public constant of ContentQueries"
            : null;
    }

    private static bool ReachesContent(SourceFile file, SyntaxToken token) =>
        ContentTable().IsMatch(token.ValueText) && !IsQueryConstant(file, token) && !IsObjectName(file, token.ValueText);

    /// <summary>The one statement that names the search path and is let through, in the server guarantees only, pins it to the catalog, where no table of the content schema can be reached.</summary>
    private static bool MovesSearchPath(SourceFile file, string text) =>
        SearchPath().IsMatch(text) && !(IsServerGuarantee(file) && CatalogOnly().IsMatch(text));

    /// <summary>A name the server's log uses for an object, in the server guarantees only: the whole string is the schema, a dot and a table, so a statement that names one is still refused.</summary>
    private static bool IsObjectName(SourceFile file, string text) => IsServerGuarantee(file) && QualifiedName().IsMatch(text);

    private static bool IsServerGuarantee(SourceFile file) =>
        file.Path.Replace(Path.DirectorySeparatorChar, '/').Contains(GuaranteesFolder, StringComparison.Ordinal);

    private static bool IsQueryConstant(SourceFile file, SyntaxToken token) =>
        IsQueriesFile(file)
        && token.Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is { Parent: ClassDeclarationSyntax { Identifier.Text: ListName, Parent: not TypeDeclarationSyntax } } field
        && IsSeenByTheList(field);

    private static bool IsSeenByTheList(MemberDeclarationSyntax field) =>
        field.Modifiers.Any(SyntaxKind.ConstKeyword) && field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword));

    private static bool IsQueriesFile(SourceFile file) =>
        file.Path.Replace(Path.DirectorySeparatorChar, '/').EndsWith(AllowedFolder + QueriesFile, StringComparison.Ordinal);

    // SQL lets blanks surround the dot; a blank only after it must lead into a lowercase word, so the sentence "the content. Then" is not a table.
    [GeneratedRegex(@"(?<![\w.])""?content""?(?:\s*\.(?:[A-Za-z_""]|\s*(?-i:[a-z_""]))|\s+\.\s*[A-Za-z_""])", RegexOptions.IgnoreCase)]
    private static partial Regex ContentTable();

    [GeneratedRegex(@"^content\.[a-z_]+$")]
    private static partial Regex QualifiedName();

    [GeneratedRegex(@"^search_?path$", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPathMember();

    [GeneratedRegex(@"^SET\s+LOCAL\s+search_path\s*(=|TO)\s*pg_catalog\s*,\s*pg_temp$", RegexOptions.IgnoreCase)]
    private static partial Regex CatalogOnly();

    [GeneratedRegex(@"search[_ ]?path", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPath();
}
