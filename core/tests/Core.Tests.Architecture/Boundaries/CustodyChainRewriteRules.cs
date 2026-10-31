using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// The chain of custody is only added to and read: no statement in the code updates, deletes from, truncates, merges into or upserts over <c>custody_event</c>, so that no route,
/// method or script rebuilds a link. The database refuses the same operations for every role (migration 0009); this is the half that stops the code from asking.
/// </summary>
internal static partial class CustodyChainRewriteRules
{
    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken,
    ];

    /// <summary>Every string in the files that reads as a statement changing the table of the events.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) => files.SelectMany(Findings);

    private static IEnumerable<Violation> Findings(SourceFile file) =>
        file.Root.DescendantTokens()
            .Where(token => TextKinds.Contains(token.Kind()) && Rewrites(token.ValueText))
            .Select(token => new Violation(
                file.Path,
                token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                "a statement changes the table of the custody events; the chain is only added to and read, and no code rebuilds a link"));

    private static bool Rewrites(string text) => Changing().IsMatch(text) || (Table().IsMatch(text) && Upserting().IsMatch(text));

    // SQL lets blanks surround the dot and quotes surround the names; the table is named with or without the schema.
    [GeneratedRegex(
        @"\b(?:UPDATE|DELETE\s+FROM|TRUNCATE(?:\s+TABLE)?|MERGE\s+INTO)\s+(?:ONLY\s+)?(?:""?public""?\s*\.\s*)?""?custody_event""?(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex Changing();

    [GeneratedRegex(@"(?<![\w])""?custody_event""?(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Table();

    [GeneratedRegex(@"\bON\s+CONFLICT\b.*\bDO\s+UPDATE\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Upserting();
}
