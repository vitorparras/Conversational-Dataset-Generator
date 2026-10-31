using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// A protected column holds the envelope of its value, bytes that mean nothing to the database: a statement that filters, orders, groups or
/// joins by one gives a result over ciphertext, which looks right and is not. Every statement over content is a constant of the list of
/// queries, which the rule on the content schema keeps in one place, so reading the statements reads them all. A statement that names a
/// protected column only to write it or to return it is let through.
/// </summary>
internal static class ProtectedColumnQueryRules
{
    /// <summary>Every statement in the files, concatenations and interpolations included, that filters or orders by one of the columns.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files, IEnumerable<string> protectedColumns)
    {
        var columns = protectedColumns.ToList();
        return files.SelectMany(file => Statements(file).Select(statement => Finding(file, statement, columns)).OfType<Violation>());
    }

    /// <summary>The names of the protected columns, as the list in the code declares them.</summary>
    public static IEnumerable<string> ColumnsDeclaredIn(IEnumerable<SourceFile> files) =>
        files.Where(file => file.FileName == "ContentColumns.cs")
            .SelectMany(file => file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            .Where(property => property.Type is IdentifierNameSyntax { Identifier.Text: "ProtectedColumn" })
            .Select(NameArgument)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .Distinct(StringComparer.Ordinal);

    private static ExpressionSyntax? NameArgument(PropertyDeclarationSyntax property) =>
        property.Initializer?.Value.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault()
            ?.ArgumentList?.Arguments.ElementAtOrDefault(1)?.Expression;

    private static Violation? Finding(SourceFile file, (SyntaxNode Node, string Text) statement, IEnumerable<string> columns)
    {
        var named = columns.Intersect(SqlClauses.NamedWhereRowsAreSelected(statement.Text), StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        var reason = $"a statement filters or orders by the protected column {string.Join(", ", named)}, whose value in the database is ciphertext";
        return named.Count > 0 ? new Violation(file.Path, SourceFile.LineOf(statement.Node), reason) : null;
    }

    private static IEnumerable<(SyntaxNode Node, string Text)> Statements(SourceFile file) =>
        file.Root.DescendantNodes()
            .Where(node => IsText(node) && node.Parent is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression })
            .Select(node => (node, Text: TextOf(node)))
            .Where(statement => SqlClauses.ReadsAsStatement(statement.Text));

    private static bool IsText(SyntaxNode node) =>
        node is InterpolatedStringExpressionSyntax or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }
        || node.IsKind(SyntaxKind.StringLiteralExpression);

    private static string TextOf(SyntaxNode node) => node switch
    {
        LiteralExpressionSyntax literal => literal.Token.ValueText,
        InterpolatedStringExpressionSyntax interpolated => string.Concat(interpolated.Contents.Select(PartOf)),
        BinaryExpressionSyntax add => TextOf(add.Left) + TextOf(add.Right),
        ParenthesizedExpressionSyntax parenthesized => TextOf(parenthesized.Expression),
        _ => " ? ",
    };

    private static string PartOf(InterpolatedStringContentSyntax part) =>
        part is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : " ? ";
}
