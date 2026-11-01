namespace Fixture.ProtectedColumns;

internal static class FiltersOrOrders
{
    public const string Where = "SELECT id FROM t WHERE raw_text = @text";

    public const string OrderBy = "SELECT id FROM t ORDER BY sanitized_text";

    public static string Interpolated(int id) => $"SELECT id FROM t WHERE id = {id} AND lower(raw_text) = @text";

    public const string Quoted = "SELECT id FROM t WHERE \"raw_text\" > @text";

    public const string Qualified = "SELECT t.id FROM t WHERE t.entries IS NOT NULL";

    public const string Raw = """
        SELECT id
        FROM t
        GROUP BY raw_text
        """;

    public const string AfterSubquery = "SELECT id FROM t WHERE id IN (SELECT item_id FROM u WHERE u.id > 1) AND raw_text = @x";

    public const string Joined = "SELECT t.id FROM t JOIN u ON u.entries = t.sanitized_text";

    public const string Window = "SELECT row_number() OVER (PARTITION BY raw_text) FROM t";

    public static readonly string Concatenated = "SELECT id FROM t " + "WHERE entries = @x";

    public const string Upper = "select id from t where RAW_TEXT = @x";

    public const string Tail = " WHERE raw_text = @x";

    public const string Distinct = "SELECT DISTINCT ON (entries) id FROM t";

    public const string Using = "SELECT t.id FROM t JOIN u USING (entries)";

    public const string HiddenByALiteral = "SELECT id FROM t WHERE note = 'from' AND raw_text = @x";

    public const string InSubselect = "SELECT id FROM t WHERE id IN (SELECT raw_text FROM u)";
}
