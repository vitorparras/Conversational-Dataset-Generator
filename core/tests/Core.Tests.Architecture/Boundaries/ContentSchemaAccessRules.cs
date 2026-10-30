using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Content lives in the content schema, and the domain a session declares is what the database judges it by, so a statement over that schema
/// belongs to the content transaction and nowhere else; a statement written beside it would reach content without declaring a domain.
/// </summary>
internal static partial class ContentSchemaAccessRules
{
    private const string AllowedFolder = "/Core.Persistence/Content/";

    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken,
    ];

    /// <summary>Every string in the files, outside the content transaction's folder, that names a table of the content schema.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) =>
        files.Where(file => !IsContentTransaction(file)).SelectMany(Findings);

    private static bool IsContentTransaction(SourceFile file) =>
        file.Path.Replace(Path.DirectorySeparatorChar, '/').Contains(AllowedFolder, StringComparison.Ordinal);

    private static IEnumerable<Violation> Findings(SourceFile file) =>
        file.Root.DescendantTokens()
            .Where(token => TextKinds.Contains(token.Kind()) && ContentTable().IsMatch(token.ValueText))
            .Select(token => new Violation(file.Path, token.GetLocation().GetLineSpan().StartLinePosition.Line + 1, "a statement reaches the content schema outside the content transaction"));

    [GeneratedRegex(@"(?<![\w.])""?content""?\.[A-Za-z_""]", RegexOptions.IgnoreCase)]
    private static partial Regex ContentTable();
}
