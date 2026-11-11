using System.Text.RegularExpressions;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// Where a statement uses a sealed column other than to carry it: the projection of a read, the columns of an insert and the assignments of an update are the ways in which
/// the sealed bytes pass through; any other mention (a filter, an order, a group, a join, a comparison, a function over it, a DISTINCT) makes the database look into what only the application opens.
/// </summary>
internal static partial class SealedColumnUse
{
    /// <summary>The sealed columns the statement uses beyond carrying them; none when it only carries them.</summary>
    /// <param name="statement">The statement as the query holds it.</param>
    /// <param name="sealedColumns">The names of the sealed columns.</param>
    public static IReadOnlyList<string> Beyond(string statement, IEnumerable<string> sealedColumns)
    {
        var found = new List<string>();
        var names = string.Join('|', sealedColumns.Select(Regex.Escape));
        var distinct = new Regex($@"SELECT\s+DISTINCT\s[^;]*?(?<![@\w])({names})\b[^;]*?\sFROM\s", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        found.AddRange(distinct.Matches(statement).Select(match => match.Groups[1].Value));
        var carried = Assignments().Replace(InsertColumns().Replace(Projection().Replace(statement, "SELECT FROM "), "INSERT INTO t "), " SET ");
        var uses = new Regex($@"(?<![@\w])({names})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        found.AddRange(uses.Matches(carried).Select(match => match.Groups[1].Value));
        return found;
    }

    [GeneratedRegex(@"SELECT\s.*?\sFROM\s", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Projection();

    [GeneratedRegex(@"INSERT\s+INTO\s+[\w.]+\s*\([^)]*\)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InsertColumns();

    [GeneratedRegex(@"\sSET\s.*?(?=\sWHERE\s|\sRETURNING\s|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Assignments();
}
