using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// The scope of the system is the one in which a content table shows the items and the maps of a batch that is not accepted, and only the operations that move keys or copy rows may declare it:
/// the rotation of the epoch, the rewrap of a domain, the check of the epochs in use and the copy of the data. The transaction that opens in that scope, and the statement that declares it,
/// are named only in the files of those operations and in <c>ContentTransaction</c>; the name of the setting that carries the scope is written only in <c>ContentTransaction</c>.
/// </summary>
internal static class ContentScopeRules
{
    private const string Setting = "app.content_scope";
    private const string Owner = "/Core.Persistence/Content/ContentTransaction.cs";

    private static readonly string[] ScopedMembers = ["BeginSystemAsync", "DeclareSystem", "BeginSnapshotAsync"];

    private static readonly string[] SystemPaths =
    [
        "/Core.Persistence/Content/Rotation/EpochRotation.cs",
        "/Core.Persistence/Content/Rotation/DomainRewrap.cs",
        "/Core.Persistence/ServerGuarantees/ContentEpochCheck.cs",
        "/Core.Persistence/ServerGuarantees/DataCopy.cs",
    ];

    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken,
    ];

    /// <summary>Every use of the members that open the scope outside the files of the system operations, and every text that names the setting outside the transaction.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files) => files.SelectMany(Findings);

    private static IEnumerable<Violation> Findings(SourceFile file)
    {
        var path = file.Path.Replace(Path.DirectorySeparatorChar, '/');
        var isOwner = path.EndsWith(Owner, StringComparison.Ordinal);
        var isSystem = Array.Exists(SystemPaths, allowed => path.EndsWith(allowed, StringComparison.Ordinal));
        return file.Root.DescendantTokens()
            .Select(token => (token, Reason: ReasonOf(token, isOwner, isSystem)))
            .Where(found => found.Reason is not null)
            .Select(found => new Violation(file.Path, found.token.GetLocation().GetLineSpan().StartLinePosition.Line + 1, found.Reason!));
    }

    private static string? ReasonOf(SyntaxToken token, bool isOwner, bool isSystem)
    {
        if (token.IsKind(SyntaxKind.IdentifierToken) && ScopedMembers.Contains(token.ValueText) && !isOwner && !isSystem)
        {
            return $"{token.ValueText} opens the scope of the system, which only the rotation, the rewrap, the check of the epochs and the copy may do";
        }

        return TextKinds.Contains(token.Kind()) && token.ValueText.Contains(Setting, StringComparison.OrdinalIgnoreCase) && !isOwner
            ? "the setting that carries the scope of the system is named only in ContentTransaction"
            : null;
    }
}
