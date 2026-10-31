using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// The server guarantees read the catalog with the application's own credential, and a session may put a schema of its own ahead of the catalog
/// in its search path: a view there that shares a catalog name would answer that the guarantee holds. So a statement of the checks names every
/// catalog table, view and function with <c>pg_catalog.</c> in the text, which does not depend on the state of the session. A statement is a string that holds a
/// SELECT or a JOIN; literals inside it are left alone, and so are the keywords that look like calls.
/// </summary>
internal static partial class CatalogQualificationRules
{
    private const string Folder = "/Core.Persistence/ServerGuarantees/";
    private const string Schema = "pg_catalog";

    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken,
    ];

    private static readonly HashSet<string> Syntax = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "and", "any", "array", "as", "by", "case", "coalesce", "distinct", "else", "exists", "filter", "from", "greatest", "in", "join", "lateral",
        "least", "not", "nullif", "on", "or", "over", "select", "then", "using", "values", "when", "where", "with",
    };

    /// <summary>Every statement of the server guarantees that names a catalog table, view or function without <c>pg_catalog.</c>.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) =>
        files.Where(IsServerGuarantee).SelectMany(Findings);

    private static bool IsServerGuarantee(SourceFile file) =>
        file.Path.Replace(Path.DirectorySeparatorChar, '/').Contains(Folder, StringComparison.Ordinal);

    private static IEnumerable<Violation> Findings(SourceFile file) =>
        file.Root.DescendantTokens()
            .Where(token => TextKinds.Contains(token.Kind()) && Statement().IsMatch(token.ValueText))
            .Select(token => (token, Names: Unqualified(token.ValueText).Distinct().ToList()))
            .Where(found => found.Names.Count > 0)
            .Select(found => new Violation(
                file.Path,
                found.token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                $"a statement names {string.Join(", ", found.Names)} without pg_catalog., so a schema ahead of the catalog in the search path could shadow it"));

    private static IEnumerable<string> Unqualified(string statement)
    {
        var text = Literal().Replace(statement, "''");
        return Identifier().Matches(text)
            .Where(name => IsCatalogName(text, name) && !Qualifier().IsMatch(text[..name.Index]))
            .Select(name => name.Groups["name"].Value);
    }

    private static bool IsCatalogName(string text, Match name)
    {
        var value = name.Groups["name"].Value;
        if (value.StartsWith("pg_", StringComparison.OrdinalIgnoreCase) && !value.Equals(Schema, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !Syntax.Contains(value) && !text[..name.Index].TrimEnd().EndsWith('.') && Call().IsMatch(text[(name.Index + name.Length)..]);
    }

    [GeneratedRegex(@"\b(SELECT|JOIN)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Statement();

    [GeneratedRegex(@"'(?:[^']|'')*'")]
    private static partial Regex Literal();

    [GeneratedRegex(@"(?<![\w""])""?(?<name>[A-Za-z_]\w*)""?(?!\w)")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"^\s*\(")]
    private static partial Regex Call();

    [GeneratedRegex(@"(?<![\w""])""?pg_catalog""?\s*\.\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Qualifier();
}
