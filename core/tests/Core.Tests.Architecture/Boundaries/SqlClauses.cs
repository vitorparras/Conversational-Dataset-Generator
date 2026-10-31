using System.Text.RegularExpressions;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Reads a statement clause by clause, and finds the identifiers that sit where a row is filtered, ordered, grouped or joined. It is not a
/// parser: it knows the clause words and the parentheses, which is what tells a sub-select's own clauses from the clause around it, and it
/// takes a name after <c>@</c>, <c>:</c> or <c>$</c> for a parameter, never for a column.
/// </summary>
internal sealed partial class SqlClauses
{
    private static readonly HashSet<string> Starting = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "SET", "VALUES", "RETURNING", "LIMIT", "OFFSET", "INSERT", "UPDATE", "DELETE", "INTO", "UNION", "FOR", "WITH", "FETCH", "JOIN",
    };

    private static readonly HashSet<string> Others = new(StringComparer.OrdinalIgnoreCase)
    {
        "ON", "BY", "CONFLICT", "AND", "OR", "NOT", "IN", "IS", "NULL", "ASC", "DESC", "DO", "OVER", "LIKE", "AS",
    };

    private static readonly HashSet<string> Filtering = new(StringComparer.OrdinalIgnoreCase) { "WHERE", "HAVING", "USING" };

    private static readonly HashSet<string> FilteringBeforeBy = new(StringComparer.OrdinalIgnoreCase) { "ORDER", "GROUP", "PARTITION" };

    private readonly List<string> words;
    private readonly Stack<bool> scopes = new();
    private readonly HashSet<string> found = new(StringComparer.Ordinal);
    private bool selecting;

    private SqlClauses(string text)
    {
        words = [.. Tokens().Matches(text).Select(match => match.Value)];
    }

    /// <summary>Whether the text reads as a statement, or the tail of one, rather than a sentence that happens to hold the words.</summary>
    public static bool ReadsAsStatement(string text) => Opening().IsMatch(text);

    /// <summary>The identifiers, in upper case and without quotes, that appear in a clause that filters, orders, groups or joins.</summary>
    public static IReadOnlySet<string> NamedWhereRowsAreSelected(string text) => new SqlClauses(text).Walk();

    private HashSet<string> Walk()
    {
        for (var index = 0; index < words.Count; index++)
        {
            Step(index);
        }

        return found;
    }

    private void Step(int index)
    {
        var word = words[index];
        if (word[0] == '\'')
        {
            return;
        }

        if (word == "(")
        {
            scopes.Push(selecting);
            return;
        }

        if (word == ")")
        {
            selecting = scopes.Count > 0 && scopes.Pop();
            return;
        }

        selecting = Moves(word, index + 1 < words.Count ? words[index + 1] : string.Empty);
        if (selecting && IsIdentifier(word))
        {
            _ = found.Add(word.Trim('"').ToUpperInvariant());
        }
    }

    private bool Moves(string word, string next) =>
        Opens(word, next) || SelectsForComparison(word) || (!ClosesFilter(word, next) && selecting);

    private bool SelectsForComparison(string word) => IsWord(word, "SELECT") && scopes.Count > 0 && scopes.Peek();

    private static bool Opens(string word, string next) => Filtering.Contains(word) || OpensBeforeBy(word, next) || OpensOn(word, next);

    private static bool OpensBeforeBy(string word, string next) => FilteringBeforeBy.Contains(word) && IsWord(next, "BY");

    private static bool OpensOn(string word, string next) => IsWord(word, "ON") && !IsWord(next, "CONFLICT");

    private static bool ClosesFilter(string word, string next) => Starting.Contains(word) || (IsWord(word, "ON") && IsWord(next, "CONFLICT"));

    private static bool IsWord(string word, string expected) => string.Equals(word, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsIdentifier(string word) => word[0] is not ('@' or ':' or '$') && !IsKeyword(word);

    private static bool IsKeyword(string word) =>
        Filtering.Contains(word) || FilteringBeforeBy.Contains(word) || Starting.Contains(word) || Others.Contains(word);

    [GeneratedRegex(@"'(?:[^']|'')*'|""[^""]*""|[@:$]?[A-Za-z_]\w*|[()]")]
    private static partial Regex Tokens();

    [GeneratedRegex(@"^\s*(SELECT|INSERT|UPDATE|DELETE|WITH|MERGE|WHERE|ORDER\s+BY|GROUP\s+BY|HAVING|JOIN)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Opening();
}
