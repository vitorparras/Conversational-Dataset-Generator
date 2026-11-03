using System.Text.RegularExpressions;
using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// The chain of custody is only added to and read: no statement in the code updates, deletes from, truncates, merges into or upserts over <c>custody_event</c>, so that no route,
/// method or script rebuilds a link. The database refuses the same operations for every role (migration 0009); this is the half that stops the code from asking.
/// A statement is judged by the text the whole expression has, not by each piece: what is joined with <c>+</c> or interpolated is read as one, and a constant name as its value.
/// </summary>
internal static partial class CustodyChainRewriteRules
{
    private static readonly SyntaxKind[] TextKinds =
    [
        SyntaxKind.StringLiteralExpression, SyntaxKind.Utf8StringLiteralExpression, SyntaxKind.InterpolatedStringExpression,
        SyntaxKind.AddExpression, SyntaxKind.ParenthesizedExpression,
    ];

    // What stands for a part of the text that is only known when the code runs; no word of a statement matches it.
    private const string Unknown = "\u0001";

    /// <summary>Every string expression in the files, taken whole, whose text reads as a statement changing the table of the events.</summary>
    public static IEnumerable<Violation> Check(IEnumerable<SourceFile> files)
    {
        var all = files.ToList();
        var compilation = SourceCompilation.Of(all);
        return all.SelectMany(file => Findings(file, compilation.GetSemanticModel(file.Tree)));
    }

    private static IEnumerable<Violation> Findings(SourceFile file, SemanticModel model) =>
        file.Root.DescendantNodes().OfType<ExpressionSyntax>()
            .Where(expression => IsWholeText(expression) && Rewrites(TextOf(expression, model)))
            .Select(expression => new Violation(
                file.Path,
                expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                "a statement changes the table of the custody events; the chain is only added to and read, and no code rebuilds a link"));

    // The outermost expression of a text: a literal, an interpolation, a sum or a parenthesis that no other sum, parenthesis or interpolation holds.
    private static bool IsWholeText(SyntaxNode expression) => IsText(expression) && !IsPartOfText(expression.Parent);

    private static bool IsText(SyntaxNode expression) => TextKinds.Contains(expression.Kind());

    private static bool IsPartOfText(SyntaxNode? parent) =>
        parent is InterpolationSyntax or ParenthesizedExpressionSyntax || (parent is not null && parent.IsKind(SyntaxKind.AddExpression));

    // The constant value the compiler gives the expression, when it has one; otherwise the pieces put together, each part that only the run knows left as unknown.
    private static string TextOf(SyntaxNode expression, SemanticModel model)
    {
        var constant = model.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is string whole)
        {
            return whole;
        }

        return expression switch
        {
            LiteralExpressionSyntax utf8 when utf8.IsKind(SyntaxKind.Utf8StringLiteralExpression) => utf8.Token.ValueText,
            ParenthesizedExpressionSyntax parenthesized => TextOf(parenthesized.Expression, model),
            BinaryExpressionSyntax sum when sum.IsKind(SyntaxKind.AddExpression) => TextOf(sum.Left, model) + TextOf(sum.Right, model),
            InterpolatedStringExpressionSyntax interpolated => string.Concat(interpolated.Contents.Select(part => PartOf(part, model))),
            _ => Unknown,
        };
    }

    private static string PartOf(InterpolatedStringContentSyntax part, SemanticModel model) => part switch
    {
        InterpolatedStringTextSyntax text => text.TextToken.ValueText,
        InterpolationSyntax hole => TextOf(hole.Expression, model),
        _ => Unknown,
    };

    private static bool Rewrites(string text) => Changing().IsMatch(text) || Truncating().IsMatch(text) || (Table().IsMatch(text) && Upserting().IsMatch(text));

    // SQL lets blanks surround the dot and quotes surround the names; the table is named with or without the schema.
    [GeneratedRegex(
        @"\b(?:UPDATE|DELETE\s+FROM|TRUNCATE(?:\s+TABLE)?|MERGE\s+INTO)\s+(?:ONLY\s+)?(?:""?public""?\s*\.\s*)?""?custody_event""?(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex Changing();

    // TRUNCATE takes a list of tables, and the table of the events may come after others.
    [GeneratedRegex(
        @"\bTRUNCATE(?:\s+TABLE)?\s+(?:[^;,]+,\s*)+(?:ONLY\s+)?(?:""?public""?\s*\.\s*)?""?custody_event""?(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex Truncating();

    [GeneratedRegex(@"(?<![\w])""?custody_event""?(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Table();

    [GeneratedRegex(@"\bON\s+CONFLICT\b.*\bDO\s+UPDATE\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Upserting();
}
